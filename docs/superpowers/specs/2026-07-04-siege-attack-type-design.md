# Siege — a wound-free attack type

**Date:** 2026-07-04
**Status:** Design approved; ready for implementation plan.

## Goal

Give the player a real pre-combat decision — *how* to attack, not just *whether* — by adding a
second attack type. Today an enemy is defeated one way: spend **Attack ≥ enemy HP**, and if your
**Defend < the enemy's Attack** you eat the counterattack as Wounds (`Player.ValidatePlayerAttackToEnemyHP`
→ `CheckWounds`). **Siege** is a parallel attack that kills the enemy *without* any counterattack
wound. Its balance is scarcity: it can never be improvised and comes only from advanced cards and
units.

This delivers the "information parity / plan your approach" intent (brainstorm goal #1) without a
separate preview screen: the enemy card already shows the enemy's Attack (the wound threat), and
that readout is exactly what tells the player whether to spend a scarce Siege or just Normal-attack.

## Design decisions

- **Siege is its own accumulated stat (Option A).** It is *not* a modifier on the Attack pool.
  Advanced cards/units build a separate Siege pool the same way every other stat accumulates; you
  cannot convert Attack → Siege. Having Siege genuinely means you drew and played the right content.
  _Why:_ matches the existing "play cards → build stat pools → spend on actions" model, keeps the
  scarcity concrete and legible, and is controllable for balance. Rejected: a per-fight "Attack is
  wound-safe this turn" flag — swingier (one card makes *all* Attack safe) and fuzzier scarcity.

- **Siege cannot be improvised.** Improvise (`ImprovisePanel`) only ever offers +1 to the four basic
  stats (Attack/Defend/Influence/Explore). Siege is deliberately outside that set, so a Normal Attack
  is always reachable but Siege is only ever as available as its source content. This requires **no
  enforcement code** — Siege simply is not one of the improvisable segments.

- **Siege is a printed stat on advanced content; empower is optional.** Cards may print Siege on the
  base line and/or the empower line (author's choice per card). Units (which have no empower step)
  print Siege on their base stat line. _Why:_ the user's design vision is that both advanced cards
  and units can grant Siege; forcing an empower gate would exclude units and over-constrain card
  authoring.

- **Siege kill grants identical rewards to a Normal kill.** The cost of Siege is scarcity, not a
  reduced payoff. No reward, XP, crystal, or influence penalty.

- **One-exchange resolution stays.** No persistent enemy HP, no multi-turn chipping, no Doom-clock
  hook (Doom is M2.5). Siege is a single resolution that skips the counterattack step.

## Mechanics

### The Siege stat
- Add `Siege` to the `StatType` flags enum (next free bit: `128`).
- Add a `playerSiege` accumulator on `Player`, parallel to `playerAttack`, with a `PlayerSiege`
  property.
- Siege resets to 0 in `Player.TurnEnd`, exactly like the other action stats.
- Siege shows in the stat HUD (`StatsDisplay`) alongside Attack/Defend/Influence/Explore, with its
  own accent colour in `StatPalette` and an `AnchorFor` entry.

### Sourcing
- `CardsSO`: add a `siege` field (and an empower counterpart, following the existing
  `Return<Stat>(isEmpowered)` pattern) so `GetCardStats(isEmpowered)` returns Siege.
- `UnitsSO`: add a `siege` field so `GetUnitStats()` returns Siege.
- The stats array contract grows from `int[4] {attack, defend, influence, explore}` to
  `int[5] {attack, defend, influence, explore, siege}`. `Player.AssignPlayerStats` /
  `UnAssignPlayerStats` consume the new slot.

### Combat resolution
Split the single fight entry point into two:
- **Fight (Normal)** — unchanged. `playerAttack ≥ enemyHP` defeats; spend Attack; `CheckWounds`
  applies the counterattack wound when `playerDefend < enemy Attack`; rewards raised.
- **Siege** — `playerSiege ≥ enemyHP` defeats; spend Siege; **skip `CheckWounds` and the Defend
  counterattack entirely**; same rewards raised.

### Combat UI
- `EnemyCard` gains a **Siege** button beside the existing Fight button.
- Fight enables when `playerAttack ≥ enemyHP` (as today via `EnableCombat`/interactable logic);
  Siege enables when `playerSiege ≥ enemyHP`.
- Both stay visible so the enemy's Attack readout drives the choice: Normal when the enemy's Attack
  won't hurt you (or you want to save Siege), Siege when you are under-defended and it matters.
- Siege button raises a Siege-specific resolution path (a new event or a flag on the existing
  `EnemyCardEvent`), mirroring `DefeatMonster()`.

### Messaging (explicitly in scope)
Every attack-related message must be Siege-aware, not Attack-only:
- The insufficient-attack validation string (`"You need {enemyHP} Attack in order to defeat this
  monster."`) needs a Siege equivalent (`"You need {enemyHP} Siege..."`) on the Siege path.
- The wound/defeat feedback in `CheckWounds` must not fire on a Siege kill (a Siege kill is always
  wound-free), and any "destroyed / wounded N times" messaging should reflect the Siege path
  (destroyed, zero wounds).
- Audit any other combat feedback that names "Attack" so it reads correctly when the kill was a Siege.

## Implementation surface (touch points)

- `Assets/Scripts/Enums/Enums/StatType.cs` — add `Siege = 128`.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` — `playerSiege` field + property; extend
  `AssignPlayerStats`/`UnAssignPlayerStats` to slot 5; reset in `TurnEnd`; split
  `ValidatePlayerAttackToEnemyHP` into Normal + Siege resolution; Siege message.
- `Assets/Scripts/GameScriptableObjectTypes/CardsSO.cs` — `siege` (+ empower) field; extend
  `GetCardStats` to `int[5]`; update the summary comment.
- `Assets/Scripts/GameScriptableObjectTypes/UnitsSO.cs` — `siege` field; extend `GetUnitStats` to
  `int[5]`.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs` — Siege button wiring, enable
  logic, Siege resolution raise.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/StatsDisplay.cs` — Siege label, cache/animate,
  `AnchorFor`.
- `StatPalette` — Siege accent colour.
- Any `EnemyCardEvent` / listener additions needed for a distinct Siege resolution.
- Prefabs/scene: the Siege button on the enemy-card prefab and a Siege stat label in the HUD are
  editor wiring — hand off as step-by-step instructions per the manual-Unity-edits workflow; do not
  hand-edit scene/prefab YAML.

## Scope boundaries (YAGNI)

- No persistent enemy HP / multi-turn combat.
- No reward changes, no Doom-clock interaction.
- **Guardian assaults get Siege for free** — they run through the same fight resolution, so no extra
  work beyond confirming the Siege path reaches them.
- **Aggro auto-combat info-parity** (seeing stats when an enemy engages you automatically) stays out
  of scope — that remains the separately-deferred hover-preview item.

## Testing

EditMode tests on the resolution logic (pure/near-pure, per the Unity test-harness constraints):
- Siege defeats when `playerSiege ≥ enemyHP`.
- Siege inflicts **zero** wounds even when `playerDefend < enemy Attack`.
- Siege does **not** fire when `playerSiege < enemyHP` (emits the Siege-insufficient message).
- Normal Fight resolution is unchanged (regression).
- `playerSiege` resets to 0 on `TurnEnd`.
- `GetCardStats` / `GetUnitStats` return the Siege slot; `AssignPlayerStats` accumulates it.

## Docs to update on implementation

- `.claude/skills/archons-rise-design/mechanics.md` — add Siege to the Stats list and note the
  wound-free attack type; `content-rules.md`/`balance.md` if Siege values need authoring guidance.
- `.claude/skills/archons-rise-roadmap/decisions-log.md` — append the Siege decision (own stat,
  non-improvisable, advanced cards + units, empower optional, scarcity is the cost).
