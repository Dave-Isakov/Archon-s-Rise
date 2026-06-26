# M1 Run-Based Save/Load Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist a run in progress (deck/hand/discard, crystals, units, map, position, run counters) so the player can quit at a settled point and resume intact.

**Architecture:** Pure-data DTOs + serializer + a generic id→SO registry + a map-delta helper live in an isolated assembly (`ArchonsRise.SaveData`) that has no game-type dependencies, so they are unit-testable with Unity EditMode/NUnit tests. The existing `Assembly-CSharp` game code (which auto-references that assembly) does the scene-aware capture/restore wiring: `DataManager` orchestrates save/load, `GridGeneration` regenerates the board from a stored seed through an isolated RNG, and `PlayerDeck`/`PlayerHand`/`DiscardPile` rebuild card zones from id lists under an `IsLoading` guard.

**Tech Stack:** Unity 6000.5.1f1 (C#), `JsonUtility` for JSON, Unity Test Framework (NUnit) for EditMode tests, Unity assembly definitions (`.asmdef`).

## Global Constraints

- Unity version: **6000.5.1f1** (Unity 6.5). Use `FindAnyObjectByType` (not deprecated `FindObjectOfType`) in any new/edited lookup.
- `JsonUtility` only: **no dictionaries, no polymorphism, no MonoBehaviours** in serialized DTOs — only `[System.Serializable]` classes with public fields, primitive arrays, and `List<>` of serializable classes.
- Save schema: `schemaVersion = 1`. Field kept for forward-compat; **no migration logic** this milestone.
- Single save slot — overwrite the existing `Save.json` path (`Application.dataPath + "/Save.json"`).
- Content id field name is exactly `id` (string) on `AllCards`. Ids are lowercase snake-case (`card_strike`, `unit_knight`).
- Crystal colors persist as counts aligned to `EmpowerType` enum declaration order.
- Spec of record: `docs/superpowers/specs/2026-06-26-m1-run-based-save-load-design.md`.

## File Structure

**New isolated assembly `Assets/Scripts/SaveData/` (pure, no game-type refs):**
- `ArchonsRise.SaveData.asmdef` — assembly definition (engine refs on, auto-referenced).
- `SaveModels.cs` — `SaveFile`, `RunState`, `PlayerState`, `MapState`, `Cell` DTOs.
- `SaveSerializer.cs` — `ToJson`/`FromJson` wrappers around `JsonUtility`.
- `ContentRegistry.cs` — generic `ContentRegistry<T>` (id→item lookup, missing/duplicate guards, ordered resolve).
- `MapDelta.cs` — defeated-cell set helpers.
- `Tests/ArchonsRise.SaveData.Tests.asmdef` — EditMode test assembly.
- `Tests/SaveSerializerTests.cs`, `Tests/ContentRegistryTests.cs`, `Tests/MapDeltaTests.cs`.

**Modified game code (stays in `Assembly-CSharp`, auto-references the assembly above):**
- `Assets/Scripts/GameScriptableObjectTypes/AllCards.cs` — add `public string id;`.
- `Assets/Scripts/Managers/DataManager.cs` — registries, seed lifecycle, capture/restore orchestration; replaces `PlayerData` usage.
- `Assets/Scripts/SaveSystem/PlayerData.cs` — **deleted** (replaced by `SaveData` DTOs).
- `Assets/Scripts/TilemapScripts/GridGeneration.cs` — isolated seeded generation; reads seed from `DataManager`.
- `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs` — `IsLoading`-guarded `Awake`; `RebuildDeck(...)`.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs` — `IsLoading`-guarded `Start`; `RebuildHand(...)`.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/DiscardPile.cs` — `RebuildDiscard(...)`.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs` — count-by-color read/restore; remove debug `OnPointerClick` crystal creation.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs` — record defeated cell.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` — settle-gated `OnApplicationQuit`.
- `Assets/Prefabs/.../SaveButton.prefab` — rewire `OnClick` from `LoadGame` to `SaveGame`.

---

## How to run EditMode tests

Tasks 1–3 use the Unity Test Framework. Two equivalent ways to run a test:

- **Editor GUI (primary):** Window ▸ General ▸ Test Runner ▸ **EditMode** tab ▸ select the test ▸ **Run Selected**. A green check = PASS, red = FAIL.
- **CLI (optional):** from the project root, substituting your installed editor path:
  ```bash
  "/c/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Unity.exe" \
    -runTests -batchmode -projectPath "." -testPlatform EditMode \
    -testResults "$TEMP/editmode-results.xml"
  ```
  Exit code 0 = all passed; open the results XML to see per-test status.

If the Test Framework package is missing, add it once: Window ▸ Package Manager ▸ install **Test Framework** (`com.unity.test-framework`). The EditMode test asmdef in Task 1 depends on it.

---

### Task 1: Pure DTOs + serializer + assemblies

**Files:**
- Create: `Assets/Scripts/SaveData/ArchonsRise.SaveData.asmdef`
- Create: `Assets/Scripts/SaveData/SaveModels.cs`
- Create: `Assets/Scripts/SaveData/SaveSerializer.cs`
- Create: `Assets/Scripts/SaveData/Tests/ArchonsRise.SaveData.Tests.asmdef`
- Test: `Assets/Scripts/SaveData/Tests/SaveSerializerTests.cs`

**Interfaces:**
- Produces:
  - `ArchonsRise.SaveData.SaveFile { int schemaVersion; RunState run; }`
  - `RunState { PlayerState player; int[] crystalCounts; string[] deckCardIds; string[] handCardIds; string[] discardCardIds; string[] unitIds; MapState map; int round; int turn; }`
  - `PlayerState { int hp, handSize, level, exp, expToNextLevel, attack, defend, influence, explore; float[] position; }`
  - `MapState { int seed; Cell[] defeatedEnemies; }`
  - `Cell { int x; int y; }` (implements `IEquatable<Cell>`)
  - `SaveSerializer.ToJson(SaveFile) -> string`, `SaveSerializer.FromJson(string) -> SaveFile`

- [ ] **Step 1: Create the runtime assembly definition**

Create `Assets/Scripts/SaveData/ArchonsRise.SaveData.asmdef`:

```json
{
    "name": "ArchonsRise.SaveData",
    "rootNamespace": "ArchonsRise.SaveData",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

(`autoReferenced: true` lets the predefined `Assembly-CSharp` use these types without edits; `noEngineReferences: false` keeps `UnityEngine.JsonUtility` available.)

- [ ] **Step 2: Write the DTOs**

Create `Assets/Scripts/SaveData/SaveModels.cs`:

```csharp
using System;

namespace ArchonsRise.SaveData
{
    [Serializable]
    public class SaveFile
    {
        public int schemaVersion = 1;
        public RunState run = new RunState();
    }

    [Serializable]
    public class RunState
    {
        public PlayerState player = new PlayerState();
        // Aligned to EmpowerType enum declaration order; one count per color.
        public int[] crystalCounts = Array.Empty<int>();
        public string[] deckCardIds = Array.Empty<string>();     // order = draw order
        public string[] handCardIds = Array.Empty<string>();
        public string[] discardCardIds = Array.Empty<string>();
        public string[] unitIds = Array.Empty<string>();
        public MapState map = new MapState();
        public int round;
        public int turn;
    }

    [Serializable]
    public class PlayerState
    {
        public int hp;
        public int handSize;
        public int level;
        public int exp;
        public int expToNextLevel;
        public int attack;
        public int defend;
        public int influence;
        public int explore;
        public float[] position = new float[3];
    }

    [Serializable]
    public class MapState
    {
        public int seed;
        public Cell[] defeatedEnemies = Array.Empty<Cell>();
    }

    [Serializable]
    public struct Cell : IEquatable<Cell>
    {
        public int x;
        public int y;

        public Cell(int x, int y) { this.x = x; this.y = y; }

        public bool Equals(Cell other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Cell c && Equals(c);
        public override int GetHashCode() => unchecked((x * 397) ^ y);
    }
}
```

- [ ] **Step 3: Write the serializer**

Create `Assets/Scripts/SaveData/SaveSerializer.cs`:

```csharp
using UnityEngine;

namespace ArchonsRise.SaveData
{
    public static class SaveSerializer
    {
        public static string ToJson(SaveFile file) => JsonUtility.ToJson(file, prettyPrint: true);

        public static SaveFile FromJson(string json) => JsonUtility.FromJson<SaveFile>(json);
    }
}
```

- [ ] **Step 4: Create the EditMode test assembly definition**

Create `Assets/Scripts/SaveData/Tests/ArchonsRise.SaveData.Tests.asmdef`:

```json
{
    "name": "ArchonsRise.SaveData.Tests",
    "rootNamespace": "ArchonsRise.SaveData.Tests",
    "references": [
        "ArchonsRise.SaveData",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [ "nunit.framework.dll" ],
    "autoReferenced": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 5: Write the failing round-trip test**

Create `Assets/Scripts/SaveData/Tests/SaveSerializerTests.cs`:

```csharp
using NUnit.Framework;
using ArchonsRise.SaveData;

namespace ArchonsRise.SaveData.Tests
{
    public class SaveSerializerTests
    {
        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new SaveFile
            {
                schemaVersion = 1,
                run = new RunState
                {
                    player = new PlayerState
                    {
                        hp = 2, handSize = 5, level = 3, exp = 7, expToNextLevel = 20,
                        attack = 1, defend = 2, influence = 3, explore = 4,
                        position = new float[] { 1.5f, -2.5f, 0f }
                    },
                    crystalCounts = new[] { 1, 0, 2, 0, 1 },
                    deckCardIds = new[] { "card_attack", "card_defend" },
                    handCardIds = new[] { "card_explore" },
                    discardCardIds = new[] { "card_wound" },
                    unitIds = new[] { "unit_knight" },
                    map = new MapState
                    {
                        seed = 123456,
                        defeatedEnemies = new[] { new Cell(3, 4), new Cell(5, 6) }
                    },
                    round = 2,
                    turn = 1
                }
            };

            string json = SaveSerializer.ToJson(original);
            SaveFile restored = SaveSerializer.FromJson(json);

            // Re-serialize and compare JSON strings: any lost/changed field shows as a diff.
            Assert.AreEqual(json, SaveSerializer.ToJson(restored));
        }

        [Test]
        public void Cell_ValueEquality_WorksInHashSet()
        {
            var set = new System.Collections.Generic.HashSet<Cell> { new Cell(1, 2) };
            Assert.IsTrue(set.Contains(new Cell(1, 2)));
            Assert.IsFalse(set.Contains(new Cell(2, 1)));
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they fail (compile) then pass**

Run via Test Runner ▸ EditMode (see "How to run EditMode tests"). On first compile after creating the asmdefs Unity will recompile; if the test types are not yet found, re-open the Test Runner. Expected after Step 5: both tests **PASS** (the implementation in Steps 2–3 already satisfies them — this task bundles model+serializer+test, so the "failing" state is the pre-implementation compile error if a step is skipped).

To see a genuine red→green, temporarily break `ToJson` (e.g. return `"{}"`), run → `RoundTrip_PreservesAllFields` FAILS, then restore it → PASS.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/SaveData
git commit -m "feat: add save DTOs, serializer, and EditMode test assembly"
```

---

### Task 2: Generic content registry

**Files:**
- Create: `Assets/Scripts/SaveData/ContentRegistry.cs`
- Test: `Assets/Scripts/SaveData/Tests/ContentRegistryTests.cs`

**Interfaces:**
- Consumes: nothing from prior tasks.
- Produces:
  - `ContentRegistry<T>(IEnumerable<T> items, Func<T,string> idSelector)` — throws `ArgumentException` on duplicate/empty ids.
  - `bool TryGet(string id, out T item)`
  - `T Get(string id)` — throws `KeyNotFoundException` if missing.
  - `List<T> Resolve(IEnumerable<string> ids)` — ordered; throws `KeyNotFoundException` on the first missing id.
  - `IReadOnlyList<T> Items { get; }`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Scripts/SaveData/Tests/ContentRegistryTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ArchonsRise.SaveData;

namespace ArchonsRise.SaveData.Tests
{
    public class ContentRegistryTests
    {
        private class Item
        {
            public string Id;
            public Item(string id) { Id = id; }
        }

        private static ContentRegistry<Item> Build(params string[] ids)
        {
            var items = new List<Item>();
            foreach (var id in ids) items.Add(new Item(id));
            return new ContentRegistry<Item>(items, i => i.Id);
        }

        [Test]
        public void Get_ReturnsItemById()
        {
            var reg = Build("a", "b");
            Assert.AreEqual("b", reg.Get("b").Id);
        }

        [Test]
        public void Resolve_PreservesOrder()
        {
            var reg = Build("a", "b", "c");
            var resolved = reg.Resolve(new[] { "c", "a", "c" });
            CollectionAssert.AreEqual(new[] { "c", "a", "c" }, resolved.ConvertAll(i => i.Id));
        }

        [Test]
        public void DuplicateId_Throws()
        {
            Assert.Throws<ArgumentException>(() => Build("a", "a"));
        }

        [Test]
        public void EmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(() => Build("a", ""));
        }

        [Test]
        public void MissingId_Throws()
        {
            var reg = Build("a");
            Assert.Throws<KeyNotFoundException>(() => reg.Get("zzz"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Test Runner ▸ EditMode. Expected: FAIL — `ContentRegistry` does not exist (compile error).

- [ ] **Step 3: Implement the registry**

Create `Assets/Scripts/SaveData/ContentRegistry.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace ArchonsRise.SaveData
{
    public class ContentRegistry<T>
    {
        private readonly Dictionary<string, T> _byId = new Dictionary<string, T>();
        private readonly List<T> _items = new List<T>();

        public ContentRegistry(IEnumerable<T> items, Func<T, string> idSelector)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (idSelector == null) throw new ArgumentNullException(nameof(idSelector));

            foreach (var item in items)
            {
                var id = idSelector(item);
                if (string.IsNullOrEmpty(id))
                    throw new ArgumentException($"Content item has a null or empty id: {item}");
                if (_byId.ContainsKey(id))
                    throw new ArgumentException($"Duplicate content id: '{id}'");
                _byId.Add(id, item);
                _items.Add(item);
            }
        }

        public IReadOnlyList<T> Items => _items;

        public bool TryGet(string id, out T item) => _byId.TryGetValue(id, out item);

        public T Get(string id)
        {
            if (_byId.TryGetValue(id, out var item)) return item;
            throw new KeyNotFoundException($"No content registered for id '{id}'.");
        }

        public List<T> Resolve(IEnumerable<string> ids)
        {
            var result = new List<T>();
            foreach (var id in ids) result.Add(Get(id));
            return result;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Test Runner ▸ EditMode. Expected: all 5 tests **PASS**.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/SaveData/ContentRegistry.cs Assets/Scripts/SaveData/Tests/ContentRegistryTests.cs
git commit -m "feat: add generic id->content registry with guards"
```

---

### Task 3: Map-delta helper

**Files:**
- Create: `Assets/Scripts/SaveData/MapDelta.cs`
- Test: `Assets/Scripts/SaveData/Tests/MapDeltaTests.cs`

**Interfaces:**
- Consumes: `Cell` (Task 1).
- Produces:
  - `MapDelta.ToSet(IEnumerable<Cell>) -> HashSet<Cell>`
  - `MapDelta.ToArray(HashSet<Cell>) -> Cell[]`
  - `MapDelta.IsDefeated(HashSet<Cell> defeated, Cell cell) -> bool`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Scripts/SaveData/Tests/MapDeltaTests.cs`:

```csharp
using NUnit.Framework;
using ArchonsRise.SaveData;

namespace ArchonsRise.SaveData.Tests
{
    public class MapDeltaTests
    {
        [Test]
        public void IsDefeated_TrueForRecordedCell()
        {
            var set = MapDelta.ToSet(new[] { new Cell(2, 3), new Cell(4, 5) });
            Assert.IsTrue(MapDelta.IsDefeated(set, new Cell(4, 5)));
            Assert.IsFalse(MapDelta.IsDefeated(set, new Cell(4, 6)));
        }

        [Test]
        public void ToArray_RoundTripsSet()
        {
            var set = MapDelta.ToSet(new[] { new Cell(1, 1), new Cell(1, 1), new Cell(2, 2) });
            var arr = MapDelta.ToArray(set);
            Assert.AreEqual(2, arr.Length); // de-duplicated
            CollectionAssert.Contains(arr, new Cell(1, 1));
            CollectionAssert.Contains(arr, new Cell(2, 2));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Test Runner ▸ EditMode. Expected: FAIL — `MapDelta` does not exist.

- [ ] **Step 3: Implement the helper**

Create `Assets/Scripts/SaveData/MapDelta.cs`:

```csharp
using System.Collections.Generic;

namespace ArchonsRise.SaveData
{
    public static class MapDelta
    {
        public static HashSet<Cell> ToSet(IEnumerable<Cell> cells) => new HashSet<Cell>(cells);

        public static Cell[] ToArray(HashSet<Cell> cells)
        {
            var arr = new Cell[cells.Count];
            cells.CopyTo(arr);
            return arr;
        }

        public static bool IsDefeated(HashSet<Cell> defeated, Cell cell) => defeated.Contains(cell);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Test Runner ▸ EditMode. Expected: both tests **PASS**.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/SaveData/MapDelta.cs Assets/Scripts/SaveData/Tests/MapDeltaTests.cs
git commit -m "feat: add map-delta defeated-cell helpers"
```

---

### Task 4: Content id field + registries in DataManager

**Files:**
- Modify: `Assets/Scripts/GameScriptableObjectTypes/AllCards.cs`
- Modify: `Assets/Scripts/Managers/DataManager.cs:29-30` (field area) and `Awake`
- Modify: `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs:72-75` (`AddRandomCard`)
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/RewardCanvas/RewardCanvas.cs:18`

**Interfaces:**
- Consumes: `ContentRegistry<T>` (Task 2).
- Produces (on `DataManager`):
  - `ContentRegistry<CardsSO> Cards { get; }`
  - `ContentRegistry<UnitsSO> Units { get; }`
  - `bool BuildRegistries()` — returns false and logs an error if any id is missing/duplicate.

This task has no automated test (it wires Unity-object registries); the registry logic itself is covered by Task 2. Verification is in-editor.

- [ ] **Step 1: Add the id field to the content base**

In `Assets/Scripts/GameScriptableObjectTypes/AllCards.cs`, add `id` (covers `CardsSO` and `UnitsSO`, which both extend `AllCards`):

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AllCards : ScriptableObject
{
    public string id;
    public string cardName;
    [TextArea(2,4)] public string cardDescription;
}
```

- [ ] **Step 2: Build registries in DataManager**

In `Assets/Scripts/Managers/DataManager.cs`, add the `using` and registry members, and call `BuildRegistries()` at the end of `Awake` (after `instance = this`). Add at top:

```csharp
using ArchonsRise.SaveData;
```

Add fields near `allCards`/`allUnits` (around line 29-30):

```csharp
    public CardsSO[] allCards;
    public UnitsSO[] allUnits;

    public ContentRegistry<CardsSO> Cards { get; private set; }
    public ContentRegistry<UnitsSO> Units { get; private set; }
```

In `Awake`, after `savePath = ...` and before/after `DontDestroyOnLoad`, call:

```csharp
        BuildRegistries();
```

Add the method:

```csharp
    public bool BuildRegistries()
    {
        try
        {
            Cards = new ContentRegistry<CardsSO>(allCards, c => c.id);
            Units = new ContentRegistry<UnitsSO>(allUnits, u => u.id);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Content registry build failed (missing/duplicate id?): {e.Message}");
            return false;
        }
    }
```

- [ ] **Step 3: Author ids on existing assets**

In the Unity Editor, select each ScriptableObject under `Assets/Scripts/ScriptableObjectData/Player/Cards`, `.../StartingCards`, `.../Rewards/Units`, and any other `CardsSO`/`UnitsSO` assets, and set the new **Id** field to a unique lowercase snake-case value, e.g.:
- Starting cards: `card_attack`, `card_defend`, `card_explore`, `card_influence`, `card_wound`.
- Acquirable cards: one `card_<name>` each (match the asset).
- Units: `unit_knight`, `unit_scout`, `unit_warrior`, `unit_merchant`.

After authoring, enter Play mode once and confirm the Console shows **no** "Content registry build failed" error. (Each id must be unique and non-empty.)

- [ ] **Step 4: Route the two random-pick sites through the registry**

In `PlayerDeck.AddRandomCard` (lines ~72-75), pick from the registry's items:

```csharp
    public void AddRandomCard()
    {
        var cards = DataManager.Instance.Cards.Items;
        deckList.Add(cards[Random.Range(0, cards.Count)]);
    }
```

In `RewardCanvas.cs:18`, replace the indexed access:

```csharp
            var cards = DataManager.Instance.Cards.Items;
            playerCard.GetComponent<Card>().cardSO = cards[Random.Range(0, cards.Count)];
```

- [ ] **Step 5: Verify in editor**

Enter Play mode; draw a reward card and use any "add random card" path. Expected: cards resolve normally, no Console errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/GameScriptableObjectTypes/AllCards.cs Assets/Scripts/Managers/DataManager.cs Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs Assets/Scripts/GameObjectScripts/CardMenuScripts/RewardCanvas/RewardCanvas.cs "Assets/Scripts/ScriptableObjectData"
git commit -m "feat: add stable content ids and id->SO registries"
```

---

### Task 5: Seed lifecycle + isolated RNG for map generation

**Files:**
- Modify: `Assets/Scripts/Managers/DataManager.cs` (seed field + `NewGame`)
- Modify: `Assets/Scripts/TilemapScripts/GridGeneration.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs:123-127` (remove debug create-on-click)

**Interfaces:**
- Consumes: nothing new.
- Produces (on `DataManager`):
  - `int CurrentSeed { get; set; }` — set on new run and on load; read by `GridGeneration`.

- [ ] **Step 1: Add the seed to DataManager and roll it on new run**

In `DataManager.cs`, add the field:

```csharp
    public int CurrentSeed { get; set; }
```

Change `NewGame` to roll a fresh seed before loading the scene:

```csharp
    public void NewGame()
    {
        CurrentSeed = new System.Random().Next(int.MinValue, int.MaxValue);
        SceneManager.LoadScene(1);
    }
```

- [ ] **Step 2: Make GridGeneration consume an isolated seeded RNG**

In `Assets/Scripts/TilemapScripts/GridGeneration.cs`, replace every `UnityEngine.Random.Range(...)` call in `Start` with draws from a local `System.Random` seeded from `DataManager.Instance.CurrentSeed`. Add a helper and use it consistently so the **order and count of draws** is fixed.

At the top of `Start`, before the loops:

```csharp
        var rngSource = new System.Random(DataManager.Instance.CurrentSeed);
        int Rng(int minInclusive, int maxExclusive) => rngSource.Next(minInclusive, maxExclusive);
        var player = FindAnyObjectByType<PlayerPosition>();
```

Then replace each `UnityEngine.Random.Range(a, b)` with `Rng(a, b)` throughout `Start` — terrain rolls, the desert-adjacency rolls, the town-placement step increments **and** `towns.towns[Rng(0,3)]`, and the enemy-placement step increments. For example the town placement becomes:

```csharp
        for(int x = 3; x < 18; x += Rng(5,7))
        {
            for(int y = 3; y < 18; y += Rng(5,7))
            {
                var tilePos = new Vector3Int(x,y);
                ground.SetTile(tilePos, townTile);
                var tile = ground.GetTile<TownRuleTile>(tilePos);
                var townToken = Instantiate(tile.m_DefaultGameObject, ground.CellToWorld(tilePos)+ new Vector3(0,-1), Quaternion.identity, townParentObject);
                townToken.GetComponent<TownToken>().townSO = towns.towns[Rng(0,3)];
            }
        }
```

> Note: `UnityEngine.Random.Range(int,int)` is max-**exclusive**, the same as `System.Random.Next(min,max)`, so the bounds carry over unchanged. Also confirm `EnemyDeck.GetNewEnemyToken` / token `Start` do not call `Random` during generation; if any do, thread the same `rngSource` in or move that draw out of the generation path. The goal: **every** generation-time draw comes from `rngSource`, in a fixed order.

- [ ] **Step 3: Remove the debug crystal-on-click behavior**

In `CrystalInventory.cs`, the `OnPointerClick` handler currently spawns a random crystal on every click (a debug artifact, and a stray global-RNG consumer). Replace its body with nothing:

```csharp
    public void OnPointerClick(PointerEventData eventData)
    {
        // Intentionally empty: crystals are gained via cards/towns/rewards, not by clicking the inventory.
    }
```

- [ ] **Step 4: Verify determinism in editor**

Temporarily add `Debug.Log($"seed={DataManager.Instance.CurrentSeed}");` at the top of `GridGeneration.Start`. Start a new game, note the seed and the visible town/enemy layout. Then force the same seed twice: in `NewGame`, temporarily hardcode `CurrentSeed = 12345;`, run twice, and confirm the board (terrain, town positions/types, enemy positions) is **identical** both times. Remove the hardcode and the debug log afterward.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Managers/DataManager.cs Assets/Scripts/TilemapScripts/GridGeneration.cs Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs
git commit -m "feat: deterministic seeded map generation via isolated RNG; remove debug crystal click"
```

---

### Task 6: Capture run state (save)

**Files:**
- Delete: `Assets/Scripts/SaveSystem/PlayerData.cs` (+ its `.meta`)
- Modify: `Assets/Scripts/Managers/DataManager.cs` (replace `playerData` with `SaveFile`; `CaptureRunState`; `SaveGame`)
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs` (expose `GetCounts`)
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs` (record defeated cell)
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs:307-312` (`OnApplicationQuit`)
- Modify: `Assets/Prefabs/.../SaveButton.prefab` (rewire OnClick to `SaveGame`)

**Interfaces:**
- Consumes: `SaveFile`, `RunState`, `Cell`, `MapDelta`, `SaveSerializer` (Task 1/3); registries (Task 4); `CurrentSeed` (Task 5).
- Produces (on `DataManager`):
  - `SaveFile current` (replaces `public PlayerData playerData`).
  - `HashSet<Cell> DefeatedEnemies { get; }` — run-level set, appended by `EnemyToken`.
  - `SaveFile CaptureRunState()` — reads live scene objects into a fresh `SaveFile`.
  - `void SaveGame()` — serializes `CaptureRunState()` to `savePath`.
- Produces (on `CrystalInventory`): `int[] GetCounts()` — counts aligned to `EmpowerType` order.

No automated test (scene-dependent); verify in editor.

- [ ] **Step 1: Delete the obsolete PlayerData**

```bash
git rm "Assets/Scripts/SaveSystem/PlayerData.cs" "Assets/Scripts/SaveSystem/PlayerData.cs.meta"
```

- [ ] **Step 2: Add crystal count export**

In `CrystalInventory.cs`, add a method that tallies the inventory by color in `EmpowerType` order. (Confirm the `EmpowerType` enum order; this returns one int per enum value.)

```csharp
    public int[] GetCounts()
    {
        var values = System.Enum.GetValues(typeof(EmpowerType));
        var counts = new int[values.Length];
        foreach (var crystal in crystalsInInventory)
        {
            if (crystal == null) continue;
            counts[(int)crystal.color]++;
        }
        return counts;
    }

    public void SetCounts(int[] counts)
    {
        // Clear current inventory GameObjects, then recreate per color.
        foreach (var c in new System.Collections.Generic.List<Crystal>(crystalsInInventory))
            if (c != null) Destroy(c.gameObject);
        crystalsInInventory.Clear();

        var values = System.Enum.GetValues(typeof(EmpowerType));
        for (int i = 0; i < counts.Length && i < values.Length; i++)
            for (int n = 0; n < counts[i]; n++)
                CreateCrystal((EmpowerType)i);
    }
```

> Verify `Crystal.color` is an `EmpowerType` and that `(int)EmpowerType.Green == 0` etc. matches the `crystals[]` index map in `CreateCrystal`. If the enum's int values are not 0..N-1 contiguous, switch `GetCounts`/`SetCounts` to map via `System.Array.IndexOf(values, crystal.color)` instead of the cast.

- [ ] **Step 3: Record defeated enemies by cell**

In `EnemyToken.cs`, when the token is destroyed because its card was defeated, record its grid cell into the run-level set. Replace `Update`:

```csharp
    void Update()
    {
        if(cardRef is not null && cardRef.IsDefeated)
        {
            if (DataManager.Instance != null)
                DataManager.Instance.DefeatedEnemies.Add(
                    new ArchonsRise.SaveData.Cell(gridPos.x, gridPos.y));
            Destroy(this.gameObject);
        }
    }
```

Add `using ArchonsRise.SaveData;` at the top if preferred instead of the fully-qualified name.

- [ ] **Step 4: Replace DataManager state + capture + save**

In `DataManager.cs`:

Replace the field `public PlayerData playerData;` with:

```csharp
    public SaveFile current = new SaveFile();
    public HashSet<Cell> DefeatedEnemies { get; private set; } = new HashSet<Cell>();
```

Add `using System.Collections.Generic;` (already present) and ensure `using ArchonsRise.SaveData;` (added in Task 4).

Add the capture method:

```csharp
    public SaveFile CaptureRunState()
    {
        var player = FindAnyObjectByType<Player>();
        var pos = FindAnyObjectByType<PlayerPosition>();
        var deck = FindAnyObjectByType<PlayerDeck>();
        var hand = FindAnyObjectByType<PlayerHand>();
        var discard = FindAnyObjectByType<DiscardPile>();
        var crystals = FindAnyObjectByType<CrystalInventory>();
        var game = GameManager.Instance;

        var file = new SaveFile { schemaVersion = 1 };
        var run = file.run;

        run.player.hp = player.PlayerHP;
        run.player.handSize = player.PlayerHandSize;
        run.player.level = player.PlayerLevel;
        run.player.exp = player.PlayerExp;
        run.player.expToNextLevel = player.ExpToNextLevel;
        run.player.attack = player.PlayerAttack;
        run.player.defend = player.PlayerDefend;
        run.player.influence = player.PlayerInfluence;
        run.player.explore = player.PlayerExplore;
        run.player.position = new[] { pos.transform.position.x, pos.transform.position.y, pos.transform.position.z };

        run.crystalCounts = crystals != null ? crystals.GetCounts() : System.Array.Empty<int>();
        run.deckCardIds = CardIds(deck.CardsInDeck);
        run.handCardIds = CardIds(hand.cardsInPlay);
        run.discardCardIds = DiscardIds(discard);
        run.unitIds = UnitIds(player);

        run.map.seed = CurrentSeed;
        run.map.defeatedEnemies = MapDelta.ToArray(DefeatedEnemies);

        run.round = game != null ? game.Round : 0;
        run.turn = game != null ? game.Turn : 0;

        return file;
    }

    private static string[] CardIds(List<Card> cards)
    {
        var ids = new List<string>(cards.Count);
        foreach (var c in cards) if (c != null && c.cardSO != null) ids.Add(c.cardSO.id);
        return ids.ToArray();
    }
```

> `DiscardIds`, `UnitIds`, and `GameManager.Round/Turn` need real accessors:
> - `DiscardPile` keeps its `cards` list private — add a public getter `public List<Card> Cards => cards;` and implement `DiscardIds` like `CardIds`.
> - `Player.units` is private — add `public IReadOnlyList<UnitsSO> Units => units;` and implement `UnitIds` by reading `u.id`.
> - `GameManager` round/turn: locate the existing round/turn counters (the scene wires `TurnEnd`/`EndOfRoundReshuffle`); expose `public int Round` / `public int Turn`. If `GameManager` has no such counters yet, add `int round; int turn;` incremented where rounds/turns advance, and default both to 0. Keep this minimal — round is the only value M2's Doom Clock needs.

Add the helper stubs concretely:

```csharp
    private static string[] DiscardIds(DiscardPile discard)
    {
        if (discard == null) return System.Array.Empty<string>();
        return CardIds(discard.Cards);
    }

    private static string[] UnitIds(Player player)
    {
        var ids = new List<string>();
        foreach (var u in player.Units) if (u != null) ids.Add(u.id);
        return ids.ToArray();
    }
```

Replace `SaveGame`:

```csharp
    public void SaveGame()
    {
        current = CaptureRunState();
        string json = SaveSerializer.ToJson(current);
        Debug.Log($"Saving data at {savePath}");
        using StreamWriter writer = new StreamWriter(savePath);
        writer.Write(json);
    }
```

- [ ] **Step 5: Add the accessors named above**

- In `DiscardPile.cs`, add: `public List<Card> Cards => cards;`
- In `Player.cs`, add: `public IReadOnlyList<UnitsSO> Units => units;`
- In `Player.cs`, make `expToNextLevel` restorable — change the existing get-only property to `public int ExpToNextLevel { get => expToNextLevel; set => expToNextLevel = value; }` (restore depends on this; otherwise it defaults to 0 and `Update()` fires a spurious level-up on load).
- In `GameManager.cs`, add `public int Round { get; set; }` and `public int Turn { get; set; }` (wire increments at round/turn advance if counters do not already exist).

- [ ] **Step 6: Gate autosave-on-quit (capture path)**

In `Player.cs`, replace `OnApplicationQuit` to use the new capture and only save at a settled state (the `IsSettledState` helper is added in Task 8; for now guard on `IsLoading` and presence of `DataManager`):

```csharp
    private void OnApplicationQuit()
    {
        if (DataManager.Instance == null || DataManager.Instance.IsLoading) return;
        DataManager.Instance.SaveGame();
    }
```

- [ ] **Step 7: Rewire the SaveButton prefab**

Open the `SaveButton` prefab (currently its `OnClick` invokes `DataManager.LoadGame`). In the Inspector, change the `Button.OnClick` target method to `DataManager.SaveGame`. Save the prefab.

- [ ] **Step 8: Verify capture in editor**

Start a new game, play a few cards (so some land in discard), gain a crystal, defeat an enemy, then click **Save**. Open `Assets/Save.json` and confirm it contains: non-empty `deckCardIds`/`handCardIds`, any `discardCardIds`, `crystalCounts`, the defeated enemy `Cell` under `map.defeatedEnemies`, the `seed`, and `round`/`turn`.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: capture full run state to Save.json (deck/hand/discard, crystals, units, map deltas)"
```

---

### Task 7: Restore run state (load)

**Files:**
- Modify: `Assets/Scripts/Managers/DataManager.cs` (`LoadGame`, `OnGameSceneLoaded`)
- Modify: `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs` (`IsLoading` guard + `RebuildDeck`)
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs` (`IsLoading` guard + `RebuildHand`)
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/DiscardPile.cs` (`RebuildDiscard`)

**Interfaces:**
- Consumes: `SaveFile`/`RunState` (Task 1), registries (Task 4), `CurrentSeed`/`DefeatedEnemies` (Task 5/6).
- Produces:
  - `PlayerDeck.RebuildDeck(List<CardsSO> orderedCards)` — clears auto-built deck, instantiates cards in order, sets `InDeck`.
  - `PlayerHand.RebuildHand(List<CardsSO> cards)` — instantiates hand cards, sets `InHand`.
  - `DiscardPile.RebuildDiscard(List<CardsSO> cards)` — instantiates discard cards, sets `InDiscard`.

No automated test (scene-dependent); verify against the acceptance criterion.

- [ ] **Step 1: Guard the default deck/hand construction**

In `PlayerDeck.Awake`, skip the default starting-deck build + shuffle when loading:

```csharp
    void Awake()
    {
        drawCommand = new CardDrawCommand(drawNewCardEvent, this);
        command = new PlayManager();

        if (DataManager.Instance != null && DataManager.Instance.IsLoading) return; // deck rebuilt from save

        foreach(var card in player.StartingHand)
            deckList.Add(card);
        foreach(var card in deckList)
            AddCardToDecklist(card);
        Shuffle(cardsInDeck);
    }
```

In `PlayerHand.Start`, skip the default draw when loading:

```csharp
    void Start()
    {
        if (DataManager.Instance != null && DataManager.Instance.IsLoading) return; // hand rebuilt from save
        DrawCards(player.PlayerHandSize);
    }
```

- [ ] **Step 2: Add the rebuild methods**

In `PlayerDeck.cs`, add (reuses the existing private `AddCardToDecklist`, but in saved order with no shuffle). Make `AddCardToDecklist` usable for ordered append (it already appends to `CardsInDeck`):

```csharp
    public void RebuildDeck(List<CardsSO> orderedCards)
    {
        foreach (var c in new List<Card>(CardsInDeck)) if (c != null) Destroy(c.gameObject);
        CardsInDeck.Clear();
        deckList.Clear();
        foreach (var so in orderedCards)
        {
            deckList.Add(so);
            AddCardToDecklist(so); // appends in order; sets InDeck=true, inactive
        }
    }
```

In `PlayerHand.cs`, add:

```csharp
    public void RebuildHand(List<CardsSO> cards)
    {
        foreach (var c in new List<Card>(cardsInPlay)) if (c != null) Destroy(c.gameObject);
        cardsInPlay.Clear();
        foreach (var so in cards)
        {
            var go = Instantiate(card, GetComponentInChildren<GridLayoutGroup>().transform);
            var comp = go.GetComponent<Card>();
            comp.cardSO = so;
            comp.InHand = true;
            comp.InDeck = false;
            go.name = so.cardName;
            go.SetActive(true);
            cardsInPlay.Add(comp);
        }
    }
```

In `DiscardPile.cs`, add (reuses `AddCardToDiscard` semantics but instantiates from SOs — needs the card prefab; add a `[SerializeField] GameObject cardPrefab;` and assign it in the Inspector):

```csharp
    [SerializeField] GameObject cardPrefab;

    public void RebuildDiscard(List<CardsSO> cards)
    {
        foreach (var c in new List<Card>(this.cards)) if (c != null) Destroy(c.gameObject);
        this.cards.Clear();
        foreach (var so in cards)
        {
            var go = Instantiate(cardPrefab, this.transform);
            var comp = go.GetComponent<Card>();
            comp.cardSO = so;
            comp.InDiscard = true;
            go.name = so.cardName;
            go.SetActive(false);
            this.cards.Add(comp);
        }
    }
```

- [ ] **Step 3: Implement restore in DataManager**

In `DataManager.cs`, rewrite `LoadGame` to deserialize into `current`, set the seed, and defer object restore to `sceneLoaded`:

```csharp
    public void LoadGame()
    {
        if(!File.Exists(savePath))
        {
            Debug.LogWarning($"No save file found at {savePath}");
            return;
        }

        using (StreamReader reader = new(savePath))
            current = SaveSerializer.FromJson(reader.ReadToEnd());

        CurrentSeed = current.run.map.seed;          // GridGeneration reads this in its Start
        DefeatedEnemies = new HashSet<Cell>(current.run.map.defeatedEnemies);

        IsLoading = true;
        SceneManager.sceneLoaded += OnGameSceneLoaded;
        SceneManager.LoadScene(1);
    }
```

Rewrite `OnGameSceneLoaded` to restore everything (the board has already regenerated from `CurrentSeed` during scene load; remove the defeated tokens, then restore player/resources/zones):

```csharp
    private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnGameSceneLoaded;

        var player = FindAnyObjectByType<Player>();
        var pos = FindAnyObjectByType<PlayerPosition>();
        var deck = FindAnyObjectByType<PlayerDeck>();
        var hand = FindAnyObjectByType<PlayerHand>();
        var discard = FindAnyObjectByType<DiscardPile>();
        var crystals = FindAnyObjectByType<CrystalInventory>();
        var game = GameManager.Instance;
        if (player == null || pos == null || deck == null || hand == null)
        {
            Debug.LogError("Loaded scene missing core objects; cannot restore save.");
            IsLoading = false;
            return;
        }

        var run = current.run;

        // Remove enemy tokens whose cell was recorded as defeated.
        foreach (var token in FindObjectsByType<EnemyToken>(FindObjectsSortMode.None))
            if (MapDelta.IsDefeated(DefeatedEnemies, new Cell(token.gridPos.x, token.gridPos.y)))
                Destroy(token.gameObject);

        player.PlayerHP = run.player.hp;
        player.PlayerHandSize = run.player.handSize;
        player.PlayerLevel = run.player.level;
        player.PlayerExp = run.player.exp;
        player.ExpToNextLevel = run.player.expToNextLevel; // restore before Update() runs its level-up check
        player.PlayerAttack = run.player.attack;
        player.PlayerDefend = run.player.defend;
        player.PlayerInfluence = run.player.influence;
        player.PlayerExplore = run.player.explore;
        pos.transform.position = new Vector3(run.player.position[0], run.player.position[1], run.player.position[2]);

        if (crystals != null) crystals.SetCounts(run.crystalCounts);

        deck.RebuildDeck(Cards.Resolve(run.deckCardIds));
        hand.RebuildHand(Cards.Resolve(run.handCardIds));
        if (discard != null) discard.RebuildDiscard(Cards.Resolve(run.discardCardIds));

        if (game != null) { game.Round = run.round; game.Turn = run.turn; }

        IsLoading = false;
    }
```

> `EnemyToken.gridPos` is computed in `EnemyToken.Start` (`gameboard.LocalToCell(transform.position)`). Because restore runs in the `sceneLoaded` callback (after `Start` on freshly spawned tokens), `gridPos` is populated. If timing proves unreliable, compute the cell directly in this loop from the token's transform via the same `Grid`.

- [ ] **Step 4: Verify the acceptance criterion in editor**

1. New game → explore/move a few tiles (spend part of the Explore budget), play cards (some to discard), gain a crystal, defeat one enemy, recruit a unit.
2. Click **Save**, then stop Play mode.
3. Enter Play mode → **Load**.
4. Confirm: player position matches where you stopped; deck/hand/discard card counts and contents match; the crystal is present; the previously defeated enemy is **absent**; the recruited unit is present; round/turn match. The rest of the map (terrain, towns, other enemies) is identical to pre-save.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: restore run state on load (seed regen + deltas + zone rebuild from ids)"
```

---

### Task 8: Settle-point gate for saving

**Files:**
- Modify: `Assets/Scripts/Managers/DataManager.cs` (`IsSettledState`, guard `SaveGame`)
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` (`OnApplicationQuit` uses the gate)

**Interfaces:**
- Produces: `bool DataManager.IsSettledState()` — true only when the board is in a saveable settled sub-state.

- [ ] **Step 1: Implement the settle check**

In `DataManager.cs`, add (reads `GameManager` modal canvases + the command stack). Use the canvases that exist on `GameManager` (combat/town/reward/card-list); adjust names to the actual fields:

```csharp
    public bool IsSettledState()
    {
        var game = GameManager.Instance;
        if (game == null) return false;
        if (IsLoading) return false;

        // No modal sub-screen open.
        if (game.combatCanvas != null && game.combatCanvas.enabled) return false;
        if (game.townCanvas != null && game.townCanvas.enabled) return false;
        if (game.cardRewardCanvas != null && game.cardRewardCanvas.enabled) return false;
        if (game.cardListCanvas != null && game.cardListCanvas.enabled) return false;

        // Undo/command stack empty (no card mid-play).
        if (game.commands != null && !game.commands.IsEmpty) return false;

        return true;
    }
```

> Confirm the exact canvas field names on `GameManager` (the codebase references `combatCanvas`, `townCanvas`, `cardRewardCanvas`, `cardListCanvas`, and `commands`). For the command stack, add `public bool IsEmpty => /* stack count == 0 */;` to the `PlayManager`/commands type if it has no emptiness check.

- [ ] **Step 2: Guard SaveGame and quit-save with the gate**

In `DataManager.SaveGame`, bail if not settled (keeps the last good save):

```csharp
    public void SaveGame()
    {
        if (!IsSettledState())
        {
            Debug.Log("Save skipped: not at a settled state.");
            return;
        }
        current = CaptureRunState();
        string json = SaveSerializer.ToJson(current);
        Debug.Log($"Saving data at {savePath}");
        using StreamWriter writer = new StreamWriter(savePath);
        writer.Write(json);
    }
```

`Player.OnApplicationQuit` already calls `SaveGame`, which now self-gates — no further change needed there.

- [ ] **Step 3: Verify the gate**

- Open the combat or town canvas, click **Save** → Console logs "Save skipped", `Save.json` unchanged.
- Close all modals (map view, no card mid-play), click **Save** → save succeeds.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Managers/DataManager.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs Assets/Scripts/Managers/Commands/PlayManager.cs
git commit -m "feat: gate saving to settled board states"
```

---

## Final verification

- [ ] Run all EditMode tests (Test Runner ▸ EditMode ▸ Run All): SaveSerializer (2), ContentRegistry (5), MapDelta (2) — all **green**.
- [ ] Full acceptance pass (Task 7 Step 4) succeeds end-to-end, including saving **after a mid-turn exploration** and resuming at that position.
- [ ] `git status` clean; update `archons-rise-roadmap/status.md` (M1 items done) and append a `decisions-log.md` entry for the M1 save schema, then advance Current Focus to M2. (Roadmap-maintenance, done via the roadmap skill — not a code commit.)
```

