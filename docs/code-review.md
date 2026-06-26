# Archon's Rise — Code Review & Modernization Notes

_Reviewed against Unity 6000.5.1f1 (Unity 6.5), ~150 C# files / ~6.6k lines. Goal: revive an unfinished game on a sound foundation._

## TL;DR

Good architectural spine (ScriptableObject GameEvent bus + Command-pattern undo + SO-authored
data), with concentrated, fixable blockers. The save/load path and a listener-leak bug were the
highest-risk items and have been fixed (see "Fixed" below). The remaining Critical item — a real
save **schema** that captures deck/board/world state — is deferred to the game-design phase
because it depends on decisions about what game state is canonical.

## Strengths

- `BaseGameEvent<T>` + `BaseGameEventListener<T,E,UER>` — clean generic decoupled pub/sub; raising
  in reverse order tolerates listeners unregistering mid-dispatch.
- Command pattern for undo (`ICommands` / `PlayManager` / `PlayCommand` / `CardDrawCommand`) is
  small and single-purpose; reuses self-toggling `Player.PlayCard` for Execute/Undo.
- Content authored as ScriptableObjects (`CardsSO`, `UnitsSO`, `EnemiesSO`, `TownsSO`) — new
  content is editor data, not code.
- `[Flags] StatType` lets one card combine Attack+Crystal+Heal.
- Generic `Deck<T>.Shuffle` — correct Fisher-Yates, reused across deck types.

## Critical issues

| # | Issue | Status |
|---|-------|--------|
| 1 | `BaseGameEvent.UnRegisterListener` had an inverted condition (`if(!Contains) Remove`) → listeners never removed → cross-scene leaks, stale refs, double-fired effects | **Fixed** |
| 2 | `DataManager.LoadGame` read the stale `playerData` field instead of the deserialized `data`, and queried `Player`/`PlayerPosition` before the gameplay scene existed | **Fixed** (deserialize → `sceneLoaded` callback populates objects; `FindAnyObjectByType`) |
| 4 | `Player.OnDisable` autosaved on every disable/scene-unload/destroy → could overwrite a good save with default stats during a load | **Fixed** (moved to `OnApplicationQuit`, guarded by `DataManager.IsLoading`) |
| 3 | Save persists only 7 scalar stats + position — **no deck/hand/discard/crystals/world/enemy/town state**. For a deckbuilder, the deck *is* the save. | **Deferred → design phase** |

### #3 details (deferred)

`PlayerData` only serializes scalar player stats + position. The commented-out fields in
`PlayerData.cs` show an abandoned attempt to serialize `Card[]` (MonoBehaviours — not serializable
to JSON). Doing this right requires design decisions:

- **What is canonical state?** deck/hand/discard as card **ids** (stable string/GUID on each
  `CardsSO`) referencing `DataManager.allCards` — not `Card` components or array indices
  (`PlayerDeck.AddRandomCard` / reward generation currently index `allCards` by position, which is
  fragile).
- **World/map:** `GridGeneration.Start` re-rolls the map every load, so a restored position is
  meaningless without persisting the map seed + token/enemy/town states.
- Recommend: define a serializable snapshot (player stats + card-id lists + map seed + token
  states), make save **explicit** (a real Save action) plus `OnApplicationQuit`, version the schema.

This is the prerequisite for "continue an unfinished game" and should be specced alongside the
game-design document.

## Important issues (not yet addressed)

- Pervasive per-frame `Update()` doing event-driven work (`GameManager`, `Player`, `Card`,
  `PlayerDeck`, `PlayButton`, `ChoiceToggles`, `DirectionButton`). Drive from existing GameEvents.
- Heavy coupling via `GameObject.Find` / `FindObjectsOfType` (~17 uses, deprecated in Unity 6) and
  static reach-through (`GameManager.Instance.ValidationMessage(...)` from gameplay). Inject
  `[SerializeField]` refs / route UI through events.
- The `if(!card.IsPlayed){+} else if(card.IsPlayed){-}` apply/revert toggle is duplicated ~10× and
  uses `IsPlayed` as both dispatch key and side-effect. Refactor to explicit Apply/Revert wired to
  Command Execute/Undo.
- `CardsSO` has a stale mutable `int[] stats` field — mutable state on a shared SO is a trap; remove.
- No `.asmdef`, no `#nullable`, no test assembly. Add a runtime + EditMode test asmdef and pin down
  combat/stat math (`CardsSO.ReturnX`, `Deck.Shuffle`, apply/revert symmetry, `Player.CheckWounds`).
- Debug/dev code left in handlers (`CrystalInventory.OnPointerClick` spawns a random crystal on any
  click; several `Debug.Log`/`print` in `Start`/`GetStack`).
- Likely UI wiring bug: `SaveButton.prefab` invokes `LoadGame`, not `SaveGame`.

## Modernization opportunities (high-value, low-risk first)

1. Replace deprecated finds: `FindObjectOfType` → `FindAnyObjectByType` / `FindObjectsByType(...)`
   (note: even `FindFirstObjectByType` is deprecated in this Unity version — use `FindAnyObjectByType`
   for single-instance lookups). Cache results where possible.
2. Collapse boilerplate properties to `[field: SerializeField] public bool X { get; set; }`.
3. Centralize input (legacy `Input.GetKeyDown` in `DataManager.Update`) → Input System + a menu-toggle event.
4. `switch` expressions for crystal/color maps (`CrystalInventory.CreateCrystal`, `Card.GetEmpowerTypeColor`).
5. File-scoped namespaces + remove unused `using System.Collections;` everywhere.
6. `Awaitable`/async scene loading for the load flow (naturally avoids the "read objects before scene exists" race).

## Recommended sequencing

1. ✅ Fix the listener leak + load path + unsafe autosave (done).
2. Design the game (design-doc skill) — including the canonical save schema (#3).
3. Add asmdefs + EditMode tests around combat/stat math.
4. Refactor the apply/revert toggle; de-couple gameplay→UI via events; modernization pass.
