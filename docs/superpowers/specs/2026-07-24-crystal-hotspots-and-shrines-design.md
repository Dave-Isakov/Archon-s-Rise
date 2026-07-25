# Crystal Hotspots & Shrines — Design

**Date:** 2026-07-24
**Status:** Design approved; ready for implementation planning.
**Pillar fit:** Pillar 3 (crystals are the spice). Both tiles create crystal-spend and
crystal-*harvest* decisions and add a strategic map-logistics layer, not flat stat sticks.

## Summary

Two new stand-on-cell hex tile types, plus a shared, extensible tile-tooltip upgrade:

- **Crystal Hotspot** — a scattered tile of a fixed crystal color. Standing on it when you press
  **End Turn** grants 1 crystal of that color as a *free passive* (does not spend the turn's Action).
  Each tile has a **charge count** that decrements per harvest; a sentinel `-1` = **unlimited** (rare
  rich veins). At 0 charges it goes dormant. MTG-style mana tapping; self-limited by the Doom race.
- **Shrine** — a one-shot stand-on-cell place. Costs the turn's **Action**. The player places **any
  4 crystals** one at a time via color buttons over the player's head. On engage the shrine rolls a
  reward **type** ({card pick / unit / large exp} — *no skills*), then a coin flip: **good** → 1× that
  reward instantly; **bad** → summons a persistent **tier-3 guardian** onto the map. Defeating it
  (now, or after fleeing and returning) grants **2×** the reward + the enemy's defeat exp (enemy drops
  nothing else). Crystals are spent regardless of outcome. The shrine is consumed on engage.
- **HexTooltip enrichment** — the tooltip becomes a feature-rich *tile-occupant descriptor* via an
  `IHexOccupant` interface every token implements, so future tile types integrate for free.

Build approach: **mirror the shipped Dungeon subsystem** (RuleTile + map Token + Tracker/ledger +
content SO + pure rule class), one save-schema bump (v7) covering both tiles. Chosen over a minimal
Place-taxonomy reuse (bends the conquer-to-win Place model) and a single unified special-tile class
(fuses a passive trigger and an interactive menu into one class that would grow tangled).

## Locked decisions (from brainstorm 2026-07-24)

- Shrine cost is **any 4 crystals** (mixed colors allowed), placed one at a time via over-head color
  buttons; hotspots yield a **fixed, visible color**, placed randomly on the map.
- Hotspot harvest fires on **every End Turn** the player is parked on a live tile; **charge-limited**
  with a `-1` unlimited sentinel; harvesting is a **free passive** (never the turn's Action).
- Shrine is **one-shot** (consumed on engage).
- Reward pool = **{card pick, unit, large exp}**, no skills. **Safe roll = 1×, fight roll = 2×** the
  rolled type + the guardian's defeat exp (guardian drops nothing else).
- Summoned guardian is a **fixed tier-3** enemy that **persists on the map**: flee (1 wound) and
  return later to finish it; the doubled reward is still owed on defeat. Crystals spent either way.
- Tile counts/placement are **tuning knobs**, seeded/spaced like the 6 dungeons (spread out, never on
  towns or the start safe ring).
- On-map feedback leans **away from ValidationMessage popups** — token visuals + the enriched tooltip
  carry state; a future **Map Event Log** (deferred) will collect map events.

---

## Section 1 — Crystal Hotspot

### Content — `CrystalHotspotSO : AllCards`
**Menu:** `ScriptableObjects/CrystalHotspot`

| Field | Type | Notes |
|-------|------|-------|
| `id` | string | Stable slug; save identity. Never rename. |
| `color` | `EmpowerType` | Single flag — the crystal it yields; shown on the token. |
| `charges` | int | Payouts before dormancy. **`-1` = unlimited** (rich-vein sentinel). |

### Map side (mirrors Dungeon; passive, not click-to-interact)
- `CrystalHotspotRuleTile : HexRuleTile` (empty subclass, `CreateAssetMenu` — the `DungeonRuleTile`
  pattern).
- `CrystalHotspotToken : MonoBehaviour, IHexOccupant` holding `gridPos`, the SO, and visual states:
  **live** (colored crystal + a small remaining-charge pip; unlimited shows an ∞/no-pip variant) and
  **dormant** (greyed, spent). It registers with `HotspotTracker` on `Start` and refreshes its visual.
  It has **no** `IPointerClickHandler` — harvesting is passive, so the token never opens a menu. It
  does implement `IHexOccupant` so the tooltip and move-blocking see it (Section 1c).

### Runtime + pure rule
- `HotspotTracker` singleton wrapping a pure `HotspotLedger` (Cell-keyed; `DungeonLedger` pattern):
  stores remaining charges per cell, `Register(cell, id)`, `Remaining(cell)`, `Harvest(cell)`,
  `Export()` / `ApplySave()`.
- `HotspotRules` (pure, mcs-testable):
  - `CanHarvest(remaining)` → `remaining != 0` (`-1` unlimited and any positive count can harvest).
  - `NextCharges(remaining)` → unlimited `-1` stays `-1`; otherwise `max(0, remaining - 1)`.

### Harvest trigger
In `TurnPhaseController.EndTurnPressed`, **before** `endTheTurn.Raise()` resets the pools: if the
player's current cell is a live hotspot (`HotspotRules.CanHarvest`), grant 1 crystal of its `color`,
apply `HotspotRules.NextCharges`, and refresh the token. **No modal, no `RewardQueue`, no
ValidationMessage** — the crystal-count HUD updating and the token's charge pip are the durable
confirmation. This harvest is one of the events the future Map Event Log will collect (deferred).

---

## Section 1b — HexTooltip enrichment (shared, in-scope)

Today `HexInteractor.TooltipText` returns only a move/scout Explore-cost line and returns `null`
whenever a place/enemy occupies the cell (those were meant to "show their own preview"). As tiles
multiply, that null is a gap: the tooltip should communicate *what is on the hex*.

- A pure `TileDescriptor` helper (mcs-testable) builds a short **icon-marked** line from an occupant's
  state:
  - **Town / Keep / Castle** → `<type-icon> Name` (+ conquered/guarded state).
  - **Dungeon** → `<dungeon> Name` (+ progress).
  - **Crystal Hotspot** → `<crystal(color)> ×N left` (or "depleted"; `∞` for unlimited).
  - **Shrine** → `<gem>×4 — gamble` (or its consumed / guarding state).
- `HexInteractor.TooltipText` composes the move-cost line **and** the occupant line; the occupant line
  takes priority where the method currently returns null.
- Scope: wire the two new tiles fully and add the town/keep/castle/dungeon descriptors (same helper).
  **Do not** redesign `EnemyPreviewPanel` — enemies keep their existing hover preview.

## Section 1c — Extensible occupant model

An **`IHexOccupant`** interface every occupying token implements, so the tooltip is plug-in rather than
a growing switch:

```csharp
public interface IHexOccupant
{
    Vector3Int Cell { get; }
    HexDescriptor Describe();   // pure data: an icon-marked line + a priority; no UI
}
```

- `TownToken`, `DungeonToken`, `CrystalHotspotToken`, and `ShrineToken` implement it.
- Lookup is O(1) by cell via a lightweight `HexOccupantRegistry` that tokens register into on `Start`
  (the `DungeonTracker.Register` pattern) — no per-frame `FindObjectsByType` scan. `PlaceOccupies`
  (currently a hardcoded `TownToken` + `DungeonToken` scan) routes through the registry too, so
  occupancy/move-blocking picks up new tiles automatically.
- The descriptor **strings** are built by `TileDescriptor` from token state (pure/testable); the
  tooltip renders whatever `Describe()` returns.
- **A future tile type integrates by implementing `IHexOccupant` and registering** — tooltip,
  occupancy checks, and move-blocking all pick it up with no edits to `HexInteractor`.

---

## Section 2 — Shrine

### Content — `ShrineSO : AllCards`
**Menu:** `ScriptableObjects/Shrine`

| Field | Type | Notes |
|-------|------|-------|
| `id` | string | Stable slug; save identity. Never rename. |
| `crystalCost` | int | Crystals to engage (default **4**). |
| `goodRollChance` | float | Coin for the safe result (default **0.5**). |
| `rewardTypes` | List&lt;`ShrineReward`&gt; | The pool it rolls the reward *type* from. |
| `unitPool` | List&lt;`UnitsSO`&gt; | Candidates when the rolled type is `Unit`. |
| `largeExp` | int | The "large exp" payout amount (the 1× value; fight pays 2×). |
| `cardTier` | int | Tier for the card-pick reward (reuses the `Rewards` card pools). |
| `summonedEnemy` | `EnemiesSO` | The tier-3 guardian on the bad roll. |

`ShrineReward` enum (append-only): `CardPick = 0, Unit = 1, LargeExp = 2`. **No skills** (skills stay a
level-up-only channel).

### Map side (mirrors Dungeon/Town — click-to-interact, stand-on-cell)
- `ShrineRuleTile : HexRuleTile` (empty subclass).
- `ShrineToken : MonoBehaviour, IHexOccupant, IPointerClickHandler`. `OnPointerClick` mirrors
  `DungeonToken`: fog/teleport guards; if adjacent instead of standing on it, dispatch a Move; if
  standing on it, open `ShrinePanel`. Opening is a **free peek** (`BeginVisit`); the action is spent
  only when the engage is confirmed. Visual states: **live**, **consumed-dormant**, **guarding** (a
  summoned enemy is loose from this shrine).

### Engage UI — `ShrinePanel` + over-head crystal buttons
- The panel shows the shrine, the `<gem>×4` cost (via `IconMarkup`), and the gamble hint. Confirm
  starts the **crystal-placement flow**: 4 color buttons float over the player's head; the player
  clicks one at a time, each placing 1 crystal of that color. Colors the player can't afford are
  disabled (`UiLock` treatment). Four slots fill in sequence.
- Each placement is **undoable** (Command pattern — reserve/spend, like card empower) until the 4th
  placement commits. The 4th placement: calls `CommitVisitAction()` (spends the turn's one Action),
  consumes the 4 crystals, and runs `ShrineRules.Resolve`.

### Resolution — `ShrineRules` (pure, mcs-testable)
- Roll a reward **type** from `rewardTypes`; roll the coin against `goodRollChance`.
- Returns a plain result struct `{ bool good, ShrineReward type }`. All Unity side-effects live in the
  tracker/panel, not the rule.
- **Good** → grant **1×** via `RewardQueue` (reuse `Rewards`: card pick at `cardTier` / unit from
  `unitPool` / `largeExp`); shrine → consumed-dormant.
- **Bad** → grant nothing now; spawn the summoned guardian (Section 3); shrine → guarding.
- TDD covers: reward-type roll over `rewardTypes`, the coin gate at `goodRollChance`, and the 1×/2×
  multiplier math (`ShrineRules.Payout(type, good)`).

---

## Section 3 — Summoned guardian + reward binding

On the bad roll the shrine spawns its `summonedEnemy` as a **mid-run `EnemyToken`**, reusing the
existing `isMidRunSpawn` path (schema v4) and Doom-scaling (`bonusHP` / `bonusAttack`), placed on the
shrine's own cell (or the nearest free adjacent cell if occupied). It aggros, previews, and fights via
`CombatController.OpenFight` exactly like any field enemy — flee = 1 wound, return later, dead-across-
reload — all for free from the existing field-combat machinery.

**The one new wire: a pending reward tag.** The summoned token carries a
`PendingShrineReward { ShrineReward type, int multiplier = 2, Vector3Int shrineCell }`. Field enemies
normally pay the tier reward table on defeat; a shrine guardian instead pays **2× the tagged reward +
its own defeat exp, and nothing else** (no crystal/card tier rolls). `CombatController`'s field-defeat
teardown checks for the tag: if present, route to `Rewards.GrantShrineReward(type, multiplier)` + the
exp roll, and mark the shrine (`shrineCell`) **consumed-dormant** (the guarding token is gone). This
keeps the special case to a single branch on an existing seam, not a parallel combat path.

---

## Section 4 — Save, placement, testing

### Save schema bump (v7)
- `hotspots[]` — `{ cell, id, remainingCharges }` (via `HotspotTracker.Export` / `ApplySave`, the
  `DungeonState` pattern; `-1` persists as unlimited).
- `shrines[]` — `{ cell, id, state }` where `state ∈ { Live, ConsumedDormant, Guarding }`.
- The summoned guardian already saves via the mid-run-spawn mechanism; **add** its `PendingShrineReward`
  to the mid-run spawn record so a bad-roll guardian and its owed reward survive quit/resume.
- Restore validates cell/id against the regenerated map (the `DungeonTracker.ApplySave` warn-and-skip
  pattern).

### Placement
- New `CrystalHotspotRuleTile` / `ShrineRuleTile` seeded in `GridGeneration`, reusing the dungeon
  spacing/blocking rules (spaced, never on towns or the start safe ring; min-spacing Chebyshev).
- Counts are tuning knobs: `hotspotCount` / `shrineCount` on the generation config with placeholder
  defaults (values TBD in playtest).
- Hotspot **color** assignment: random per tile, roughly even across the four colors.

### Testing (mcs / EditMode — the pure-test harness)
- `HotspotRulesTests` — `CanHarvest`; charge decrement; `-1` unlimited stays `-1`; floor at 0.
- `ShrineRulesTests` — reward-type roll over `rewardTypes`; coin gate at `goodRollChance`; 1×/2×
  `Payout` math.
- `TileDescriptorTests` — each occupant produces the right icon-marked line, incl. hotspot charge
  states (N / depleted / ∞) and shrine live / consumed / guarding.
- Pure rules take an injected `rng` (the `DungeonRules.PickFlagTargets` pattern) for determinism.

### Content-authoring contract additions (`content-rules.md`)
- `CrystalHotspotSO`, `ShrineSO`, the `ShrineReward` enum, and the `IHexOccupant` note.

---

## Deferred / out of scope

- **Map Event Log** ("log gatherer") — a menu to review map events (harvests, spawns, clears, doom
  flags) instead of transient popups. Not designed here; hotspot/shrine events are structured so they
  can feed it later. See memory `map-feedback-tooltip-and-log`.
- Per-tile-color reward theming, shrine reward *previews* before commit, and hotspot cooldown/re-arm
  variants — all future tuning, not this pass.

## Roadmap placement

New content system; not on the critical path to a winnable loop (M2.5 win/lose still pending). Slots as
a post-M2.14 content milestone (proposed **M2.15 — Crystal Hotspots & Shrines**) or a "Later — content
expansion" item, at the user's discretion when sequencing.
