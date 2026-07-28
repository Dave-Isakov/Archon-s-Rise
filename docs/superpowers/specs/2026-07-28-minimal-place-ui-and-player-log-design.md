# Minimal Place UI & Player Log — Design

**Date:** 2026-07-28
**Status:** Design approved; ready for implementation planning.
**Pillar fit:** Playability/flow. No game rule changes — this removes clicks that sit between the
player and the decision they came to make, and puts an extensibility spine under place interaction so
the next place type does not repeat the rework.

## Summary

A full UI overhaul in two phases behind one goal: **stop interrupting the player**.

- **Phase A — place interaction.** Towns, dungeons and shrines stop opening full-screen canvases on
  click. A small arc of icon buttons fans over the player's head — one slot per action that is
  genuinely available right now — in the style the shrine established (spec 2026-07-27). Places that
  have a detail menu append a **ledger slot** that opens the existing menu unchanged. Underneath sits
  the real deliverable: a shared **`PlaceTokenBase` + `PlaceActionRules` + `FanLayout`** spine, so a
  new place type is one subclass and one rules method rather than another copy of everything.
- **Phase B — the player log.** The blocking message canvas is retired. Informational events become
  non-blocking corner **toasts** plus entries in an openable **history**. Modals survive only where
  the player must decide something.
- **Throughout — click-off everywhere.** Every dismissable surface in the game gains click-off and
  loses its exit button, with two deliberate exceptions.

## Locked decisions (from brainstorm 2026-07-28)

**Fan behaviour**
- The fan shows **every currently-valid action**, not a single "primary" one — an unconquered Keep
  fans Assault; a conquered Castle fans Recruit/Heal/Cards/Crystal.
- The full menu opens from its **own ledger slot**, *not* by overloading the `?` HelpIcon.
  `HelpIcon` zeroes its alpha and raycasts when tips are off, so overloading it would strand a
  tips-off player with no route to the menu. `?` keeps meaning help.
- Actions needing a follow-up choice open their **existing sub-panels** (`RecruitPanel`, crystal
  picker). Heal, Assault, Delve and shrine Engage act straight from the fan.
- Hovering a fan slot previews what it will fight, through the shipped `EnemyPreviewPanel` —
  guardians for Assault, the next enemy for Delve.

**Extensibility (the point of the overhaul)**
- `TownToken`, `DungeonToken` and `ShrineToken` each duplicate ~25 lines of entry handling today.
  That is extracted into **`PlaceTokenBase`**; a new place type implements `Describe()`,
  `BuildActions()` and `Dispatch()` only.
- The shrine **is** folded in — at the *entry* layer. A live shrine fans `[Engage]`; Engage swaps in
  the existing payment widget, which keeps its own cycling semantics on a shared `FanLayout`.
  Payment slots are **not** modelled as `PlaceAction`s: they cycle state rather than dispatch, and
  forcing them together would rewrite working code for no player-visible gain.
- Dormant/guarding shrines show a **disabled Engage slot** instead of firing a message — matching how
  towns and dungeons already show state through UI.

**Click-off**
- **Click-off dismisses anything that costs nothing to reopen.** No exit buttons. Exceptions: the
  run-end screen (terminal) and the card-pick reward canvas (click-off there permanently forfeits a
  card on a mis-click, so it keeps an explicit Skip). `LevelUpModal` is a forced choice with no close
  path today and stays that way.

**Messages**
- Routing rule, no exceptions: **anything needing a decision stays a modal; anything that merely
  informs becomes a toast + log entry.** All 36 `ValidationMessage` call sites demote.
- The log is **in-memory, capped at 100, grouped by day, not saved.** It answers "what did I just
  miss", which a session covers; persisting it would cost a save-schema bump plus a migrator plus
  every migrator test that asserts the version number.

**Scope**
- **Combat is out of scope.** It is already being moved on-board by
  `2026-07-24-combat-feel-on-board-design.md` and `2026-07-27-combat-enemy-placement-design.md`,
  which touch the same files. Same philosophy, separate track.
- `mainMenuCanvas` and `runEndCanvas` are system/terminal surfaces and are untouched.
  `HexTooltip`, `EnemyPreviewPanel` and `TutorialBanner` are hover/rail overlays, not menus.

---

## Section 1 — The extensibility spine

This section is why the overhaul is worth doing now rather than per-menu later.

### 1.1 `PlaceTokenBase` — one entry path

`TownToken.OnPointerClick`, `DungeonToken.OnPointerClick` and `ShrineToken.OnPointerClick` are today
near-identical: fog check → teleport deferral → adjacency-move dispatch → "you must be standing here"
→ `BeginVisit` → open UI. Extract:

```
abstract class PlaceTokenBase : MonoBehaviour, IPointerClickHandler, IHexOccupant
{
    public Vector3Int gridPos;              // assigned by GridGeneration
    protected PlayerPosition player;        // cached in Start
    protected Grid gameboard;

    public Vector3Int Cell => gridPos;
    public virtual bool BlocksMove => true; // places are entered by standing on them

    protected abstract string PlaceName { get; }
    protected abstract bool HasMenu { get; }             // shrines: false
    public abstract HexDescriptor Describe();
    public abstract IReadOnlyList<PlaceAction> BuildActions();
    public abstract void Dispatch(PlaceActionId id);

    public void OnPointerClick(PointerEventData e) { /* the shared entry sequence */ }
}
```

`Dispatch` living on the token — not in the fan — is what makes this extensible: `PlaceFan` never
learns about place types, and a new place needs no edit to any shared file.

Registration with `HexOccupantRegistry` (and unregistration in `OnDestroy`) moves into the base as
well, since all three currently repeat it and a missed `OnDestroy` leaks a registry entry.

**Risk to manage:** changing a MonoBehaviour's base class is safe for prefab serialization (component
identity is the concrete class), but the three tokens also carry per-type `[SerializeField]` wiring
that must not be disturbed. The plan should convert one token, verify in the editor, then do the
other two.

### 1.2 `PlaceActionRules` — pure, in the existing `ArchonsRise.Places` asmdef

The tests asmdef already references this assembly (that is how `PlaceRulesTests` runs), so no asmdef
work is needed here.

```
enum PlaceActionId { Assault, Recruit, Heal, Cards, Crystal, Delve, Engage, OpenMenu }

readonly struct PlaceAction
{
    PlaceActionId id;
    IconConcept   icon;        // glyph on the slot
    IconConcept   costIcon;    // Influence / Explore / Crystal; ignored when costAmount == 0
    int           costAmount;  // 0 = no cost badge
    bool          enabled;     // false => UiLock dim + non-interactable
}
```

Three entry points, one per place family — deliberately not one god-struct, since they share no
inputs beyond `visitCanAct` and `hasMenu`:

| Entry point | Snapshot fields | Result |
|---|---|---|
| `ForTown` | `placeType`, `conquered`, `guardiansRemaining`, `influence`, `healCost`, `anyUnitAffordable`, `visitCanAct`, `hasMenu` | `[Assault]` when not conquered; else `AllowedServices(placeType)` filtered and ordered, each `enabled` by affordability **and** `visitCanAct` |
| `ForDungeon` | `complete`, `explore`, `delveCost`, `visitCanAct`, `hasMenu` | `[Delve]` unless complete, `enabled` by `explore >= delveCost && visitCanAct` |
| `ForShrine` | `state`, `crystalCost`, `visitCanAct` | `[Engage]` always; `enabled` only when `state == Live` |

Invariants:

- `OpenMenu` is appended **last** and **always enabled**, but only when the snapshot's `hasMenu` is
  true. Shrines have no detail menu, so they get no ledger slot — this is why `hasMenu` lives in the
  pure layer rather than being assumed.
- `Cards` at a Castle appears **present but disabled** (the M2 purchase-economics stub), not hidden.
  `PlaceService.Cards` genuinely is allowed there; hiding it would misreport the place.
- Action order is fixed and authored in the rules, so the fan never reshuffles between opens.

### 1.3 `FanLayout` — the shared arc renderer

Extracted from `ShrinePanel` so both consumers share it: slot pooling, `FanMath.Solve`, placement +
tilt, the click-off catcher, and parking at the authored offset above screen centre. That parking
needs no per-hex projection for the same reason as the shrine's: entry requires standing on the cell
and the camera rides `PlayerPosition`, so the place is always screen centre.

Two consumers, deliberately different on top of it:
- **`PlaceFan`** — binds a `PlaceAction` list, dispatches ids back to the token.
- **`ShrinePanel`** — keeps its cycling picks and appended checkmark, now laid out by `FanLayout`
  instead of its own copy.

---

## Section 2 — Phase A: the place fan

### 2.1 Scene components

- **`PlaceFanSlot`** — one prefab: icon `Image`, optional cost badge, `Button`, `CanvasGroup`.
  `Bind(PlaceAction, Action<PlaceActionId>)` swaps the sprite, sets the badge, applies `UiLock` when
  disabled. Icon + amount only, never words (the shipped Play/Convert convention). Mirrors
  `ShrineSlotButton`.
- **`PlaceFan`** — `Open(PlaceTokenBase)`, `Dismiss()`. Renders `token.BuildActions()` through
  `FanLayout` and routes clicks to `token.Dispatch(id)`.
- **Live re-gating** — `PlaceFan.Update` rebuilds the action list each frame and re-renders **only
  when it differs**. Delve therefore unlocks the instant an Explore card is played and Recruit locks
  when influence drops, with no per-frame `FanMath` cost and no event wiring. This replaces the five
  scattered `Update()` loops in the `TownButtons` subclasses.
- **`FanPreviewTrigger : PreviewTrigger`** on the slot prefab — remaining guardians for `Assault`,
  the next dungeon enemy (behind `PreviewRules.CanPreview()`) for `Delve`, empty otherwise, anchored
  to the slot. Extending the shipped `PreviewTrigger` means gamepad focus drives it later unchanged.
  The existing `PlacePreviewTrigger` stays on the in-menu `AssaultButton`, untouched.

### 2.2 Dispatch, per place type

| Action | Behaviour |
|---|---|
| `Assault` | `GuardianAssault.Instance.Begin(town)` |
| `Heal` | Raises the same town + influence-cost events as `HealButton`, then `CommitVisitAction` |
| `Recruit` | `RecruitPanel.Open(town)` |
| `Crystal` | Opens the crystal pop-out |
| `Cards` | Disabled stub; no handler |
| `Delve` | The current `DungeonPanel.Delve` body, lifted into a shared method both callers use |
| `Engage` | Swaps the place fan for `ShrinePanel`'s payment fan |
| `OpenMenu` | Town: `townCanvas.enabled = true` + `CreateTown` + `PrepareButtons` + both existing events. Dungeon: `dungeonCanvas.enabled = true` + `DungeonPanel.Open` |

Opening the fan stays a **free peek**; the turn's action is still committed by the service, exactly
as today.

### 2.3 Carry-overs that will silently break if missed

- `DungeonPanel.Open` raises `onDungeonOpenTutorial`. Once the fan is the normal path, players stop
  opening the panel and that one-shot never fires. **The raise moves to fan-open.** (There is no town
  equivalent — the town `?` lives on the town canvas and stays reachable through the ledger slot, so
  nothing else needs moving. Any future panel-open one-shot must use the same fan-open hook.)
- `CreateCrystalButtons.Update` force-disables the crystal buttons whenever `townCanvas.enabled` is
  false. Opening the picker from the fan would leave every crystal permanently non-interactable.
  **That gate must key off the pop-out's own open state.**
- `DataManager.CanSave` blocks saving while the town/dungeon canvas is open. The fan replaces those
  as the normal path, so **the fan must join that guard** or a mid-visit save becomes possible.

### 2.4 What Phase A does not change

`RecruitPanel`, `DisbandPanel` and the crystal picker's contents; `GuardianAssault`; the shrine
payment rules; the full town and dungeon menus and their `?` icons; and every turn/action rule.

---

## Section 3 — Universal click-off

**`ClickOffCatcher`** — a full-screen transparent `Image` with `raycastTarget` enabled, at sibling
index 0 so it renders *behind* content and never swallows content clicks, firing a `UnityEvent` on
click. One prefab per surface, each wiring that surface's close method.

**Full surface inventory and verdict:**

| Surface | Today | Change |
|---|---|---|
| `townCanvas` | full-screen menu + close button | Fan replaces it as the default; menu behind ledger; close button → catcher |
| `dungeonCanvas` | panel + Close/Leave button | Fan replaces it; panel behind ledger; button → catcher |
| `ShrinePanel` | bespoke fan + own catcher | Entry unified; catcher → shared `ClickOffCatcher` |
| crystal pop-out | `CrystalDismissCatcher` | Bespoke catcher → shared one; `CrystalDismissCatcher` deleted |
| `RecruitPanel` | `cancelButton` | Button deleted → catcher |
| `DisbandPanel` | own Canvas | Catcher added (cancels the pending hire; nothing is spent yet) |
| `UnitPickerPanel` | `doneButton` | Button deleted → catcher |
| `HelpPopup` | X button + its own outside-click catcher | X deleted; catcher → shared one |
| `cardCanvas` / `CardInspector` | information | Catcher added |
| `unitCanvas` / `UnitInspector` | information | Catcher added |
| `cardListCanvas` | information | Catcher added |
| `messageCanvas` | blocking modal | **Deleted entirely** (Phase B) |
| `cardRewardCanvas` | forced choice | **No catcher.** Keeps Skip — forfeiting a card is unrecoverable |
| `LevelUpModal` | forced choice, no close path today | **No catcher.** Unchanged |
| `runEndCanvas` | terminal | **No catcher.** Unchanged |
| `mainMenuCanvas` | system menu | Out of scope |
| `combatCanvas` | combat | Out of scope — in-flight rework |

---

## Section 4 — Phase B: toast rail + player log

### 4.1 Pure layer — `Assets/Scripts/Log/` (**new** `ArchonsRise.Log` asmdef)

A new pure folder needs its own asmdef **and** a reference added to the EditMode tests asmdef, or the
tests fail to resolve the types (`CS0103`) even though the game compiles.

```
readonly struct LogEntry { int day; string text; }
```

`PlayerLogCore` — `Append(day, text)` into a ring buffer capped at **100**, `Entries` newest-first,
`Clear()`. Day dividers are **derived at render time** from where `day` changes between adjacent
entries, never stored as pseudo-entries, so eviction cannot orphan a header.

`text` already carries `IconMarkup` sprite tags, so entries render identically to today's messages
with no reformatting.

### 4.2 Scene layer

- **`GameLog`** — lazily-created scene singleton (the `RewardQueue` / `ConquestTracker` pattern: no
  scene wiring, and scene scope means a new run starts blank). `Post(string)` stamps the current
  `GameManager.Round` as the day, appends to the core, and hands the text to the rail. Suppressed
  when `RunEndController.HasEnded`, matching today's guard.
- **`ToastRail`** — spawns a toast prefab into a corner container: fade in, dwell ~3.5s, fade out,
  destroy. Max 4 visible; a 5th makes the oldest begin its fade early. The rail's `CanvasGroup` sets
  `blocksRaycasts = false` and its canvas sorts above everything, so a toast can float over a
  card-pick modal without ever eating a click.
- **`LogPanel`** — a HUD button opens a scrollable, newest-first list with day dividers. Closes by
  clicking off, per Section 3. No exit button.

### 4.3 Migration, including the guard tail

1. `GameManager.ValidationMessage` becomes a one-line forwarder to `GameLog.Post` — a single-file
   change that converts all 36 call sites at once and keeps everything compiling.
2. A mechanical rename pass replaces the call sites with `GameLog.Post` and deletes the shim.
3. **Delete** `messageCanvas`, `messageText`, `returnButton`, `ReturnButton()`, the `messageDone`
   field, and `MessageController` (which exists solely to dismiss the message canvas).
4. **Drop the `messageCanvas.enabled` guards** — they exist because the old canvas blocked input, and
   toasts do not:

   | Site | Guard today |
   |---|---|
   | `DataManager:101` | blocks save while a message is up |
   | `HandFocusController:26` | suppresses hand focus |
   | `UnitsLane:51` | suppresses lane interaction |
   | `UnitInspectorNavController:30` | tracks `_messageWasUp` |
   | `RunEndController:77` | force-closes it on run end |

### 4.4 Behavioural consequences to expect in playtest

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
the editor is open).

**`PlaceActionRulesTests`**
- Unconquered place → `[Assault, OpenMenu]` only.
- Conquered Town → Recruit/Heal/Crystal in the authored order, then `OpenMenu`.
- Conquered Castle → includes `Cards`, present but `enabled == false`.
- Influence below `healCost` → `Heal` present, `enabled == false`.
- `visitCanAct == false` → every service disabled, `OpenMenu` still enabled.
- Complete dungeon → `[OpenMenu]` only.
- `explore < delveCost` → `Delve` present, disabled.
- Live shrine → `[Engage]` enabled, **no** `OpenMenu` (`hasMenu == false`).
- Dormant and guarding shrines → `[Engage]` present, disabled.

**`PlayerLogCoreTests`**
- Appending past the cap evicts oldest-first and leaves exactly 100 entries.
- `Entries` is newest-first.
- Divider derivation across a day boundary, including when eviction removes a day's first entry.
- `Clear` empties the buffer.

Scene behaviour — fan layout, toast timing, click-off ordering, and the `PlaceTokenBase` conversion's
effect on movement and teleport targeting — is verified by play acceptance, not automated tests.

## Editor authoring

Scene and prefab wiring is done manually in the Unity editor from step-by-step instructions; the
implementation plan produces those instructions rather than hand-editing scene or prefab YAML.

- `PlaceFanSlot` prefab + per-action sprites (Assault, Recruit, Heal, Cards, Crystal, Delve, Engage,
  ledger).
- `PlaceFan` object on the gameplay canvas with its `fanContainer` offset and `FanSettings`.
- `ShrinePanel` re-pointed at the shared `FanLayout`.
- `ClickOffCatcher` prefab into each surface in the Section 3 table, each wired to that surface's
  close method.
- Toast prefab + `ToastRail` container; `LogPanel` with its scroll view and HUD open button.
- Removal of the message canvas and the retired exit/cancel/done/X buttons.

## Acceptance (USER)

1. Stand on an unconquered Keep, click it → a single Assault icon fans over the player plus the
   ledger icon; hovering Assault previews the remaining guardians.
2. Conquer it, reopen → Recruit and Crystal fan out; Recruit greys out when influence is short and
   ungreys the moment influence rises, without closing the fan.
3. Click the ledger icon → the existing full town menu opens exactly as before, help `?` included.
4. Stand on a dungeon → Delve shows its Explore cost, greys until enough Explore is played, and
   hovering it previews the next enemy.
5. Stand on a live shrine → Engage fans and opens the payment widget unchanged. A spent or guarded
   shrine shows a disabled Engage rather than a message.
6. Buying a crystal from the fan works with the town menu never opened.
7. Clicking off any fan, menu, panel, inspector or popup closes it with nothing spent. The only exit
   buttons left in the game are the card-pick Skip and the run-end/main-menu controls.
8. Defeat several enemies in one fight → reward toasts stack in the corner and fade on their own; no
   click is needed to return to play; the card pick opens underneath them.
9. Open the log from the HUD → every message from the run so far, newest first, grouped by day.
10. Movement, adjacency-click-to-move and teleport targeting onto all three place types behave
    exactly as before the `PlaceTokenBase` extraction.

## Explicitly out of scope

Combat UI (in-flight rework), the main menu, the run-end screen, hover overlays (`HexTooltip`,
`EnemyPreviewPanel`), the tutorial banner rail, and any change to game rules, costs or balance.
