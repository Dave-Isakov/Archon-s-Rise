# Task 7 Report — Restore Run State on Load

## Commit
`39ac5a0` — `feat: restore run state on load (seed regen + deltas + zone rebuild from ids)`

---

## Files Changed

| File | Change |
|------|--------|
| `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` | Added setter to `ExpToNextLevel` property (`get => expToNextLevel; set => expToNextLevel = value;`) |
| `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs` | Guarded `Awake` to skip default deck build + shuffle when `IsLoading`; moved `drawCommand`/`command` init before the guard; added `RebuildDeck(List<CardsSO>)` |
| `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs` | Guarded `Start` to skip default `DrawCards` when `IsLoading`; added `RebuildHand(List<CardsSO>)` |
| `Assets/Scripts/GameObjectScripts/PlayerScripts/DiscardPile.cs` | Added `[SerializeField] GameObject cardPrefab;` field; added `RebuildDiscard(List<CardsSO>)` |
| `Assets/Scripts/Managers/DataManager.cs` | Removed `public PlayerData playerData;` field; rewrote `LoadGame()` to deserialize `SaveFile` via `SaveSerializer.FromJson`, set `CurrentSeed` and `DefeatedEnemies`, then load scene; rewrote `OnGameSceneLoaded` to remove defeated enemy tokens, restore all player fields (including `ExpToNextLevel` first), restore position, crystals, deck/hand/discard zones, and round/turn |
| `Assets/Scripts/SaveSystem/PlayerData.cs` | **Deleted** (`git rm`) |
| `Assets/Scripts/SaveSystem/PlayerData.cs.meta` | **Deleted** (`git rm`) |

---

## PlayerData Removal — Grep Confirmation

After deletion and rewrite, a full grep of `Assets/Scripts` for `PlayerData` and `playerData` found **zero references in any C# file outside `PlayerData.cs` itself**. The only hits were inside the file being deleted. No compile-breaking dangling references remain.

```
grep result: only Assets/Scripts/SaveSystem/PlayerData.cs matched (the file being deleted)
DataManager.cs: 0 matches
All other scripts: 0 matches
```

---

## No-Compile-Verification Note

Unity Editor was not opened during this task (project lock constraint). Correctness was verified by:
- Reading every target file's actual content before editing
- Confirming all referenced types (`SaveSerializer`, `MapDelta`, `Cell`, `SaveFile`) are in the `ArchonsRise.SaveData` namespace already imported via `using ArchonsRise.SaveData;` in `DataManager.cs`
- Confirming `FindObjectsOfType<T>()` is the existing pattern used in `PlayerHand.cs` (line 146) and `CrystalInventory.cs` (line 201) — consistent with codebase style
- IDE diagnostics after each edit showed only warnings/hints, no errors remaining after the `playerData` references were fully replaced

---

## Deferred Editor Steps (Required Before Testing)

### 1. Assign DiscardPile.cardPrefab in Inspector (REQUIRED)
`DiscardPile` now has a `[SerializeField] GameObject cardPrefab;` field. Without assigning it, `RebuildDiscard` will throw a NullReferenceException.

**Steps:**
1. Open the `GameBoard` scene in the Unity Editor
2. Select the `DiscardPile` GameObject in the Hierarchy
3. In the Inspector, find the **Card Prefab** field under the `DiscardPile` component
4. Drag the `Card` prefab (same prefab used by `PlayerDeck` and `PlayerHand`) into that slot
5. Save the scene (`Ctrl+S`)

### 2. Load Acceptance Test (full in-editor verification)
1. Enter Play mode → **New Game**
2. Move the player a few tiles (spend some Explore budget)
3. Play some cards (move a few to discard)
4. Gain at least one crystal
5. Defeat one enemy
6. Recruit a unit from a town
7. Click **Save**, then stop Play mode
8. Enter Play mode → **Load**
9. Verify:
   - Player position matches the saved position
   - Deck card count and contents match (check via card list)
   - Hand card count and contents match
   - Discard pile count and contents match
   - Crystal count is correct
   - The previously defeated enemy token is **absent** from the board
   - The recruited unit appears in the Units panel
   - Round and Turn counters match the saved values
   - The rest of the map (terrain, towns, remaining enemies) is identical to pre-save

---

## IDE Diagnostics Summary

All remaining diagnostics after final edit are non-blocking:

| Severity | Code | Location | Description |
|----------|------|----------|-------------|
| Warning | CS0618 | DataManager.cs:158 | `FindObjectsOfType<T>()` obsolete — consistent with existing codebase pattern |
| Information | IDE0031 | DataManager.cs:174,178 | Null-check simplification hint — style only |
| Hint | IDE0003 | DiscardPile.cs:39 | `this.transform` simplification — style only |

No errors (CS errors) remain.

---

## Fix: deferred restore coroutine

### Lifecycle reasoning

`SceneManager.sceneLoaded` fires after every `Awake()` in the new scene but **before** any `Start()`. The original `OnGameSceneLoaded` performed the entire run-state restore inline at that point, producing three bugs:

1. **Defeated-enemy tokens not found.** `GridGeneration.Start()` generates the map and spawns `EnemyToken` GameObjects. Because `Start()` had not yet run at `sceneLoaded` time, `FindObjectsOfType<EnemyToken>()` returned an empty set, so the defeated-enemy removal loop was a no-op and defeated enemies reappeared after load.

2. **Duplicate hand draw.** `PlayerHand.Start()` skips its default `DrawCards` call only when `DataManager.IsLoading` is still `true`. The original code cleared `IsLoading = false` at the end of the `sceneLoaded` callback — before `PlayerHand.Start()` ran. This meant `Start()` saw `IsLoading == false` and drew a fresh default hand on top of the just-rebuilt loaded hand, duplicating cards.

3. **`EnemyToken.gridPos` unset.** Each token's `gridPos` is assigned in `EnemyToken.Start()`. Accessing it in the `sceneLoaded` callback (before `Start()`) meant the comparison `new Cell(token.gridPos.x, token.gridPos.y)` always used the default `(0, 0)`, corrupting defeat-status detection even if tokens had been found.

### Exact restructuring

`OnGameSceneLoaded` is now a two-liner that unsubscribes itself and launches a coroutine:

```csharp
private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
{
    SceneManager.sceneLoaded -= OnGameSceneLoaded;
    StartCoroutine(RestoreAfterSceneInit());
}
```

`RestoreAfterSceneInit()` opens with `yield return null`, which suspends until the end of the current frame — after every `Start()` in the scene has executed. The entire former inline body then runs verbatim inside the coroutine, with two targeted changes:

- The early-out guard uses `IsLoading = false; yield break;` instead of `return;` (required inside a coroutine).
- The deprecated `FindObjectsOfType<EnemyToken>()` is replaced with `FindObjectsByType<EnemyToken>()` (the Unity 6 non-deprecated, parameterless overload).
- `IsLoading = false;` remains the **last line**, keeping it `true` through the entire `Start()` phase so `PlayerHand.Start()` correctly skips its default draw.

`LoadGame()` is unchanged: it already sets `CurrentSeed`, `DefeatedEnemies`, `IsLoading = true`, subscribes the callback, and calls `SceneManager.LoadScene(1)` — all correct.

`using System.Collections;` was already present at line 1; no new import was needed.

### Compilation note

Unity Editor was not opened during this fix (project lock constraint). Correctness was verified by careful reading of the diff. The user must verify load behavior in Play mode: enter Play → Load → confirm defeated enemies absent, hand matches saved state, no duplicate cards drawn.
