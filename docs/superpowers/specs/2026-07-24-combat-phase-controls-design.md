# Combat Phase Controls — Design Spec

**Date:** 2026-07-24
**Status:** Design approved; folds into the ongoing *Combat Feel — Fighting on the Board* work (not a standalone plan).

## Problem

Phased combat currently advances through a single always-on multi-purpose button (`CombatController.multiButton`, the repurposed Flee button). It sits statically in the combat canvas and is the player's control for three unrelated things — **Engage** (Siege→Defend), **Defend** (Defend→Attack, which actually *takes the counterattack*), and **Withdraw** (flee). It reads as clutter, its placement collides with the now board-anchored enemy cards, the one-button-three-meanings labelling is muddy, and because Withdraw occupies the same spot the commit press lands on, a reflexive double-click can flee the fight — clunky and unrecoverable.

## Goal

Replace the one static button with two purpose-built, phase-aware controls that keep the fight central and legible:

1. An **Advance** control — the Siege/Defend commit — that lives *opposite the enemies* near the heart of combat, sways subtly for life, guides the player through Siege, and previews the incoming counterattack live during Defend.
2. A separate **Withdraw** control — pushed to the periphery, muted — so fleeing is a deliberate, out-of-the-way act that can never be hit by an accidental second click.

No change to combat *resolution*: Siege spend, the group counterattack, kill banking, phases, and rewards resolve exactly as today (`Engage()` / `ResolveDefend()` / `Withdraw()` are still the entry points). This is input + presentation.

## Background: how the fight already works

- **Siege phase.** Playing a Siege card raises the `playerSiege` pool (undoable via the command stack). Clicking a live enemy's Siege button spends the pool to remove that enemy wound-free (`Player.SiegeEnemy` → `ResolveAttack(Siege)` → `NotifyDefeated`), and that removal clears the undo stack (irreversible). Influence removals work the same way from the `playerInfluence` pool. `Engage()` zeroes `playerSiege`, clears the stack, and moves to Defend.
- **Defend phase.** Playing Defend cards raises `playerDefend`. `ResolveDefend()` computes `wounds = CombatRules.GroupWoundCount(playerDefend, Σ live enemy EffectiveAttack, playerToughness)`, applies the wounds, and moves to Attack.
- **Attack phase.** Per-enemy Fight buttons spend `playerAttack` to kill survivors; clearing the set wins. `Withdraw()` flees for 1 wound (3 for a guardian assault).
- **Board-anchored arc.** Enemy cards fan in an arc centred on `arcOrigin` — the enemy source's projected board position — via `CombatLayoutRules` and `CombatController.ApplySlot`. Positions are in `enemyCardCombatPosition` local UI space, where the origin (0,0) is screen-centre = the player.

## Part A — Behavior

### The Advance control (primary commit)

Positioned **opposite the enemy cluster through the combat centre**: mirror the live enemies' centroid through the origin (the player at screen-centre) and place the button a fixed distance out along that ray. Enemies fan upper-right → button sits lower-left of the player, central to the fight but clear of both the enemies and the player sprite. It re-solves on every arc re-layout (spawn, mid-fight kill, flee) and carries the enemy-card **idle sway**. The anchor is clamped to a safe band so it can never creep into the top HUD or the bottom hand.

**Siege phase.** Default caption **"Engage"**.
- When the player stages Siege (`playerSiege > 0`) **and at least one live enemy is siege-killable** (`EffectiveHP ≤ playerSiege`), the Advance button **hides** and every live enemy's **Siege and Influence buttons glow** — signalling "spend it on a target."
- Undoing the staged card (pool back to 0) makes Engage **reappear** and the glow switch off.
- Leftover siege that can't kill anything keeps **Engage visible**, so the player is never trapped with a hidden button and unspendable pool.
- The same rule drives the Influence glow off `playerInfluence`.
- Press **Engage** → Defend phase.

**Defend phase.** The button stays put and relabels to a **live** readout using TMP sprite tags:
- **"Gain X 🩹 — use 🛡!!"**, glowing red, where `X = CombatRules.GroupWoundCount(playerDefend, Σ enemy attack, toughness)`.
- As Defend cards are played, **X drops**. When `playerDefend ≥ Σ enemy attack` (X = 0), the label flips to **"→ Counterattack!"** with a positive (non-red) glow.
- Press → resolve the counterattack (`ResolveDefend()`) → Attack phase.

**Attack phase.** The Advance button is **hidden** — the player wins by clearing enemies with the per-enemy Fight buttons; there is no primary commit here.

### The Withdraw control (last resort)

A **separate** control, in a **different** location pushed toward the screen edge (further out along the same "away from the fight" bias), with **muted** styling. Shown **only** in the Attack phase. Because it is a distinct object far from the Advance anchor, no commit press can ever land on it — resolving the counterattack and fleeing are physically separated. Press → `Withdraw()`.

### Enemy button glow

A material/shader glow toggled on each live enemy's Siege and Influence buttons, on while the matching pool is staged in the Siege phase, off otherwise (and off entirely once past Siege).

## Part B — Architecture

### Pure logic (mcs-tested, no `UnityEngine`)

- **`CombatLayoutRules.OppositeAnchor(float centroidX, float centroidY, float distance)`** → the mirrored button slot: `-normalize(centroid) * distance` (returns plain floats / the existing `Slot`-style value type, never `Vector2`). Degenerate centroid (≈ origin) falls back to a fixed downward direction so the anchor is always defined. Lives beside the existing arc geometry.
- **`CombatPhaseRules.Advance(...)`** — the button's display state as pure data:
  - `enum AdvanceKind { Hidden, Engage, TakeHit, Counterattack }`
  - `struct AdvanceState { AdvanceKind Kind; int Wounds; }`
  - `AdvanceState Advance(CombatPhase phase, int playerSiege, bool anySiegeKillable, int playerDefend, int enemyAttackTotal, int toughness)`:
    - **Siege:** `playerSiege > 0 && anySiegeKillable` → `Hidden`; else `Engage`.
    - **Defend:** `wounds = CombatRules.GroupWoundCount(...)`; `wounds == 0` → `Counterattack`; else `TakeHit(wounds)`.
    - **Attack / Resolved:** `Hidden`.
  - Depends only on the already-pure `CombatRules`.

### Presentation MonoBehaviours (main assembly)

- **`CombatButtons`** — one controller owning the Advance and Withdraw button GameObjects. While a fight is live it reads, each frame, `CombatController.Phase`, the enemy centroid, the live enemy attack total, and the player pools; feeds them through the pure helpers; and applies **visibility**, **label** (sprite-tagged), **colour** (red / counterattack glow), and **position** (`OppositeAnchor` for Advance; a further-out edge anchor for Withdraw). Button clicks dispatch to the existing `Engage()` / `ResolveDefend()` / `Withdraw()`. One per-frame read, consistent with the existing enemy-glow and defend-clamp `Update`s.
- The enemy-card **idle sway** component is reused on the Advance button's content pivot.
- **`EnemyCard.SetActionGlow(bool)`** — toggles the glow material on this card's Siege and Influence buttons. `CombatButtons` (or `CombatController`) drives it across the live set from the staged-pool signal.

### Data exposure (added to `CombatController`)

- `int LiveEnemyAttackTotal` — Σ live `EffectiveAttack`.
- `bool AnySiegeKillable(int siege)` — any live enemy with `EffectiveHP ≤ siege`.
- `Vector2 EnemyCentroidLocal` — average of the live cards' `anchoredPosition` (returns `Vector2`; it is a MonoBehaviour, this is fine).

Player pools (`PlayerSiege`, `PlayerDefend`, `PlayerInfluence`, `PlayerToughness`) are already public.

### Retired

- `CombatController.multiButton` / `OnMultiButton` and `CombatPhaseRules.ButtonLabel` — superseded by `CombatButtons`. The old `GameManager.fleeButton` / multi-button wiring is reconciled into the new Withdraw control during implementation.

### Undo, rewards, save

Untouched. The Update-driven controller reacts to pool changes from undo automatically (no explicit undo hook). The reward queue, kill banking, and save paths are unaffected — the buttons only dispatch the same combat calls.

## Coordinate spaces

All button positions are `anchoredPosition` in `enemyCardCombatPosition` local UI space, where origin = screen-centre = the player — the same space the arc uses. The enemy centroid is computed in that space, so `OppositeAnchor` mirrors through the player correctly.

## Testing

- **Pure, mcs-tested + EditMode NUnit:** `OppositeAnchor` (mirrors across origin, correct distance, degenerate fallback) and `CombatPhaseRules.Advance` (each phase → correct `AdvanceKind`; Siege hide only when killable siege staged; Defend wound count and the `Counterattack` flip at `Defend ≥ attack`). Verified RED/GREEN via the Mono `mcs` harness; USER confirms green in the Test Runner.
- **Play-test (USER):** Engage hide/show on Siege play/undo with the enemy glow; the live Defend readout dropping to "Counterattack!"; the Advance button sitting opposite the enemies and swaying; Withdraw appearing only in Attack, muted and far; no double-click-flee possible.

## Editor / stylistic work (USER)

- Glow shader/material on the enemy Siege/Influence buttons.
- Advance button styling (red Defend state, positive Counterattack state) and its idle-sway pivot.
- Withdraw button muted styling and edge placement.
- Add/​wire `CombatButtons` with its Advance button, Withdraw button, sway pivot, and references; remove the old always-on button from the canvas.

## Self-Review

- **Placeholder scan:** none — every control, phase, and rule is specified.
- **Consistency:** the pure `AdvanceState` drives every label/visibility decision the behavior section describes; positioning is the pure `OppositeAnchor`; combat resolution entry points are unchanged.
- **Scope:** one cohesive feature (combat phase-commit controls). Fits as a continuation of the combat-feel work; no decomposition needed.
- **Ambiguity:** "opposite the enemies" is pinned to *mirror the live centroid through the origin at a fixed distance*, clamped to a safe band; the Siege hide is pinned to *staged pool AND a killable target*.
