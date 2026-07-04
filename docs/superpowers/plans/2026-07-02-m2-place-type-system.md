# M2 Place-Type System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the homogeneous "town" with a typed Town / Keep / Castle taxonomy plus a resumable guardian-conquest mechanic (defeat all guardians to conquer; retreat costs 3 wounds; progress persists across save/load) and gate services by type + conquest.

**Architecture:** Pure rules live in a new engine-light assembly `ArchonsRise.Places` (`PlaceType`, `PlaceService`, `PlaceRules`, `ConquestLedger`) so they are unit-testable outside the scene, following the `ArchonsRise.SaveData` / `ArchonsRise.CardPlay` pattern. Scene-side, two lazily-bootstrapped MonoBehaviour singletons (`ConquestTracker` wrapping the ledger, `GuardianAssault` driving assault combat) avoid scene edits; the only scene change is one new Assault button in the town menu. Persistence extends the M1 save file with a `places` array and a v1→v2 schema bump.

**Tech Stack:** Unity 6000.5.1f1 (C#), `JsonUtility` JSON saves, Unity Test Framework (NUnit) EditMode tests, `.asmdef` assemblies.

**Spec of record:** `docs/superpowers/specs/2026-06-30-m2-place-type-system-design.md`

## Global Constraints

- Unity **6000.5.1f1**; use `FindAnyObjectByType` / `FindObjectsByType`, never deprecated `FindObjectOfType`.
- `JsonUtility` only for save DTOs: `[System.Serializable]` classes, public fields, primitive arrays — no dictionaries, no properties.
- Save schema bumps to **`schemaVersion = 2`**; v1 files migrate by default-fill (absent `places` ⇒ nothing conquered). No other migration logic.
- Guardian counts are **data-driven** from the `guardians` roster list; assault logic reads `roster.Count`, never a hardcoded per-type number. Starting rosters: Town 0, Keep 1, Castle 2.
- Retreat from an assault in progress costs exactly **`PlaceRules.RetreatWoundCount = 3`** wounds and preserves `defeatedCount`. Closing the place menu without starting an assault is free.
- `TownsSO` **keeps its class name** (no `PlaceSO` rename). Dungeons are **untouched**.
- Places can only be interacted with while the player is **standing on the place's cell** — adjacent is not enough (user decision 2026-07-02).
- **Scene and prefab changes are performed by the user in the Unity editor**, guided by a step-by-step checklist — never by hand-editing `.unity` YAML (solo-dev workflow preference). Plain `.cs` and `.asset` file edits are done directly.
- Castle **Cards service is a visible-but-disabled stub**; healing stays free.
- Service availability is computed from `placeType` via `PlaceRules`, **not** from the legacy `activity` flags — except the Crystal/Resources button, which the spec's table omits: it keeps its legacy `activity` flag gate AND additionally requires conquest (recorded as a decision in Task 11).
- The generated map must contain **at least 2 Castles** (M2.5's victory needs them).
- New pure code goes in `Assets/Scripts/Places/`; EditMode tests go in `Assets/Tests/EditMode/` (referenced from `ArchonsRise.Tests.EditMode.asmdef`).

## File Structure

**New pure assembly `Assets/Scripts/Places/`:**
- `ArchonsRise.Places.asmdef` — references `ArchonsRise.SaveData` (for `Cell` / `PlaceConquest`), auto-referenced by `Assembly-CSharp`.
- `PlaceType.cs` — `enum PlaceType { Town, Keep, Castle }`.
- `PlaceService.cs` — `[Flags] enum PlaceService`.
- `PlaceRules.cs` — allowed-services table, conquest predicate, retreat-wound constant.
- `ConquestLedger.cs` — pure cell → conquest-state registry.

**Modified save assembly `Assets/Scripts/SaveData/`:**
- `SaveModels.cs` — `PlaceConquest` DTO, `RunState.places`, `schemaVersion = 2` default.
- `SaveMigrator.cs` (new) — v1→v2 default-fill.
- `Tests/SaveSerializerTests.cs` — round-trip covers `places`.
- `Tests/SaveMigratorTests.cs` (new).

**Modified game code (`Assembly-CSharp`):**
- `GameScriptableObjectTypes/TownsSO.cs` — `placeType`, `guardians`.
- `GameObjectScripts/GameBoardObjects/TownToken.cs` — `gridPos`, tracker registration, assault-aware menu open.
- `Managers/ConquestTracker.cs` (new) — scene-scoped MonoBehaviour singleton wrapping `ConquestLedger`.
- `GameObjectScripts/GameBoardObjects/GuardianAssault.cs` (new) — resumable assault driver.
- `Managers/GameManager.cs` — retreat delegation, shared combat-canvas teardown.
- `GameObjectScripts/DeckScripts/EnemyDeck.cs` — expose enemy-card prefab.
- `GameObjectScripts/DeckScripts/EndTurnButton.cs`, `EndRoundButton.cs` — combat gate includes assaults.
- `GameObjectScripts/TownMenuScripts/RecruitButton.cs`, `HealButton.cs`, `CardButton.cs`, `CrystalButton.cs` — type + conquest gating.
- `GameObjectScripts/TownMenuScripts/AssaultButton.cs` (new; Unity generates the `.meta`).
- `TilemapScripts/GridGeneration.cs` — town pick over full list, `gridPos` assignment, ≥2-Castle guarantee.
- `Managers/DataManager.cs` — capture/restore/migrate conquest state.

**Scene / content:**
- `Assets/Scenes/GameBoard.unity` — one new Assault button under the town menu layout (added by the user in the editor, Task 7).
- `Assets/Scripts/ScriptableObjectData/Non-Player/Towns/Stonegate Keep.asset`, `Castle Veyrune.asset` — new places (created by the user in the editor, Task 9).
- `Assets/Scripts/ScriptableObjectData/Non-Player/Locations/Redonya.asset` — towns list gains the two new entries (Inspector edit, Task 9).

**Docs (part of this milestone per the spec):**
- `.claude/skills/archons-rise-design/mechanics.md`, `balance.md`, `content-rules.md`
- `.claude/skills/archons-rise-roadmap/milestones.md`, `status.md`, `decisions-log.md`

---

## How to run EditMode tests

The Unity editor is usually **open** with this project (check `Get-Process Unity`), which holds the project lock and blocks CLI `-runTests -batchmode`. Two paths:

**A. Pure-class RED/GREEN loop (primary during implementation)** — compile the pure sources + tests with `csc` against Unity's bundled NUnit and run them with a tiny reflection runner. One-time setup — write this runner to the scratchpad as `Runner.cs`:

```csharp
using System;
using System.Reflection;
using NUnit.Framework;

static class Runner
{
    static int Main(string[] args)
    {
        int failed = 0, passed = 0;
        var asm = Assembly.LoadFrom(args[0]);
        foreach (var t in asm.GetTypes())
        foreach (var m in t.GetMethods())
        {
            if (m.GetCustomAttribute<TestAttribute>() == null) continue;
            var obj = Activator.CreateInstance(t);
            try { m.Invoke(obj, null); passed++; Console.WriteLine($"PASS {t.Name}.{m.Name}"); }
            catch (Exception e) { failed++; Console.WriteLine($"FAIL {t.Name}.{m.Name}: {e.InnerException?.Message ?? e.Message}"); }
        }
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
```

Then per run (PowerShell, from the repo root; `$scratch` = the session scratchpad dir):

```powershell
$nunit = (Get-ChildItem "Library\PackageCache\com.unity.ext.nunit*\net472\unity-custom\nunit.framework.dll")[0].FullName
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
Copy-Item $nunit $scratch
& $csc /nologo /t:library "/out:$scratch\M2Tests.dll" "/r:$nunit" `
    Assets\Scripts\Places\PlaceType.cs Assets\Scripts\Places\PlaceService.cs `
    Assets\Scripts\Places\PlaceRules.cs Assets\Scripts\Places\ConquestLedger.cs `
    Assets\Scripts\SaveData\SaveModels.cs Assets\Scripts\SaveData\SaveMigrator.cs `
    Assets\Tests\EditMode\PlaceRulesTests.cs Assets\Tests\EditMode\ConquestLedgerTests.cs `
    Assets\Scripts\SaveData\Tests\SaveMigratorTests.cs
& $csc /nologo "/out:$scratch\Runner.exe" "/r:$scratch\nunit.framework.dll" $scratch\Runner.cs
Push-Location $scratch; & .\Runner.exe M2Tests.dll; Pop-Location
```

(Include in the file list only the sources that exist yet at that task. `SaveModels.cs` and `SaveMigrator.cs` are UnityEngine-free by design so they csc-compile; `SaveSerializer.cs` is NOT — never add it to this list.)

**B. Unity Test Runner (authoritative)** — after each pure-logic task, ask the user to confirm in **Window ▸ General ▸ Test Runner ▸ EditMode** at their next editor focus. The `SaveSerializerTests` round-trip extension (Task 2) runs **only** here because it needs `JsonUtility`.

---

### Task 1: `ArchonsRise.Places` assembly — `PlaceType`, `PlaceService`, `PlaceRules`

**Files:**
- Create: `Assets/Scripts/Places/ArchonsRise.Places.asmdef`
- Create: `Assets/Scripts/Places/PlaceType.cs`
- Create: `Assets/Scripts/Places/PlaceService.cs`
- Create: `Assets/Scripts/Places/PlaceRules.cs`
- Modify: `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`
- Test: `Assets/Tests/EditMode/PlaceRulesTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces (global namespace, like `DrawGate`):
  - `enum PlaceType { Town = 0, Keep = 1, Castle = 2 }`
  - `[Flags] enum PlaceService { None = 0, Recruit = 1, Heal = 2, Cards = 4 }`
  - `static PlaceService PlaceRules.AllowedServices(PlaceType type)`
  - `static bool PlaceRules.IsConquered(int defeatedCount, int rosterSize)`
  - `const int PlaceRules.RetreatWoundCount = 3`

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/PlaceRulesTests.cs`:

```csharp
using NUnit.Framework;

public class PlaceRulesTests
{
    [Test]
    public void AllowedServices_Town_RecruitAndHeal()
    {
        Assert.AreEqual(PlaceService.Recruit | PlaceService.Heal,
            PlaceRules.AllowedServices(PlaceType.Town));
    }

    [Test]
    public void AllowedServices_Keep_RecruitOnly()
    {
        Assert.AreEqual(PlaceService.Recruit, PlaceRules.AllowedServices(PlaceType.Keep));
    }

    [Test]
    public void AllowedServices_Castle_RecruitHealCards()
    {
        Assert.AreEqual(PlaceService.Recruit | PlaceService.Heal | PlaceService.Cards,
            PlaceRules.AllowedServices(PlaceType.Castle));
    }

    [Test]
    public void IsConquered_FalseBelowRoster_TrueAtRoster()
    {
        Assert.IsFalse(PlaceRules.IsConquered(0, 2));
        Assert.IsFalse(PlaceRules.IsConquered(1, 2));
        Assert.IsTrue(PlaceRules.IsConquered(2, 2));
    }

    [Test]
    public void IsConquered_EmptyRoster_TrueImmediately()
    {
        Assert.IsTrue(PlaceRules.IsConquered(0, 0)); // a Town has no guardians
    }

    [Test]
    public void RetreatWoundCount_IsThree()
    {
        Assert.AreEqual(3, PlaceRules.RetreatWoundCount);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run harness path A (file list: `PlaceType.cs PlaceService.cs PlaceRules.cs` + this test — the files don't exist yet, so compile fails with missing-file/missing-type errors). Expected: **csc error** (types undefined).

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Places/PlaceType.cs`:

```csharp
// The typed map-place taxonomy. Dungeons are conceptually the 4th place type
// but keep their own existing implementation and are not conquerable-for-win.
public enum PlaceType
{
    Town = 0,
    Keep = 1,
    Castle = 2,
}
```

Create `Assets/Scripts/Places/PlaceService.cs`:

```csharp
using System;

// Services a place can offer once its services are open (conquered, or a
// guardian-less Town). Derived from PlaceType via PlaceRules so designers
// cannot author invalid combos; the legacy TownsSO.activity flags no longer
// drive availability (except the Crystal button — see CrystalButton).
[Flags]
public enum PlaceService
{
    None = 0,
    Recruit = 1,
    Heal = 2,
    Cards = 4,
}
```

Create `Assets/Scripts/Places/PlaceRules.cs`:

```csharp
// Pure rules for the place-type system. Thresholds and service tables live
// here so balance is centralized. No scene dependency.
public static class PlaceRules
{
    // Fleeing an assault with guardians remaining (vs. 1 wound for field-combat flee).
    public const int RetreatWoundCount = 3;

    public static PlaceService AllowedServices(PlaceType type)
    {
        switch (type)
        {
            case PlaceType.Keep: return PlaceService.Recruit;
            case PlaceType.Castle: return PlaceService.Recruit | PlaceService.Heal | PlaceService.Cards;
            default: return PlaceService.Recruit | PlaceService.Heal; // Town
        }
    }

    public static bool IsConquered(int defeatedCount, int rosterSize)
        => defeatedCount >= rosterSize;
}
```

Create `Assets/Scripts/Places/ArchonsRise.Places.asmdef`:

```json
{
    "name": "ArchonsRise.Places",
    "rootNamespace": "",
    "references": ["ArchonsRise.SaveData"],
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

In `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`, extend `references` to:

```json
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "ArchonsRise.CardPlay",
        "ArchonsRise.Enums",
        "ArchonsRise.Hand",
        "ArchonsRise.Places",
        "ArchonsRise.SaveData"
    ],
```

- [ ] **Step 4: Run to verify it passes**

Harness path A again. Expected: `6 passed, 0 failed`, exit 0.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Places Assets/Tests/EditMode/PlaceRulesTests.cs Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef
git commit -m "feat: PlaceType taxonomy + PlaceRules pure service/conquest rules"
```

(Unity will generate `.meta` files for the new folder/files next time it focuses; commit them with the next task if they appear. Ask the user to confirm the suite in Test Runner path B at next editor focus.)

---

### Task 2: Save schema v2 — `PlaceConquest`, `RunState.places`, `SaveMigrator`

**Files:**
- Modify: `Assets/Scripts/SaveData/SaveModels.cs`
- Create: `Assets/Scripts/SaveData/SaveMigrator.cs`
- Test: `Assets/Scripts/SaveData/Tests/SaveMigratorTests.cs`
- Modify (test): `Assets/Scripts/SaveData/Tests/SaveSerializerTests.cs`

**Interfaces:**
- Consumes: existing `ArchonsRise.SaveData` DTOs.
- Produces:
  - `ArchonsRise.SaveData.PlaceConquest { int x; int y; int defeatedCount; }`
  - `RunState.places : PlaceConquest[]` (defaults to empty)
  - `SaveFile.schemaVersion` default **2**
  - `static SaveFile SaveMigrator.Migrate(SaveFile file)` — idempotent, never null-`places`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Scripts/SaveData/Tests/SaveMigratorTests.cs`:

```csharp
using NUnit.Framework;
using ArchonsRise.SaveData;

namespace ArchonsRise.SaveData.Tests
{
    public class SaveMigratorTests
    {
        [Test]
        public void V1_MigratesToV2_WithNothingConquered()
        {
            var v1 = new SaveFile { schemaVersion = 1 };
            v1.run.places = null; // simulate a field absent from old JSON

            var migrated = SaveMigrator.Migrate(v1);

            Assert.AreEqual(2, migrated.schemaVersion);
            Assert.IsNotNull(migrated.run.places);
            Assert.AreEqual(0, migrated.run.places.Length);
        }

        [Test]
        public void V2_IsUntouched()
        {
            var v2 = new SaveFile(); // defaults to schemaVersion 2
            v2.run.places = new[] { new PlaceConquest { x = 3, y = 4, defeatedCount = 1 } };

            var migrated = SaveMigrator.Migrate(v2);

            Assert.AreEqual(2, migrated.schemaVersion);
            Assert.AreEqual(1, migrated.run.places.Length);
            Assert.AreEqual(1, migrated.run.places[0].defeatedCount);
        }
    }
}
```

In `Assets/Scripts/SaveData/Tests/SaveSerializerTests.cs`, inside the `RoundTrip_PreservesAllFields` initializer, change `schemaVersion = 1,` to `schemaVersion = 2,` and add after `unitIds = new[] { "unit_knight" },`:

```csharp
                    places = new[]
                    {
                        new PlaceConquest { x = 5, y = 9, defeatedCount = 1 },
                        new PlaceConquest { x = 8, y = 3, defeatedCount = 2 }
                    },
```

- [ ] **Step 2: Run to verify failure**

Harness path A with file list `SaveModels.cs SaveMigrator.cs Tests/SaveMigratorTests.cs`. Expected: **csc error** — `PlaceConquest` / `SaveMigrator` undefined. (The `SaveSerializerTests` change is Unity-only; it fails to compile in the editor until Step 3, which is its RED.)

- [ ] **Step 3: Write the implementation**

In `Assets/Scripts/SaveData/SaveModels.cs`:

1. Change `public int schemaVersion = 1;` to:

```csharp
        // v2 (M2): adds RunState.places (guardian-conquest progress).
        public int schemaVersion = 2;
```

2. In `RunState`, after `public MapState map = new MapState();` add:

```csharp
        // One entry per place with defeatedCount > 0; keyed by grid cell.
        // Guardians die in order and never respawn, so a single count fully
        // captures a place's conquest state.
        public PlaceConquest[] places = Array.Empty<PlaceConquest>();
```

3. At the bottom of the namespace (after `Cell`), add:

```csharp
    [Serializable]
    public struct PlaceConquest
    {
        public int x;
        public int y;
        public int defeatedCount;
    }
```

Create `Assets/Scripts/SaveData/SaveMigrator.cs`:

```csharp
using System;

namespace ArchonsRise.SaveData
{
    // Upgrades old save files in place. v1 -> v2: the places array did not
    // exist; absent means nothing conquered. Idempotent; UnityEngine-free.
    public static class SaveMigrator
    {
        public static SaveFile Migrate(SaveFile file)
        {
            if (file.run.places == null)
                file.run.places = Array.Empty<PlaceConquest>();
            if (file.schemaVersion < 2)
                file.schemaVersion = 2;
            return file;
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Harness path A (same file list). Expected: `2 passed, 0 failed`. Ask the user to run the full EditMode suite (path B) — including the extended `RoundTrip_PreservesAllFields` — at next editor focus.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/SaveData
git commit -m "feat: save schema v2 - PlaceConquest array + v1 default-fill migration"
```

---

### Task 3: `ConquestLedger` — pure conquest registry

**Files:**
- Create: `Assets/Scripts/Places/ConquestLedger.cs`
- Test: `Assets/Tests/EditMode/ConquestLedgerTests.cs`

**Interfaces:**
- Consumes: `PlaceType`, `PlaceRules` (Task 1); `ArchonsRise.SaveData.Cell`, `PlaceConquest` (Task 2).
- Produces (global namespace):
  - `void Register(Cell cell, PlaceType type, int rosterSize)` — idempotent; preserves an already-applied `defeatedCount`.
  - `int DefeatedCount(Cell cell)` — 0 when unknown.
  - `void RecordDefeat(Cell cell)`
  - `bool IsConquered(Cell cell)` — false for unregistered cells.
  - `int ConqueredCastleCount()`
  - `PlaceConquest[] Export()` — only entries with `defeatedCount > 0`.
  - `void ApplySavedCount(int x, int y, int defeatedCount)` — tolerant of arriving before OR after `Register`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/ConquestLedgerTests.cs`:

```csharp
using NUnit.Framework;
using ArchonsRise.SaveData;

public class ConquestLedgerTests
{
    [Test]
    public void Progression_AdvancesAndConquersAtRosterSize()
    {
        var ledger = new ConquestLedger();
        var keep = new Cell(4, 7);
        ledger.Register(keep, PlaceType.Keep, 1);

        Assert.AreEqual(0, ledger.DefeatedCount(keep));
        Assert.IsFalse(ledger.IsConquered(keep));

        ledger.RecordDefeat(keep);
        Assert.AreEqual(1, ledger.DefeatedCount(keep));
        Assert.IsTrue(ledger.IsConquered(keep));
    }

    [Test]
    public void Town_WithEmptyRoster_IsConqueredImmediately()
    {
        var ledger = new ConquestLedger();
        var town = new Cell(1, 1);
        ledger.Register(town, PlaceType.Town, 0);
        Assert.IsTrue(ledger.IsConquered(town));
    }

    [Test]
    public void UnregisteredCell_NotConquered_ZeroCount()
    {
        var ledger = new ConquestLedger();
        Assert.IsFalse(ledger.IsConquered(new Cell(9, 9)));
        Assert.AreEqual(0, ledger.DefeatedCount(new Cell(9, 9)));
    }

    [Test]
    public void ConqueredCastleCount_CountsOnlyConqueredCastles()
    {
        var ledger = new ConquestLedger();
        var castleDone = new Cell(2, 2);
        var castleHalf = new Cell(3, 3);
        var keepDone = new Cell(4, 4);
        ledger.Register(castleDone, PlaceType.Castle, 2);
        ledger.Register(castleHalf, PlaceType.Castle, 2);
        ledger.Register(keepDone, PlaceType.Keep, 1);

        ledger.RecordDefeat(castleDone);
        ledger.RecordDefeat(castleDone);
        ledger.RecordDefeat(castleHalf);
        ledger.RecordDefeat(keepDone);

        Assert.AreEqual(1, ledger.ConqueredCastleCount());
    }

    [Test]
    public void Export_OnlyEntriesWithProgress()
    {
        var ledger = new ConquestLedger();
        ledger.Register(new Cell(2, 2), PlaceType.Castle, 2);
        ledger.Register(new Cell(5, 5), PlaceType.Keep, 1);
        ledger.RecordDefeat(new Cell(2, 2));

        var exported = ledger.Export();
        Assert.AreEqual(1, exported.Length);
        Assert.AreEqual(2, exported[0].x);
        Assert.AreEqual(2, exported[0].y);
        Assert.AreEqual(1, exported[0].defeatedCount);
    }

    [Test]
    public void ApplySavedCount_BeforeOrAfterRegister_BothRestore()
    {
        var before = new ConquestLedger();
        before.ApplySavedCount(6, 6, 1);
        before.Register(new Cell(6, 6), PlaceType.Castle, 2);
        Assert.AreEqual(1, before.DefeatedCount(new Cell(6, 6)));
        Assert.IsFalse(before.IsConquered(new Cell(6, 6)));

        var after = new ConquestLedger();
        after.Register(new Cell(6, 6), PlaceType.Castle, 2);
        after.ApplySavedCount(6, 6, 2);
        Assert.IsTrue(after.IsConquered(new Cell(6, 6)));
        Assert.AreEqual(1, after.ConqueredCastleCount());
    }
}
```

- [ ] **Step 2: Run to verify failure**

Harness path A (full pure file list from the harness section, minus `SaveMigratorTests.cs` if you prefer — both fine). Expected: **csc error** — `ConquestLedger` undefined.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Places/ConquestLedger.cs`:

```csharp
using System.Collections.Generic;
using ArchonsRise.SaveData;

// Pure conquest registry: grid cell -> (place type, roster size, guardians
// defeated). The MonoBehaviour ConquestTracker wraps one of these per run.
// Restore order is not guaranteed (a saved count may arrive before the place
// registers itself), so entries are created on first touch from either side.
public class ConquestLedger
{
    private class Entry
    {
        public PlaceType type;
        public int rosterSize;
        public int defeatedCount;
    }

    private readonly Dictionary<Cell, Entry> entries = new Dictionary<Cell, Entry>();

    public void Register(Cell cell, PlaceType type, int rosterSize)
    {
        var e = GetOrCreate(cell);
        e.type = type;
        e.rosterSize = rosterSize;
    }

    public int DefeatedCount(Cell cell)
        => entries.TryGetValue(cell, out var e) ? e.defeatedCount : 0;

    public void RecordDefeat(Cell cell) => GetOrCreate(cell).defeatedCount++;

    public bool IsConquered(Cell cell)
        => entries.TryGetValue(cell, out var e)
           && PlaceRules.IsConquered(e.defeatedCount, e.rosterSize);

    public int ConqueredCastleCount()
    {
        int count = 0;
        foreach (var e in entries.Values)
            if (e.type == PlaceType.Castle && PlaceRules.IsConquered(e.defeatedCount, e.rosterSize))
                count++;
        return count;
    }

    public PlaceConquest[] Export()
    {
        var result = new List<PlaceConquest>();
        foreach (var kv in entries)
            if (kv.Value.defeatedCount > 0)
                result.Add(new PlaceConquest
                {
                    x = kv.Key.x,
                    y = kv.Key.y,
                    defeatedCount = kv.Value.defeatedCount
                });
        return result.ToArray();
    }

    public void ApplySavedCount(int x, int y, int defeatedCount)
        => GetOrCreate(new Cell(x, y)).defeatedCount = defeatedCount;

    private Entry GetOrCreate(Cell cell)
    {
        if (!entries.TryGetValue(cell, out var e))
        {
            e = new Entry();
            entries[cell] = e;
        }
        return e;
    }
}
```

- [ ] **Step 4: Run to verify pass**

Harness path A with the full pure file list. Expected: **all Task 1–3 tests pass** (6 + 2 + 6 = `14 passed, 0 failed`).

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Places/ConquestLedger.cs Assets/Tests/EditMode/ConquestLedgerTests.cs
git commit -m "feat: ConquestLedger pure conquest registry with save import/export"
```

---

### Task 4: `TownsSO` fields, `TownToken.gridPos`, `ConquestTracker`, `GridGeneration`

Unity-side wiring; no pure test cycle. Verified by compile (editor console clean) + Task 10's in-scene checklist.

**Files:**
- Modify: `Assets/Scripts/GameScriptableObjectTypes/TownsSO.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs`
- Create: `Assets/Scripts/Managers/ConquestTracker.cs`
- Modify: `Assets/Scripts/TilemapScripts/GridGeneration.cs`

**Interfaces:**
- Consumes: `PlaceType`, `ConquestLedger` (Tasks 1, 3); `ArchonsRise.SaveData.Cell`, `PlaceConquest` (Task 2).
- Produces:
  - `TownsSO.placeType : PlaceType`, `TownsSO.guardians : List<EnemiesSO>`
  - `TownToken.gridPos : Vector3Int` (set by `GridGeneration` at spawn)
  - `ConquestTracker.Instance : ConquestTracker` (lazy, scene-scoped)
  - `ConquestTracker` methods: `Register(Vector3Int, PlaceType, int)`, `DefeatedCount(Vector3Int) -> int`, `RecordDefeat(Vector3Int)`, `IsConquered(Vector3Int) -> bool`, `ConqueredCastleCount() -> int`, `ExportPlaces() -> PlaceConquest[]`, `ApplySave(PlaceConquest[])`

- [ ] **Step 1: Extend `TownsSO`**

In `Assets/Scripts/GameScriptableObjectTypes/TownsSO.cs`, after `public TownSize townSize;` add:

```csharp
    // Typed place taxonomy (M2). Service availability derives from this via
    // PlaceRules, superseding the legacy activity flags below (kept because
    // CrystalButton still reads Resources from them).
    public PlaceType placeType;
    // Conquest roster, fought in order; empty for a Town. Guardian counts are
    // data-driven: assault logic reads guardians.Count, never a constant.
    public List<EnemiesSO> guardians = new List<EnemiesSO>();
```

- [ ] **Step 2: Create `ConquestTracker`**

Create `Assets/Scripts/Managers/ConquestTracker.cs`:

```csharp
using UnityEngine;
using ArchonsRise.SaveData;

// Runtime conquest registry for the current run. A dedicated component (not
// bolted onto GameManager) wrapping the pure ConquestLedger. Lazily creates
// its own scene GameObject so no scene edit is needed; being scene-scoped
// (no DontDestroyOnLoad) means a new run naturally starts blank.
public class ConquestTracker : MonoBehaviour
{
    private readonly ConquestLedger ledger = new ConquestLedger();

    private static ConquestTracker instance;
    public static ConquestTracker Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("ConquestTracker").AddComponent<ConquestTracker>();
            return instance;
        }
    }

    public void Register(Vector3Int cell, PlaceType type, int rosterSize)
        => ledger.Register(ToCell(cell), type, rosterSize);

    public int DefeatedCount(Vector3Int cell) => ledger.DefeatedCount(ToCell(cell));

    public void RecordDefeat(Vector3Int cell) => ledger.RecordDefeat(ToCell(cell));

    public bool IsConquered(Vector3Int cell) => ledger.IsConquered(ToCell(cell));

    public int ConqueredCastleCount() => ledger.ConqueredCastleCount();

    public PlaceConquest[] ExportPlaces() => ledger.Export();

    public void ApplySave(PlaceConquest[] places)
    {
        if (places == null) return;
        foreach (var p in places)
            ledger.ApplySavedCount(p.x, p.y, p.defeatedCount);
    }

    private static Cell ToCell(Vector3Int v) => new Cell(v.x, v.y);
}
```

- [ ] **Step 3: Extend `TownToken`**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs`, add a `gridPos` field, a `Start` that registers with the tracker (mirrors `EnemyToken.gridPos` for stable identity over the seeded map), and an on-the-cell guard: places open only while the player is **standing on** them, not adjacent (user decision 2026-07-02).

```csharp
public class TownToken : MonoBehaviour, IPointerClickHandler
{
    public TownsSO townSO;
    // Stable identity over the seeded map; assigned by GridGeneration at spawn.
    public Vector3Int gridPos;
    [SerializeField] TownDeck deck;
    [SerializeField] TownEvent onClick_OpenTownMenu;
    [SerializeField] TownEvent onClick_GetTownData;
    private PlayerPosition player;
    private Grid gameboard;

    void Start()
    {
        player = FindAnyObjectByType<PlayerPosition>();
        gameboard = FindAnyObjectByType<Grid>();
        ConquestTracker.Instance.Register(gridPos, townSO.placeType, townSO.guardians.Count);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Places are entered, not reached into: the player must be standing on
        // this cell (adjacency is enough for enemies, not for places).
        if (gameboard.LocalToCell(player.transform.position) != gridPos)
        {
            GameManager.Instance.ValidationMessage(
                $"You must be standing in {townSO.cardName} to enter it.");
            return;
        }

        GameManager.Instance.townCanvas.enabled = true;
        deck.CreateTown(this);
        onClick_GetTownData.Raise(this);
        onClick_OpenTownMenu.Raise(this);
    }
}
```

- [ ] **Step 4: `GridGeneration` — assign `gridPos`, pick over the full towns list, guarantee 2 Castles**

In `Assets/Scripts/TilemapScripts/GridGeneration.cs`, replace the town-spawning loop:

```csharp
        for(int x = 3; x < 18; x+=(Rng(5,7)))
        {
            for(int y = 3; y < 18; y+=(Rng(5,7)))
            {
                var tilePos = new Vector3Int(x,y);
                ground.SetTile(tilePos, townTile);
                var tile = ground.GetTile<TownRuleTile>(tilePos);
                var townToken = Instantiate(tile.m_DefaultGameObject, ground.CellToWorld(tilePos)+ new Vector3(0,-1), Quaternion.identity, townParentObject);
                townToken.GetComponent<TownToken>().townSO = towns.towns[Rng(0,3)];
            }
        }
```

with:

```csharp
        var placedTowns = new List<TownToken>();
        for(int x = 3; x < 18; x+=(Rng(5,7)))
        {
            for(int y = 3; y < 18; y+=(Rng(5,7)))
            {
                var tilePos = new Vector3Int(x,y);
                ground.SetTile(tilePos, townTile);
                var tile = ground.GetTile<TownRuleTile>(tilePos);
                var townToken = Instantiate(tile.m_DefaultGameObject, ground.CellToWorld(tilePos)+ new Vector3(0,-1), Quaternion.identity, townParentObject);
                var placed = townToken.GetComponent<TownToken>();
                placed.townSO = towns.towns[Rng(0, towns.towns.Count)];
                placed.gridPos = tilePos;
                placedTowns.Add(placed);
            }
        }

        // M2.5's victory is "conquer 2 Castles", so the seeded map must always
        // contain at least 2. Upgrade the last-placed non-Castle tokens if the
        // random picks came up short. Deterministic over the seed (consumes no
        // extra RNG draws) and runs before the tokens' Start, so conquest
        // registration sees the final types.
        var castleSO = towns.towns.Find(t => t.placeType == PlaceType.Castle);
        if (castleSO != null)
        {
            int castles = 0;
            foreach (var t in placedTowns)
                if (t.townSO.placeType == PlaceType.Castle) castles++;
            for (int i = placedTowns.Count - 1; i >= 0 && castles < 2; i--)
                if (placedTowns[i].townSO.placeType != PlaceType.Castle)
                {
                    placedTowns[i].townSO = castleSO;
                    castles++;
                }
        }
```

Note the count change `Rng(0,3)` → `Rng(0, towns.towns.Count)`: the number of RNG draws is unchanged, so tile layout and enemy spawns for an existing seed are identical; only which town asset lands where can shift. v1 saves carry no conquest state, so this is cosmetic.

- [ ] **Step 5: Compile check + commit**

Ask the user to focus the Unity editor (or run `Get-Process Unity` — if it is NOT running, open the project once) and confirm the console shows no compile errors. Then:

```powershell
git add Assets/Scripts/GameScriptableObjectTypes/TownsSO.cs Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs Assets/Scripts/Managers/ConquestTracker.cs Assets/Scripts/TilemapScripts/GridGeneration.cs
git commit -m "feat: typed TownsSO with guardian roster; TownToken gridPos + ConquestTracker registration; seeded map guarantees 2 Castles"
```

(Also `git add` any newly generated `.meta` files for Task 1–4 files before committing.)

---

### Task 5: `GuardianAssault` + retreat via the Flee button + turn-button gating

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/GameBoardObjects/GuardianAssault.cs`
- Modify: `Assets/Scripts/GameObjectScripts/DeckScripts/EnemyDeck.cs`
- Modify: `Assets/Scripts/Managers/GameManager.cs`
- Modify: `Assets/Scripts/GameObjectScripts/DeckScripts/EndTurnButton.cs:37-39`
- Modify: `Assets/Scripts/GameObjectScripts/DeckScripts/EndRoundButton.cs:27-28`

**Interfaces:**
- Consumes: `ConquestTracker.Instance` (Task 4), `PlaceRules.RetreatWoundCount` (Task 1), existing `EnemyCard`, `PlayerHand.AddWound()`, `GameManager` canvases.
- Produces:
  - `GuardianAssault.Instance : GuardianAssault` (lazy, scene-scoped)
  - `GuardianAssault.Begin(TownToken place)`, `GuardianAssault.Retreat()`, `bool InProgress`, `static bool AnyInProgress`
  - `EnemyDeck.PrefabEnemyCard : GameObject`
  - `GameManager.CloseCombatCanvas()`

**Design notes (retreat semantics, locked in the spec):** the 3-wound penalty applies to abandoning an assault **in progress** ("a failed assault costs the player 3 wounds"); closing the place menu without pressing Assault is free. The existing Flee button doubles as the assault's Retreat action — `GameManager.FleeCombat()` delegates to the assault when one is active, so no new combat-canvas button is needed. Guardians chain within one assault: defeating one auto-spawns the next (multiple cards under `enemyCardCombatPosition` is an existing pattern — `CheckCombatants` counts children).

- [ ] **Step 1: Expose the enemy-card prefab on `EnemyDeck`**

In `Assets/Scripts/GameObjectScripts/DeckScripts/EnemyDeck.cs`, after `[SerializeField] GameObject prefabEnemyCard;` add:

```csharp
    // GuardianAssault spawns the same combat card without a scene reference.
    public GameObject PrefabEnemyCard => prefabEnemyCard;
```

- [ ] **Step 2: Create `GuardianAssault`**

Create `Assets/Scripts/GameObjectScripts/GameBoardObjects/GuardianAssault.cs`:

```csharp
using UnityEngine;

// Drives a resumable assault on one guarded place. Modeled on
// Dungeon.SpawnDungeonEnemy's sequential spawn but with conquest semantics;
// kept separate so dungeon behavior is untouched. Guardians are fought in
// order (guardians[defeatedCount]); defeated guardians never respawn. The
// next guardian auto-spawns when the previous one falls; the assault ends
// when the roster is exhausted (conquered) or the player retreats (3 wounds,
// progress kept — GameManager.FleeCombat delegates here).
public class GuardianAssault : MonoBehaviour
{
    private TownToken place;
    private EnemyCard activeCard;
    private bool defeatRecorded;

    private static GuardianAssault instance;
    public static GuardianAssault Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("GuardianAssault").AddComponent<GuardianAssault>();
            return instance;
        }
    }

    public bool InProgress => place != null;

    // Read by turn/round gating and FleeCombat without lazily creating the singleton.
    public static bool AnyInProgress => instance != null && instance.InProgress;

    public void Begin(TownToken town)
    {
        place = town;
        // Tear down the place menu the button click came from.
        foreach (var card in FindObjectsByType<TownCard>(FindObjectsSortMode.None))
            Destroy(card.gameObject);
        GameManager.Instance.townCanvas.enabled = false;
        GameManager.Instance.CombatCanvasActive();
        SpawnNextGuardian();
    }

    private void Update()
    {
        if (place == null || activeCard == null) return;

        if (activeCard.IsDefeated && !defeatRecorded)
        {
            defeatRecorded = true;
            ConquestTracker.Instance.RecordDefeat(place.gridPos);

            if (ConquestTracker.Instance.IsConquered(place.gridPos))
            {
                GameManager.Instance.ValidationMessage(
                    $"{place.townSO.cardName} is conquered! Its services are now open to you.");
                place = null; // combat canvas closes via CheckCombatants on card click
                activeCard = null;
            }
            else
            {
                // Chain the next guardian now so the canvas-close check
                // (CheckCombatants childCount == 1) keeps the fight open.
                SpawnNextGuardian();
            }
        }
    }

    public void Retreat()
    {
        if (!InProgress) return;

        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        for (int i = 0; i < PlaceRules.RetreatWoundCount; i++)
            hand.AddWound();

        foreach (var card in GameManager.Instance.enemyCardCombatPosition
                     .GetComponentsInChildren<EnemyCard>())
            Destroy(card.gameObject);

        place = null;
        activeCard = null;
        GameManager.Instance.CloseCombatCanvas();
        GameManager.Instance.ValidationMessage(
            $"You retreat from the assault and suffer {PlaceRules.RetreatWoundCount} wounds! Your progress is not lost.");
    }

    private void SpawnNextGuardian()
    {
        var roster = place.townSO.guardians;
        var next = roster[ConquestTracker.Instance.DefeatedCount(place.gridPos)];

        var prefab = FindAnyObjectByType<EnemyDeck>().PrefabEnemyCard;
        var cardObject = Instantiate(prefab,
            GameManager.Instance.enemyCardCombatPosition.transform);
        cardObject.transform.localPosition = Vector3.zero;
        cardObject.transform.localScale = new Vector3(1.75f, 1.75f);
        activeCard = cardObject.GetComponent<EnemyCard>();
        activeCard.enemySO = next;
        defeatRecorded = false;
    }
}
```

- [ ] **Step 3: `GameManager` — shared teardown + retreat delegation**

In `Assets/Scripts/Managers/GameManager.cs`:

1. Add after `EndCombat()`:

```csharp
    // Shared canvas teardown for every non-victory combat exit (token flee,
    // assault retreat).
    public void CloseCombatCanvas()
    {
        combatCanvas.enabled = false;
        combatCanvas.GetComponentInChildren<Animator>().enabled = false;
        EndCombat();
    }
```

2. Rewrite `FleeCombat()` to delegate and reuse the teardown:

```csharp
    // Player gives up the current fight. During a guardian assault the Flee
    // button acts as Retreat (3 wounds, conquest progress kept); in field
    // combat it takes one wound and de-aggros the engaged token.
    public void FleeCombat()
    {
        if (GuardianAssault.AnyInProgress)
        {
            GuardianAssault.Instance.Retreat();
            return;
        }

        // Guard: activeCombatant is set only by a real fight, never while the
        // combat canvas is merely previewing an out-of-range enemy token.
        if (activeCombatant == null) return;

        playerHand.GetComponent<PlayerHand>().AddWound();

        foreach (var card in enemyCardCombatPosition.GetComponentsInChildren<EnemyCard>())
            Destroy(card.gameObject);

        activeCombatant.isAggro = false;
        if (activeCombatant.player != null)
            activeCombatant.player.inCombat = false;

        CloseCombatCanvas();

        ValidationMessage("You flee the battle and suffer a wound!");
    }
```

(Delete the four lines the new `CloseCombatCanvas()` call replaces: the two `combatCanvas` lines and the `EndCombat()` call.)

- [ ] **Step 4: Turn/round buttons treat an assault as an active fight**

`Assets/Scripts/GameObjectScripts/DeckScripts/EndTurnButton.cs` — change the gate call to:

```csharp
        endTurnButton.interactable = TurnButtonGate.EndTurn(
            GameManager.Instance.activeCombatant != null || GuardianAssault.AnyInProgress,
            DrawGate.Evaluate(deck.CardsInDeck.Count, hand.cardsInPlay.Count, player.PlayerHandSize));
```

`Assets/Scripts/GameObjectScripts/DeckScripts/EndRoundButton.cs` — change to:

```csharp
        endRoundButton.interactable = TurnButtonGate.EndRound(
            GameManager.Instance.activeCombatant != null || GuardianAssault.AnyInProgress);
```

- [ ] **Step 5: Compile check + commit**

User confirms a clean Unity console, then:

```powershell
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/GuardianAssault.cs Assets/Scripts/GameObjectScripts/DeckScripts/EnemyDeck.cs Assets/Scripts/Managers/GameManager.cs Assets/Scripts/GameObjectScripts/DeckScripts/EndTurnButton.cs Assets/Scripts/GameObjectScripts/DeckScripts/EndRoundButton.cs
git commit -m "feat: GuardianAssault resumable conquest combat; Flee doubles as 3-wound Retreat; turn buttons gate on assaults"
```

---

### Task 6: Town-menu service gating + `AssaultButton` class

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/RecruitButton.cs`
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/HealButton.cs`
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/CardButton.cs`
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/CrystalButton.cs`
- Create: `Assets/Scripts/GameObjectScripts/TownMenuScripts/AssaultButton.cs` (Unity generates its `.meta` on import — commit it too)

**Interfaces:**
- Consumes: `PlaceRules.AllowedServices`, `PlaceService` (Task 1), `ConquestTracker.Instance.IsConquered/DefeatedCount` (Task 4), `GuardianAssault.Instance.Begin` (Task 5), existing `TownButtons` base.
- Produces: `AssaultButton : TownButtons` (scene-wired in Task 7).

Menu contract (spec): **unconquered guarded place** → Assault visible, all service buttons hidden ("Retreat" pre-assault = closing the menu via the town card, free). **Conquered place or Town** → the type's services show, Assault hides. Cards on a conquered Castle is present-but-disabled.

- [ ] **Step 1: Rewrite `RecruitButton.UpdateButtonText`**

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecruitButton : TownButtons
{
    private void Update() {
        if (_town is not null)
            if(currentPlayerInfluence < _town.townSO.recruitLevel)
                thisButton.interactable = false;
    }
    public override void UpdateButtonText()
    {
        if (_town is not null)
        {
            buttonText.text = "Recruit " + _town.townSO.recruitLevel.ToString();
            bool allowed = PlaceRules.AllowedServices(_town.townSO.placeType).HasFlag(PlaceService.Recruit);
            bool open = ConquestTracker.Instance.IsConquered(_town.gridPos);
            if (allowed && open)
            {
                thisButton.gameObject.SetActive(true);
                if(currentPlayerInfluence < _town.townSO.recruitLevel)
                {
                    thisButton.interactable = false;
                }
                else
                    thisButton.interactable = true;
                    thisButton.onClick.RemoveAllListeners();
                    thisButton.onClick.AddListener(() => townEvent.Raise(_town));
                    thisButton.onClick.AddListener(() => influenceCostEvent.Raise(_town.townSO.recruitLevel));
            }
            else
            {
                thisButton.gameObject.SetActive(false);
            }
        }
    }
}
```

(The only change is the `if` condition: `activity.HasFlag(...)` → `allowed && open`. The dangling-indentation listener wiring is pre-existing style — leave it.)

- [ ] **Step 2: Same change in `HealButton.cs`**

Identical pattern with `PlaceService.Heal` and `healLevel`:

```csharp
            buttonText.text = "Heal " + _town.townSO.healLevel.ToString();
            bool allowed = PlaceRules.AllowedServices(_town.townSO.placeType).HasFlag(PlaceService.Heal);
            bool open = ConquestTracker.Instance.IsConquered(_town.gridPos);
            if (allowed && open)
```

(rest of the method body unchanged).

- [ ] **Step 3: `CardButton.cs` — visible-but-stubbed on a conquered Castle**

Replace `UpdateButtonText` with:

```csharp
    public override void UpdateButtonText()
    {
        if (_town is not null)
        {
            bool allowed = PlaceRules.AllowedServices(_town.townSO.placeType).HasFlag(PlaceService.Cards);
            bool open = ConquestTracker.Instance.IsConquered(_town.gridPos);
            if (allowed && open)
            {
                // M2 stub: the Castle card shop is a deferred follow-up. The
                // button is present so the service slot is visible, but buying
                // is disabled until the purchase economics land.
                thisButton.gameObject.SetActive(true);
                buttonText.text = "Cards (soon)";
                thisButton.interactable = false;
                thisButton.onClick.RemoveAllListeners();
            }
            else
            {
                thisButton.gameObject.SetActive(false);
            }
        }
    }
```

Also delete `CardButton`'s `Update()` method entirely (it only force-disabled on influence; the stub is always disabled).

- [ ] **Step 4: `CrystalButton.cs` — legacy flag AND conquest**

The spec's service table omits Resources; keep the legacy authored flag but require conquest. Change only the `if` line:

```csharp
            if (_town.townSO.activity.HasFlag(TownsSO.TownActivity.Resources)
                && ConquestTracker.Instance.IsConquered(_town.gridPos))
```

- [ ] **Step 5: Create `AssaultButton`**

Create `Assets/Scripts/GameObjectScripts/TownMenuScripts/AssaultButton.cs`:

```csharp
using UnityEngine;

// Shown only for a guarded, not-yet-conquered place (a Town's empty roster is
// conquered immediately, so it never shows one). Assaulting is free; the cost
// is fighting the roster — or 3 wounds to retreat mid-assault. Closing the
// menu without assaulting costs nothing.
public class AssaultButton : TownButtons
{
    public override void UpdateButtonText()
    {
        if (_town is null) return;

        bool show = !ConquestTracker.Instance.IsConquered(_town.gridPos);
        thisButton.gameObject.SetActive(show);
        if (!show) return;

        int remaining = _town.townSO.guardians.Count
                        - ConquestTracker.Instance.DefeatedCount(_town.gridPos);
        buttonText.text = $"Assault ({remaining} guardian{(remaining == 1 ? "" : "s")})";
        thisButton.interactable = true;
        thisButton.onClick.RemoveAllListeners();
        thisButton.onClick.AddListener(() => GuardianAssault.Instance.Begin(_town));
    }
}
```

- [ ] **Step 6: Compile check + commit**

User focuses the Unity editor and confirms a clean console (this also makes Unity generate `AssaultButton.cs.meta` — include it in the commit):

```powershell
git add Assets/Scripts/GameObjectScripts/TownMenuScripts
git commit -m "feat: town-menu services gate on place type + conquest; AssaultButton"
```

---

### Task 7: Scene wiring — Assault button in the town menu (in-editor, user-driven)

**Files:**
- Modify: `Assets/Scenes/GameBoard.unity` (saved by the Unity editor — never hand-edit the YAML)

**Interfaces:**
- Consumes: `AssaultButton` component (Task 6).
- Produces: a wired Assault button the town menu shows/hides via the same two scene events the other buttons use.

Per the solo-dev workflow rule, **the user performs every step in the Unity editor** while the executor narrates this checklist and waits for confirmation. The town-menu button column is the GridLayoutGroup object that parents `RecruitButton`, `CrystalButton`, `HealButton`, and `CardButton` (find it by searching the Hierarchy for `RecruitButton` inside the town canvas). The layout only positions **active** children, and Assault is visible exactly when the service buttons are hidden, so the 500px column never overflows.

- [ ] **Step 1: Duplicate RecruitButton**

1. Open the GameBoard scene. In the Hierarchy search box type `RecruitButton`, select it, then clear the search to see it under its button-column parent.
2. Ctrl+D to duplicate. Rename the copy `AssaultButton` and drag it to be the **last** sibling in the button column.
3. On its child `Text (TMP)` object, change the text to `Assault`.

- [ ] **Step 2: Swap the component**

1. On `AssaultButton`, remove the `Recruit Button (Script)` component (component ⋮ menu ▸ Remove Component).
2. Add Component ▸ **Assault Button** (the Task 6 script).
3. In the new component, assign: **This Button** ← the object's own `Button` component; **Button Text** ← the child `Text (TMP)`. Leave **_Town**, **Town Event**, and **Influence Cost Event** empty (assault raises neither).

- [ ] **Step 3: Re-wire the two listeners**

Duplicating then swapping the component broke the listeners' persistent-call targets, so fix both listener components on `AssaultButton`:

1. **Town Listener** (its Game Event asset is `onClick_GetTownData`): in its Unity Event Response, set the target to the `AssaultButton` component and the function to **TownButtons ▸ SetTownCard** (dynamic TownToken).
2. **Int Listener** (Game Event = the current-influence event): first entry → target `AssaultButton` component, function **TownButtons ▸ SetCurrentInfluence** (dynamic int); second entry → target `AssaultButton` component, function **AssaultButton ▸ UpdateButtonText ()** (static, parameterless).
3. If an entry shows `Missing (Object)` and won't retarget, remove it (−) and re-add it (+) with the target/function above.

- [ ] **Step 4: Verify and save**

1. Save the scene (Ctrl+S). Console must be clean.
2. Play-mode sanity check: standing on a plain Town, click it → the menu opens with **no** Assault button and no errors. (The full guarded-place check happens in Task 10 once Task 9's Keep/Castle content exists.)

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scenes/GameBoard.unity
git commit -m "feat: Assault button wired into town menu scene"
```

---

### Task 8: Persist conquest state through `DataManager`

**Files:**
- Modify: `Assets/Scripts/Managers/DataManager.cs`

**Interfaces:**
- Consumes: `ConquestTracker.Instance.ExportPlaces()/ApplySave()` (Task 4), `SaveMigrator.Migrate` (Task 2).
- Produces: conquest state in `Save.json`; restored on load.

- [ ] **Step 1: Migrate on load**

In `LoadGame()`, change:

```csharp
        using (StreamReader reader = new(savePath))
            current = SaveSerializer.FromJson(reader.ReadToEnd());
```

to:

```csharp
        using (StreamReader reader = new(savePath))
            current = SaveMigrator.Migrate(SaveSerializer.FromJson(reader.ReadToEnd()));
```

- [ ] **Step 2: Capture**

In `CaptureRunState()`, change `var file = new SaveFile { schemaVersion = 1 };` to `var file = new SaveFile { schemaVersion = 2 };` and, next to the other `run.map` lines, add:

```csharp
        run.places = ConquestTracker.Instance.ExportPlaces();
```

- [ ] **Step 3: Restore**

In `RestoreNow()`, after the defeated-enemy-token removal loop, add:

```csharp
        // Re-apply guardian-conquest progress to the regenerated places. The
        // town tokens registered themselves (rosterSize/type) during their
        // Start; the ledger tolerates either order regardless.
        ConquestTracker.Instance.ApplySave(run.places);
```

- [ ] **Step 4: Compile check + commit**

User confirms a clean Unity console, then:

```powershell
git add Assets/Scripts/Managers/DataManager.cs
git commit -m "feat: conquest state persists - capture places, migrate v1 saves, restore on load"
```

---

### Task 9: Author Keep + Castle content and add them to the map pool (in-editor, user-driven)

**Files (all created/saved by the Unity editor):**
- Create: `Assets/Scripts/ScriptableObjectData/Non-Player/Towns/Stonegate Keep.asset`
- Create: `Assets/Scripts/ScriptableObjectData/Non-Player/Towns/Castle Veyrune.asset`
- Modify: `Assets/Scripts/ScriptableObjectData/Non-Player/Locations/Redonya.asset`
- Modify: `Assets/Scripts/ScriptableObjectData/Non-Player/Towns/Garth Barracks.asset`

**Interfaces:**
- Consumes: `TownsSO.placeType/guardians` fields (Task 4).
- Produces: a town pool of 5 (3 Towns, 1 Keep, 1 Castle) that `GridGeneration` draws from via `Location`→`TownDeck`.

Per the solo-dev workflow rule, the user creates these in the editor (new assets need Unity-generated GUIDs; inventing them by hand is exactly the risk to avoid). All names/numbers are placeholder content — tune freely.

- [ ] **Step 1: Create the Keep asset**

In the Project window, navigate to `Assets/Scripts/ScriptableObjectData/Non-Player/Towns/`, right-click ▸ Create ▸ ScriptableObjects ▸ Cards ▸ TownCards. Name the file `Stonegate Keep` and fill the Inspector:

| Field | Value |
|-------|-------|
| Card Name | `Stonegate Keep` |
| Card Description | `A squat border fortress whose bandit garrison taxes every road through the pass. Break its guardian and its fighters will follow you instead.` |
| Town Size | `Fortress` |
| Activity | `Recruit` |
| Recruitable Units | size 1 → `Warrior` |
| Recruit Level | `4` |
| Card Level / Resource Level / Heal Level | `0` / `0` / `0` |
| **Place Type** | `Keep` |
| **Guardians** | size 1 → `BanditFootsoldier` |

- [ ] **Step 2: Create the Castle asset**

Same menu; name it `Castle Veyrune`:

| Field | Value |
|-------|-------|
| Card Name | `Castle Veyrune` |
| Card Description | `Seat of a fallen archon, now held by corrupted champions. Whoever commands its towers commands the valley below.` |
| Town Size | `City` |
| Activity | `Recruit, Cards, Heal` (flags) |
| Recruitable Units | size 1 → `Knight` |
| Recruit Level | `6` |
| Card Level | `5` |
| Resource Level | `0` |
| Heal Level | `3` |
| **Place Type** | `Castle` |
| **Guardians** | size 2 → `Corrupted Troll`, `Sorceror` |

(Both guardian enemies already carry non-empty `defeatRewards`, so the existing reward path is safe.)

- [ ] **Step 3: Add both to Redonya's town pool**

Select `Assets/Scripts/ScriptableObjectData/Non-Player/Locations/Redonya.asset`; in the Inspector, grow **Towns** from 3 to 5 and drag `Stonegate Keep` and `Castle Veyrune` into the new slots.

- [ ] **Step 4: Existing towns stay Towns by default**

The three existing town assets need no edits — an unset `placeType` deserializes to `Town` and `guardians` to empty. One coherence touch-up while in the Inspector: `Garth Barracks` has **Heal Level 0** but the Town type now offers Heal — set it to `1` so its heal button does something.

- [ ] **Step 5: Verify + commit**

Confirm in the Inspector that both new assets show Place Type Keep/Castle and populated Guardians lists, then:

```powershell
git add "Assets/Scripts/ScriptableObjectData/Non-Player/Towns" "Assets/Scripts/ScriptableObjectData/Non-Player/Locations/Redonya.asset"
git commit -m "content: Stonegate Keep + Castle Veyrune with guardian rosters; Redonya pool grows to 5 places"
```

---

### Task 10: Manual in-scene verification (spec's acceptance pass)

No files. Run with the user in the Unity editor (Play mode). Check each; on any failure, stop and debug with superpowers:systematic-debugging before proceeding.

- [ ] **Standing-on rule:** click a town from an adjacent cell → menu does NOT open, "must be standing in" message shows; move onto the cell → menu opens.
- [ ] **Gating:** New game → click a plain Town (standing on it) → Recruit + Heal show (no Assault, no Cards); click a Keep → only "Assault (1 guardian)"; click a Castle → only "Assault (2 guardians)".
- [ ] **Free menu close:** open a Keep's menu, click the place card to close it → no wounds added.
- [ ] **Assault + conquest:** assault a Keep with enough Attack → guardian fight uses the normal combat flow, defeat grants rewards, conquered message appears; reopen the Keep → Recruit shows, Assault gone.
- [ ] **Chained guardians:** assault a Castle → defeating guardian 1 spawns guardian 2 without closing the canvas.
- [ ] **Retreat:** assault a Castle, defeat guardian 1, press Flee → exactly 3 Wound cards enter the hand, canvas closes; reopen → "Assault (1 guardian)" (progress kept). Field-combat flee still costs 1 wound.
- [ ] **Turn gating:** during an assault fight, End Turn and Round End are disabled.
- [ ] **Cards stub:** conquer a Castle (defeat both guardians) → menu shows Recruit, Heal, and a disabled "Cards (soon)".
- [ ] **Persistence:** with a half-assaulted Castle, Save (settled state) → quit Play → Load → the Castle still shows "Assault (1 guardian)"; a conquered Keep still offers services. Open `Save.json` and confirm `"schemaVersion": 2` and a `places` entry.
- [ ] **v1 migration:** if a pre-M2 `Save.json` exists (or by hand-editing `schemaVersion` to 1 and deleting the `places` line), Load works and nothing is conquered.
- [ ] **Castles present:** across 2–3 new games, the map always contains at least 2 Castles (inspect town tokens).
- [ ] **EditMode suite:** user runs Window ▸ General ▸ Test Runner ▸ EditMode → all green (including `SaveSerializerTests`, `SaveMigratorTests`, `PlaceRulesTests`, `ConquestLedgerTests`).

No commit (nothing changes unless bugs were fixed; commit fixes individually as `fix:` commits).

---

### Task 11: Design-bible + roadmap updates

**Files:**
- Modify: `.claude/skills/archons-rise-design/mechanics.md`
- Modify: `.claude/skills/archons-rise-design/balance.md`
- Modify: `.claude/skills/archons-rise-design/content-rules.md`
- Modify: `.claude/skills/archons-rise-roadmap/milestones.md`
- Modify: `.claude/skills/archons-rise-roadmap/status.md`
- Modify: `.claude/skills/archons-rise-roadmap/decisions-log.md` (append-only)

- [ ] **Step 1: `mechanics.md`** — where the win condition is described, replace the "control N towns AND level" framing with:

> **Win — conquer 2 Castles.** Map places are typed: **Town / Keep / Castle** (+ existing Dungeons). Guarded places (Keep 1 guardian, Castle 2 — data-driven rosters) must have **all guardians defeated in order** to be conquered; defeated guardians never respawn, so conquest is resumable. Retreating from an assault in progress costs **3 wounds** (field-combat flee stays 1); closing a place's menu without assaulting is free. Services gate by type (Town: Recruit+Heal; Keep: Recruit; Castle: Recruit+Heal+Cards) and open only once the place is conquered (Towns, guardian-less, open immediately). Places are entered by **standing on their cell** — adjacent interaction is not allowed (unlike enemies). Territory is the sole win axis — no Level/Influence gate.

- [ ] **Step 2: `balance.md`** — replace the "Archon Win Threshold" section body with:

```markdown
## Archon Win Threshold
- Conquer **2 Castles** (no Level/Influence gate — territory is the sole win axis).
- Guardian rosters (data-driven starting counts): **Town 0, Keep 1, Castle 2**; Dungeon 2 (existing).
- **Assault retreat penalty: 3 wounds** (`PlaceRules.RetreatWoundCount`) vs. 1 for field-combat flee.
- _Starting values — tune in playtest._ Grow rosters or castle count to lengthen runs.
```

- [ ] **Step 3: `content-rules.md`** — in the `TownsSO` table add rows:

```markdown
| `placeType` | `PlaceType` | Town / Keep / Castle — drives allowed services via `PlaceRules` |
| `guardians` | List<`EnemiesSO`> | Conquest roster, fought in order; empty for a Town |
```

and replace the town **Rule:** paragraph with:

> **Rule:** Service availability is computed from `placeType` (`PlaceRules.AllowedServices`), NOT the legacy `activity` flags (exception: the Crystal/Resources button still reads `activity`). Town: Recruit+Heal, opens unguarded. Keep: Recruit, 1 guardian. Castle: Recruit+Heal+Cards(stub), 2 guardians. Castles are the win currency — conquering 2 wins the run (M2.5).

Also add `PlaceType`/`PlaceService` to the "Enums used below" list: `PlaceType: Town=0, Keep=1, Castle=2` and `PlaceService [Flags]: None=0, Recruit=1, Heal=2, Cards=4` (source: `Assets/Scripts/Places/`).

- [ ] **Step 4: `milestones.md`** — retitle M2 and insert M2.5:

Replace the M2 section with:

```markdown
## M2 — Place-type system  ✅ DONE (fill date)
**Goal:** typed places (Town/Keep/Castle) + resumable guardian conquest + service gating.
**Scope:** `PlaceType` taxonomy; data-driven guardian rosters; assault/retreat (3 wounds);
conquest persistence (save schema v2); services gate by type + conquest; Cards stub.
Spec: `docs/superpowers/specs/2026-06-30-m2-place-type-system-design.md`.
**Acceptance:** a Keep/Castle can be assaulted, conquered across sessions, and gates its
services; retreat costs 3 wounds and keeps progress. ✅

## M2.5 — Win/lose systems  _(Current Focus)_
**Goal:** make a run winnable and losable.
**Scope:**
- **Victory** — conquer **2 Castles** (`ConquestTracker.ConqueredCastleCount()`).
- **Doom Clock** — rises each round; reaching max loses the run.
- **Wound-out** — lose when Wounds ≥ threshold.
- **Game-over screen** for both outcomes.

**Acceptance:** a run can be won by conquering 2 Castles and lost by clock-max or wound-out.
```

(mark M2 done only if Task 10 passed; otherwise leave `_(Current Focus)_` on M2 and hold this step). Update the `Current Focus` line in the roadmap `SKILL.md` to point at M2.5 when M2 completes.

- [ ] **Step 5: `status.md`** — under "Exists (in code)" add:

```markdown
- **Place-type system** — Town/Keep/Castle taxonomy, guardian-conquest assaults (resumable,
  3-wound retreat), services gated by type + conquest, conquest persisted (schema v2). ✅ M2.
```

Fix the "Missing" list: remove the duplicated win-check line and retag the win/clock/wound-out entries **M2.5** (they were listed as M2).

- [ ] **Step 6: `decisions-log.md`** — append (adjust date to the actual day):

```markdown
- **2026-07-02 — M2 retargeted to the place-type system; win/lose becomes M2.5.**
  M2 now builds Town/Keep/Castle taxonomy, data-driven guardian conquest (rosters: Town 0,
  Keep 1, Castle 2), 3-wound assault retreat, type+conquest service gating, and schema-v2
  persistence. Victory changes to **conquer 2 Castles** (no Level/Influence gate).
  _Why:_ the old "control 3 towns" win had no control mechanic behind it; typed places make
  territory meaningful and tie conquest to the existing combat system. Spec:
  `docs/superpowers/specs/2026-06-30-m2-place-type-system-design.md`.

- **2026-07-02 — M2 implementation decisions.**
  (1) The **Crystal/Resources service keeps its legacy `activity`-flag gate** (plus conquest) —
  the spec's service table omits it, and silently deleting a working service was worse; fold it
  into `PlaceService` when the design decides its place. (2) **Seeded maps guarantee ≥ 2
  Castles** (last-placed tokens upgrade if random picks came up short) so the M2.5 victory is
  always reachable. (3) **Retreat penalty applies only to an assault in progress** (user-confirmed
  2026-07-02) — clicking a guarded place opens the menu with all services locked and an Assault
  button; closing the menu without pressing Assault is free; the combat Flee button doubles as
  Retreat (3 wounds) during assaults. (4) **Places are entered by standing on their cell** —
  adjacent clicks are rejected with a message (enemies keep their adjacency interaction).
  (5) `GridGeneration` now draws towns from the full pool (`Rng(0, towns.Count)` instead of the
  hardcoded `Rng(0,3)`); RNG draw count is unchanged, so old seeds keep their tile layout — only
  town identities shift (v1 saves carry no conquest state, so this is cosmetic).
```

- [ ] **Step 7: Commit**

```powershell
git add .claude/skills/archons-rise-design .claude/skills/archons-rise-roadmap
git commit -m "docs: design bible + roadmap reflect M2 place-type system, M2.5 win/lose split"
```
