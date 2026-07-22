# Multi-Enemy Phased Combat (Spec 2 of 2) — Design

**Date:** 2026-07-21
**Status:** Approved (pending final spec review), ready for implementation plan
**Scope note:** This is **Spec 2** of the two-spec turn/combat change. **Spec 1**
(`2026-07-21-turn-phase-system-design.md`, shipped as M2.13) restructured the *turn* into
Explore→Action→End and made combat "the Action-phase interaction." Spec 2 (this doc) replaces the
current single-enemy combat with a **multi-enemy, phased** model — **Siege → Defend → Attack →
auto-flee** — shared by field combat and guardian assaults, and delivers the long-deferred
**simultaneous-guardian** fight.

## Problem

Today a fight is single-enemy and single-shot:
- **Field:** an aggro'd `EnemyToken` spawns one `EnemyCard` in the combat canvas with Fight / Siege /
  Influence buttons.
- **Resolution** (`Player.ResolveAttack` + pure `CombatRules`): `CanDefeat` (Attack + Siege ≥ HP;
  Siege covers an Attack shortfall) → spend pools → `WoundCount` from Defend vs the enemy's Attack
  (one wound per HP-bite of the shortfall) → the counterattack drains Defend → `ResolveDefeat`
  (rewards + teardown). Siege is wound-free; Influence pays the enemy off wound-free with rewards.
- **Guardians** (`GuardianAssault`) are fought **one at a time**, chained on defeat in `Update`;
  retreat costs 3 wounds and keeps progress (`ConquestTracker` records each defeat, so conquest is
  resumable across save). **Field flee** costs 1 wound.

There is no group combat and no tactical structure *within* a fight: Siege is just "wound-free
damage," and a Castle's two guardians are a sequence of two identical single fights. The design wants
guardian assaults to be a real multi-enemy engagement, and wants Siege to carry genuine strategic
weight (thin the group *before* it hits you), on one engine shared by every fight.

## Goals

- One **phased combat model** — **Siege → Defend → Attack → auto-flee** — used by every fight, whether
  it holds one enemy (field token, Keep guardian, dungeon delve) or several (a Castle's guardian pair).
- **Siege is a pre-emptive thinning tool.** Killing an enemy in the Siege phase removes its Attack
  from the counterattack that follows, so Siege reduces incoming wounds, not just enemy HP.
- **Simultaneous guardians:** a guarded place spawns its **whole remaining roster at once**, replacing
  the one-at-a-time chain, while preserving the resumable-conquest guarantee (per-kill banking,
  3-wound retreat keeps progress).
- **One multi-purpose combat button** ("Engage" → "Withdraw") drives the whole fight; the explicit
  Flee button is retired — **auto-flee** is simply what happens when the Attack phase ends with
  survivors.
- The pillar-critical logic (phase gating, the group counterattack) lives in **pure, mcs-testable
  rules**; a single **`CombatController`** owns the phase machine and swallows the combat glue
  currently scattered across `GameManager`, `GuardianAssault`, and per-enemy teardown.

**Non-goals:** field enemy **packs** (map tokens stay single-enemy this spec; they run the shared
engine, and packs become trivial content/spawn work later); **authored enemy attack abilities**
(enemies remain Attack/HP only); a **save-schema bump** (guardian progress already persists through
`ConquestTracker.RecordDefeat`); any change to the turn/round system from Spec 1.

## Design

### 1. The phased combat model

A single fight runs one `CombatPhase` machine entirely inside the turn's one Action (the Spec 1
`BeginAction` already committed the move stack and locked further movement). The four beats:

**1. Siege phase** (combat opens here)
- Each enemy card shows its **Siege** and **Influence** buttons live; the **Fight** button is
  **locked**.
- **Siege** kills an enemy wound-free when the Siege pool ≥ that enemy's Effective HP (spends its HP
  from the pool).
- **Influence** pays one enemy off wound-free (`canInfluence`), removing it before it can
  counterattack — placed here because Siege and Influence are both "remove an enemy before it hits
  you."
- The one multi-purpose button reads **"Engage."**

**2. Defend resolution** (triggered by **Engage** — automatic, no per-enemy input)
- The counterattack is the **summed Effective Attack of all surviving enemies**, compared **once**
  against the player's banked **Defend** pool; the shortfall becomes Wounds by the existing HP-bite
  rule. Every enemy removed in the Siege phase is Attack no longer in that sum — this is where
  Siege-first pays off.
- The counterattack happens **once per fight.** After it resolves, prolonging the Attack phase
  (playing more cards, more attacks) does **not** provoke a second counterattack — the same
  single-shot spirit as combat today.
- **Unspent Siege is cleared to 0** by the act of Engaging (see §2).

**3. Attack phase**
- **Fight** buttons go live. A Normal attack spends the **Attack** pool to kill an enemy
  (Attack ≥ its Effective HP). The player finishes off the survivors, one enemy at a time.
- The multi-purpose button now reads **"Withdraw."**

**4. Resolution / auto-flee**
- **All enemies dead → auto-win:** the fight closes with no button press; rewards resolve (§3).
- **Player presses Withdraw with survivors alive → that *is* the flee:** field/dungeon = 1 wound,
  guardian = 3-wound retreat. Kills already banked (§3).

**There is no bail during the Siege phase** — the only Siege-phase button is "Engage," so once a fight
opens the player must Engage (and take the counterattack) before they can Withdraw. This is a
deliberate consequence of the single-button model: committing to a fight commits you to the
counterattack unless you clear the group wound-free via Siege/Influence first. It removes today's
"peek an aggro enemy and flee for a cheap 1 wound," raising the stakes of starting a fight. (If
playtest shows this is too punishing, a Siege-phase "Retreat" that flees pre-counterattack for the
normal flee cost is a clean, isolated addition — but it is out of scope here.)

Card play, stat **conversion**, **unit** options, **skill** activation, and **crystal** empower remain
usable in any phase (unchanged from Spec 1) — this is why combat stays in the board scene (§6).

### 2. Siege is a Siege-phase-only currency

- Pressing **Engage** clears any **unspent Siege pool to 0** — it is consumed by committing to the
  counterattack. Siege does **not** carry into the Attack phase.
- Siege cards co-flag **Attack** (`[[siege-cards-co-flag-attack]]`), so a Siege card the player
  *couldn't* use in the Siege phase (enemy HP too high to kill wound-free) is best **held and played
  in the Attack phase**, where it contributes its **Attack** value as extra damage. This creates a
  real "spend it now or save it for damage" decision.
- **Attack and Defend pools carry across all phases.** Defend persists specifically so a
  **convert Defend→Attack** play stays useful in the Attack phase.
- Consequence: Normal attacks in the Attack phase spend **Attack only** — there is no leftover Siege
  to borrow, so the current `CombatRules.SiegeSpentOnNormal` "Siege covers an Attack shortfall" borrow
  is effectively retired inside the new flow. The helper stays for the Siege-phase kill check but is
  no longer exercised by Normal attacks.

### 3. Kills, rewards & resolution (by context)

The `CombatController` carries a **context** — `Field`, `Guardian(place)`, or `Dungeon` — that decides
what a kill and a fight-end mean.

**On each enemy defeat (immediately):**
- Remove its card from the live set.
- **Commit the undo stack** — a kill is irreversible, exactly as today.
- For a **guardian**, call `ConquestTracker.RecordDefeat(place)` right then, so a later withdraw banks
  it and the next assault spawns only the survivors.
- **Tally** the kill for end-of-fight reward payout.

**Reward payout is deferred to fight-end.** Rather than popping a card-pick/exp modal the instant an
enemy dies mid-Siege-phase, all tallied kills resolve their rewards through the existing
**`RewardQueue`**, in kill order, once combat closes — on a **win or a withdraw**. This keeps reward
modals from interrupting the Siege/Attack decisions and stops card-pick rewards cluttering a hand the
player is still fighting with. (For a single-enemy field fight this is behaviourally identical to
today: the one kill ends the fight, so its reward resolves at the same moment.)

**Fight-end outcomes:**
- **All enemies dead → win:** tallied rewards resolve. For a guardian fight, if the roster is now
  empty the place is **conquered** (validation message + `RunEndRules.IsVictory` castle check, as
  today).
- **Withdraw with survivors → flee:** field/dungeon = **1 wound**; guardian = **3-wound retreat**
  (`PlaceRules.RetreatWoundCount`). Tallied kills' rewards still pay out; banked guardian defeats
  persist for the next assault.

### 4. Architecture

**Pure, mcs-testable rules** (pillar-critical logic kept out of MonoBehaviours):
- **`CombatPhase` enum** `{ Siege, Attack, Resolved }`. `Defend` is the **instantaneous Engage
  transition**, not a resting state — it gets only a brief HUD flash (§5).
- **`CombatPhaseRules`** (pure): `CanSiege(phase)`, `CanInfluence(phase)` (both Siege-phase only),
  `CanNormalAttack(phase)` (Attack-phase only), and the button label/state per phase
  (Siege→"Engage", Attack→"Withdraw").
- **`CombatRules` extension** (pure): a group-counterattack helper — total = Σ surviving enemies'
  Effective Attack, then reuse the existing HP-bite `WoundCount` against that total. Existing
  `CanDefeat` / `SiegeSpentOnNormal` remain for the Siege-phase kill check.

**Scene orchestration — `CombatController`** (MonoBehaviour singleton, same pattern as the other
managers). Owns:
- `CurrentPhase`, the live set of `EnemyCard`s, the context, and the multi-purpose button.
- **`OpenFight(enemies, context)`** — spawns the enemy card(s) into `enemyCardCombatPosition`, sets
  phase to Siege, wires the button to "Engage." Field and dungeon pass one enemy; a guardian assault
  passes the whole remaining roster.
- **`Engage()`** — clears the Siege pool, runs the group counterattack (applies wounds), commits the
  undo stack, moves to Attack phase, relabels the button "Withdraw," raises `onPhaseChanged`.
- **Per-enemy kill entry points** (Siege kill / Normal kill) route through the controller, which does
  the immediate on-defeat handling (§3) and checks for auto-win.
- **`Withdraw()`** — ends the fight with survivors: applies the context's flee cost, pays tallied
  rewards, closes the canvas.

It **absorbs** the logic currently scattered across `GameManager.CheckCombatants` (the childCount==1
close check), `GuardianAssault.Update`'s chain loop, and the per-enemy `TeardownDefeat` flow.

**Simultaneous guardians:** `GuardianAssault.Begin` spawns the **whole remaining roster at once**
(`townSO.guardians` minus `ConquestTracker.DefeatedCount`) via `CombatController.OpenFight(...,
Guardian(place))`, and the `Update`-driven "spawn next on defeat" chain is deleted. Per-kill
`RecordDefeat` + 3-wound retreat preserve the resumable-conquest guarantee under simultaneous spawn.

**Player pool spends** (`Player.ResolveAttack`, `SiegeEnemy`, `InfluenceEnemy`) are re-pointed to go
through the controller so phase gating and the deferred-reward tally are enforced in one place rather
than each button resolving independently.

### 5. HUD, the one button & undo

- **Phase label** in the combat canvas: `Siege Phase` → a brief **"Counterattack!"** flash on Engage
  → `Attack Phase`. Event-driven off `CombatController.onPhaseChanged`; optional per-phase colour tint
  (fits M2.11's icon language).
- **The single multi-purpose button** is the repurposed Flee button: **"Engage"** in the Siege phase
  (runs the counterattack, opens the Attack phase) → **"Withdraw"** in the Attack phase (ends the
  fight; survivors = flee). Clearing the whole set **auto-wins** with no press. The standalone Flee
  button is retired.
- **Undo:** **Engage** is a commit point (the counterattack is irreversible — consistent with Spec 1's
  commit model); **each enemy defeat commits** as it does today. Card plays within a phase stay
  undoable until they are spent in a kill or consumed by Engage.

The scene/prefab wiring (phase-label TMP + listener, the repurposed multi-purpose button, laying out
the guardian pair in `enemyCardCombatPosition`) is **manual USER editor work** from step-by-step
instructions; no hand-edited scene/prefab YAML.

### 6. Same-scene decision

Combat stays in the board scene (`GameBoard.unity`), under the existing toggled `combatCanvas`
grouped in one hierarchy branch. A separate combat scene was considered and rejected: combat is **not**
a sealed context — the player plays cards, converts stats, uses units, activates skills, and spends
crystals **during** a fight — so a combat scene would have to drag nearly the whole board UI with it.
The codebase is also built on same-scene singletons + `FindAnyObjectByType`, and Unity cannot
serialize cross-scene inspector references, so a split would break every drag-wire for little gain. If
hierarchy clutter ever bites, the clean escalation is an **additive scene overlay**
(`LoadSceneAsync(Combat, Additive)` over the still-loaded board) — deferred, not this spec.

## Testing

- **Pure / TDD via the mcs harness:**
  - `CombatPhaseRules` — `CanSiege` / `CanInfluence` true only in Siege; `CanNormalAttack` true only
    in Attack; button label/state per phase.
  - `CombatRules` group counterattack — Σ surviving Attack → HP-bite wound count; removing an enemy
    (Siege-thinning) reduces the sum and thus the wounds; boundary cases (Defend ≥ total → 0 wounds,
    exact HP bites). Extends `CombatRulesTests`.
- **Manual in-editor (step-by-step + acceptance checklist):** the phase-label TMP + listener, the
  repurposed multi-purpose button, the simultaneous-guardian spawn/layout, per-context flee costs,
  deferred reward ordering through `RewardQueue`.

## Risks / Notes

- **`CombatController` is a consolidation refactor**, not just additive — it pulls combat glue out of
  `GameManager`, `GuardianAssault`, and `Player`. The migration must keep every existing exit path
  working (field win/flee, guardian conquer/retreat, dungeon delve win/flee) — each is an acceptance
  line.
- **Deferred rewards change payout timing** for multi-kill fights (guardians). Single-enemy fights are
  unaffected. Verify `RewardQueue` ordering when two guardian kills tally in one fight.
- **Simultaneous guardians must preserve resumability:** per-kill `RecordDefeat` fires the instant a
  guardian dies (not at fight-end), so a withdraw after one kill banks exactly that kill. This is the
  explicitly-deferred M2 tension; get it under test/acceptance.
- **Siege-clear-on-Engage must be reliable** — a leftover Siege pool bleeding into the Attack phase
  would silently reinstate the retired borrow and undercut the "save the Siege card for damage"
  decision.
- **Interaction gating must be complete:** every entry point that starts a fight (field token, guardian
  assault, dungeon delve) must open through `CombatController.OpenFight`, or a fight bypasses the phase
  machine.
- **Decisions to record** in `../../.claude/skills/archons-rise-roadmap/decisions-log.md` on
  implementation: the Siege→Defend→Attack→auto-flee model; Siege as a Siege-phase-only currency
  cleared at Engage; the summed single counterattack (Siege thins it); Influence resolved in the Siege
  phase; deferred reward payout through `RewardQueue`; simultaneous guardians with per-kill banking and
  3-wound retreat; the one repurposed multi-purpose button (Flee retired); the same-scene decision.
- **Docs to update:** `mechanics.md` Combat section → the phased model; a new milestone (e.g.
  **M2.14 — Multi-enemy phased combat**) in `milestones.md` marked as Spec 2 of the turn/combat change.
  No `balance.md` numbers change (retreat = 3 / flee = 1 already exist); the model is structural.
