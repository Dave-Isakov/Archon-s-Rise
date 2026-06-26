# Content Authoring Contract

How to author each content type so it stays consistent and works with the existing code. All
content types inherit **`AllCards`**: `cardName` (string) and `cardDescription` (TextArea string).
**Source of truth:** `Assets/Scripts/GameScriptableObjectTypes/` and `Assets/Scripts/Enums/`. If a
field here ever disagrees with those scripts, the scripts win — update this file.

## Enums used below
- **`StatType`** `[Flags]`: `None=0, Attack=1, Defend=2, Explore=4, Influence=8, Heal=16, Wound=32, Crystal=64`.
  Combine with `|` (e.g. `Explore | Crystal`).
- **`EmpowerType`** `[Flags]`: `None=0, Red=1, Yellow=2, Green=4, Purple=8`. Use `None` for a card/unit
  that cannot be empowered.
- **`RewardType`** `[Flags]`: `None=0, Experience=1, Crystals=2, Cards=4`.
- **`RewardLevel`**: `Beginner, Intermediate, Advanced, Master`.
- **`TownSize`**: `Town, Village, Fortress, City`.

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
| `reward` | `RewardLevel` | Reward tier on defeat |
| `defeatRewards` | List&lt;`RewardsSO`&gt; | Rewards granted on defeat |
| `canInfluence` | bool | Can be dealt with via Influence |
| `influenceCost` | int | Forced to 0 when `canInfluence` is false |

## Town — `TownsSO`
**Menu:** `ScriptableObjects/Cards/TownCards`

| Field | Type | Notes |
|-------|------|-------|
| `townSize` | `TownSize` | Town / Village / Fortress / City |
| `activity` | `TownActivity` (flags) | None / Recruit / Cards / Heal / Resources |
| `recruitableUnits` | List&lt;`UnitsSO`&gt; | Units available to recruit here |
| `recruitLevel`, `cardLevel`, `resourceLevel`, `healLevel` | int | Service levels per activity |

**Rule:** Towns are the currency of the domination win — controlling them counts toward the Archon
threshold, so town placement and difficulty pace the whole run.

## Unit — `UnitsSO`
**Menu:** `ScriptableObjects/Units`

| Field | Type | Notes |
|-------|------|-------|
| `attack`, `defend`, `explore`, `influence` | int | Stats added when played |
| `healAmount`, `numCrystals` | int | Heal / crystals |
| `cardType` | `StatType` (flags) | Which stats this unit provides |
| `sprite` | Sprite | Unit art |
| `color` | Color | Unit tint |
| `unitLetter` | char | Display letter |
| `empowerType` | `EmpowerType` | Crystal affinity |

**Rule:** Recruited at towns with Influence; played to add stats. Units have no per-card empower
toggle like cards (`GetUnitStats` returns base values).

## Reward — `RewardsSO`
**Menu:** `ScriptableObjects/Cards/RewardCards`

| Field | Type | Notes |
|-------|------|-------|
| `rewardType` | `RewardType` (flags) | Experience / Crystals / Cards |
| `rewardLevel` | `RewardLevel` | Beginner / Intermediate / Advanced / Master |
| `expAmount` | int | Experience granted |
| `numCrystals` | int | Crystals granted |

## Dungeon — `DungeonsSO`
**Menu:** `ScriptableObjects/Dungeons`

| Field | Type | Notes |
|-------|------|-------|
| `exploreCost` | int | Explore to enter |
| `enemies` | List&lt;`EnemiesSO`&gt; | Enemies inside |
| `rewards` | List&lt;`RewardsSO`&gt; | Clear rewards |

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
