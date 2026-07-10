# Content Authoring Contract

How to author each content type so it stays consistent and works with the existing code. All
content types inherit **`AllCards`**: `cardName` (string) and `cardDescription` (TextArea string).
**Source of truth:** `Assets/Scripts/GameScriptableObjectTypes/` and `Assets/Scripts/Enums/`. If a
field here ever disagrees with those scripts, the scripts win — update this file.

## Enums used below
- **`StatType`** `[Flags]`: `None=0, Attack=1, Defend=2, Explore=4, Influence=8, Heal=16, Wound=32, Crystal=64`.
  Combine with `|` (e.g. `Explore | Crystal`).
- **`EmpowerType`** `[Flags]`: `None=0, Red=1, Yellow=2, Green=4, Purple=8`. Use `None` for a card/unit
  that cannot be empowered. All-colors (any-crystal cost / wild crystal) = all four flags set = `15`.
- **`UnitEffect`**: `Attack=0, Defend=1, Explore=2, Influence=3, Siege=4, Heal=5, Crystallize=6`. One
  unit option's effect (append-only — new members go at the end).
- **`SkillCadence`**: `PerTurn=0, PerRound=1, Passive=2`. `SkillEffect` (append-only) now also has
  `RecruitEnemies` (the Charismatic passive gate).
- **`TownSize`**: `Town, Village, Fortress, City`.
- **`PlaceType`**: `Town=0, Keep=1, Castle=2` (source: `Assets/Scripts/Places/`).
- **`PlaceService`** `[Flags]`: `None=0, Recruit=1, Heal=2, Cards=4` (source: `Assets/Scripts/Places/`).

---

## Card — `CardsSO`
**Menu:** `ScriptableObjects/Cards/PlayerCards`

| Field | Type | Notes |
|-------|------|-------|
| `attack`, `defend`, `explore`, `influence` | int | Base stat values |
| `healAmount`, `numCrystals` | int | Base heal / crystals granted |
| `empowerAttack`, `empowerDefend`, `empowerExplore`, `empowerInfluence` | int | Empowered stat values |
| `empowerHealAmount`, `empowerNumCrystals` | int | Empowered heal / crystals |
| `cardType` | `StatType` (flags) | Which stats this card provides |
| `empowerType` | `EmpowerType` | Crystal color needed to empower |
| `isChoice` | bool | Player picks which stat to apply |

**Rules:**
- The four **action stats** (Attack/Defend/Explore/Influence) are gated in code by
  `cardType.HasFlag(...)` — `ReturnAttack/Defend/Explore/Influence` return 0 unless the matching flag
  is set, even if the int has a value. So you MUST flag every action stat you give a value to.
- `healAmount` and `numCrystals` are read directly (not gated by a `HasFlag` check in `CardsSO`).
  Still set the corresponding `Heal` / `Crystal` flag on `cardType` so the card's effect type is
  self-describing. **In short: `cardType` should flag every effect the card provides.**
- For each stat you give a value, set BOTH the base and the `empower*` value.
- Empower values should exceed base values (pillar 3 — empowering must feel worth a crystal).
- Set `empowerType` to the crystal color the card requires; use `None` if it can't be empowered.
- `isChoice`: set **true** only for cards that let the player choose *which* stat to apply at play
  time (the StatChoiceToggles flow — cards offering mutually-exclusive stat options). Set **false**
  for cards that always apply all their effects together — even multi-effect cards (e.g. Heal+Crystal)
  are `false` if both always apply. A single-stat card is `false`.

## Enemy — `EnemiesSO`
**Menu:** `ScriptableObjects/Cards/EnemyCards`

| Field | Type | Notes |
|-------|------|-------|
| `enemyHP` | int | Player needs Attack ≥ this to defeat it |
| `enemyAttack` | int | Player Defend < this → Wounds |
| `canInfluence` | bool | Can be dealt with via Influence |
| `influenceCost` | int | Forced to 0 when `canInfluence` is false |
| `recruitedUnit` | `UnitsSO` | Optional. When set AND the player owns Charismatic, paying the influence cost recruits this unit (rewards + unit). Null = pay-to-leave only. |
| `tier` | int | Doom-gated difficulty tier (1–3) |

## Town — `TownsSO`
**Menu:** `ScriptableObjects/Cards/TownCards`

| Field | Type | Notes |
|-------|------|-------|
| `townSize` | `TownSize` | Town / Village / Fortress / City |
| `activity` | `TownActivity` (flags) | None / Recruit / Cards / Heal / Resources |
| `recruitableUnits` | List&lt;`UnitsSO`&gt; | Units available to recruit here |
| `recruitLevel`, `cardLevel`, `resourceLevel`, `healLevel` | int | Service levels per activity |
| `placeType` | `PlaceType` | Town / Keep / Castle — drives allowed services via `PlaceRules` |
| `guardians` | List&lt;`EnemiesSO`&gt; | Conquest roster, fought in order; empty for a Town |

**Rule:** Service availability is computed from `placeType` (`PlaceRules.AllowedServices`), NOT the
legacy `activity` flags (exception: the Crystal/Resources button still reads `activity`). Town:
Recruit+Heal, opens unguarded. Keep: Recruit, 1 guardian. Castle: Recruit+Heal+Cards(stub), 2
guardians. Castles are the win currency — conquering 2 wins the run (M2.5).

## Unit — `UnitsSO`
**Menu:** `ScriptableObjects/Units`

| Field | Type | Notes |
|-------|------|-------|
| `options` | List&lt;`UnitOption`&gt; | The unit's authored options; the pop-out renders exactly these |
| `influenceCost` | int | Recruit price at towns (per-unit) |
| `sprite` | Sprite | Unit art |
| `color` | Color | Unit tint |
| `unitLetter` | char | Display letter |

**`UnitOption` fields:** `effect` (`UnitEffect`), `amount` (int), `grantColor` (`EmpowerType` — only
used by `Crystallize`), `crystalCost` (`EmpowerType` — `None` = free; a color = 1 crystal of that
color, wild satisfies; all-colors/`15` = any 1 crystal).

**Rules:** Recruited at towns for `influenceCost` (or via enemy influence + Charismatic). The pop-out
lets the player pick one option; using it applies the effect and exhausts the unit for the round.
A crystal-costed option ≈ twice its free sibling's amount (see [balance.md](balance.md)). The legacy
flat-stat fields (`attack`/`defend`/…/`empowerType`, `GetUnitStats`) are retired.

## Reward tuning — `RewardTuningSO`
**Menu:** `ScriptableObjects/RewardTuning` — one shared asset, wired onto the `Rewards` component.

Combat/dungeon rewards derive entirely from a **tier** (1–3), not per-enemy bundles
(spec 2026-07-10). On defeat: Experience is always granted (bell-curve sampled from the
tier's exp range), then a crystal and a card are rolled **independently** against the
tier's odds. The pure math lives in `RewardRules`; the numeric knobs live in the nested
`RewardTuning`; the card pools live on the SO (card refs are Unity objects).

| Field | Type | Notes |
|-------|------|-------|
| `tuning.expBellSamples` | int | Uniform draws averaged for the exp bell (higher = tighter centre) |
| `tuning.tier1/2/3` | `RewardTierTuning` | Per-tier `expMin`, `expMax`, `crystalChance`, `cardChance` |
| `tuning.levelTier2`, `levelTier3` | int | Player level at which level-up card picks step up a pool tier |
| `tier1Cards` / `tier2Cards` / `tier3Cards` | List&lt;`CardsSO`&gt; | Per-tier card reward pools — **pool membership IS a card's rarity** (a card may appear in several tiers) |

**Rules:** an enemy/dungeon's `tier` selects the config. Crystals are the common bonus,
cards the rare one — tune `crystalChance` > `cardChance`. There is **no per-enemy reward
authoring**: set the enemy's `tier` and the reward falls out. See [balance.md](balance.md)
for the starting bands.

## Dungeon — `DungeonsSO`
**Menu:** `ScriptableObjects/Dungeons`

| Field | Type | Notes |
|-------|------|-------|
| `exploreCost` | int | Explore to enter |
| `enemies` | List&lt;`EnemiesSO`&gt; | Enemies inside |
| `tier` | int | Reward tier (1–3) every reward event pays out at |
| `rewardCount` | int | Number of reward events the dungeon offers before rewards are exhausted |

## Location — `LocationsSO`
**Menu:** `ScriptableObjects/LocationsSO`

| Field | Type | Notes |
|-------|------|-------|
| `exploreCost` | int | Explore to reveal |
| `enemies` | List&lt;`EnemiesSO`&gt; | Enemies present |
| `towns` | List&lt;`TownsSO`&gt; | Towns present |
| `dungeons` | List&lt;`DungeonsSO`&gt; | Dungeons present |

## Player — `PlayerSO`
**Menu:** `ScriptableObjects/PlayerSO`

| Field | Type | Notes |
|-------|------|-------|
| `playerName` | string | Hero name |
| `playerHandSize` | int | Starting hand size |
| `startingHand` | List&lt;`CardsSO`&gt; | Starting deck/hand |
