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
- **+1 per round**; certain events (failed dungeon, ignored threat) add **+1 to +3** extra.
- Reaching max = run lost.
- _Starting values — tune in playtest._ Max vs. per-round rate sets the run's overall time budget.

## Wound-out (tactical loss)
- Lose if **Wounds in deck ≥ 6**, OR **HP reduced to 0**.
- _Starting values — tune in playtest._ Tighten the wound count to make combat losses more punishing.

## Crystal Costs
- Empowering a card spends **1 crystal** of the card's `empowerType`.
- Per-card cost can be raised via `empowerNumCrystals` for premium effects.
- _Starting rule — tune per card._

## Reward Tiers
Map `RewardLevel` to rising payouts (`expAmount` / `numCrystals` / card rarity):

| Tier | expAmount | numCrystals | Card rarity |
|------|-----------|-------------|-------------|
| Beginner | 1–2 | 0–1 | common |
| Intermediate | 3–4 | 1–2 | common/uncommon |
| Advanced | 5–7 | 2–3 | uncommon/rare |
| Master | 8–12 | 3–4 | rare |

_Starting bands — tune in playtest._

## Leveling Curve
- `expToNextLevel` growth follows existing code: **`expToNextLevel += playerLevel + 12`** on level-up.
- Per-level rewards (from [mechanics.md](mechanics.md)): even level → +1 stat, odd → +HP,
  every 3rd → +hand size, every level → +skill.
- _Starting values — tune in playtest._ Adjust the `+12` constant to speed up or slow down leveling.

## Unlock Pool (meta-progression)
- Unlock categories: **cards, units, enemies, events**.
- Cadence: **1 unlock per run win** (drawn from the locked pool), entering the future-run content pool.
- No power carryover — unlocks only widen variety (pillar 4).
- _Starting cadence — tune in playtest._ Could grant partial unlocks on strong losses to soften the curve.
