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
- Lose if **Wounds in deck ≥ 6**. (HP is toughness, never depletes — decision 2026-07-06.)
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
| 3 | +1 HP, card pick |
| 4 | +1 hand size, +1 army size |
| 5 | skill pick |
| 6 | +1 HP, card pick |
| 7 | skill pick, +1 army size |
| 8 | +1 hand size |
| 9 | +1 HP, skill pick |
| 10 | +1 army size, +1 hand size, card pick |

- Baselines: hand size **5**, army cap **1**, HP **2**. Levels past the last entry grant nothing.
- **Card pick** = the standard choose-1-of-3 card reward screen (same pool as enemy defeats).
- Table entries are per-level **counts** (skill picks, card picks, bonuses) — every knob is
  inspector-tunable per level with no code change.
- _Starting values — tune in playtest._ Adjust the `+12` constant to speed up or slow down leveling.

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
