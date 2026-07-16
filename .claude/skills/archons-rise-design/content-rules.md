# Content Authoring Contract

How to author each content type so it stays consistent and works with the existing code. All
content types inherit **`AllCards`**: `cardName` (string) and `cardDescription` (TextArea string).
**Source of truth:** `Assets/Scripts/GameScriptableObjectTypes/` and `Assets/Scripts/Enums/`. If a
field here ever disagrees with those scripts, the scripts win — update this file.

## Enums used below
- **`StatType`** `[Flags]`: `None=0, Attack=1, Defend=2, Explore=4, Influence=8, Heal=16, Wound=32, Crystal=64, Siege=128, Refresh=256`.
  Combine with `|` (e.g. `Explore | Crystal`). `Refresh` (spec 2026-07-14) is an immediate-effect flag
  like Heal/Crystal (not a per-turn pool): it opens the mid-round refresh picker.
- **`EmpowerType`** `[Flags]`: `None=0, Red=1, Yellow=2, Green=4, Purple=8`. Use `None` for a card/unit
  that cannot be empowered. All-colors (any-crystal cost / wild crystal) = all four flags set = `15`.
- **`UnitEffect`**: `Attack=0, Defend=1, Explore=2, Influence=3, Siege=4, Heal=5, Crystallize=6`. One
  unit option's effect (append-only — new members go at the end).
- **`SkillCadence`**: `PerTurn=0, PerRound=1, Passive=2`. `SkillEffect` (append-only) now also has
  `RecruitEnemies` (the Charismatic passive gate), `ConvertStat` (1:1 pool conversion), and
  `RefreshUnits` (opens the refresh picker with `magnitude` as the budget).
- **`TownSize`**: `Town, Village, Fortress, City`.
- **`PlaceType`**: `Town=0, Keep=1, Castle=2` (source: `Assets/Scripts/Places/`).
- **`PlaceService`** `[Flags]`: `None=0, Recruit=1, Heal=2, Cards=4` (source: `Assets/Scripts/Places/`).

---

## UI language — icons & costs (spec 2026-07-15, M2.11)

One canonical icon per concept and one layout dialect across every panel. `IconMarkup`
(`Assets/Scripts/UiLanguage/`) is the single owner of TMP sprite-tag names and cost strings —
authored text and panel code both go through it; **never hand-roll a `<sprite=…>` literal or a
bare-number cost.** Validation tests (`IconRegistryValidationTests`) enforce this over every
authored `cardDescription`.

- **Costs are `[icon][number]`** with no space: `<sprite="gem" index=0>3` (= `IconMarkup.Cost`).
  Buttons read `[icon] Label` (e.g. Heal, Recruit, Delve).
- **Canonical tag names** (filename = tag, case-sensitive) are the 17 `IconMarkup.TmpName` values:
  `Sword` (Attack), `shield` (Defend), `scroll` (Explore), `gem` (Influence), `Heal`, `wound`,
  `crystal`, `siege`, `hp`, `doom`, `xp` (Experience), `army`, `town`, `keep`, `castle`, `dungeon`,
  `empower`.
- **Empowered-line header** (spec 2026-07-16): the `empower` glyph replaces the literal word
  "Empower" at the head of an empowered line — `<sprite="empower" index=0> <stat>: N`
  (e.g. `<sprite="empower" index=0> <sprite="Sword" index=0>: 6`). Empower is a modifier concept,
  not an action stat, so it is exempt from the per-line action-stat ordering.
- **`shield` means Defend only.** Enemy toughness is `hp` everywhere — never the Defend shield.
- **Action-stat order is Attack, Defend, Explore, Influence**, per line, everywhere the four appear
  together. Lines with a conversion arrow (`->` / `→`) are directional and exempt.
- **Crystal colors tint the one `crystal` glyph** with the canonical hexes (Red `#E5484D`,
  Yellow `#F5D90A`, Green `#46A758`, Purple `#8E4EC6`); `None` and all-colors render untinted.
  Use `IconMarkup.CrystalTag`.
- **Locked / unaffordable = `CanvasGroup.alpha 0.4`** via `UiLock`, on top of `Button.interactable`.
- **Adding a new icon:** one single-glyph TMP Sprite Asset in
  `Assets/TextMesh Pro/Resources/Sprite Assets/` (asset name = tag), plus an `IconConcept` member,
  its `IconMarkup.TmpName` case, and an `IconRegistry.asset` entry — then the validation tests green.

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
| `convertTo` | `StatType` | Conversion target (one action stat); `None` = card has no conversion |
| `convertFrom` | `StatType` (flags) | Conversion sources (action flags only; never contains `convertTo`) |
| `convertRequiresEmpower` | bool | true = the convert toggle is offered only on the empowered play |
| `refresh`, `empowerRefresh` | int | Refresh budget (base / empowered); needs the `Refresh` flag on `cardType` |

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
- **Conversion** (spec 2026-07-14): set `convertTo` to one action stat and `convertFrom` to the action
  stats to drain (an "convert everything into Influence" card flags the three action stats *other than*
  Influence). Rules enforced by `ConvertRules.IsValid` + `OnValidate`: target is exactly one action
  stat; sources are action stats only (Siege/Heal/Crystal/Wound never participate); the target is
  **never** among the sources; and a card **cannot be both `isChoice` and a converter**. Leave
  `convertTo = None` for non-converters. `convertRequiresEmpower = true` gates the toggle behind an
  empowered play.
- **Refresh** (spec 2026-07-14): to make a refresh card, flag `Refresh` on `cardType` and set
  `refresh` / `empowerRefresh` (the budget); `OnValidate` warns if refresh values are set without the
  flag. Pair Refresh with a small secondary stat (e.g. `Explore | Refresh` with `explore 1`) so the
  card is never a dead play when the refresh fizzles.

## Skill — `SkillsSO`
**Menu:** `ScriptableObjects/Skill`

| Field | Type | Notes |
|-------|------|-------|
| `effect` | `SkillEffect` | What activating does (stat gain, crystal, heal, `ConvertStat`, `RefreshUnits`, …) |
| `magnitude` | int | Effect amount; for `RefreshUnits` it is the refresh **budget** |
| `crystalColor` | `EmpowerType` | Only for `GainCrystal` |
| `cadence` | `SkillCadence` | `PerTurn` / `PerRound` / `Passive` |
| `convertFrom`, `convertTo` | `StatType` | Only for `ConvertStat` — same 1:1 conversion rules as cards (action stats only; target not in sources) |

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
color, wild satisfies; all-colors/`15` = any 1 crystal), `influenceCost` (int — in-turn Influence
price; spec 2026-07-14).

**Rules:** Recruited at towns for `influenceCost` (or via enemy influence + Charismatic). The pop-out
lets the player pick one option; using it applies the effect and exhausts the unit for the round.
A crystal-costed option ≈ twice its free sibling's amount (see [balance.md](balance.md)). An option
costs a **crystal OR Influence OR is free — never both** (`UnitsSO.OnValidate` warns if an option
sets both `crystalCost` and `influenceCost`); author stronger variants as separate option rows. The
legacy flat-stat fields (`attack`/`defend`/…/`empowerType`, `GetUnitStats`) are retired.

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
| `exploreCost` | int | Cost of **each** delve (flat × 3) |
| `enemies` | List&lt;`EnemiesSO`&gt; | **Exactly 3**: slot 0 = tier-1 fight, slot 1 = tier-2, slot 2 = tier-3 (OnValidate warns) |
| `tier` | int | Tier the completion bundle pays at |
| `rewardCount` | int | Bundle scale: this many crystals AND this many card picks (guaranteed) |

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
