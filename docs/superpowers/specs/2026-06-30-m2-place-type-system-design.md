# M2 — Place-Type System

**Date:** 2026-06-30
**Milestone:** M2 (retargeted from "Win/lose systems" to "Place-type system"; win/lose becomes M2.5)
**Status:** Design approved — ready for implementation plan.

## Summary

Replace the single homogeneous "town" with a typed taxonomy of map places — **Town / Keep /
Castle** (Dungeons already exist as their own type) — each with different services and guardian
counts. Add a **resumable guardian-conquest mechanic**: guarded places must have all their guardians
defeated to be conquered, defeated guardians never respawn, and a failed assault costs the player 3
wounds. Conquest state persists across save/load. Services (recruit / heal / cards) are gated by
place type and unlock only after conquest.

This milestone builds the **taxonomy + conquest + gating**. It deliberately does **not** build the
win/lose systems — those (doom clock, wound-out, "conquer 2 Castles" victory, game-over screen)
become **M2.5**, layered on top of the conquered-Castle count this milestone exposes.

## Motivation

The original M2 win condition was "control 3 towns AND reach Level 8", but no town-control mechanic
existed and every town was an identical service zone. The revised design makes territory meaningful:
places differ in what they offer and how hard they are to take, and the run is won (in M2.5) by
conquering the apex places — **Castles**. Conquest via guardians ties territory to the existing
combat system rather than a bespoke control flag.

## Design Decisions (locked)

- **Scope split.** M2 = place-type system + conquest + gating. M2.5 = doom clock, wound-out,
  victory (conquer 2 Castles), and the game-over screen.
- **Victory (M2.5, recorded here for context).** Conquer **2 Castles**. No Level/Influence power
  gate — territory is the sole win axis.
- **Guardian counts are data-driven, not hardcoded.** Each place carries a guardian roster (a list);
  the assault reads `roster.Count`. Starting counts: Town 0, Keep 1, Castle 2, Dungeon 2 (existing).
  A future difficulty scaler can populate/grow the roster without touching assault logic.
- **Conquest is resumable.** Guardians are fought in order; defeated guardians persist (no respawn).
  All must fall to conquer. Retreating with guardians remaining costs **3 wounds** (vs. the 1-wound
  field-combat flee) and preserves progress.
- **Services gate by type and by conquest.** Town (no guardians) opens immediately; Keep/Castle open
  only after conquest.
- **`TownsSO` keeps its class name**; a `placeType` identifier inside the data object carries the
  type. Renaming to `PlaceSO` is deferred as negligible naming debt.
- **Dungeons are untouched** — conceptually the 4th place type, but already implemented and not
  conquerable-for-win.
- **Card shop + paid healing deferred.** M2 wires the Cards button as a present-but-stubbed control
  and leaves healing free; the purchase economics are a focused follow-up.

## Place Taxonomy

`enum PlaceType { Town, Keep, Castle }` (new).

Allowed services derive from `PlaceType` via a pure static map — designers cannot author invalid
combos. Per-place data (recruit pool, heal level) is still authored on the asset.

| Type   | Guardians (start) | Recruit | Heal | Cards        |
|--------|-------------------|---------|------|--------------|
| Town   | 0                 | ✓       | ✓    | —            |
| Keep   | 1                 | ✓       | —    | —            |
| Castle | 2                 | ✓       | ✓    | ✓ (stubbed)  |

Dungeon (existing `DungeonsSO`): 2 enemies, no services, not conquerable-for-win.

## Components

### `TownsSO` (extended)
Add:
- `placeType : PlaceType`
- `guardians : List<EnemiesSO>` — the conquest roster (empty for Town).

The legacy `TownActivity activity` flags field is superseded by type-derived services; leave it in
place if other code reads it, but availability is computed from `placeType`, not authored flags.

### `PlaceRules` (new, plain C#, testable)
Pure functions holding the rules, no `MonoBehaviour`:
- `AllowedActivities(PlaceType) -> TownActivity` (or an equivalent service set) — the table above.
- `IsConquered(defeatedCount, rosterSize) -> bool` — `defeatedCount >= rosterSize`.
- `RetreatWoundCount` constant = 3.
Thresholds/constants live here so balance is centralized.

### `GuardianAssault` (new `MonoBehaviour`)
Drives a resumable assault on one guarded place. Modeled on `Dungeon.SpawnDungeonEnemy`'s sequential
spawn but with conquest semantics; kept separate so dungeon behavior is untouched.
- Spawns `guardians[defeatedCount]` into the existing `EnemyCard` / combat flow.
- Defeating a guardian grants its `defeatRewards` (existing reward path via
  `OnEnemyDefeat_GetRewards`) and increments `defeatedCount`.
- **Retreat** (a menu/combat action) with guardians remaining → `PlayerHand.AddWound()` ×
  `PlaceRules.RetreatWoundCount`, tear down combat, preserve `defeatedCount`.
- On `defeatedCount == roster.Count` → mark conquered; unlock services.

### `TownToken` (extended) + place menu
- `TownToken` gains a `gridPos : Cell` (mirroring `EnemyToken`) for stable identity over the seeded
  map.
- The place menu, on open, asks a conquest registry whether this place (by `gridPos`) is conquered:
  - **Unconquered guarded place** → show **Assault** + **Retreat** only.
  - **Conquered place (or a Town)** → show the type's services (Recruit / Heal / Cards-stub),
    disabling any not allowed by `PlaceType`.

### `ConquestTracker` (new `MonoBehaviour` singleton)
A dedicated component (not bolted onto `GameManager`, keeping that class from growing further).
Runtime registry mapping place `Cell -> defeatedCount`. Provides:
- `DefeatedCount(Cell)`, `RecordDefeat(Cell)`, `IsConquered(Cell, rosterSize)`.
- `ConqueredCastleCount()` — consumed by M2.5's victory check.
Backed by the same reproducible-over-seed pattern as `DataManager.DefeatedEnemies`.

## Data Flow

1. Player clicks a place token → menu opens → registry consulted by `gridPos`.
2. Guarded + unconquered → **Assault**: spawn next guardian → combat → on defeat, reward +
   `RecordDefeat(cell)`; loop until roster exhausted (conquered) or player **Retreats** (3 wounds,
   progress kept).
3. Conquered (or Town) → services shown per `PlaceRules.AllowedActivities`; Recruit spends Influence
   (existing), Heal calls `TownHeal` (existing), Cards is stubbed.
4. Conquering a Castle increments the tracker's conquered-Castle count (read by M2.5).

## Persistence (extends M1)

`RunState` gains:
```
PlaceConquest[] places;   // { int x; int y; int defeatedCount; }
```
- `CaptureRunState` writes one entry per place with `defeatedCount > 0`.
- `RestoreNow` re-applies `defeatedCount` to regenerated place tokens matched by `gridPos`.
- **Schema bump to v2** with a v1→v2 default-fill: absent `places` ⇒ nothing conquered.

Guardians are beaten in order and never respawn, so a single `defeatedCount` per place fully captures
state (the next guardian fought is always `guardians[defeatedCount]`; conquered ⇔ count == roster
size).

## Testing

EditMode tests on the pure logic (alongside existing `SaveSerializerTests`, `MapDeltaTests`):
- `PlaceRules.AllowedActivities` returns the correct service set for each `PlaceType`.
- Conquest progression: `defeatedCount` advances; `IsConquered` false at `count < size`, true at
  `count == size`; retreat leaves `defeatedCount` unchanged.
- `ConqueredCastleCount` counts only conquered Castles.
- Save round-trip: a partially-assaulted place serializes and restores its `defeatedCount` (extend
  `SaveSerializerTests`).

Assault spawning, menu gating, and retreat wounds are verified manually in-scene.

## Out of Scope (this milestone)

- Doom clock, wound-out loss, victory check, game-over screen (**M2.5**).
- Castle card-purchase shop and paid-healing economics (focused follow-up).
- HP-to-0 loss (no HP-damage source exists yet).
- Difficulty scaling of guardian rosters (the data-driven seam is built; the scaler is later).
- Renaming `TownsSO` → `PlaceSO`.

## Design-Bible & Roadmap Updates (part of this work)

Because this changes the canonical win condition and content model, the implementation plan must also:
- Update `archons-rise-design/mechanics.md` (win = conquer Castles; place types; guardian conquest).
- Update `archons-rise-design/balance.md` (guardian counts, retreat-wound penalty, victory = 2
  Castles).
- Update `archons-rise-design/content-rules.md` (`TownsSO` gains `placeType` + `guardians`; service
  availability by type).
- Update `archons-rise-roadmap/milestones.md` and `status.md` (M2 retargeted; insert M2.5).
- Append the decisions above to `archons-rise-roadmap/decisions-log.md`.
