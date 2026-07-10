# Combat Rewards Rework — Design (2026-07-10)

## Problem

The combat-reward path half-ignores its own data. Today an enemy defeat calls
`Rewards.GetReward(EnemyCard)`, which picks **one random `RewardsSO`** from
`enemy.enemySO.defeatRewards` and funnels it through `Grant()`. Of that
`RewardsSO`'s four fields, only `expAmount` and the `rewardType` flags do anything:

- **Experience** honors `expAmount`. ✅
- **Crystals** always grants exactly **one crystal of a random color** — `numCrystals`
  is never read.
- **Cards** draws **3 random cards from a single flat `rewardPool`** on the `Rewards`
  component — the enemy, the reward tier, and card strength are all ignored.
- `rewardLevel` is authored but, per the code comment, "not yet consumed."

Consequences: reward magnitude is not tied to enemy difficulty (a Master enemy and a
Beginner enemy give statistically the same loot except the exp number), the
Beginner→Master tier table in `balance.md` has zero effect, and `RewardsSO` is a hollow
indirection layer. There is no coherent, tunable spine for "defeating things makes you
stronger."

## Goals

1. **Experience is the reliable backbone** of every combat reward, scaled by enemy tier.
2. **Crystals are a common bonus, cards are a rare bonus** — independent probability
   rolls, with odds tuned per tier in one place.
3. **Rewards get stronger as the run progresses** via **tiered card pools** (stronger
   pre-authored cards gated to higher tiers), not a card-upgrade system.
4. **Retire the hollow `RewardsSO` indirection** — the reward an enemy/dungeon grants is
   derived from its **tier**, not a hand-authored bundle.
5. Follow the established **pure-rules + tuning-SO + mcs-TDD** pattern already used by
   `DoomRules` / `DoomTuning` / `DoomTuningSO`.

## Non-Goals (explicitly deferred — each is its own feature)

- **Card-upgrade system.** Strength comes from tiered pools of pre-authored cards, not
  from upgrading existing cards.
- **Town card-shredding / deck-thinning.** The "shred cards forever to build toward a
  better deck" idea is a *town service*, not a combat reward; it gets its own design.
- **Tier-scaled crystal count / color weighting.** Crystals stay "1 crystal, random
  color" on a successful roll for now. A future pass can add per-tier crystal count.

## The Model

Reward tiers align to the **3 enemy tiers** (`EnemiesSO.tier`, 1–3, already gated by
`DoomRules.MaxTier`). The `RewardLevel` enum's 4th value (`Master`) is left out of the
active set. Naming: **Tier 1 = Beginner, Tier 2 = Intermediate, Tier 3 = Advanced.**

On an enemy defeat, rewards derive entirely from the enemy's `tier`:

1. **Experience — always.** Bell-curve sampled from the tier's `[expMin, expMax]` and
   granted to `Player.PlayerExp`.
2. **Crystal — often.** One independent roll against the tier's `crystalChance`; on
   success, one crystal of a random color (unchanged `CrystalInventory.CreateCrystal`).
3. **Card — rarely.** One independent roll against the tier's `cardChance`; on success,
   the existing choose-1-of-3 screen, drawn from **that tier's** card pool.

Dungeon **clears** use the same model, keyed off a new `DungeonsSO.tier`.

## Components

### `RewardTuning` (pure plain class) — new
Unity-free, mcs-testable. Mirrors `DoomTuning`.

- An array/entries of **3 tier configs**, each: `expMin`, `expMax`, `crystalChance`
  (0–1 float), `cardChance` (0–1 float).
- One global knob `expBellSamples` (default **3**) controlling how tight the bell is.

Card pools are **not** in this class — card references are Unity objects and live on the
SO side (below).

### `RewardRules` (pure static class) — new, TDD'd
Lives in a new pure folder + asmdef (`Assets/Scripts/Rewards/`, `ArchonsRise.Rewards`),
mirroring `Assets/Scripts/Doom/` / `ArchonsRise.Doom`.

- `SampleExp(int tier, RewardTuning t, System.Func<int,int> rng) → int`
  **Bell curve = average of `expBellSamples` uniform draws** in `[expMin, expMax]`
  inclusive, rounded to nearest int. K=3 concentrates results on the range's centre
  (e.g. Tier 1 `[1,5]` → mostly 2–4; Tier 3 `[6,10]` → mostly 7–9). Deterministic under
  an injected `rng` (same `Func<int,int>` injection style as `SpawnRules.PickEnemyIndex`).
- `Roll(float chance, System.Func<float> rng01) → bool` — the crystal/card gate.
- `TierIndex(int tier) → int` — clamps tier to `[1,3]` and maps to the config index.

### `RewardTuningSO` (Unity SO wrapper) — new
Inspector wrapper following `DoomTuningSO`. Wraps one `RewardTuning`, **plus** holds the
**3 per-tier `List<CardsSO>` card pools** (Unity object refs must live here, not in the
pure class). Exposes accessors so `Rewards` can get a tier's pool.

### `Rewards` (MonoBehaviour) — rewritten
- Drops the `Deck<RewardsSO>` base, the `rewards` list, `Shuffle(rewards)`, and the
  legacy no-context `GetReward()`. Becomes a plain `MonoBehaviour`.
- Gains a `[SerializeField] RewardTuningSO tuning` reference; drops the flat
  `rewardPool` field (pools now live per-tier on the tuning SO).
- `GetReward(EnemyCard enemy)` → reads `enemy.enemySO.tier`, calls a single
  `Grant(int tier)`.
- `Grant(int tier)`:
  - `player.PlayerExp += RewardRules.SampleExp(tier, tuning.Data, rng)`.
  - `if (RewardRules.Roll(tuning.CrystalChance(tier), rng01))` → `CreateCrystal(random)`.
  - `if (RewardRules.Roll(tuning.CardChance(tier), rng01))` → `OfferCardChoice(tier)`.
- `OfferCardChoice(int tier, System.Action onClosed = null)` — draws its 3 candidates
  from `tuning.CardPool(tier)` (empty pool → invoke `onClosed` and skip, as today).

### Dungeon path — converted
- `DungeonsSO`: replace `List<RewardsSO> rewards` with `int tier`.
- `Dungeon` (runtime): drop its `List<RewardsSO> rewards`; carry the dungeon's `tier`.
- `Rewards.GetReward(Dungeon dungeon)` → `Grant(dungeon.tier)`. (Same tiered grant; a
  dungeon clear is simply a defeat of its own tier. Whether dungeons deserve a guaranteed
  card is a tuning question, not a code one — leave it to `cardChance` for now.)

### Level-up card picks — unified
`LevelUpController` currently calls `Rewards.OfferCardChoice` (shared flat pool). Under
the new signature it passes a **tier derived from player level** so higher-level picks
draw from stronger pools — the same "scales with progress" story as enemy drops. Mapping
lives in one small helper (e.g. level thresholds → tier 1/2/3), tunable alongside the
reward tuning.

## Retirement / Cleanup

- **Delete** `RewardsSO.cs`, `RewardType.cs`, `RewardLevel.cs`.
- **Remove** `EnemiesSO.defeatRewards` and `DungeonsSO.rewards`; add `DungeonsSO.tier`.
- **Remove** any `RewardsSO` registration from `DataManager` / `ContentRegistryPopulator`
  and delete orphaned `RewardCards` `.asset` files. (Enemy `.asset` files keep only their
  `tier`; the orphaned `defeatRewards:` YAML is dropped automatically by Unity on
  re-serialize.)
- The implementation plan must verify a clean compile after removal and that no prefab or
  scene still wires a now-deleted field.

## Starting Tuning Values (tune in playtest)

| Tier | expMin | expMax | crystalChance | cardChance |
|------|--------|--------|---------------|------------|
| 1 Beginner     | 1 | 5  | 0.50 | 0.08 |
| 2 Intermediate | 3 | 7  | 0.60 | 0.12 |
| 3 Advanced     | 6 | 10 | 0.70 | 0.18 |

`expBellSamples = 3`. Card pools: Tier 1 = the beginner card set; Tiers 2–3 add the
stronger pre-authored cards (a card may appear in multiple tiers — pool membership *is*
its rarity). These bands replace the `Reward Tiers` table in `balance.md`.

## Testing

- **Pure TDD (mcs harness)** for `RewardRules`:
  - `SampleExp` never returns outside `[expMin, expMax]`; with a fixed rng sequence it
    returns the exact rounded average; a centred rng lands mid-range.
  - `Roll` boundaries: chance 0 → always false, chance 1 → always true, threshold
    behaviour under a fixed `rng01`.
  - `TierIndex` clamps out-of-range tiers.
- **Manual Unity verification:** defeat a Tier-1 and a Tier-3 enemy repeatedly; confirm
  exp lands in-band and centre-weighted, crystals drop roughly at the tuned rate, and
  card offers are rare and pull from the correct tier pool. Clear a dungeon → tiered
  grant fires. Level up → card pick pulls from the level-appropriate pool.

## Docs to update alongside implementation

- `.claude/skills/archons-rise-design/content-rules.md` — `EnemiesSO` / `DungeonsSO` /
  reward rows (remove `defeatRewards`, `rewardLevel`, `RewardsSO`; add `DungeonsSO.tier`;
  document `RewardTuningSO`).
- `.claude/skills/archons-rise-design/balance.md` — replace the Reward Tiers table with
  the per-tier exp ranges + drop chances above.
- `.claude/skills/archons-rise-roadmap/decisions-log.md` — append this decision.

## Open Follow-ups (not this spec)

- Town card-shredding / deck-thinning service.
- Card-upgrade system.
- Per-tier crystal **count** / color weighting.
