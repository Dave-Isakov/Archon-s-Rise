# Hand & Card-Play Rework — Design

**Date:** 2026-06-29
**Status:** Approved design, ready for implementation planning
**Scope:** Rework how the player hand is displayed and how cards are inspected and played
(Improvise / Empower / Choice / Play), with gamepad support in mind. Precedes gameplay-loop
milestones M2/M3.

## Goal

Replace the current flat `GridLayoutGroup` hand and the toggle-driven card menu with:
1. A **static fan arc** hand at the bottom of the screen with a focused-card lift.
2. A **center-screen pop-out** that floats the selected card to the middle and wraps it with
   distinct **Improvise / Empower / Choice / Play** sections.
3. A clean, single-source-of-truth play-mode model that **fixes the choice/improvise selection-wipe bug** by construction.
4. Satisfying **play-commit feedback** (echo-flight to the boosted stat, crystal-spend flourish, stat count-up).
5. **Full gamepad support** via Unity's new Input System, working alongside mouse/keyboard.

This is a presentation + input rework. It deliberately does **not** touch the stat math, the
command/undo system, or the crystal consume/restore path.

## Background — why the bug exists

The current card menu is a web of `Toggle`s that raise `ToggleEvent`/`CardEvent` at each other:
`ImprovToggle.ToggleImprovButtons()` raises `OnToggle_ButtonsToggleOnOff` →
`ChoiceToggles.DeactivateToggle()` → deactivates the choice toggle and resets the play listener.
There is no single owner of "how will this card be played," so turning Improvise on destroys the
player's choice selection. The redesign introduces one state object and makes the sections render
from it, so nothing can destroy peer state.

## Locked design decisions

These were decided during brainstorming and are fixed inputs to the plan:

- **Fan:** static fixed arc (not cursor-tracking). Spread ≈ ±33° across the hand. Focused card
  lifts ≈ **40px** and scales **1.3×**, untilted; non-focused cards dim (~0.86 brightness).
  Wounds read as a distinct grey card.
- **On select:** the hand stays visible but dims; the selected card floats to **center screen**.
- **Pop-out layout:** card centered, **Choice banner on top** (only for choice cards),
  **Improvise panel on the left**, **Empower panel on the right**, **Play bar on the bottom**
  with a **Back** control. Selection is segment-based, not checkboxes; the card and the Play
  button update live to reflect the current selection (Play reads e.g. "PLAY · +2 Attack").
- **Play-mode rules:** a card resolves in exactly one mode — **Normal** (printed values),
  **Choice** (one chosen stat for multi-flag cards), or **Improvise** (flat +1 to a chosen stat).
  **Empower** is an orthogonal modifier allowed for Normal/Choice only. Improvise is mutually
  exclusive with Empower **for now**, expressed as a rule (`CanEmpower`) so a future
  "empowered improvise" mechanic is a one-line rule change, not a rewrite. When Improvise is
  active, the Choice and Empower sections dim and lock with a one-line reason; **no state is destroyed** — toggling Improvise off restores the prior choice/empower selection.
- **Empower = display-only crystal reservation:** toggling Empower on visually reserves a matching
  crystal (does not consume it); toggling off returns it. The real consume/restore stays in the
  existing Play/Undo path.
- **Play feedback:** echo-flight (ghost + floating value) to the boosted stat, crystal-spend
  flourish when empowered, improved stat count-up/pop. The real card stays in hand (marked played)
  until commit, then slides to discard.
- **Gamepad:** full support now via the new Input System, alongside mouse/keyboard.

## Approach (chosen: incremental layer-swap)

Keep the proven backbone — `PlayCommand`, `ICommands`, `GameManager.commands`
(`AddCommand`/`ClearStack`/`Commit`), and every `Player` stat method — **unchanged**. Replace only
the presentation + selection layer. The new controller routes a play to the *same* existing
`CardEvent` that the old buttons raised, so undo, commit-to-discard, and turn-end reset all keep
working.

Rejected alternatives: a full card-system rebuild (touches stat/command core right before the
gameplay milestones — too much regression surface) and a minimal patch (doesn't deliver the
center pop-out / wrap panel the design calls for and leaves the fragile toggle web in place).

## Architecture

### CardPlaySelection (new, plain C# class — not a MonoBehaviour)

The single source of truth for an in-progress play. Pure logic, unit-testable in EditMode.

```
enum PlayMode { Normal, Choice, Improvise }

class CardPlaySelection
    Card card
    PlayMode mode
    StatType chosenStat        // the chosen stat for Choice or Improvise
    bool empowered

    int[] ResolveStats()       // {atk, def, inf, exp} this play will apply
    bool  CanEmpower()         // card.cardSO.empowerType != None && mode != Improvise
    bool  IsPlayable()
    string Describe()          // "+2 Attack", "+1 Defend (improvised)" — drives the Play label
```

- **Choice list = the `StatType` [Flags] enum.** A choice card is one whose `cardSO.cardType`
  has multiple flags; the Choice banner generates one segment per set flag (cards have ≤ ~3 in
  practice). No separate choice-list data is introduced.
- `ResolveStats()` mirrors the existing math: Normal → `cardSO.GetCardStats(empowered)`; Choice →
  only the chosen stat via `ReturnAttack/Defend/Influence/Explore(empowered)`; Improvise → flat +1
  to the chosen stat. The class computes the **preview**; the actual application still happens in
  `Player` via the existing events (see Commit).

### CardInspector (new MonoBehaviour — the controller)

Owns the current `CardPlaySelection` and drives the pop-out. Responsibilities:
- Open/close: float the focused card to center, dim the hand, show/hide the wrap-around sections.
- Hold the section-view references and call `Render()` on them whenever the selection changes.
- Expose mutators the views call back into: `SetMode(PlayMode)`, `SetChosenStat(StatType)`,
  `ToggleEmpower(bool)`. These enforce the rules centrally (e.g. entering Improvise locks
  Choice/Empower visually but keeps their stored values).
- Hold the ~7 existing `CardEvent` references currently scattered across `PlayButton`/`ImprovButtons`,
  and on Play pick the one matching the selection (see Commit) and push a `PlayCommand`.

### Section views (new, small MonoBehaviours; replace the old toggle scripts)

Each reads from the selection and calls back into the inspector — they never talk to each other.
- **ChoiceBanner** — one segment per `cardType` flag; selecting sets `chosenStat` + `mode = Choice`.
  Hidden for non-choice cards.
- **ImprovisePanel** — four +1 stat options; selecting sets `chosenStat` + `mode = Improvise`.
  Hidden/disabled for non-empowerable cards (Wounds, `EmpowerType.None`).
- **EmpowerPanel** — a crystal-spend control; toggles `empowered` and drives the reservation +
  the `+2 → +4` preview. Disabled when `!CanEmpower()`.
- **PlayBar** — a single Play button whose label is `selection.Describe()`, plus Back.

These replace and delete: `Empower.cs`, `ImprovToggle.cs`, `ImprovButtons.cs` (+ `ImprovAttack/Defend/Explore/Influence`),
`ChoiceToggles.cs` (+ `AttackToggle/DefendToggle/InfluenceToggle/ExploreToggle`), `PlayButton.cs`,
and the `ToggleEvent` wiring between them. `Improv.cs`/`CardMenuInterface.cs` are removed if no
longer referenced.

### Commit — routing to the existing command/undo (unchanged)

On Play, the inspector:
1. Sets `card.IsEmpowered = selection.empowered`.
2. Selects the existing `CardEvent` for the selection and pushes the existing command:
   `GameManager.commands.AddCommand(new PlayCommand(eventForSelection, card))`.

| Selection | Existing event → `Player` method (unchanged) |
|---|---|
| Normal | `onPlay_SetCardDataToPlayer` → `PlayCard` |
| Choice = Attack | `onPlay_SetAttackDataToPlayer` → `AttackChoice` |
| Choice = Defend / Influence / Explore | matching `…ChoiceToPlayer` event |
| Improvise = Attack | improv attack event → `ImprovAttack` |
| Improvise = Defend / Influence / Explore | matching improv event |

Empower's crystal consume already lives inside `Player.EmpowerCrystalCheck` (and restore in
`UndoEmpower`), fired on execute/undo. The inspector only sets the flag and picks the route.
`PlayCommand`, `ICommands`, `commands.*`, and all `Player` stat methods are untouched.

### Empower crystal reservation (display only)

Today the Empower toggle (`CrystalInventory.ToggleEmpowered`) only sets `IsEmpowered`; the real
spend is `EmpowerCrystal()` at Play and `RegenCrystal()` at Undo. We add a thin visual layer:
- New `Crystal.SetReserved(bool)` — dims/marks the crystal sprite; the crystal **stays in
  `crystalsInInventory`**.
- EmpowerPanel on: `SelectEmpowerCrystal()` (or the All-crystal) → `SetReserved(true)`,
  `card.IsEmpowered = true` (drives preview/glow). Off: `SetReserved(false)`, `IsEmpowered = false`.
- Play → existing `EmpowerCrystal()` consumes a matching crystal for real (untouched).
  Undo → existing `RegenCrystal()` restores it (untouched); reserved visual cleared.
- If no matching crystal exists, the toggle refuses with the existing validation message.

## Hand presentation (fan)

`PlayerHand` keeps its responsibilities (draw, wounds, heal, rebuild-from-save, `cardsInPlay`),
but card layout moves from `GridLayoutGroup` to a **fan layout** component using DOTween:
- Cards positioned along a fixed arc (≈ ±33° spread), evenly spaced, lower at the edges.
- Focused card (mouse hover or controller focus) lifts ≈ 40px, scales 1.3×, untilts, z-sorts on top;
  others dim.
- Wounds styled distinctly (grey) and non-interactive for play.
- Re-layout on draw/discard/heal so the arc stays balanced as the hand size changes.

The save/load hand rebuild (`RebuildHand`, `IsLoading` guard) and the `cardListCanvas`
"view whole hand" path are preserved.

## Play-commit feedback / juice

A play is undoable, so the card itself does not fly to discard on Play. Three layers (all DOTween,
all reversible):
1. **Echo-flight to the stat** — the real card returns to its fan slot marked *played*
   (existing `playedIcon`); a color-coded **echo** (ghost of the card + a floating "+N" in the
   stat's color) arcs from the card to the matching stat in `StatsDisplay`. On Undo, a fainter
   echo flies back and the value counts down.
2. **Stat pop / count-up** — `StatsDisplay` changes from rewriting text every frame to detecting a
   value change and animating old→new with a punch-scale + color flash. Runs in reverse for undo.
3. **Crystal-spend flourish (empower only)** — the reserved crystal drains/shatters toward the card
   just before the stat echo, hooked onto the existing `EmpowerCrystal()`; reverse on `RegenCrystal()`.

On **commit** (command stack cleared at an exploration/influence/turn-end point), the played cards
in the fan slide together to the discard pile in one sweep.

## Input / gamepad (new Input System)

- Add `com.unity.inputsystem`; set **Active Input Handling = Both** so the three legacy-`Input`
  files (`DataManager`, `PlayerPosition`, the commented block in `Card`) keep working. Migrate them
  opportunistically; not a blocker.
- Add a `UI.inputactions` asset (Navigate / Submit / Cancel / Point / Click) and swap the
  `EventSystem` from `StandaloneInputModule` → `InputSystemUIInputModule`. Existing
  `IPointerClickHandler` UIs keep working unchanged.
- New `HandFocusController`: Left/Right moves the focused card in the fan (drives the lift),
  Submit opens the inspector for the focused card.
- In the inspector, the wrap-around layout maps to directions: **Up = Choice, Left = Improvise,
  Right = Empower, Down = Play**; Navigate cycles options within a section; Submit selects/toggles;
  Cancel backs out to the fan. Mouse and gamepad operate simultaneously.

## Testing

- **No project test assembly exists today** (scripts compile into `Assembly-CSharp`). Add an
  EditMode test assembly for the pure-logic `CardPlaySelection`.
- EditMode coverage: `ResolveStats` for Normal/Choice/Improvise (± empower), the `CanEmpower` rule,
  `Describe`, and the **regression test for the bug**: select Choice=Attack → switch to Improvise →
  switch back, assert the choice survives.
- MonoBehaviour/visual layers (fan, inspector, juice, input navigation) are verified manually in
  Play mode.

## Phased delivery (one spec, four phases)

Each phase is independently verifiable:
1. **Selection model + inspector wiring** — `CardPlaySelection`, `CardInspector`, section views,
   empower reservation; routes to the existing commands; deletes the old toggle scripts.
   **Fixes the bug.** EditMode-tested. UI functional-but-plain.
2. **Fan hand presentation** — static fan arc, focus lift, dim, wound styling, re-layout.
3. **Pop-out visuals + juice** — center float, wrap-around panel styling, echo-flight,
   crystal flourish, improved stat pop.
4. **Gamepad / Input System** — package, `UI.inputactions`, module swap, `HandFocusController`,
   inspector navigation.

## Out of scope / non-goals

- No change to stat math, `PlayCommand`/`ICommands`/`commands.*`, or the crystal consume/restore path.
- No "empowered improvise" mechanic yet (design only anticipates it via the `CanEmpower` rule).
- No new card content, balance, or gameplay-loop systems (those are milestones M2/M3).

## Dependencies & risks

- **DOTween** (incl. DOTween UI module) is already vendored at `Assets/Plugins/Demigiant` — no new
  dependency for animation.
- **Input System migration risk:** keep Active Input Handling = Both; the three legacy-`Input`
  files must be verified after the package is added.
- **Wholesale deletion** of ~10 menu scripts: confirm no scene/prefab references remain (the
  inspector + views replace them; rewire the card-menu prefab/canvas accordingly).
