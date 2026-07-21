# Turn-Phase System (Spec 1 of 2) — Design

**Date:** 2026-07-21
**Status:** Approved (pending final spec review), ready for implementation plan
**Scope note:** This is **Spec 1** of a two-spec change. Spec 1 (this doc) restructures the
**turn** into phases, turns the **round** into a fixed-length "day," reworks the undo/commit model,
and adds a phase HUD + tutorial. **Spec 2** (a later, separate brainstorm) replaces the current
single-enemy combat with a multi-enemy, phased (Siege→Defend→Attack→auto-flee) model shared by field
combat and guardian assaults. Combat is untouched here beyond becoming "the Action-phase interaction."

## Problem

The turn has no structure and no action limit. A player builds one large pool from a single hand and
then freely fights multiple enemies, recruits, buys, and moves in the same turn, and can stall
indefinitely across turns to farm crystals — which defeats Doom's role as an end-of-round pressure.
There is no per-turn strategic decision ("assault this guardian *or* move toward the dungeon"), which
is the "too much freedom, not enough strategy" the design currently suffers from.

Mechanically today:
- A turn = play cards → build the four action pools (+ Siege) → take *any number* of actions →
  End Turn (pools reset, hand tops up) or End Round (full reset, doom tick, unit/skill refresh).
- The undo stack (`PlayManager`) commits (`ClearStack`) on movement (`Player.Exploration`),
  influence spends (`Player.Influence`), combat teardown, End Turn, and End Round — so **movement is
  already a hard commit point** and cannot be undone.
- Movement is not a command; `Player.Exploration` just sets the explore value and clears the stack.
- Rounds advance only by an explicit **End Round** button (or a forced case when the deck can't
  refill), so nothing bounds how long a player lingers.

## Goals

- A turn is **strictly one-way**: **Explore → Action → End**, cycling inside a bounded Round.
- **Exactly one interaction** ("encounter or visit") per turn, making each turn a real decision.
  Moving and acting are both **optional**; the only turn-flow control is **End Turn**.
- Phase transitions are **implicit**, not button-driven: taking the action ends Explore; pressing
  **End Turn** ends the turn. No phase-advance button, no manual End Round button.
- The **round is a fixed "day"** of N turns; when the budget runs out the round **auto-ends** (forced
  long rest: reshuffle + Doom tick + unit/skill refresh), so Doom advances on a bounded cadence and
  stalling is impossible.
- **Movement becomes undoable** — the undo stack commits on action-start, End Turn, round-end, and
  irreversible reveals (fog), never on an ordinary move.
- A **phase HUD label** plus a **turns-remaining countdown** (repurposed from the Round/Turn text)
  tell the player where they are in the turn and how much of the day is left.
- The **tutorial** teaches the Explore→Action→End rhythm, the one-action rule, and the day countdown.

**Non-goals:** any combat change (single-enemy combat stays exactly as it is — that is Spec 2);
touching `CombatRules`; the difficulty/guardian-count work; authored enemy attack effects; a
save-schema bump.

## Design

### 1. The turn state machine

Three phases per turn, strictly one-way, cycling inside a bounded Round:

- **Explore** — the player may **move** (spend Explore) and reveal the map. Card play, conversion,
  unit options, and skills are also allowed (pools are open all turn).
- **Action** — the player takes **exactly one** encounter-or-visit. Entering an action is the
  **implicit Explore→Action transition** — movement ends and the movement stack commits. Card play is
  still allowed here (e.g. to build Attack before starting a fight).
- **End** — triggered by the **End Turn button**: runs the existing `TurnEnd` (pools reset to 0, hand
  tops up from the deck), decrements the day's turn budget, then a new turn begins at Explore.

Rules that fall out of this:

- **Both moving and acting are optional.** A player may press End Turn with no move and no action.
- **Movement is Explore-only.** After the action is taken the player cannot move again this turn.
- **Interactions are Action-only and capped at one**, tracked by an `actionTaken` flag reset each
  turn.
- **"One action" = one encounter or one visit:**
  - one **combat**, OR
  - one **place-visit** — entering a Town/Keep/Castle; *within that single visit* the player may use
    **all** the place's services (recruit, heal, buy crystals, assault). The army cap already limits
    recruiting, so an open visit is not over-permissive, OR
  - one **dungeon delve** (spends the dungeon's explore cost + its one fight). A full 3-delve dungeon
    therefore takes three turns.
- **Card play / conversion / unit options / skills are allowed in any phase.** This is deliberate: it
  keeps stat conversion (e.g. Defend→Influence) usable everywhere, in and out of the Action.
- **End Turn gate (unchanged from today):** blocked only when the hand is full and nothing has been
  played this turn (ending would draw nothing and merely tick the counter). Once any card is played,
  End Turn is unrestricted. This is exactly the existing `EndTurnButton.HandFullUnplayed` logic.

### 2. The round as a bounded "day"

- A round is a fixed budget of **`turnsPerRound`** turns (a tuning constant in `balance.md`; a future
  lever could let Doom shrink it). The current turn count is shown as a **countdown** (see §4).
- Each **End Turn** decrements the remaining turns. When it reaches **0 the round auto-ends** — no
  button. The **deck-can't-refill case also auto-ends the round** (secondary trigger) so the player
  never stalls with a dead hand.
- Round-end (the "long rest") is the existing `RoundPlus` behavior, now fired automatically:
  full hand reset (discard + unplayed hand → deck, shuffle, fresh full hand), **Doom rises**,
  units/skills refresh, the turn budget resets to `turnsPerRound`, and a new turn begins at Explore.
- The manual **End Round button is removed.** Nothing else about Doom/round pacing changes; it simply
  advances on a bounded, unavoidable cadence now.

### 3. Phase-state architecture

- **`TurnPhase` enum** `{ Explore, Action, End }`.
- **Pure `TurnPhaseRules`** (no scene/Unity dependency, unit-testable via the mcs pure-test harness):
  - `CanMove(TurnPhase)` — true only in Explore.
  - `CanInteract(TurnPhase, bool actionTaken)` — true only in Explore/Action with the action not yet
    spent (entering an action performs the Explore→Action transition itself).
  - `ShouldCommitOnMove(bool revealedNewFog)` — the fog-reveal commit predicate (see §4/undo).
- **Pure `RoundRules`** (also mcs-testable): given `turnsRemaining` and whether the deck can refill,
  `IsRoundOver(...)` and the next `turnsRemaining` after a turn end. Keeps the day math out of the
  MonoBehaviour.
- **`TurnPhaseController`** (MonoBehaviour, singleton like the other managers): owns `CurrentPhase`,
  `actionTaken`, and `turnsRemaining`; raises **`onPhaseChanged`** (and a turns-remaining event for
  the HUD). It exposes `BeginAction()` (called by interaction entry points — sets `actionTaken`,
  commits the stack, moves to Action) and `EndTurn()` (runs `TurnEnd`, decrements the day, auto-ends
  the round when `RoundRules.IsRoundOver`, else starts the next turn at Explore).
- **Existing systems query the controller** instead of acting freely:
  - Movement (`ExplorationButton` / arrow buttons) is enabled only when `CanMove` is true.
  - Combat-start, place-menu-open, and dungeon-delve entry check `CanInteract` and route through
    `BeginAction()`.
- Spec 2's combat sub-phases will be a **separate `CombatPhase` machine** running entirely inside one
  Action; it shares only the HUD-label helper with this system, not the state machine.

### 4. Undo / commit rework

Today `ClearStack()` is called from movement, influence spend, combat teardown, End Turn, and End
Round. The new model commits on exactly these events:

1. **Action start** (`BeginAction()`) — committing the movement/Explore stack; you can't undo your
   path once you commit to the encounter/visit.
2. **End Turn** and **auto round-end.**
3. **Irreversible reveals** — a move that uncovers previously-hidden fog. (Spec 2 will add
   "movement that triggers a forced engage" to this list.)

**Movement becomes a real `MoveCommand`** pushed on the undo stack:
- **Execute:** reposition the player, spend Explore, reveal any newly-uncovered fog.
- **Undo:** reposition back, refund Explore, re-hide the fog it revealed.
- A move that reveals **no** new fog stays undoable. A move that **does** reveal new fog
  **auto-commits** immediately after (an explicit `ClearStack()`), so it drops off the undoable
  stack — the player can never undo away knowledge they have gained. This is
  `TurnPhaseRules.ShouldCommitOnMove(revealedNewFog)` in action.

**Deliberately unchanged (commit immediately, as today):** influence spends, crystal purchases, and
combat resolution. These are treated as irreversible economic/informational reveals, consistent with
the fog rule, keeping the change surgical — **movement is the one newly-undoable action**. (Making
purchases undoable-until-boundary is a clean future extension but widens the blast radius into the
crystal/influence commit paths; out of scope for Spec 1.)

### 5. HUD + turn-flow buttons

**Repurposed day countdown.** The existing `Round: X Turn: Y` TMP (drawn in `GameManager.Update`)
becomes a **turns-remaining countdown**, e.g. `Turns left: 3`. Round/turn *numbers* are no longer
shown to the player (round number is still tracked internally for Doom/save). Updated **event-driven**
off the controller's turns-remaining event rather than per-frame.

**Phase label.** A new TMP beside the countdown reading `Phase: Explore` / `Action` / `End`, updated
event-driven off `onPhaseChanged`. Optional per-phase color tint for legibility (fits M2.11's icon
language).

**Buttons.** Today: End Turn, End Round, Undo. Rework:
- **End Turn** is the only turn-flow control kept. Pressing it runs the End phase; it also triggers
  the automatic round-end when the day's budget is spent (or the deck can't refill). Keeps its
  existing `HandFullUnplayed` gate.
- **End Round button removed** (round-end is automatic — §2).
- **No phase-advance button** (Explore→Action is implicit via taking the action — §1).
- **Undo** unchanged.
- The existing `TurnButtonGate` simplifies: "no advancing out of Action mid-combat" still gates End
  Turn; the old "deck can't refill → must End Round" branch becomes "End Turn auto-ends the round"
  instead of disabling the button. Gamepad `TurnFlowShortcuts`: North = End Turn, West = Undo — the
  End Round shortcut path is dropped.

The actual scene/prefab wiring (repurposed countdown TMP, new phase-label TMP + listener, removed End
Round button) is **manual USER editor work** from step-by-step instructions; no hand-edited
scene/prefab YAML.

### 6. Tutorial refresh + save/load

**Tutorial.** The M2.12 rail currently teaches a free-form "play a card → move → fight → end turn."
It is re-sequenced to teach the phases + the day, keyed off the new `onPhaseChanged` / turns-remaining
events:
- Rail steps roughly: "This is the **Explore** phase — move with your Explore." → "Taking an action
  ends exploring — you get **one** per turn: fight, visit, or delve." → "**End the turn** when you're
  done; the day counts down and refreshes when it hits zero."
- One `HelpEntrySO` (`?` icon) on the countdown/phase area explaining the Explore→Action→End rhythm,
  the one-action rule, and the day/Doom cadence.
- Content authoring (the `TutorialStepSO` / `HelpEntrySO` assets + a `TutorialTarget` on the HUD) is
  USER editor work; the design supplies exact copy + wiring steps, and `TutorialCopyValidationTests`
  is updated to pin the new copy.

**Save/load. No schema bump.** The save already persists round/turn, so `turnsRemaining` (and the
internal round number) ride the existing fields. On load, **phase resets to Explore with
`actionTaken = false`**; the day's remaining-turns state restores from the save. Mid-turn quit is an
edge case and pools already reset predictably, so re-entering Explore on load is acceptable.

## Testing

- **Pure / TDD via the mcs harness:**
  - `TurnPhaseRules` — `CanMove`, `CanInteract`/action-spent, `ShouldCommitOnMove` (fog-reveal commit
    predicate).
  - `RoundRules` — `IsRoundOver` (budget-exhausted and deck-can't-refill triggers) and the
    next-`turnsRemaining` math.
- **Manual in-editor (step-by-step instructions + acceptance checklist):** the scene wiring — day
  countdown TMP, phase label TMP, removed End Round button, movement/interaction gating hookups,
  tutorial assets — consistent with the EditMode-while-editor-open constraint.
- `CombatRules` is untouched (Spec 2).

## Risks / Notes

- **Behavior change is intentional and large:** free-form, unbounded multi-action turns become
  one-action phased turns inside a fixed-length day. This is the core goal, not a regression.
- **Movement-as-command** is the main new mechanism; the fog-reveal detection must be reliable, or an
  undo could restore hidden fog (hence the pure `ShouldCommitOnMove` test).
- **`turnsPerRound` needs tuning** so a round's deck draw and Doom pacing feel right; place it in
  `balance.md` and expect to iterate. The deck-can't-refill secondary round-end trigger must be wired
  so a short deck can't strand the player mid-day.
- **Scene wiring performed manually by the user:** the countdown/phase-label TMPs + listeners, the
  removed End Round button, gating hookups, and the tutorial content assets. Provide exact editor
  steps; never hand-edit scene/prefab YAML.
- **Interaction gating must be complete:** every entry point that starts an encounter/visit (combat
  start, place-menu open, delve entry) must route through `BeginAction()`, or the one-action rule
  leaks.
- **Decisions to record** in `../../.claude/skills/archons-rise-roadmap/decisions-log.md` on
  implementation: the Explore→Action→End turn model; the one-encounter/one-visit action definition;
  implicit phase transitions with End Turn as the only control; the bounded round/"day" with
  automatic round-end and a `turnsPerRound` budget; the repurposed HUD countdown; the
  movement-undoable / commit-on-fog-reveal undo rule; and the no-schema-bump load reset.
- A new milestone entry (e.g. **M2.13 — Turn phases & bounded rounds**) should be added to
  `milestones.md`, with Spec 2 (multi-enemy phased combat) queued after it. `balance.md` gains the
  `turnsPerRound` tuning value; `mechanics.md`'s Turn/Round Flow section is updated to the new model.
