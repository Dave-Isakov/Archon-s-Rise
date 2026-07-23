# Hex-Selection Exploration — Design

**Date:** 2026-07-23
**Status:** Approved design, pending implementation plan
**Supersedes:** the six-`DirectionButton` arrow movement placeholder

## Goal

Replace the placeholder six-arrow movement UI with a click-and-controller-friendly
model where **hexes themselves are the targets**. The player points at a hex and the
game gives contextual feedback: move onto adjacent revealed ground, scout adjacent
fog, read a distant hex's cost, or (via a card) teleport to any visible hex. All
movement is gated by a single axis — **Explore cost** — with no impassable terrain.

## Non-Goals

- **No pathfinding.** Feedback is per-hex, never a multi-step path total. Distant
  hexes report only their own entry cost.
- **No full controller navigation this pass.** The architecture leaves a clean
  pointer-source seam; the controller cursor is a later, small follow-up.

## Design Decisions (locked during brainstorming)

1. **Affordable adjacent move = single click, undoable** (as today via `MoveCommand`).
2. **Fog scouting reveals new, un-undoable knowledge → takes a confirming click**
   (arm on first click, confirm on second click of the same fog).
3. **Distant (non-adjacent) hover shows that hex's own entry cost only** — informational,
   not actionable. No path total (honest with no pathfinding).
4. **Teleport is built fully this pass** (for gameplay testing of the movement loop):
   a card puts the map into a targeting mode; any *visible* terrain hex becomes a valid
   target. Teleport is **Explore-phase movement**; landing adjacent to an enemy arms
   combat, and the fight is the turn's Action.
5. **No impassable terrain** (changed mid-brainstorm). Everything is enterable, gated
   only by Explore cost. **Mountain cost = 4, Water cost = 5.** The only non-target is a
   cell off the generated map.
6. **Mouse-first with a controller-ready seam** (`IHexPointerSource`).

## Architecture — Approach A (chosen)

A single interactor owns "the current cell," gets it from a swappable pointer source,
asks a pure rules function what that cell *means*, and drives highlight + tooltip +
dispatch. Movement/scout execution moves out of `DirectionButton` into a controller.

### Components

| Unit | Type | Responsibility | Depends on |
|---|---|---|---|
| `IHexPointerSource` | interface | Reports the cell the player points at + whether confirm was pressed this frame. **Mouse impl now**; controller-cursor impl later. | `Grid`, camera / input |
| `HexActionRules` | pure static class | Given cell facts + explore pool + mode, returns a `HexAction` verdict. **No Unity types** → EditMode-testable. | nothing |
| `HexInteractor` | MonoBehaviour (one in scene) | Each frame: read pointer source → look up terrain/fog/occupancy from tilemaps → call `HexActionRules` → drive highlight + tooltip → on confirm, dispatch to `ExplorationController`. Owns `teleportMode`. | pointer source, tilemaps, rules, `ExplorationController` |
| `ExplorationController` | MonoBehaviour | Executes the action: undoable move (`MoveCommand`), fog-scout (spend + reveal + commit), teleport (reposition + raise events). Owns the explore pool; exposes `Map`/`Fog`; hosts `ApplyMove`. | `MoveCommand`, `PlayerPosition`, existing events |

### Data lookups `HexInteractor` gathers before calling the (pure) rules

- **Terrain target?** any terrain tilemap has a tile — `ground || water || mountains`.
  The only non-target is a cell off the generated map (no tile anywhere).
- **Entry cost** — from whichever terrain tilemap holds the tile (`HexRuleTile.exploreCost`):
  plains/forest/desert from `ground`, **water = 5, mountain = 4**. A small `TerrainAt(cell)`
  helper returns `(hasTile, cost)` so the rules stay pure.
- **Fog?** — `MapFog.IsHidden(cell)`.
- **Enemy on cell?** — the existing visible-enemy scan (`EnemyToken`; fog-hidden enemies
  don't count).
- **Place on cell?** — town/dungeon token lookup (place cells are ordinary move targets;
  entering by standing on the cell is existing on-arrival token behavior).

`HexInteractor` needs references to **all three terrain tilemaps + fog**, not just `ground`.

## Decision model — `HexActionRules`

Pure function; inputs are primitives the interactor gathers.

**Inputs:** `isSameCell`, `hasTerrain`, `entryCost`, `isAdjacent`, `isFog`,
`enemyOnCell`, `explorePool`, `fogCost`, `teleportMode`.

**Verdict:**
```
HexAction {
  Kind             // enum below
  Cost             // explore cost relevant to this action
  Affordable       // explorePool >= Cost
  RequiresConfirm  // true for irreversible actions (fog scout)
}
```
Text is **not** built here — the tooltip maps `Kind`/`Cost`/`Affordable` to a string,
so the rules stay string-free and easy to assert.

**`HexActionKind`:** `None, OffMap, DistantInfo, DistantFog, Move, ScoutFog, EnemyFight, TeleportTarget`

**Precedence — normal mode (first match wins):**
1. `isSameCell` → **None** (you're here; no feedback).
2. `!hasTerrain` → **OffMap** (off the generated map; inert).
3. `!isAdjacent` → **DistantFog** if `isFog` (tooltip "Unexplored"), else **DistantInfo**
   (that hex's entry cost only — informational).
4. `isAdjacent && enemyOnCell` → **EnemyFight** — the interactor does **not** consume
   this click; `EnemyToken`'s own click + preview handle the fight, unchanged.
5. `isAdjacent && isFog` → **ScoutFog** — `Cost = fogCost` (flat 2, as today),
   `RequiresConfirm = true`. Tooltip differs by `Affordable`.
6. `isAdjacent && hasTerrain` → **Move** — `Cost = entryCost`, single click when
   `Affordable` (undoable `MoveCommand`); otherwise the click is refused with a message.

**Teleport-mode override:** a cell is a valid **TeleportTarget** iff
`hasTerrain && !isFog && !enemyOnCell && !isSameCell` (any visible terrain hex; enemies
still block; fog never targetable). Everything else → **None**.

**Cost model:**
- Move onto revealed terrain = terrain `entryCost` (water 5, mountain 4). Undoable.
- Scout adjacent fog = flat `fogCost` (2). Irreversible, confirm click.
- Teleport = **terrain cost waived** (the card is the cost).

**Phase gate stays at dispatch, not in the pure rules.** `ExplorationController` checks
`TurnPhaseController.CanMove` (Explore phase) before executing any move/scout/teleport,
reusing today's "You can only move during the Explore phase." message. The rules describe
*what a cell is*; the controller decides *whether you may act now*.

## Feedback UI

**Highlight.** A dedicated **highlight Tilemap** (overlay above terrain, below tokens)
stamps a hex-shaped tile on the pointed cell, tinted by verdict: affordable Move → green,
unaffordable Move → red, ScoutFog → blue, TeleportTarget → purple, DistantInfo → neutral/none.
**Additionally**, a subtle persistent tint on the **affordable adjacent ring** keeps
reachability glanceable without hovering each hex (the one thing the arrows did well).

**Tooltip.** A small screen-space panel anchored near the pointed cell (so a controller
cursor can reuse the anchor later). Uses the existing **explore/scroll icon** via
`IconMarkup`, consistent with card text.

| Kind | Tooltip |
|---|---|
| Move (affordable) | "Move here — 🗞 X" |
| Move (unaffordable) | "Need 🗞 X to move here" |
| ScoutFog (affordable) | "Scout this fog — 🗞 2" |
| ScoutFog (unaffordable) | "Need 🗞 2 to scout this fog" |
| DistantInfo | "‹Terrain› — 🗞 X" (info only) |
| DistantFog | "Unexplored" |
| TeleportTarget | "Teleport here" |
| EnemyFight / None / OffMap | *(no tooltip — enemy token shows its own preview)* |

## End-to-end flows

- **Move (affordable):** hover → green + "Move here — X". Click → `ExplorationController`
  runs today's path: phase check → enemy-on-cell defense → **undoable `MoveCommand`**
  (reposition, spend explore, raise explore + `sendNewPositionOfPlayer` → aggro re-check).
- **Move (unaffordable):** click → existing "Need X to explore!" validation message; no change.
- **Scout fog (reveals in place — does not relocate the player):** the adjacent fog is
  lifted, then the player walks in next as a normal move (today's behavior). Because it's
  irreversible: **first click arms** ("Click again to scout"), **second click on the same
  fog confirms** → spend 2, run the existing fog-reveal algorithm, `ClearStack` (commit).
  The revealed hex then becomes a normal Move target.
- **Distant hover:** entry-cost tooltip only; a click is a no-op (too far to act on directly).
- **Enemy-occupied adjacent:** interactor ignores the click — `EnemyToken`'s own click +
  preview handle the fight, as now.
- **Teleport:** see below.

## Teleport card + targeting mode

**Card model.** Add `bool grantsTeleport` to `CardsSO`. (A `CardEffect { None, Teleport }`
enum is the more extensible alternative; the bool is chosen for now.) The card may still
carry normal stats; a pure teleport card carries only `grantsTeleport`.

**Trigger.** Playing the card (Explore phase) raises a new `onTeleportRequested` `VoidEvent`,
scene-wired to `HexInteractor.EnterTeleportMode()` — same event-asset pattern as
`OnPlay_SetExploreDataToPlayer`. This flips `teleportMode` on, so the decision model returns
`TeleportTarget` for every visible terrain hex.

**Targeting.**
- valid targets (purple, "Teleport here") = `hasTerrain && !isFog && !enemyOnCell && !isSameCell`
  — interior lakes/mountains are valid (stand for free), enemies still block, fog never targetable.
- a **Cancel** affordance (Esc / right-click / on-screen Cancel button) exits targeting.

**Confirm → `ExplorationController.Teleport(cell)`:** reposition the player, raise
`sendNewPositionOfPlayer` (→ every enemy re-runs `CheckAggro`) and the explore event.
**Terrain cost waived.** Exit teleport mode.

**Landing next to an enemy (explicit test case):** the position event runs `CheckAggro`,
which sets that enemy `isAggro = true` (arms it) — it does *not* auto-start the fight. The
now-aggro enemy is immediately attackable; **clicking it starts combat, which spends the
turn's Action** (`BeginAction`). Same code path as walking adjacent — behavior is identical
whether walked or teleported.

**Undo / cancel semantics:** teleport targets are visible-only, so nothing irreversible is
revealed → the reposition is **undoable** (no `ClearStack` commit). Canceling target
selection without picking **returns the card to hand** (undoes the play). The exact
command-object granularity (reposition riding the card's `PlayCommand` vs. a linked
`TeleportCommand`) is settled in the implementation plan against the existing play pipeline.
Contract: **pick = undoable teleport; cancel = card back in hand; no partial state.**

## Controller seam (no controller work this pass)

The mouse source reads pointer → `grid.WorldToCell` each frame, confirm = left click. A
future controller source moves a cursor cell-by-cell via the parity compass with a confirm
button, and flips `InputContextState` when Board owns navigation. `HexInteractor` consumes
whichever `IHexPointerSource` is active — that interface is the entire seam.

## Testing

- EditMode unit tests for `HexActionRules` covering the full precedence table: same-cell →
  `None`; off-map → `OffMap`; distant terrain → `DistantInfo` (+cost); distant fog →
  `DistantFog`; adjacent enemy → `EnemyFight`; adjacent fog affordable/unaffordable →
  `ScoutFog` + `Affordable` + `RequiresConfirm`; adjacent terrain affordable/unaffordable →
  `Move` + `Affordable`; teleport-mode valid/invalid → `TeleportTarget`/`None`.
- Per the established pure-class pattern: `HexActionRules` gets its **own folder asmdef**
  referenced by the tests asmdef (else EditMode CS0103); MonoBehaviours (`HexInteractor`,
  `ExplorationController`) stay in the main assembly. Verify via the **mcs harness** if the
  editor lock blocks batch-mode `runTests`.

## Retirement / re-homing (code)

- Delete `DirectionButton.cs` and the six arrow GameObjects.
- `ExplorationController` **absorbs** `ApplyMove`, the `Map`/`Fog` public tilemap exposure,
  the move/scout execution, and the explore pool.
- Re-point the four `DirectionButton` consumers to `ExplorationController`: `MoveCommand`
  ctor, `MapFog.Fog()`, `DataManager` (×2, fog save/load), `LateGameSaveTool`.
- **Kept & reused unchanged:** `MoveCommand`, `MapFog` (only its lookup target changes), the
  fog-reveal algorithm, and all explore/position event assets.

## Data edit

- `MountainRuleTile.exploreCost` 5 → 4 (Water already 5).

## Manual Unity steps (user does scene/asset work; no YAML edits from the assistant)

1. Create the **highlight Tilemap** (above terrain, below tokens) + a highlight tile asset.
2. Create the **tooltip UI** panel (screen-space, TMP + explore icon).
3. Add the **`HexInteractor` + `ExplorationController`** GameObject and wire refs (Grid;
   ground/water/mountains/fog + highlight tilemaps; tooltip; `PlayerPosition`; explore +
   `sendNewPositionOfPlayer` events).
4. Create the **`onTeleportRequested` VoidEvent** asset + a listener → `HexInteractor.EnterTeleportMode`;
   wire the teleport card's play to raise it.
5. Author a **teleport card** asset (`grantsTeleport = true`) into a test deck.
6. Remove the six arrow GameObjects.

## Tutorial impact

The movement rail step (`move.asset`, plus the new `pick-card.asset`) currently teaches the
arrows and highlights arrow buttons via `TutorialTarget`. Those steps need re-pointing to
the hex-click interaction. Exact rail changes are an item in the implementation plan; the
scene/asset authoring is user work.

## Open items deferred to the implementation plan

- Exact teleport command-object structure vs. the existing play pipeline.
- Precise highlight tile art / tint values and tooltip layout.
- Whether the affordable-ring tint is always-on or toggled.
