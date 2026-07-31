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
- **`EnemyTrait`** `[Flags]` (spec 2026-07-29, append-only): `None=0`; self —
  `Armored=1, Elusive=2, Hulking=4, Swift=8, Brutal=16, Toxic=32, Leech=64, Harrying=128,
  Vengeful=256`; aura — `Warlord=512, Miasma=1024, Ironclad=2048, Outrider=4096`. Not saved — read
  from `EnemiesSO.traits` via the enemy's stable id, like `enemyHP`.

---

## UI language — icons & costs (spec 2026-07-15, M2.11)

One canonical icon per concept and one layout dialect across every panel. `IconMarkup`
(`Assets/Scripts/UiLanguage/`) is the single owner of TMP sprite-tag names and cost strings —
authored text and panel code both go through it; **never hand-roll a `<sprite=…>` literal or a
bare-number cost.** Validation tests (`IconRegistryValidationTests`) enforce this over every
authored `cardDescription`.

- **Costs are `[icon][number]`** with no space: `<sprite="gem" index=0>3` (= `IconMarkup.Cost`).
  Buttons read `[icon] Label` (e.g. Heal, Recruit, Delve).
- **Canonical tag names** (filename = tag, case-sensitive) are the 18 `IconMarkup.TmpName` values:
  `Sword` (Attack), `shield` (Defend), `scroll` (Explore), `gem` (Influence), `Heal`, `wound`,
  `crystal`, `siege`, `hp`, `doom`, `xp` (Experience), `army`, `town`, `keep`, `castle`, `dungeon`,
  `empower`, `refresh`.
- **Refresh** (spec 2026-07-16): the `refresh` glyph replaces the word in card text
  (`<sprite="refresh" index=0> 3`); panel headers keep icon + word (unit picker:
  `<refresh> Refresh — N left`). `IconMarkup.TryForStat` maps `StatType.Refresh` →
  `IconConcept.Refresh` (the M2.11-era exclusion is lifted).
- **Empowered-line header** (spec 2026-07-16): the `empower` glyph replaces the literal word
  "Empower" at the head of an empowered line — `<sprite="empower" index=0> <stat>: N`
  (e.g. `<sprite="empower" index=0> <sprite="Sword" index=0>: 6`). Empower is a modifier concept,
  not an action stat, so it is exempt from the per-line action-stat ordering.
- **`shield` means Defend only.** Enemy HP is `hp` everywhere — never the Defend shield.
  ("Toughness" now names the character stat only; enemies keep HP, a real depleting pool.)
- **Action-stat order is Attack, Defend, Explore, Influence**, per line, everywhere the four appear
  together. Lines with a conversion arrow (`->` / `→`) are directional and exempt.
- **Crystal colors tint the one `crystal` glyph** with the canonical hexes (Red `#E5484D`,
  Yellow `#F5D90A`, Green `#46A758`, Purple `#8E4EC6`); `None` and all-colors render untinted.
  Use `IconMarkup.CrystalTag`.
- **Locked / unaffordable = `CanvasGroup.alpha 0.4`** via `UiLock`, on top of `Button.interactable`.
- **Adding a new icon:** one single-glyph TMP Sprite Asset in
  `Assets/TextMesh Pro/Resources/Sprite Assets/` (asset name = tag), plus an `IconConcept` member,
  its `IconMarkup.TmpName` case, and an `IconRegistry.asset` entry — then the validation tests green.
- **`IconMarkup.TraitBadge` is the sole owner of trait glyphs** (spec 2026-07-29 §8.1): it returns a
  single-character letter badge today (e.g. `"A"` for Armored) and will return a `<sprite=…>` tag
  once trait art lands — call sites never change either way. The "never hand-roll a glyph" rule
  covers trait badges exactly like every other icon concept.

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
| `traits` | `EnemyTrait` (flags) | Authored combat traits (spec 2026-07-29); `None` by default. Never modifies `influenceCost` — see §5.3 below |

## Enemy Trait Tuning — `EnemyTraitTuningSO`
**Menu:** `ScriptableObjects/EnemyTraitTuning` — one shared asset, wired onto `CombatController`
(spec 2026-07-29, same pattern as `RewardTuningSO` on `Rewards` and `DoomTuning`). Enemies tick
trait boxes; this asset owns every magnitude, so a keyword (e.g. "Armored") means one fixed thing
game-wide and a playtest retune is a single field edit.

| Field | Meaning |
|-------|---------|
| `armorSiegeMult` | Armored: Siege cost multiplier |
| `hulkAttackMult` | Hulking: Attack cost multiplier |
| `swiftThreatMult` | Swift: threat multiplier |
| `brutalSurchargeMult` | Brutal: multiples of base Attack added past the cap |
| `warlordBonus` | Warlord: Attack granted to each *other* survivor |
| `toxicCopies` | Toxic: discard copies per wound in its share |
| `leechCrystals` | Leech: crystals stolen per wound in its share |
| `vengefulWounds` | Vengeful: wounds on an Attack-phase kill |
| `harryHandPenalty` | Harrying: hand-size reduction next turn |

Starting values: see [balance.md](balance.md).

### §5.3 Enemy trait authoring rules
- **Field (solo) enemies draw only from self traits.** A granting aura on a solo enemy silently
  degenerates into its own self trait (solo Miasma ≡ Toxic) — wasted authoring and a confusing first
  encounter with the keyword.
- **Auras are guardian-only** (Keep/Castle rosters), keeping guarded places structurally different
  from map fights rather than merely bigger.
- **Aura enemies are authored at low HP (2–4)** so the Siege-targeting puzzle they create stays
  winnable.
- **Tier caps:** tier 1 — at most one self trait, never an aura. Tier 2 — one or two self traits, or
  one weak aura. Tier 3 — an aura plus a self trait.
- **Never author Elusive with Armored** (the second is dead text — Elusive already removes Siege
  entirely), and never author Vengeful or Elusive on tier 1.
- **Elusive carries no `canInfluence` requirement** (decided 2026-07-31, reversing the earlier
  mandatory-pairing rule). Closing the wound-free routes and forcing the fight down the
  Defend-then-Attack line — or out through a flee — IS the choice Elusive presents; guaranteeing an
  Influence out on every Elusive enemy made the trait a soft redirect instead of pressure. Pair
  Elusive with `canInfluence` when a *particular* enemy wants the "bribe it, don't besiege it"
  identity, never as a blanket rule.
- **Traits are additive to the tier system, not a replacement** — tier still drives rewards and doom
  gating; traits drive texture.
- **No trait modifies `influenceCost`.** Influence's balance lever is scarcity, never trait-modified
  price (see [balance.md](balance.md)).

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

## Crystal Hotspot — `CrystalHotspotSO`
**Menu:** `ScriptableObjects/CrystalHotspot` (spec 2026-07-24, Plan 1)

| Field | Type | Notes |
|-------|------|-------|
| `color` | `EmpowerType` | Single color this tile yields (Red/Yellow/Green/Purple — never a combined flag) |
| `charges` | int | Payouts before the tile goes dormant. **`-1` = unlimited** (rich vein, never depletes) |
| `crystalSprite` | `Sprite` | This color's crystal art; the token displays it. Color art lives on its own SO asset (red art on the red SO, etc.), **not** on the rule tile — the `CrystalHotspotRuleTile` is one color-agnostic ground marker (single Default Sprite; no tiling rules) placed under every hotspot. |

Save identity is the **inherited `AllCards.id`** (a stable slug; **never rename** — a v8 `HotspotState`
restores by it). Do **not** re-declare `id` on the subclass: Unity errors ("same field name serialized
multiple times") on a duplicate serialized field shadowing the base.

A crystal hotspot is a passive map tile. Standing on it at **End Turn** grants **1 crystal** of its
`color` and spends a charge — it is **free** (never the turn's Action) and shows **no popup / no
`RewardQueue`**; the HUD crystal count and the token's pip/dormant state are the only feedback
(memory `map-feedback-tooltip-and-log`). The `CrystalHotspotRuleTile` asset carries a **static
`exploreCost` = 3** (mine-like): placement overwrites the underlying ground tile, so the entry cost is
the hotspot tile's own cost, **not** the terrain it landed on — a deliberate flat "expensive to reach,
farm the crystals" trade rather than terrain-scaled cost. Charge math is the pure `HotspotRules`; per-run state is the
`Cell`-keyed `HotspotLedger` wrapped by the scene `HotspotTracker`; only drawn-down/depleted tiles
are saved (`RunState.hotspots`, schema **v8**), fresh tiles re-derive from the map seed.

**Hex tooltip occupants (`IHexOccupant` + `TileDescriptor`):** map tokens describe themselves to the
hex tooltip by implementing `IHexOccupant` (`Cell`, `Describe() → HexDescriptor`, `BlocksMove`) and
registering with `HexOccupantRegistry` on `Start`. The line text is built by the pure
`TileDescriptor` (`Hotspot`/`Town`/`Dungeon`), always via `IconMarkup` — never a hand-rolled sprite
tag. A new tile type integrates with **zero `HexInteractor` edits**: implement + register. `BlocksMove`
is `true` for place-like tokens (towns/dungeons block move-dispatch), `false` for passive tiles
(hotspots — you must be able to park on them).

## Shrine — `ShrineSO`
**Menu:** `ScriptableObjects/Shrine` (spec 2026-07-24, Plan 2)

| Field | Type | Notes |
|-------|------|-------|
| `crystalCost` | int | Crystals to engage. **Any colors** — 4 by default; the panel spends one per placement |
| `goodRollChance` | float (0–1) | Chance of the safe result (instant 1×). Default `0.5` |
| `rewardTypes` | List&lt;`ShrineReward`&gt; | Types this shrine can roll; drawn uniformly |
| `unitPool` | List&lt;`UnitsSO`&gt; | Candidates when the rolled type is `Unit` |
| `largeExp` | int | The 1× large-exp payout (a fight pays 2×) |
| `cardTier` | int | Tier for the card-pick reward (reuses the `Rewards` card pools) |
| `summonedEnemy` | `EnemiesSO` | The fixed **tier-3** guardian summoned on the bad roll. **Must be in the `EnemyDeck` pool** or the spawn is skipped with a warning |

Save identity is the **inherited `AllCards.id`** — do **not** re-declare `id` on the subclass (Unity
errors on a duplicate serialized field), same as `CrystalHotspotSO`.

`ShrineReward` is **append-only** (`CardPick = 0`, `Unit = 1`, `LargeExp = 2`) — saved ints must stay
stable. **No skills**: skills are a level-up channel, never a shrine payout. Crystals are not a payout
either; the shrine consumes them.

A shrine is a **stand-on, one-shot** place (`BlocksMove = true`). Opening the panel is a free peek: a
fan of `crystalCost` slots arcs over the shrine (`FanMath`, the same solver the hand uses). Clicking a
slot **cycles** it through the crystals the player can still spare — colors already claimed by other
slots drop out — and around to empty again, so any slot can be un-set without a Cancel button. Because
a bucket is only offered while genuinely spare, **an unaffordable payment is unrepresentable** and the
confirm can never fail. Clicking off the fan dismisses at no cost.

Selection is **non-destructive**: slots hold picks, not crystals. Nothing leaves the inventory until
the player presses the checkmark that appears (on an appended fan seat) once every slot is filled.
**That checkmark is the commit point** — it spends the turn's action and the crystals **regardless of
the outcome**, and is shown `UiLock`-dimmed if the turn's action is already gone. Payment spends the
exact buckets chosen: a wild is consumed only when the player picked wild, never as a silent
substitute for a missing color. Then it resolves: **safe** → 1× the rolled reward immediately, shrine
→ `ConsumedDormant`; **bad** → a persistent tier-3 guardian on a free neighbouring cell, shrine →
`Guarding`. Defeating (or influencing away) that guardian pays **2×** the rolled reward plus its
defeat exp and **nothing else** — the guardian is exp-only, its loot *is* the shrine's doubled
reward. Fleeing costs the usual 1 wound and leaves it standing; the shrine stays `Guarding` until it
falls.

Roll math is the pure `ShrineRules` (`IsGood`/`RollType`/`RewardCount`) and slot-cycle math the pure
`ShrinePaymentRules` (`NextPick`/`Spare`/`IsComplete`, `-1` = an empty slot); per-run state is the
`Cell`-keyed `ShrineLedger` wrapped by the scene `ShrineTracker`. Only non-`Live` shrines are saved
(`RunState.shrines`, schema **v10**); fresh shrines re-derive from the map seed. The guardian's owed
reward rides on the mid-run spawn record (`SpawnedEnemy.shrineRewardType` / `shrineCellX` /
`shrineCellY`; **`-1` = an ordinary spawn**), so it survives save/reload.

## Location — `LocationsSO`
**Menu:** `ScriptableObjects/LocationsSO`

| Field | Type | Notes |
|-------|------|-------|
| `exploreCost` | int | Explore to reveal |
| `enemies` | List&lt;`EnemiesSO`&gt; | Enemies present |
| `towns` | List&lt;`TownsSO`&gt; | Towns present |
| `dungeons` | List&lt;`DungeonsSO`&gt; | Dungeons present |

## Character — `CharacterSO`
**Menu:** `ScriptableObjects/CharacterSO` (was `PlayerSO` before 2026-07-23)

A playable character is a **content bundle**, never a rules variant: characters differ only in the
fields below. Rule-bending belongs in `SkillEffect` (the Charismatic precedent), and every character
starts with zero skills. `DataManager.ActiveCharacter` is the single runtime source of truth.

| Field | Type | Notes |
|-------|------|-------|
| `id` | string | Stable slug; recorded in the save (`RunState.characterId`) — never rename it |
| `characterName` | string | Display name |
| `startingToughness` | int | Divisor of the Defend shortfall. **Minimum 1** — 0 hangs `WoundCount` |
| `handSize` | int | Base hand size before level bonuses |
| `improvAttack` / `improvDefend` / `improvExplore` / `improvInfluence` | int | Improvise yields |
| `startingDeck` | List&lt;`CardsSO`&gt; | The opening deck |
| `levelTable` | `LevelRewardsSO` | Progression curve; characters may share one |
| `skillPool` | `SkillPoolSO` | Skills offered on skill-pick levels |
| `animatorController` | `RuntimeAnimatorController` | Avatar animation (spec Part D) |

## Skill pool — `SkillPoolSO`
**Menu:** `ScriptableObjects/SkillPool`

| Field | Type | Notes |
|-------|------|-------|
| `skills` | List&lt;`SkillsSO`&gt; | Draw pool for level-up skill picks |

Split out of `LevelRewardsSO` on 2026-07-23 so characters that share a progression curve can still
draw from different skill pools — retuning the exp curve stays a single-asset edit.

## Tutorial & help copy (spec 2026-07-15, M2.12)

Tutorial and help text is authored as ScriptableObject assets under `Assets/Tutorial/`, never in
code. Three types (all in the `ArchonsRise.Tutorial` asmdef):

| Type | Menu | Fields | Persistence key |
|------|------|--------|-----------------|
| `TutorialStepSO` | `ArchonsRise/Tutorial/Rail Step` | `id`, `bannerText`, `highlightTargetId`, `completionEventId` | `tut.railStep` (index) |
| `TutorialOneShotSO` | `ArchonsRise/Tutorial/One-Shot Tip` | `id`, `bannerText`, `highlightTargetId`, `triggerEventId` | `tut.oneshot.<id>` |
| `HelpEntrySO` | `ArchonsRise/Tutorial/Help Entry` | `panelId`, `title`, `body` | `tut.help.<panelId>` |

**Rules:**
- **All copy embeds registry icons** via the canonical `<sprite="name" index=0>` tags (names from
  `IconMarkup.TmpName`, the same 18 as card text). `TutorialCopyValidationTests` fails on any unknown
  tag — never hand-roll a glyph name.
- **`id` / `panelId` are PlayerPrefs key components — never rename after ship** (a rename resets the
  player's seen state). They must be unique and non-empty (`TutorialCopyValidationTests` pins this).
- **One-shots stay short (≤ 2 sentences)** and point at a panel's `?` for the durable explanation —
  the reactive tip teaches the moment, the help entry is the reference.
- **`completionEventId` / `triggerEventId` come from the event-id contract** (below). An empty
  `completionEventId` makes a rail step informational (advances on the banner's **Next** button).
- **New one-shots / help entries must be added to the `TutorialManager`'s `oneShots` / `helpEntries`
  lists** in the scene, or Skip and Reset won't clear their persisted keys.

**Event-id contract** (rail step / one-shot completion strings; each is wired as a listener's Static
string argument onto the TutorialManager object, except `doom-band` which is a DYNAMIC int on
`NotifyDoom`):
`card-played`, `player-moved`, `combat-started`, `enemy-resolved`, `turn-ended`, `wound`, `crystal`,
`level-up`, `town-entered`, `dungeon-entered`, `deck-cant-refill`, `doom-band`.

Tutorial state is **device-level PlayerPrefs only** (keys namespaced `tut.*`) — the run save schema
(v6) is untouched by the tutorial.
