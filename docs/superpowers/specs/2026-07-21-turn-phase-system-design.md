# Turn-Phase System (Spec 1 of 2) — Design

**Date:** 2026-07-21
**Status:** Approved, ready for implementation plan
**Scope note:** This is **Spec 1** of a two-spec change. Spec 1 (this doc) restructures the
**turn** into enforced phases, reworks the undo/commit model, and adds a phase HUD + tutorial.
**Spec 2** (a later, separate brainstorm) replaces the current single-enemy combat with a
multi-enemy, phased (Siege→Defend→Attack→auto-flee) model shared by field combat and guardian
assaults. Combat is untouched here beyond becoming "the Action-phase interaction."

## Problem

The turn has no structure and no action limit. A player builds one large pool from a single hand
and then freely fights multiple enemies, recruits, buys, and moves in the same turn. There is no
per-turn strategic decision ("assault this guardian *or* move toward the dungeon"), which is the
"too much freedom, not enough strategy" the design currently suffers from.

Mechanically today:
- A turn = play cards → build the four action pools (+ Siege) → take *any number* of actions →
  End Turn (pools reset, hand tops up) or End Round (full reset, doom tick, unit/skill refresh).
- The undo stack (`PlayManager`) commits (`ClearStack`) on movement (`Player.Exploration`),
  influence spends (`Player.Influence`), combat teardown, End Turn, and End Round — so **movement
  is already a hard commit point** and cannot be undone.
- Movement is not a command; `Player.Exploration` just sets the explore value and clears the stack.

## Goals

- A turn is **strictly one-way**: **Explore → Action → End**, cycling inside the existing Round.
- **Exactly one interaction** ("encounter or visit") per turn, making each turn a real decision.
- **Movement becomes undoable** — the undo stack commits on phase boundaries and on irreversible
  reveals (fog), never on an ordinary move.
- A **phase HUD label** beside the Round/Turn text tells the player which phase they are in.
- The **tutorial** teaches the Explore→Action→End rhythm and the one-action rule.

**Non-goals:** any combat change (single-enemy combat stays exactly as it is — that is Spec 2);
touching `CombatRules`; the difficulty/guardian-count work; authored enemy attack effects; a
save-schema bump.

## Design

### 1. The turn state machine

Three phases per turn, strictly one-way, cycling inside the unchanged Round structure (Round end
still does the full hand reset + doom tick + unit/skill refresh):

- **Explore** — the player may **move** (spend Explore) and reveal the map. Card play, conversion,
  unit options, and skills are also allowed (pools are open all turn). Advancing commits the stack.
- **Action** — the player takes **exactly one** encounter-or-visit, then advances (skipping is
  allowed). Card play is still allowed here (e.g. to build Attack before starting a fight).
- **End** — runs the existing `TurnEnd` (pools reset to 0, hand tops up), then a new turn begins at
  Explore.

Rules that fall out of this:

- **Movement is Explore-only.** After entering Action the player cannot move again this turn.
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

### 2. Phase-state architecture

- **`TurnPhase` enum** `{ Explore, Action, End }`.
- **Pure `TurnPhaseRules`** (no scene/Unity dependency, unit-testable via the mcs pure-test harness):
  - `Next(TurnPhase)` — legal one-way transition (End wraps to Explore of the next turn).
  - `CanMove(TurnPhase)` — true only in Explore.
  - `CanInteract(TurnPhase, bool actionTaken)` — true only in Action with the action not yet spent.
  - `ShouldCommitOnMove(bool revealedNewFog)` — the fog-reveal commit predicate (see §3).
- **`TurnPhaseController`** (MonoBehaviour, singleton like the other managers): owns
  `CurrentPhase` and `actionTaken`; exposes `AdvancePhase()`; raises **`onPhaseChanged`**. On every
  advance it calls `commands.ClearStack()` (the single, canonical commit point — see §3). On entering
  Explore it clears `actionTaken`; on the End→Explore wrap it invokes the existing turn-end flow.
- **Existing systems query the controller** instead of acting freely:
  - Movement (`ExplorationButton` / arrow buttons) is enabled only when `CanMove` is true.
  - Combat-start, place-menu-open, and dungeon-delve entry check `CanInteract`; on success they set
    `actionTaken = true`.
- Spec 2's combat sub-phases will be a **separate `CombatPhase` machine** running entirely inside one
  Action; it shares only the HUD-label helper with this system, not the state machine.

### 3. Undo / commit rework

Today `ClearStack()` is called from movement, influence spend, combat teardown, End Turn, and End
Round. The new model commits on exactly two kinds of event:

1. **Phase boundaries** — Explore→Action, Action→End, and Round end (all via
   `TurnPhaseController.AdvancePhase()` / the End Round path).
2. **Irreversible reveals** — a move that uncovers previously-hidden fog. (Spec 2 will add
   "movement that triggers a forced engage" to this list.)

**Movement becomes a real `MoveCommand`** pushed on the undo stack:
- **Execute:** reposition the player, spend Explore, reveal any newly-uncovered fog.
- **Undo:** reposition back, refund Explore, re-hide the fog it revealed.
- A move that reveals **no** new fog stays undoable. A move that **does** reveal new fog
  **auto-commits** immediately after (an explicit `ClearStack()`), so it drops off the undoable stack
  — the player can never undo away knowledge they have gained. This is the
  `TurnPhaseRules.ShouldCommitOnMove(revealedNewFog)` predicate in action.

**Deliberately unchanged (commit immediately, as today):** influence spends, crystal purchases, and
combat resolution. These are treated as irreversible economic/informational reveals, consistent with
the fog rule, and keeps the change surgical — **movement is the one newly-undoable action**. (Making
purchases undoable-until-phase-boundary is a clean future extension but widens the blast radius into
the crystal/influence commit paths; explicitly out of scope for Spec 1.)

### 4. Phase HUD label + turn-flow buttons

**Phase label.** A new TMP beside the existing Round/Turn text (which is unchanged), reading
`Phase: Explore` / `Action` / `End`. Updated **event-driven** off `onPhaseChanged` via a listener on
the label — not per-frame `Update()` — matching the roadmap's "events over `Update()`" direction and
avoiding the startup-flash bug class. Optional per-phase color tint for legibility (fits M2.11's icon
language).

**Turn-flow buttons.** Today: End Turn, End Round, Undo. Rework:
- One **context advance button** whose label tracks the next boundary: **"To Action ▶"** in Explore,
  **"End Turn ▶"** in Action. Advancing commits the stack (§3); from Action it runs the End phase
  (`TurnEnd`) and starts the next turn at Explore.
- **End Round** stays its own button (full round reset), enabled only at a phase boundary — never
  mid-combat, never mid-interaction.
- **Undo** is unchanged.
- The existing `TurnButtonGate` rules fold onto the advance button: no advancing out of Action
  mid-combat; when the deck can't refill the hand, End Round is required (the advance button gates
  off). Gamepad `TurnFlowShortcuts`: North = advance (was End Turn), West = Undo — barely changes.

The actual scene/prefab wiring (new TMP, relabeled button) is **manual USER editor work** from
step-by-step instructions; no hand-edited scene/prefab YAML.

### 5. Tutorial refresh + save/load

**Tutorial.** The M2.12 rail currently teaches a free-form "play a card → move → fight → end turn."
It is re-sequenced to teach the three phases, keyed off the new `onPhaseChanged` events:
- Rail steps roughly: "This is the **Explore** phase — move with your Explore." → "You're in the
  **Action** phase — take one action: fight, visit, or delve." → "**End** the turn to refresh."
- One `HelpEntrySO` (`?` icon) on the phase label explaining the Explore→Action→End rhythm and the
  one-action rule.
- Content authoring (the `TutorialStepSO` / `HelpEntrySO` assets + a `TutorialTarget` on the phase
  label) is USER editor work; the design supplies exact copy + wiring steps, and
  `TutorialCopyValidationTests` is updated to pin the new copy.

**Save/load.** **No schema bump.** On load, reset to **Explore** phase with `actionTaken = false`.
Mid-turn quit is an edge case, pools already reset predictably, and this is single-player so the
theoretical "quit to refund your action" is not a concern. Persisting the exact phase would be a
small schema bump and is explicitly not done.

## Testing

- **Pure / TDD via the mcs harness:** `TurnPhaseRules` — legal transitions, `CanMove`,
  `CanInteract`/action-spent, and `ShouldCommitOnMove` (fog-reveal commit predicate). These are the
  logic worth pinning.
- **Manual in-editor (step-by-step instructions + acceptance checklist):** the scene wiring — phase
  label TMP, relabeled advance button, movement/interaction gating hookups, tutorial assets —
  consistent with the EditMode-while-editor-open constraint.
- `CombatRules` is untouched (Spec 2).

## Risks / Notes

- **Behavior change is intentional and large:** free-form multi-action turns become one-action
  phased turns. This is the core goal, not a regression.
- **Movement-as-command** is the main new mechanism; the fog-reveal detection must be reliable, or an
  undo could restore hidden fog (hence the pure `ShouldCommitOnMove` test).
- **Scene wiring performed manually by the user:** the phase-label TMP + listener, the relabeled
  context-advance button, gating hookups, and the tutorial content assets. Provide exact editor
  steps; never hand-edit scene/prefab YAML.
- **Interaction gating must be complete:** every entry point that starts an encounter/visit (combat
  start, place-menu open, delve entry) must check `CanInteract` and set `actionTaken`, or the
  one-action rule leaks.
- **Decisions to record** in `../../.claude/skills/archons-rise-roadmap/decisions-log.md` on
  implementation: the Explore→Action→End turn model, the one-encounter/one-visit action definition,
  the movement-undoable / commit-on-fog-reveal undo rule, and the no-schema-bump load reset.
- A new milestone entry (e.g. **M2.13 — Turn phases**) should be added to `milestones.md`, with Spec
  2 (multi-enemy phased combat) queued after it.
