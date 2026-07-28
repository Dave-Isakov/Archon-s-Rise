# Minimal Place UI & Player Log — Design

**Date:** 2026-07-28
**Status:** Design approved; ready for implementation planning.
**Pillar fit:** Playability/flow. Neither phase changes a game rule — both remove clicks that sit
between the player and the decision they came to make.

## Summary

Two phases behind one goal: **stop interrupting the player**.

- **Phase A — the place fan.** Standing on a town/keep/castle or a dungeon and clicking it no longer
  opens a full-screen canvas. Instead a small arc of icon buttons fans over the player's head — one
  per service that is actually available right now — in the style the shrine already established
  (spec 2026-07-27). The last slot is a **ledger icon** that opens the existing full menu unchanged,
  for players who want the detail. Every dismissable surface in the game gains **click-off to close**
  and loses its exit button.
- **Phase B — the player log.** `GameManager.ValidationMessage`'s blocking message canvas is retired.
  Informational events become **non-blocking toasts** in a corner rail *and* entries in an openable
  **history panel**. Modals survive only where the player must decide something.

Build approach for Phase A: a **pure action descriptor + one generic fan renderer**, driven by two
callers (town, dungeon). Chosen over re-parenting the existing `TownButtons` MonoBehaviours into a
fan container, which would have kept the visibility logic in five untestable `Update()` loops and
would have needed a second solution for dungeons anyway.

## Locked decisions (from brainstorm 2026-07-28)

- The fan shows **every currently-valid service**, not a single "primary" action — an unconquered
  Keep fans Assault; a conquered Castle fans Recruit/Heal/Cards/Crystal.
- The full menu opens from its **own ledger slot in the fan**, *not* by overloading the `?` HelpIcon.
  The `?` keeps meaning help only. (Overloading it would have made the full menu unreachable with
  tips off, since `HelpIcon` zeroes its alpha and raycasts in that state.)
- Services needing a follow-up choice open their **existing sub-panels** directly (`RecruitPanel`,
  crystal picker). Heal, Assault and Delve commit straight from the fan.
- **Dungeons are in Phase A**, sharing the same fan. Hovering the Delve slot shows the next enemy
  through the existing `EnemyPreviewPanel`, the same as hovering an Assault button or a map enemy.
- The `ShrinePanel` fan is **left alone** — it is a cycling multi-slot payment widget, not a list of
  actions, and folding it into the shared component would risk working code for no gain.
- **Click-off dismisses everything** that costs nothing to reopen; no exit buttons. Two exceptions:
  the run-end screen (terminal) and the card-pick reward canvas (click-off there would permanently
  forfeit a card on a mis-click, so it keeps an explicit Skip button).
- Message routing rule, no exceptions: **anything needing a decision stays a modal; anything that
  merely informs becomes a toast + log entry.** All 36 `ValidationMessage` call sites demote.
- The log is **in-memory, capped at 100 entries, grouped by day**, and is **not** saved. No
  save-schema bump, no migrator, no migrator-test churn.

---

## Section 1 — Phase A: the place fan

### 1.1 Pure layer — `Assets/Scripts/Places/` (existing `ArchonsRise.Places` asmdef)

The tests asmdef already references this assembly (that is how `PlaceRulesTests` runs), so no asmdef
work is required.

```
enum PlaceActionId { Assault, Recruit, Heal, Cards, Crystal, Delve, OpenMenu }

readonly struct PlaceAction
{
    PlaceActionId id;
    IconConcept   icon;        // glyph on the slot
    IconConcept   costIcon;    // Influence / Explore; ignored when costAmount == 0
    int           costAmount;  // 0 = no cost badge
    bool          enabled;     // false => UiLock dim + non-interactable
}
```

`PlaceActionRules` exposes two entry points rather than one god-struct, because towns and dungeons
share no inputs beyond `visitCanAct`:

| Entry point | Snapshot fields | Result |
|---|---|---|
| `ForTown(TownActionSnapshot)` | `placeType`, `conquered`, `guardiansRemaining`, `influence`, `healCost`, `anyUnitAffordable`, `visitCanAct` | `[Assault]` when not conquered; else `AllowedServices(placeType)` filtered and ordered, each `enabled` by affordability **and** `visitCanAct`. Then `OpenMenu`. |
| `ForDungeon(DungeonActionSnapshot)` | `complete`, `explore`, `delveCost`, `visitCanAct` | `[Delve]` unless complete, `enabled` by `explore >= delveCost && visitCanAct`. Then `OpenMenu`. |

Rules that must hold:

- `OpenMenu` is **always last and always enabled**, mirroring how the shrine appends its checkmark.
- `Cards` at a Castle appears **present but disabled** (the M2 purchase-economics stub), not hidden —
  `PlaceService.Cards` genuinely is an allowed service there, and hiding it would misreport the place.
- Service order is fixed and authored in the rules, so the fan does not reshuffle between opens.

### 1.2 Scene layer — main assembly

- **`PlaceFanSlot`** — one prefab: icon `Image`, optional cost badge, `Button`, `CanvasGroup`.
  `Bind(PlaceAction, Action<PlaceActionId>)` swaps the sprite, sets the badge, applies `UiLock` when
  disabled. Icon + amount only, no words (per the shipped Play/Convert convention). Mirrors
  `ShrineSlotButton`.
- **`PlaceFan`** — pooled slots inside a `fanContainer` laid out by `FanMath.Solve` with
  `SpreadDegrees = 0` so icons stay upright. The container is parked at an authored offset above
  screen centre with no per-hex projection, valid for the same reason as the shrine's: entry requires
  standing on the cell, and the camera rides `PlayerPosition`, so the place is always screen centre.
  A `ClickOffCatcher` behind the arc dismisses for free. Public API: `Open(TownToken)`,
  `Open(DungeonToken)`, `Dismiss()`.
- **Live re-gating** — `PlaceFan.Update` rebuilds the snapshot each frame and re-renders **only when
  the resulting action list differs**. Delve therefore unlocks the instant an Explore card is played
  and Recruit locks when influence drops, with no per-frame `FanMath` cost and no event wiring. This
  replaces the five scattered `Update()` loops in the `TownButtons` subclasses.
- **`FanPreviewTrigger : PreviewTrigger`** on the slot prefab — resolves remaining guardians for
  `Assault`, the next dungeon enemy (behind `PreviewRules.CanPreview()`) for `Delve`, and an empty
  list otherwise, anchored to the slot. Because it extends the shipped `PreviewTrigger`, gamepad
  focus will drive it later with no change. The existing `PlacePreviewTrigger` stays on the in-menu
  `AssaultButton`, untouched.

### 1.3 Routing

| Action | Behaviour |
|---|---|
| `Assault` | `GuardianAssault.Instance.Begin(town)` |
| `Heal` | Raises the same town + influence-cost events as `HealButton`, then `CommitVisitAction` |
| `Recruit` | `RecruitPanel.Open(town)` |
| `Crystal` | Opens the crystal picker |
| `Cards` | Disabled stub; no handler |
| `Delve` | The current `DungeonPanel.Delve` body, lifted into a shared method both callers use |
| `OpenMenu` | Town: `townCanvas.enabled = true` + `CreateTown` + `PrepareButtons` + both existing events. Dungeon: `dungeonCanvas.enabled = true` + `DungeonPanel.Open`. |

`TownToken.OnPointerClick` and `DungeonToken.OnPointerClick` keep their fog check, teleport
deferral, adjacency-move dispatch and `BeginVisit` call verbatim — only the final "open the canvas"
lines are replaced with `PlaceFan.Open(this)`. Opening the fan remains a **free peek**; the turn's
action is still committed by the service, exactly as today.

### 1.4 Carry-overs that will break if forgotten

- `DungeonPanel.Open` currently raises `onDungeonOpenTutorial`. If players stop opening the panel,
  that one-shot never fires. **Move the raise to fan-open.**
- The town help pulse keys off panel opens for the same reason. **Move it to fan-open.**

### 1.5 What Phase A explicitly does not change

The full town and dungeon menus, their `HelpIcon`s, `RecruitPanel`, `DisbandPanel`, the crystal
picker's contents, `GuardianAssault`, `ShrinePanel`, and every turn/action rule.

---

## Section 2 — Phase A: universal click-off

**`ClickOffCatcher`** — a full-screen transparent `Image` with `raycastTarget` enabled, at sibling
index 0 so it renders *behind* content and never swallows content clicks, firing a `UnityEvent` on
click. One prefab per canvas, each wiring its own close method.

It replaces: the bespoke `CrystalDismissCatcher`, `RecruitPanel.cancelButton`, `DungeonPanel`'s
Close/Leave button, the town menu's close button, and `ShrinePanel`'s hand-rolled catcher.

Surfaces that receive it: place fan, town full menu, dungeon panel, recruit panel, crystal pop-out,
disband panel, shrine fan, help popup, card inspector / card list.

Exceptions, both deliberate:

- **Run-end screen** — terminal by design; `RunEndController.HasEnded` already suppresses everything
  else. No catcher.
- **Card reward pick** — keeps its explicit Skip button. The consistent rule is "click-off dismisses
  anything that costs nothing to reopen"; forfeiting a card reward is unrecoverable, so it does not
  qualify.

---

## Section 3 — Phase B: toast rail + player log

### 3.1 Pure layer — `Assets/Scripts/Log/` (**new** `ArchonsRise.Log` asmdef)

This is a new pure folder, so it needs its own asmdef **and** a reference added to the EditMode tests
asmdef, or the tests fail to resolve the types.

```
readonly struct LogEntry { int day; string text; }
```

`PlayerLogCore` — `Append(day, text)` into a ring buffer capped at **100**, `Entries` newest-first,
`Clear()`. Day dividers are **derived at render time** from where `day` changes between adjacent
entries, never stored as pseudo-entries, so eviction cannot orphan a header.

`text` already carries `IconMarkup` sprite tags, so entries render identically to today's messages
with no reformatting.

### 3.2 Scene layer

- **`GameLog`** — lazily-created scene singleton (the `RewardQueue` / `ConquestTracker` pattern: no
  scene wiring needed, and being scene-scoped means a new run starts blank). `Post(string message)`
  stamps the current `GameManager.roundNum` as the day, appends to the core, and hands the text to
  the rail. Suppressed when `RunEndController.HasEnded`, matching today's guard.
- **`ToastRail`** — spawns a toast prefab into a corner container: fade in, dwell ~3.5s, fade out,
  destroy. Max 4 visible; a 5th makes the oldest begin its fade early. The rail's `CanvasGroup` sets
  `blocksRaycasts = false` and its canvas sorts above everything, so a toast can float over a
  card-pick modal without ever eating a click.
- **`LogPanel`** — a HUD button opens a scrollable, newest-first list with day dividers. Closes by
  clicking off, per Section 2. No exit button.

### 3.3 Migration

1. `GameManager.ValidationMessage` becomes a one-line forwarder to `GameLog.Post`. A single-file
   change that converts all 36 call sites at once and keeps everything compiling.
2. A mechanical rename pass replaces the call sites with `GameLog.Post` and deletes the shim, along
   with `messageCanvas`, `messageText`, `ReturnButton` and the `messageDone` field.

### 3.4 Behavioural consequences to expect in playtest

- Today `PayReward` enqueues the defeat message *then* the card pick, so `RewardQueue` serialises
  them. Afterwards the toast fires immediately and the card pick opens immediately — the player reads
  "Ogre defeated, +7 ⚔, red crystal" as a toast while the card choice is already on screen. This is
  the intended improvement, not a regression.
- `RewardQueue` is left arbitrating card and skill picks only. Its `Flush()` on run end is unaffected.
- The `Debug.LogError` guard against double-opening the message canvas disappears with the canvas.
- Multi-enemy fights post one toast per kill; the 4-slot rail absorbs this without coalescing.

---

## Testing

Pure logic via the mcs harness (the Unity editor lock makes batch-mode `runTests` unreliable while
the editor is open):

**`PlaceActionRulesTests`**
- Unconquered place → `[Assault, OpenMenu]` only.
- Conquered Town → Recruit/Heal/Crystal in the authored order, then `OpenMenu`.
- Conquered Castle → includes `Cards`, present but `enabled == false`.
- Influence below `healCost` → `Heal` present, `enabled == false`.
- `visitCanAct == false` → every service disabled, `OpenMenu` still enabled.
- Complete dungeon → `[OpenMenu]` only.
- `explore < delveCost` → `Delve` present, disabled.

**`PlayerLogCoreTests`**
- Appending past the cap evicts oldest-first and leaves exactly 100 entries.
- `Entries` is newest-first.
- Divider derivation across a day boundary, including when eviction removes the first entry of a day.
- `Clear` empties the buffer.

Scene behaviour (fan layout, toast timing, click-off ordering) is verified by play acceptance, not
automated tests.

## Editor authoring

Scene and prefab wiring is done manually in the Unity editor from step-by-step instructions; the
implementation plan must produce those instructions rather than hand-editing scene or prefab YAML.

New/changed authoring:
- `PlaceFanSlot` prefab + per-action sprites (Assault, Recruit, Heal, Cards, Crystal, Delve, ledger).
- `PlaceFan` object on the gameplay canvas with its `fanContainer` offset and `FanSettings`.
- `ClickOffCatcher` prefab dropped into each listed canvas, each wired to that surface's close method.
- Toast prefab + `ToastRail` container; `LogPanel` with its scroll view and HUD open button.
- Removal of the message canvas and the retired exit/cancel buttons.

## Acceptance (USER)

1. Stand on an unconquered Keep, click it → a single Assault icon fans over the player plus the
   ledger icon; hovering Assault previews the remaining guardians.
2. Conquer it, reopen → Recruit and Crystal fan out; Recruit greys out when influence is short and
   ungreys the moment influence rises, without closing the fan.
3. Click the ledger icon → the existing full town menu opens exactly as before, help `?` included.
4. Stand on a dungeon → Delve icon shows its Explore cost, greyed until enough Explore is played,
   and hovering it previews the next enemy.
5. Clicking anywhere off the fan, the town menu, the recruit panel or the dungeon panel closes it
   with nothing spent. No exit buttons remain except the card-pick Skip.
6. Defeat several enemies in one fight → reward toasts stack in the corner and fade on their own; no
   click is needed to return to play; the card pick opens underneath them.
7. Open the log from the HUD → every message from the run so far, newest first, grouped by day.
