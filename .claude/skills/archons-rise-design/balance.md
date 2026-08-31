# Balance

Tuning knobs for Archon's Rise. Every value below is a **starting value — tune in playtest**, not a
final number. [mechanics.md](mechanics.md) defines how these systems work; this file gives them
numbers.

## Archon Win Threshold
- Conquer **2 Castles** (no Level/Influence gate — territory is the sole win axis).
- Guardian rosters (data-driven starting counts): **Town 0, Keep 1, Castle 2**; Dungeon 2 (existing).
- **Assault retreat penalty: 3 wounds** (`PlaceRules.RetreatWoundCount`) vs. 1 for field-combat flee.
- _Starting values — tune in playtest._ Grow rosters or castle count to lengthen runs.

## Doom Clock
- Starts at **0**, maximum **20**.
- **+1 per round**, **plus +1 per flagged, uncleared dungeon** (M2.9 — a flagged dungeon ticks
  until cleared; the round add is `1 + flaggedCount`).
- Bands: low **0–6**, mid **7–13**, high **14–20** (`lowBandMax` 6, `midBandMax` 13).
- Reaching max = run lost.
- _Starting values — tune in playtest._ Max vs. per-round rate sets the run's overall time budget.

## Turns per Round ("Day" length) (spec 2026-07-21, M2.13)
A round is a Doom-band-scaled day; its turn budget shrinks as Doom climbs (`DoomRules.TurnsForBand`,
fields on `DoomTuning`):

| Band | Doom | `turnsPerRound` |
|------|------|-----------------|
| Low  | 0–6  | **6** (`lowBandTurns`)  |
| Mid  | 7–13 | **4** (`midBandTurns`)  |
| High | 14–20| **3** (`highBandTurns`) |

The day also ends early if the deck can't refill the hand (forced rest). _Starting values — tune in
playtest._ Set them on the `DoomTuning.asset` (new fields default to 0 on existing assets).

## Terrain & the Opening Walk
Per-cell terrain roll (`GridGeneration`), with `exploreCost` in brackets:

| Terrain | Roll | Cost |
|---|---|---|
| Plains | 0–45 (46%) | **1** |
| Forest | 46–75 (30%) | **2** |
| Desert | 76–89 (14%) | **3** |
| Water | 90–94 (5%) | **5** |
| Mountain | 95–99 (5%) | **4** |

### Gentle start (spec 2026-08-13)
The start is the map's **corner** `(0,0)`, so it has only **two on-map exits** — `(0,1)` and `(1,0)`.
That makes a harsh roll there far costlier than the flat 24% suggests: on 1% of maps *both* exits
rolled water/mountain, so a new player's first step cost 4–5 explore.

Inside `gentleStartRadius` (**2** hex steps, a 6-cell sliver once clipped to the corner) harsh rolls
are remapped: **desert → plains, water/mountain → forest**. Plains and forest rolls are untouched.

| | before | after |
|---|---|---|
| Avg explore cost of the opening cells | 1.93 | **1.40** |
| Cost-3-or-worse hexes | 24.1% | **0%** |
| Both exits at cost 4–5 | 1.01% | **0%** |
| Total cost to cross the opening | 11.6 | **8.4** |

Applied *after* the desert pass and off the roll already taken, so it **consumes no rng of its own** —
every cell outside the radius keeps the terrain its seed has always produced. Every other placement
system (towns from x,y ≥ 3; dungeons/hotspots/shrines/zones via `startSafeRadius` 3) already excludes
everything inside the radius, so for a given seed the change is provably confined to those 6 cells.
_Raise `gentleStartRadius` to 3 for an 11-cell opening; 0 disables._

## Map Spawns & the Opening Fight
- **6 spawn zones** (`spawnZoneCount`), min spacing **4** Chebyshev (`zoneMinSpacing`), seeded outside
  the **start safe radius 3** (`startSafeRadius`). A 7th zone is force-inserted at the nearest valid
  land ring from d=2 so the opening always has a reachable threat.
- **2 enemies per zone** at map gen (`initialEnemiesPerZone`), tier 1 with no stat bonus (doom is 0).

### Starter isolation (spec 2026-08-13)
A field encounter drags in **every** enemy adjacent to the player (`FieldEncounterRules`), so a
two-enemy pack next to the start made the tutorial's first fight a 2v1 on ~99% of maps — measured
cause of testers wounding out inside three combats.

The first enemy placed within `starterEnemyRadius` (5) of the start is the **starter**, and the cells
that share an approach hex with it are quarantined from all later placement
(`SpawnRules.StarterQuarantine` — the radius-2 disk minus the centre, 18 cells):

> **No other enemy may share an approach hex with the starter — no single cell may touch both.**

Consequences, measured over 3000 simulated maps:

| | before | after |
|---|---|---|
| Starter fight can be 2v1 | 99.0% | **0.0%** |
| Enemies placed at map gen | 14.00 | 12.87 |

The near-start zone loses its second enemy through the existing "skip, never force-place" path, so
this needs no separate tuning knob. **Packs elsewhere on the map are untouched** — the swarm rule is a
pillar of field combat, and only the opening is protected. _Tune via `starterEnemyRadius`; the
quarantine radius itself is fixed by the combat rule and is not a balance knob._

## Dungeons (M2.9)
- **6 dungeons per map** (`dungeonCount`), min spacing **4** Chebyshev (`dungeonMinSpacing`), never
  on towns or within the start safe radius (3).
- **3 delves each**, flat `exploreCost` per delve: **2** for tier-lite dungeons, **3** for the
  tougher three. Fights are exp-only; `DoomRules.MaxTier` does **not** gate them.
- **Completion bundle:** 1 exp roll at `tier` + `rewardCount` crystals + `rewardCount` card picks,
  all guaranteed. `rewardCount` **1** for five dungeons, **2** for the showpiece (Wyrm's Hollow).
- **Flags:** `flagsOnMidBand` **1** + `flagsOnHighBand` **1** (one dungeon flagged on first entry
  into each band, once per run).
- **Doom relief on clear:** `dungeonCompleteRelief` **−1** unflagged, `flaggedCompleteRelief` **−3**
  flagged.
- _Starting values — tune in playtest._

## Wound-out (tactical loss)
- Lose if **Wounds in deck ≥ 6**. (Toughness is a divisor, never depletes — decision 2026-07-06.)
- _Starting values — tune in playtest._ Tighten the wound count to make combat losses more punishing.

## Crystal Costs
- Empowering a card spends **1 crystal** of the card's `empowerType`.
- Per-card cost can be raised via `empowerNumCrystals` for premium effects.
- _Starting rule — tune per card._

## Crystal Purchase (at Places)
- Every conquered Place sells crystals; the buyer picks the color, one crystal per purchase.
- **Price is the Place's `resourceLevel`** (Influence per crystal) — per-place, so stronger/rarer
  Places can charge more. `0` means free, so every selling Place needs a non-zero value.
- Starting band: **Town 2–4, Keep 3, Castle 4** (Influence per crystal). Merchant-flavoured Towns
  can sit at the top of the Town band.
- _Starting values — tune in playtest._ Influence is the sole limiter on crystal count (per the
  2026-07-10 decision), so this price trades Influence pressure against Empower power (pillar 3).

## Reward Tiers
Combat/dungeon rewards derive from a **tier** (1–3, = enemy `tier`, gated by
`DoomRules.MaxTier`) on the shared `RewardTuningSO`. Experience is **always** granted,
bell-curve sampled (`RewardRules.SampleExp` — average of `expBellSamples` uniform draws,
so results centre on the range's middle). Crystals and cards are **independent bonus
rolls** against per-tier chances (crystals common, cards rare).

| Tier | exp range (centre-weighted) | crystalChance | cardChance |
|------|-----------------------------|---------------|------------|
| 1 Beginner     | 1–5  (mostly 2–4) | 0.50 | 0.08 |
| 2 Intermediate | 3–7  (mostly 4–6) | 0.60 | 0.12 |
| 3 Advanced     | 6–10 (mostly 7–9) | 0.70 | 0.18 |

- `expBellSamples = 3` (raise to tighten the bell; 1 = flat/uniform).
- On a crystal roll: **1 crystal, random color** (per-tier count/color weighting is a
  future pass). On a card roll: choose-1-of-3 from that **tier's card pool** — pool
  membership is the card's rarity, so stronger cards simply live only in higher tiers.
- **Level-up card picks** scale with player level: tier 2 at level ≥ `levelTier2` (4),
  tier 3 at level ≥ `levelTier3` (7). Same "strength tracks progress" story as enemy drops.
- Dungeons carry their own `tier` + `rewardCount` (number of reward events).

_Starting bands — tune in playtest._

## Leveling Curve
- `expToNextLevel` growth follows existing code: **`expToNextLevel += playerLevel + 12`** on
  level-up; overflow exp carries into the next level.
- Reward table (decision 2026-07-06 — data-driven via `LevelRewardsSO`):

| Level | Reward |
|---|---|
| 2 | skill pick |
| 3 | +1 Toughness, card pick |
| 4 | +1 hand size, +1 army size |
| 5 | skill pick |
| 6 | +1 Toughness, card pick |
| 7 | skill pick, +1 army size |
| 8 | +1 hand size |
| 9 | +1 Toughness, skill pick |
| 10 | +1 army size, +1 hand size, card pick |

- Baselines: hand size **5**, army cap **1**, Toughness **2**. Levels past the last entry grant nothing.
- **Card pick** = the standard choose-1-of-3 card reward screen (same pool as enemy defeats).
- Table entries are per-level **counts** (skill picks, card picks, bonuses) — every knob is
  inspector-tunable per level with no code change.
- _Starting values — tune in playtest._ Adjust the `+12` constant to speed up or slow down leveling.

## Enemy Traits (spec 2026-07-29)
Starting values on the shared `EnemyTraitTuningSO` (`content-rules.md`'s `EnemyTraitTuningSO`
section):

| Field | Start |
|-------|-------|
| `armorSiegeMult` | 2 |
| `hulkAttackMult` | 2 |
| `swiftThreatMult` | 2 |
| `brutalSurchargeMult` | 1 |
| `warlordBonus` | 1 |
| `toxicCopies` | 1 |
| `leechCrystals` | 1 |
| `vengefulWounds` | 1 |
| `harryHandPenalty` | 1 |

**Tier guidance:**
- **Tier 1:** at most 1 self trait, no auras.
- **Tier 2:** one–two self traits, or one weak aura.
- **Tier 3:** an aura plus a self trait.

**`canInfluence` availability target: ~30% of enemies map-wide.** This is a standing authoring rule
across the enemy pool (not per-roster). Reasoning: Influence dominates Siege on every axis it can be
compared on — wound-free (like Siege), full rewards (like Siege), but also **improvisable** (unlike
Siege, which is deliberately non-improvisable — a stated pillar) and **can recruit** (with
Charismatic). With no cost lever available to traits (see content-rules.md §5.3 — no trait may modify `influenceCost`),
**availability is Influence's only balance lever**: too far below ~30% and Influence builds have
nothing to spend on; much above it and Siege stops mattering, which collapses the reason
non-improvisable Siege exists at all. Elusive enemies hold no standing claim on this budget — the
mandatory Elusive/`canInfluence` pairing was dropped 2026-07-31 (`content-rules.md` §5.3), so the
whole ~30% is free to sit wherever Influence is most interesting. An Elusive enemy left at
`canInfluence = false` is a deliberate "Defend, Attack, or flee" fight, not an authoring error.
- _Starting values — tune in playtest._

## Unit Recruit Costs
- Per-unit **Influence** price bands: **cheap 2–3** (single-effect / utility), **standard 3–4**
  (two solid options), **premium 5+** (strong or dual costed options).
- A **crystal-costed option** delivers roughly **2× its free sibling's amount** (paying a crystal
  must feel worth it — pillar 3). E.g. Knight: Defend 3 free / Defend 6 for 1 Red.
- An **Influence-costed option** (spec 2026-07-14) prices ≈ the **recruit-value of the stat burst** it
  grants — a mercenary's "Attack 5 — 3 Influence" trades Influence pressure for a combat spike. One
  cost type per option; author stronger tiers as separate rows.
- _Starting values — tune in playtest._

## Conversion & Refresh (spec 2026-07-14)
- **Converter cards** price **~1 point under** a vanilla same-tier card of the same stat: the 1:1
  conversion is an opt-in upside, so the base stat line is slightly discounted to pay for it. E.g.
  Shield Bash (Defend 3 / empower Defend 5, may convert Defend→Attack) sits just under a plain Defend
  card. Convert **skills** (e.g. Tactician) are per-round.
- **Refresh** budget ≈ recruit value: base **Refresh ≈ one cheap unit** (`influenceCost` ~2–3),
  empowered **≈ two cheap or one elite** unit. E.g. Mobilize Refresh 3 / empower Refresh 6. Pair
  refresh with a small secondary stat (Explore 1) so a fizzle is never a wasted card.
- _Starting values — tune in playtest._

## Skill Pool
- Skill pick offers **3** random unowned skills; a pick is skipped if the pool is exhausted.
- Starting pool (10): per-turn — Drillmaster +1 Attack, Shieldwall +1 Defend, Envoy +1 Influence,
  Pathfinder +1 Explore; per-round — Crystallize Red/Yellow/Green/Purple (1 crystal of that
  color), Field Medic (heal 1 wound); passive — **Charismatic** (recruit influenced enemies that
  have a `recruitedUnit`).
- Cadence is the balance lever: strong effects (crystals, healing) are per-round only; passives are
  always-on gates with no activation.
- _Starting pool — tune in playtest._ M3's unlock pool can add skills to future runs.

## Unlock Pool (meta-progression)
- Unlock categories: **cards, units, enemies, events**.
- Cadence: **1 unlock per run win** (drawn from the locked pool), entering the future-run content pool.
- No power carryover — unlocks only widen variety (pillar 4).
- _Starting cadence — tune in playtest._ Could grant partial unlocks on strong losses to soften the curve.

## Unit Armor (spec 2026-08-12)
- `armorClass` scales with recruit price so armor is bought, not free:
  **cheap (2–3 Influence) → 1**, **standard (3–4) → 2**, **premium (5+) → 3**, **support/caster → 0**.
- **The army cap is already the stacking limit** — it starts at 1 and rises only on milestone
  level-ups, so unlimited stacking can never exceed a level-gated resource. No separate soak cap.
- Toxic makes a unit a **heal-2** problem, which a per-round Field Medic (heal 1) can never clear
  alone — that is the intended cost, not an oversight.
- _Starting values — tune in playtest._
