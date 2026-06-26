# M1 — Run-Based Save/Load — Design

_Status: approved design, pre-implementation. Milestone M1 in `archons-rise-roadmap`.
Completes deferred code-review Critical #3 (`docs/code-review.md`)._

## Goal

Persist a run in progress so the player can quit and resume with deck, map, crystals, and the
run counters intact.

**Acceptance:** quit mid-run — including after exploring/moving partway through a turn — and resume
with deck, hand, discard, map, recruited units, crystals, position, and round/turn intact.

## Context (current state)

- `PlayerData` serializes only 7 scalar player stats + position. The deck/hand/discard, crystals,
  units, and world are not persisted.
- The four action stats (Attack/Defend/Influence/Explore) **reset every turn** (`Player.TurnEnd`).
  They are a per-turn budget, not durable run identity.
- **Cards** have no stable id. `CardsSO : AllCards` exposes only `cardName` + `cardDescription`, and
  `DataManager.allCards` is referenced by **array position** (`PlayerDeck.AddRandomCard`, reward
  generation) — fragile to reordering.
- **Card zones are three:** deck (`PlayerDeck.CardsInDeck`), hand (`PlayerHand.cardsInPlay`), and
  **discard** (`DiscardPile`). A played card is routed to discard during the turn
  (`Card.PlayedCardDiscard` → `onTurnEnd_CleanUpPlayedCard` → `DiscardPile.AddCardToDiscard`, which
  sets `InDiscard` and deactivates it). Discard reshuffles into the deck only at **round end**
  (`PlayerDeck.EndOfRoundReshuffle` / `DiscardPile.ReshuffleToDeck`). A player who ends a turn has
  cards sitting in discard, so discard must be saved.
- **Crystals** are GameObjects tagged with an `EmpowerType` color, held in
  `CrystalInventory.crystalsInInventory`. Persistable as a count per color. Remove the ability to destroy crystals on click.
- **Map** (`GridGeneration.Start`) regenerates from scratch on every scene load and is **fully
  unseeded** — it calls `UnityEngine.Random` directly for terrain, town placement + type, and enemy
  placement + type. Tokens have no stable id. Defeated enemies are `Destroy`'d
  (`EnemyToken.Update` on `cardRef.IsDefeated`).

## Design decisions (with rationale)

1. **Map fidelity = seed + deltas.** Persist a run seed so the board re-rolls identically, plus a
   small delta for changes since generation. _Why:_ faithful resume without snapshotting every tile;
   far cheaper than full board enumeration.
2. **Towns need no per-token delta in M1.** Recruiting only appends to the persisted unit list and
   mutates player resources; the town token itself has no durable mutated state, so the seed fully
   reproduces it. _Only defeated enemies need a delta_ (they are destroyed and must not respawn).
   Town/capture state is reserved for when town capture lands (later milestone) via a schema bump.
3. **Save timing = settled sub-states, which include mid-turn.** A snapshot is valid whenever the
   board is settled: exploration/map view active, no modal canvas open (combat/town/reward/
   card-list), command/undo stack empty, no card mid-toggle. This occurs at turn start **and after a
   completed exploration/movement** — so movement progress within a turn survives.
   _Why:_ the player asked that movement (which reveals new board state and affects the next
   exploration) not be rewound to turn start; settling after each exploration captures it.
4. **Snapshot captures the current settled state, not an idealized turn start.** It records current
   position + current action-stat budget + resources + all three card zones. It does **not** capture
   the undo stack or per-card `IsPlayed`/`IsEmpowered` flags (reset to false on load). _Why:_ the
   seed makes recruitment/combat replay equivalently, and the transient toggle/undo state is not
   worth the serialization complexity.
5. **Content identity = explicit string id.** Add a serialized `string id` to the content base
   (`AllCards`, and the unit content type), authored once per asset (`"card_strike"`,
   `"unit_knight"`). _Why:_ human-readable saves, stable across renames/reorders, replaces fragile
   index-based lookup.
6. **Isolated seeded RNG for map generation** (see "Map determinism" below). _Why:_ guarantees
   "same seed ⇒ same board" regardless of any other `Random` consumer.
7. **Single save slot, `schemaVersion = 1`, no migration logic.** Nothing is released; the version
   field is kept for forward-compat but no upgrade path is coded yet.

## Data model (the schema)

Serialized to JSON via Unity `JsonUtility`. All `[Serializable]` plain classes — **no MonoBehaviours
and no dictionaries** (`JsonUtility` supports neither). Lists of `[Serializable]` classes and
primitive arrays are fine.

```
SaveFile
 ├ int schemaVersion              // = 1 for M1
 └ RunState run
     ├ PlayerState player
     │   ├ int hp
     │   ├ int handSize
     │   ├ int level
     │   ├ int exp
     │   ├ int expToNextLevel
     │   ├ int attack, defend, influence, explore   // current per-turn budget at the settle point
     │   └ float[3] position
     ├ int[] crystalCounts        // indexed by EmpowerType order (Green/Purple/Red/Yellow/All)
     ├ string[] deckCardIds       // order preserved = draw order (NOT reshuffled on load)
     ├ string[] handCardIds       // wounds are just cards carrying the wound id
     ├ string[] discardCardIds    // cards played/ended this round, awaiting round-end reshuffle
     ├ string[] unitIds           // recruited units
     ├ MapState map
     │   ├ int seed
     │   └ Cell[] defeatedEnemies // each {int x, int y} grid cell
     └ int round, turn            // round persisted now; a future Doom Clock derives from it
```

`Cell` is a small `[Serializable]` struct/class `{ int x; int y; }`.

Notes:
- **Crystals** persist as a count per color, not as GameObjects. `crystalCounts` is aligned to the
  `EmpowerType` enum order; document the alignment at the field.
- **No town delta** in M1 (decision 2).

## Content id system + registry

- Add `public string id;` to `AllCards` (covers all card types) and to the unit content base/SO.
- `DataManager` builds an `id → SO` dictionary from `allCards` (and an analogous one for
  `allUnits`) at startup.
- Route all content lookups through the registry instead of array index: reward generation,
  `PlayerDeck.AddRandomCard`, and the deck/hand/discard rebuild on load.
- One-time authoring pass to stamp ids on existing assets. A load-time guard logs an error if any
  referenced id is missing, or if duplicate ids exist in the registry.

## Map: seed + deltas

### Determinism — why an isolated RNG

A PRNG is a deterministic state machine: it holds state `S`, outputs `r = g(S)`, then advances
`S ← f(S)` with fixed integer math. `Random.InitState(seed)` sets `S₀ = seed`, which fixes the
entire output sequence `r₁, r₂, …` for that seed. A seeded map is just a function of that fixed
sequence.

Reproduction therefore needs **two** conditions on replay: (1) the same seed, and (2) the same draws
in the same order and count. Condition (2) is the risk, because `UnityEngine.Random` is **one global
stream shared by the whole program** — any other `Random.Range` (e.g. `CrystalInventory.OnPointerClick`)
drawn before or during generation shifts every subsequent draw and changes the board even when the
seed matches.

**Mitigation (removes the risk):** map generation uses its **own isolated generator** seeded with the
run seed — either a dedicated `System.Random` instance, or snapshot/restore `Random.state` around the
generation pass. Every generation-time draw (terrain, town placement + type, enemy placement + type)
goes through that one instance in a fixed order. The global gameplay stream is then independent and
cannot perturb generation, so "same seed ⇒ same board" is a guarantee.

### Seed lifecycle

- The run seed is generated at **new-run start** and held on the `DontDestroyOnLoad` `DataManager`,
  so it is available **before** `GridGeneration.Start` runs.
- `GridGeneration` reads the seed from `DataManager` and generates terrain → towns → enemies through
  the isolated generator. Token identity is its **grid cell** (deterministic from the seed) — no new
  id field is needed on tokens.
- On resume, the seed comes from the save instead of being freshly rolled.

### Deltas

- `defeatedEnemies` is a run-level set of grid cells, recorded when an enemy is defeated.
- On resume: re-roll the board from the saved seed, then remove tokens whose cell is in
  `defeatedEnemies`.

## Save / restore flow

### Settle point

Defined as: exploration/map view active, no modal canvas open (combat / town / reward / card-list),
command/undo stack empty, no card in a mid-played toggle. A small `IsSettledState()` helper
centralizes this check. Settle points occur at turn start and after each completed
exploration/movement.

### Save triggers

- **Explicit Save action** from the map view. Fix the `SaveButton` prefab, which currently wrongly
  invokes `LoadGame`.
- **Autosave on `OnApplicationQuit`** only if currently at a settle point; otherwise retain the last
  good snapshot. (`Player.OnApplicationQuit` already guards on `DataManager.IsLoading`.)

### Capture

Build `RunState` from live objects at the settle point: player stats/HP/level/exp, current
action-stat budget, position, crystal counts by color, the three card-zone id lists (in order),
recruited unit ids, the run seed, the defeated-enemy cell set, and round/turn.

### Restore

1. `LoadGame` deserializes JSON → `SaveFile`, sets `DataManager.IsLoading = true`, loads scene 1.
2. On `sceneLoaded`: `GridGeneration` reads the **saved seed** and generates the board through the
   isolated generator.
3. Apply enemy deltas (remove/skip defeated cells).
4. Restore player stats/HP/level/exp/budget, position, crystal counts, round/turn.
5. **Rebuild deck + hand + discard from the id lists in saved order**, setting each card's
   `InDeck`/`InHand`/`InDiscard` flag. Deck order is preserved (no reshuffle).
6. Clear `IsLoading`.

`PlayerDeck.Awake` and `PlayerHand.Start` are **guarded by `IsLoading`** so that on a load they skip
the default starting-deck build/shuffle and the default starting-hand draw, deferring to the rebuild
above. This guarded-load path is the most invasive integration point.

## Scope / non-goals (YAGNI for M1)

- Multiple save slots.
- Exact mid-turn capture of played-card toggles and the undo stack.
- Full board (per-tile) enumeration.
- Encryption / compression of the save file.
- The actual Doom Clock mechanic and win/lose conditions (milestone M2) — only the `round` input is
  persisted now.
- Schema migration logic (nothing released; `schemaVersion` field kept for forward-compat only).

## Testing

- **EditMode tests** (add a minimal test assembly — also closes the "no test asmdef" code-review gap)
  for the pure-data pieces:
  - `RunState` JSON round-trip equality (serialize → deserialize → equal).
  - id-registry lookup (id → SO, missing-id and duplicate-id guards).
  - deck/hand/discard rebuild-from-ids preserves order and sets zone flags.
  - enemy-delta application (a defeated cell is excluded after regeneration).
- **Manual / PlayMode** against the acceptance criterion: explore partway through a turn, quit, and
  resume; verify deck, hand, discard, map (including a previously defeated enemy staying dead),
  crystals, recruited units, position, and round/turn are intact.

## Affected code (orientation, not exhaustive)

- `Assets/Scripts/SaveSystem/PlayerData.cs` → replaced/expanded into the `SaveFile`/`RunState` DTOs.
- `Assets/Scripts/Managers/DataManager.cs` — seed lifecycle, id registry, save/restore orchestration.
- `Assets/Scripts/GameScriptableObjectTypes/AllCards.cs` (+ unit SO) — add `id`.
- `Assets/Scripts/TilemapScripts/GridGeneration.cs` — isolated seeded generation; read seed.
- `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs`,
  `.../PlayerScripts/PlayerHand.cs`, `.../PlayerScripts/DiscardPile.cs` — `IsLoading`-guarded build,
  rebuild-from-ids.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs` — crystal counts by color.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs` — record defeated cell.
- `SaveButton.prefab` — rewire to `SaveGame`.
