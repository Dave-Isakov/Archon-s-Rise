# Level-Up Rewards — Design (M2.4)

**Date:** 2026-07-06
**Status:** Approved pending user review
**Slots in as:** M2.4, ahead of M2.5 (win/lose) — a run needs progression worth losing before
win/lose lands.

## Goal

Level-ups currently do nothing but raise the exp curve (`Player.PlayerLevelUp` only increments
`playerLevel`). This milestone makes leveling pay out three reward kinds:

1. **Skills** — activatable abilities picked from a pool, usable once per turn or once per round.
2. **Hand size** — +1 card in hand at milestone levels.
3. **Army size** — a new cap on how many units the player can field, raised at milestone levels
   (the cap does not exist today: `RecruitButton` only checks Influence).

This **replaces** the old comment-scheme (even level → +1 stat, odd → +HP, every 3rd → +hand,
every level → skill) with a fixed, data-driven reward table.

## Decisions (user-approved 2026-07-06)

- **Fixed reward table**, data-driven via a `LevelRewardsSO` asset. No per-level choice of reward
  *kind*; the only choice is which skill to take when a level grants a skill pick.
- **Skill pick = choose 1 of 3** random skills the player does not already own, drawn from the pool.
- **Skills are an exhaustible skill bar**: click to activate, token exhausts (dims) until its
  cadence refreshes it — per-turn skills on turn end, per-round skills on round end. Mirrors unit
  exhaust/refresh.
- **Army cap starts at 1**; at cap, recruiting offers **disband-to-hire** (pick an existing unit to
  disband, pay full Influence for the new one; cancelling is free).
- **HP is toughness only** (confirmed 2026-07-06): HP is the divisor in
  `CombatRules.WoundCount` — the Defend shortfall is chopped into HP-sized bites, one Wound per
  bite. HP never depletes; the "HP reduced to 0" loss clause is removed from the design bible.
  The tactical loss axis is solely Wounds ≥ threshold (M2.5). `+1 HP` on the table = permanent
  damage resistance.
- **Hand size and army cap are derived, not stored**: both are always computable as
  base + sum of table bonuses up to the current level. The save schema stores neither; only
  owned skills and their exhaust state are new save data. Migration from v2 is trivial.
- **Heal skill is in the pool** at per-round cadence (heal 1 wound), reusing the existing
  `PlayerHand.HealWound` / `RestoreHealedWound` undo-safe path. Kept rare so it pressures but does
  not undercut the Wound clock.

## Data model

### `SkillsSO` (new content type)

`CreateAssetMenu` ScriptableObject, one asset per skill:

| Field | Type | Notes |
|---|---|---|
| `skillId` | string | stable id (like card ids from M1) — what saves reference |
| `skillName`, `description` | string | UI text |
| `icon` | Sprite | skill-bar token art |
| `effect` | `SkillEffect` enum | `GainAttack, GainDefend, GainInfluence, GainExplore, GainCrystal, HealWound` |
| `magnitude` | int | stat amount / crystals granted / wounds healed |
| `crystalColor` | `EmpowerType` | only meaningful for `GainCrystal` |
| `cadence` | `SkillCadence` enum | `PerTurn` or `PerRound` |

### `LevelRewardsSO` (one asset — the whole table)

- `skillPool`: `List<SkillsSO>` — every skill in the game (M3's unlock pool can later filter this).
- `entries`: list of `LevelRewardEntry { int level; int hpBonus; int handSizeBonus;
  int armySizeBonus; bool grantsSkillPick; }`.
- Levels beyond the last entry grant nothing (tune later).

### Starting reward table (tune in playtest)

| Level | Reward |
|---|---|
| 2 | skill pick |
| 3 | +1 HP |
| 4 | +1 hand size, +1 army size |
| 5 | skill pick |
| 6 | +1 HP |
| 7 | skill pick, +1 army size |
| 8 | +1 hand size |
| 9 | +1 HP, skill pick |
| 10 | +1 army size, +1 hand size |

### Starting skill pool (9 skills — tune in playtest)

Per-turn (weak, spammable): **Drillmaster** +1 Attack · **Shieldwall** +1 Defend ·
**Envoy** +1 Influence · **Pathfinder** +1 Explore.
Per-round (strong, once a round): **Crystallize Red / Yellow / Green / Purple** (gain 1 crystal of
that color) · **Field Medic** (heal 1 wound).

## Runtime design

### Pure rules (unit-testable, no Unity)

- **`LevelRules`** — `RewardsFor(level, table)`, `DerivedHandSize(baseSize, level, table)`,
  `DerivedArmyCap(level, table)` (base cap 1), `DrawSkillChoices(pool, ownedIds, rng, count=3)`
  (excludes owned; returns fewer if the pool runs dry, zero → skip the pick).
- **`ArmyRules`** — `CanRecruit(unitCount, cap)`, `NeedsDisband(unitCount, cap)`.

### Level-up flow

`PlayerLevelUp()` (still polled in `Update` — exp only changes at discrete reward moments):

1. `playerLevel++`; `playerExp -= expToNextLevel` (**fixes the current overflow discard**,
   `playerExp = 0`); recompute `expToNextLevel`.
2. Apply `hpBonus` directly; hand size / army cap need no action (derived).
3. If the entry grants a skill pick, raise a `LevelUpEvent` → the **Level-Up modal** opens on the
   message canvas (existing modal-capture pattern), showing the 3 drawn skills; picking one adds it
   to owned skills + skill bar. If exp still exceeds the next threshold, the next level-up queues
   its modal after the current one closes.

### Skill bar

- Persistent `SkillBar` panel; one `SkillToken` per owned skill (icon + dim state).
- Activating an unexhausted token applies the effect and exhausts it. Activation is **undoable via
  the existing command stack** (like card plays): stat gains reverse; `GainCrystal` destroys the
  granted crystal on undo (mirror of empower's crystal handling); `HealWound` undoes via
  `RestoreHealedWound`. Exhaustion clears on undo.
- Refresh: per-turn tokens in the turn-end flow, per-round tokens in the round-end flow (alongside
  `RefreshUnits`). Undo-stack clear also purges healed wounds as today.

### Army cap + disband-to-hire

- `RecruitButton` gains the cap check: below cap → recruit as today; at cap → open the **Disband
  panel** (lists current units; pick one to disband → old unit destroyed, new unit hired,
  Influence spent; Cancel closes free). Disband-then-hire is a single atomic action — no state
  where the player has paid but has no unit, and it is **not undoable** (like town services).

## Save schema v2 → v3

- Add `ownedSkillIds: string[]` and `exhaustedSkillIds: string[]` to the player blob.
- Hand size / army cap / table state: **not stored** (derived from level).
- v2 migration: both lists default empty. HP already persists.

## UI / scene work (user-wired in the editor, per usual)

1. **SkillBar panel** + SkillToken prefab.
2. **Level-Up modal** on the message canvas (3 skill choice buttons).
3. **Disband panel** in the town menu flow.
Controller navigation for these follows at the controller milestone; build them with the same
focus-outline/navigation conventions as the card pop-out.

## Testing

- `LevelRules` / `ArmyRules` via the existing pure-test harness (mcs) + EditMode NUnit tests:
  table lookup, derivation sums, skill draw exclusion/exhaustion-of-pool, cap checks.
- Exp overflow: gaining exp past two thresholds at once yields two level-ups and carries remainder.
- Manual acceptance: level to 2 → skill modal offers 3; picked skill appears, activates once,
  refreshes on its cadence and undoes cleanly; level 4 → hand tops to 6 next turn and a second
  unit can be hired; at cap, recruit opens disband flow; save/load round-trips owned + exhausted
  skills and derived sizes.

## Out of scope

- Skill rarity/tiers, upgradeable skills, skill removal.
- Choice of reward kind on level-up (table is fixed).
- Feeding the M3 unlock pool into `skillPool` (hook exists; wiring is M3).
- Win/lose screens (M2.5).
