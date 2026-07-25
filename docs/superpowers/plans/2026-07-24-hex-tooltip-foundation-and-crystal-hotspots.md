# Hex Tooltip Foundation + Crystal Hotspots Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add scattered Crystal Hotspot tiles that grant a fixed-color crystal when the player ends a turn parked on them (charge-limited, `-1` = unlimited), and upgrade the HexTooltip into an extensible tile-occupant descriptor so this and future tiles communicate what's on a hex.

**Architecture:** Mirror the shipped Dungeon subsystem — a pure rule class + `Cell`-keyed ledger (mcs-testable), a MonoBehaviour tracker wrapping the ledger, a `HexRuleTile` subclass + a map token, a content `ScriptableObject`, one save-schema bump (v8). Harvest is a free passive fired from the existing turn-end chain, not a click handler. The tooltip gains an `IHexOccupant` interface every token implements plus a pure `TileDescriptor` string builder, so new tiles integrate with zero edits to `HexInteractor`.

**Tech Stack:** Unity 2022+ (C#), Unity Tilemap + RuleTile, TextMeshPro (`IconMarkup` sprite tags), assembly definitions, NUnit EditMode tests run via Unity Test Runner (pure classes also verifiable via the repo's mcs CLI harness while the editor is open — see memory `unity-editmode-tests-while-editor-open`).

## Global Constraints

- **Design source of truth:** `docs/superpowers/specs/2026-07-24-crystal-hotspots-and-shrines-design.md`. This plan is **Plan 1** of two; **Plan 2 (Shrines)** builds on the `IHexOccupant`/`TileDescriptor`/registry foundation here.
- **Pure classes are UnityEngine-free** so they compile in the mcs harness. They use `ArchonsRise.SaveData.Cell`, never `UnityEngine.Vector3Int`. Enums in `Assets/Scripts/Enums/` (e.g. `EmpowerType`) are plain enums and are allowed in pure classes.
- **New pure class ⇒ its own folder asmdef + a reference from the EditMode tests asmdef** (memory `pure-class-asmdef-placement`), or EditMode tests fail `CS0103`. MonoBehaviours stay in the main assembly (no asmdef).
- **Never hand-edit scene/prefab YAML.** Scene wiring, prefab authoring, and `.asset` creation are USER editor steps — the plan writes the C# and gives exact editor instructions (memory `manual-unity-edits-for-risky-changes`).
- **Icon/cost text goes through `IconMarkup`** (`ArchonsRise.UiLanguage`) — never hand-roll a `<sprite=…>` literal (content-rules.md, M2.11).
- **On-map feedback:** hotspot harvest updates token visuals + the tooltip only — **no `ValidationMessage` popup, no `RewardQueue`** (memory `map-feedback-tooltip-and-log`).
- **Stat/effect enums are append-only.** `EmpowerType` = `None=0, Red=1, Yellow=2, Green=4, Purple=8` (all-colors = 15).
- **Save schema is currently v7.** This plan bumps it to **v8**. Migration must default `hotspots` to empty (a v7 save has no hotspots).

---

## File Structure

**New pure classes (mcs-testable):**
- `Assets/Scripts/Hotspots/HotspotRules.cs` — charge math (`ArchonsRise.Hotspots`).
- `Assets/Scripts/Hotspots/HotspotLedger.cs` — `Cell`-keyed per-run charge state; export/restore.
- `Assets/Scripts/Hotspots/ArchonsRise.Hotspots.asmdef` — references `ArchonsRise.SaveData`.
- `Assets/Scripts/HexTooltip/TileDescriptor.cs` — pure icon-marked descriptor strings (`ArchonsRise.HexTooltipInfo`).
- `Assets/Scripts/HexTooltip/HexDescriptor.cs` — the plain descriptor struct returned by occupants.
- `Assets/Scripts/HexTooltip/ArchonsRise.HexTooltipInfo.asmdef` — references `ArchonsRise.UiLanguage`, `ArchonsRise.SaveData`.

**New MonoBehaviours / content (main assembly):**
- `Assets/Scripts/Managers/HotspotTracker.cs` — scene singleton wrapping `HotspotLedger`.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalHotspotToken.cs` — map token, implements `IHexOccupant`.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/IHexOccupant.cs` — the occupant interface.
- `Assets/Scripts/Managers/HexOccupantRegistry.cs` — cell → occupant lookup.
- `Assets/Scripts/TilemapScripts/CrystalHotspotRuleTile.cs` — `HexRuleTile` subclass.
- `Assets/Scripts/GameScriptableObjectTypes/CrystalHotspotSO.cs` — content type.

**Modified:**
- `Assets/Scripts/SaveData/SaveModels.cs` — `HotspotState` struct + `RunState.hotspots` + `schemaVersion = 8`.
- `Assets/Scripts/SaveData/SaveMigrator.cs` — v7→v8 default line.
- `Assets/Scripts/Managers/TurnPhaseController.cs` — harvest hook in `EndTurnPressed`.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractor.cs` — `TooltipText` + `PlaceOccupies` via registry/descriptor.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs`, `DungeonToken.cs` — implement `IHexOccupant`.
- `Assets/Scripts/TilemapScripts/GridGeneration.cs` — hotspot placement.
- Whichever serializer builds/reads `RunState` (the class calling `DungeonTracker.Export`/`ApplySave`) — hotspot export/restore. **Task 6 locates it by grepping for `DungeonTracker.Instance.Export`.**

**New tests:**
- `Assets/Tests/EditMode/HotspotRulesTests.cs`
- `Assets/Tests/EditMode/HotspotLedgerTests.cs`
- `Assets/Tests/EditMode/TileDescriptorTests.cs`
- `Assets/Scripts/SaveData/Tests/SaveMigratorV8Tests.cs` (mirrors the existing `SaveMigratorV*Tests.cs` location)

---

### Task 1: `HotspotRules` pure charge math

**Files:**
- Create: `Assets/Scripts/Hotspots/HotspotRules.cs`
- Create: `Assets/Scripts/Hotspots/ArchonsRise.Hotspots.asmdef`
- Test: `Assets/Tests/EditMode/HotspotRulesTests.cs`
- Modify: `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` (add reference)

**Interfaces:**
- Produces: `HotspotRules.CanHarvest(int remaining) → bool`; `HotspotRules.NextCharges(int remaining) → int`. Sentinel: `remaining == -1` means unlimited.

- [ ] **Step 1: Create the asmdef**

Create `Assets/Scripts/Hotspots/ArchonsRise.Hotspots.asmdef`:

```json
{
    "name": "ArchonsRise.Hotspots",
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

- [ ] **Step 2: Reference the new asmdef from the EditMode tests asmdef**

Open `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` and add `"ArchonsRise.Hotspots"` to its `references` array (leave every existing reference in place).

- [ ] **Step 3: Write the failing test**

Create `Assets/Tests/EditMode/HotspotRulesTests.cs`:

```csharp
using NUnit.Framework;
using ArchonsRise.Hotspots;

public class HotspotRulesTests
{
    [Test]
    public void CanHarvest_TrueForPositiveCharges()
    {
        Assert.IsTrue(HotspotRules.CanHarvest(3));
        Assert.IsTrue(HotspotRules.CanHarvest(1));
    }

    [Test]
    public void CanHarvest_TrueForUnlimitedSentinel()
    {
        Assert.IsTrue(HotspotRules.CanHarvest(-1));
    }

    [Test]
    public void CanHarvest_FalseWhenDepleted()
    {
        Assert.IsFalse(HotspotRules.CanHarvest(0));
    }

    [Test]
    public void NextCharges_DecrementsPositive()
    {
        Assert.AreEqual(2, HotspotRules.NextCharges(3));
        Assert.AreEqual(0, HotspotRules.NextCharges(1));
    }

    [Test]
    public void NextCharges_UnlimitedStaysUnlimited()
    {
        Assert.AreEqual(-1, HotspotRules.NextCharges(-1));
    }

    [Test]
    public void NextCharges_FloorsAtZero()
    {
        Assert.AreEqual(0, HotspotRules.NextCharges(0));
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run the EditMode suite (Unity ▸ Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run, filter `HotspotRulesTests`). Per repo convention you may instead compile+run just this class via the mcs CLI harness while the editor is open.
Expected: FAIL — `ArchonsRise.Hotspots` / `HotspotRules` does not exist (`CS0103`/namespace not found).

- [ ] **Step 5: Write the minimal implementation**

Create `Assets/Scripts/Hotspots/HotspotRules.cs`:

```csharp
namespace ArchonsRise.Hotspots
{
    // Pure crystal-hotspot charge math (spec 2026-07-24). Unity-free so it is
    // mcs-CLI-testable (DungeonRules pattern). Sentinel: remaining == -1 means
    // an unlimited "rich vein" that never depletes.
    public static class HotspotRules
    {
        public const int Unlimited = -1;

        // A hotspot yields a crystal while it has charges left, or forever when
        // unlimited. A depleted (0) tile is dormant.
        public static bool CanHarvest(int remaining) => remaining != 0;

        // Charges after one harvest: unlimited stays unlimited; a finite count
        // steps down and never goes negative.
        public static int NextCharges(int remaining)
            => remaining == Unlimited ? Unlimited : (remaining > 0 ? remaining - 1 : 0);
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run the EditMode suite filtered to `HotspotRulesTests`.
Expected: PASS (6/6).

- [ ] **Step 7: Commit**

```bash
git add "Assets/Scripts/Hotspots/HotspotRules.cs" "Assets/Scripts/Hotspots/ArchonsRise.Hotspots.asmdef" "Assets/Tests/EditMode/HotspotRulesTests.cs" "Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef"
git commit -m "feat: HotspotRules charge math (pure, TDD)"
```

---

### Task 2: `HotspotState` save struct + `HotspotLedger`

**Files:**
- Modify: `Assets/Scripts/SaveData/SaveModels.cs`
- Create: `Assets/Scripts/Hotspots/HotspotLedger.cs`
- Test: `Assets/Tests/EditMode/HotspotLedgerTests.cs`

**Interfaces:**
- Consumes: `HotspotRules` (Task 1); `ArchonsRise.SaveData.Cell`, `HotspotState`.
- Produces: `HotspotLedger.Register(Cell, string hotspotId, int charges)`, `.Remaining(Cell) → int`, `.CanHarvest(Cell) → bool`, `.Harvest(Cell)` (decrements per `HotspotRules.NextCharges`), `.Export() → HotspotState[]`, `.ApplySavedState(HotspotState) → bool`. `HotspotState { int x; int y; string hotspotId; int remainingCharges; }`.

- [ ] **Step 1: Add the `HotspotState` struct + `RunState.hotspots`**

In `Assets/Scripts/SaveData/SaveModels.cs`, add to `RunState` (after the `dungeons` field, keeping the existing v6 comment block intact):

```csharp
        // One entry per hotspot with charges consumed (or a depleted finite
        // tile); positions and SO assignment re-derive from the map seed,
        // hotspotId is a content sanity check on restore (v8).
        public HotspotState[] hotspots = Array.Empty<HotspotState>();
```

And add the struct next to `DungeonState`:

```csharp
    [Serializable]
    public struct HotspotState
    {
        public int x;
        public int y;
        public string hotspotId;
        public int remainingCharges; // -1 = unlimited (never persisted as depleted)
    }
```

- [ ] **Step 2: Write the failing test**

Create `Assets/Tests/EditMode/HotspotLedgerTests.cs`:

```csharp
using NUnit.Framework;
using ArchonsRise.Hotspots;
using ArchonsRise.SaveData;

public class HotspotLedgerTests
{
    [Test]
    public void Harvest_DecrementsRemaining()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(2, 3), "red_node", 3);
        l.Harvest(new Cell(2, 3));
        Assert.AreEqual(2, l.Remaining(new Cell(2, 3)));
    }

    [Test]
    public void Harvest_UnlimitedStaysUnlimited()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(0, 0), "rich_vein", -1);
        l.Harvest(new Cell(0, 0));
        Assert.AreEqual(-1, l.Remaining(new Cell(0, 0)));
        Assert.IsTrue(l.CanHarvest(new Cell(0, 0)));
    }

    [Test]
    public void CanHarvest_FalseOnceDepleted()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(1, 1), "one_shot", 1);
        l.Harvest(new Cell(1, 1));
        Assert.IsFalse(l.CanHarvest(new Cell(1, 1)));
    }

    [Test]
    public void Export_OnlyEmitsChangedOrDepleted()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(4, 4), "full", 3);   // untouched → not exported
        l.Register(new Cell(5, 5), "used", 3);
        l.Harvest(new Cell(5, 5));               // changed → exported
        var export = l.Export();
        Assert.AreEqual(1, export.Length);
        Assert.AreEqual(5, export[0].x);
        Assert.AreEqual(2, export[0].remainingCharges);
    }

    [Test]
    public void ApplySavedState_RestoresRemaining()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(5, 5), "used", 3);
        bool ok = l.ApplySavedState(new HotspotState { x = 5, y = 5, hotspotId = "used", remainingCharges = 1 });
        Assert.IsTrue(ok);
        Assert.AreEqual(1, l.Remaining(new Cell(5, 5)));
    }

    [Test]
    public void ApplySavedState_FalseOnIdMismatch()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(5, 5), "used", 3);
        bool ok = l.ApplySavedState(new HotspotState { x = 5, y = 5, hotspotId = "other", remainingCharges = 1 });
        Assert.IsFalse(ok);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run the EditMode suite filtered to `HotspotLedgerTests`.
Expected: FAIL — `HotspotLedger` does not exist.

- [ ] **Step 4: Write the minimal implementation**

Create `Assets/Scripts/Hotspots/HotspotLedger.cs`:

```csharp
using System.Collections.Generic;
using ArchonsRise.SaveData;

namespace ArchonsRise.Hotspots
{
    // Pure per-run hotspot charge state (spec 2026-07-24). Mirrors DungeonLedger:
    // Cell-keyed, exports only tiles whose charges changed from their authored
    // start, HotspotTracker wraps it for the scene.
    public class HotspotLedger
    {
        private class Entry
        {
            public string hotspotId;
            public int startCharges;
            public int remaining;
        }

        private readonly Dictionary<Cell, Entry> entries = new Dictionary<Cell, Entry>();

        public void Register(Cell cell, string hotspotId, int charges)
        {
            var e = GetOrCreate(cell);
            e.hotspotId = hotspotId;
            e.startCharges = charges;
            e.remaining = charges;
        }

        public int Remaining(Cell cell)
            => entries.TryGetValue(cell, out var e) ? e.remaining : 0;

        public bool CanHarvest(Cell cell)
            => entries.TryGetValue(cell, out var e) && HotspotRules.CanHarvest(e.remaining);

        public void Harvest(Cell cell)
        {
            if (entries.TryGetValue(cell, out var e))
                e.remaining = HotspotRules.NextCharges(e.remaining);
        }

        // Only tiles that have been drawn down from their authored start (a
        // save-size optimisation identical to DungeonLedger.Export).
        public HotspotState[] Export()
        {
            var list = new List<HotspotState>();
            foreach (var kv in entries)
                if (kv.Value.remaining != kv.Value.startCharges)
                    list.Add(new HotspotState
                    {
                        x = kv.Key.x,
                        y = kv.Key.y,
                        hotspotId = kv.Value.hotspotId,
                        remainingCharges = kv.Value.remaining
                    });
            return list.ToArray();
        }

        // Restore one saved entry. False when the cell was never registered or
        // the saved id doesn't match the regenerated map (content drift) — the
        // caller warns and skips, like DungeonLedger.ApplySavedState.
        public bool ApplySavedState(HotspotState s)
        {
            if (!entries.TryGetValue(new Cell(s.x, s.y), out var e)) return false;
            if (e.hotspotId != s.hotspotId) return false;
            e.remaining = s.remainingCharges;
            return true;
        }

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
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run the EditMode suite filtered to `HotspotLedgerTests`.
Expected: PASS (6/6).

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/SaveData/SaveModels.cs" "Assets/Scripts/Hotspots/HotspotLedger.cs" "Assets/Tests/EditMode/HotspotLedgerTests.cs"
git commit -m "feat: HotspotState save struct + HotspotLedger (pure, TDD)"
```

---

### Task 3: Save schema v8 bump + migration

**Files:**
- Modify: `Assets/Scripts/SaveData/SaveModels.cs` (`schemaVersion = 8`)
- Modify: `Assets/Scripts/SaveData/SaveMigrator.cs`
- Test: `Assets/Scripts/SaveData/Tests/SaveMigratorV8Tests.cs`

**Interfaces:**
- Consumes: `HotspotState` (Task 2), `SaveFile`, `SaveMigrator.Migrate`.
- Produces: a v7 file migrates to v8 with `run.hotspots == []`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Scripts/SaveData/Tests/SaveMigratorV8Tests.cs` (mirror the existing `SaveMigratorV*Tests.cs` files in that folder — check one for the exact `using`/namespace):

```csharp
using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV8Tests
{
    [Test]
    public void V7File_MigratesToV8_WithEmptyHotspots()
    {
        var file = new SaveFile { schemaVersion = 7 };
        file.run.hotspots = null; // a v7 file has no hotspots key

        SaveMigrator.Migrate(file);

        Assert.AreEqual(8, file.schemaVersion);
        Assert.IsNotNull(file.run.hotspots);
        Assert.AreEqual(0, file.run.hotspots.Length);
    }

    [Test]
    public void Migrate_PreservesExistingHotspots()
    {
        var file = new SaveFile { schemaVersion = 8 };
        file.run.hotspots = new[] { new HotspotState { x = 1, y = 2, hotspotId = "red", remainingCharges = 1 } };

        SaveMigrator.Migrate(file);

        Assert.AreEqual(1, file.run.hotspots.Length);
        Assert.AreEqual("red", file.run.hotspots[0].hotspotId);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run the EditMode suite filtered to `SaveMigratorV8Tests`.
Expected: FAIL — `schemaVersion` stays 7 / `hotspots` stays null (migration line missing).

- [ ] **Step 3: Add the migration line + bump the version constant**

In `Assets/Scripts/SaveData/SaveMigrator.cs`, add before the `if (file.schemaVersion < 7)` block:

```csharp
            // v7 -> v8: hotspots array did not exist; absent means no hotspots
            // harvested (fresh, full-charge tiles re-derive from the map seed).
            if (file.run.hotspots == null)
                file.run.hotspots = Array.Empty<HotspotState>();
```

Then change the trailing bump from `< 7` / `= 7` to:

```csharp
            if (file.schemaVersion < 8)
                file.schemaVersion = 8;
```

In `Assets/Scripts/SaveData/SaveModels.cs`, change `SaveFile.schemaVersion` default to `8` and extend its comment: `// v8: adds RunState.hotspots (crystal-hotspot charge state).`

- [ ] **Step 4: Run the test to verify it passes**

Run the EditMode suite filtered to `SaveMigratorV8Tests`. Also re-run `SaveMigratorTests` and `SaveMigratorV*Tests` to confirm no regression.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/SaveData/SaveMigrator.cs" "Assets/Scripts/SaveData/SaveModels.cs" "Assets/Scripts/SaveData/Tests/SaveMigratorV8Tests.cs"
git commit -m "feat: save schema v8 — hotspot charge state migration"
```

---

### Task 4: `HexDescriptor` + `TileDescriptor` pure strings

**Files:**
- Create: `Assets/Scripts/HexTooltip/HexDescriptor.cs`
- Create: `Assets/Scripts/HexTooltip/TileDescriptor.cs`
- Create: `Assets/Scripts/HexTooltip/ArchonsRise.HexTooltipInfo.asmdef`
- Test: `Assets/Tests/EditMode/TileDescriptorTests.cs`
- Modify: `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` (add reference)

**Interfaces:**
- Consumes: `IconMarkup` / `IconConcept` (`ArchonsRise.UiLanguage`); `EmpowerType`.
- Produces:
  - `struct HexDescriptor { public string Line; public int Priority; }` (higher priority wins when several occupants share a cell; place > tile > enemy is not needed here — one occupant per cell in practice, priority breaks ties).
  - `TileDescriptor.Hotspot(EmpowerType color, int remaining) → string` (e.g. `<crystal(tinted)> ×3` / `∞` / `depleted`).
  - `TileDescriptor.Town(string name, IconConcept typeIcon, bool conquered) → string`.
  - `TileDescriptor.Dungeon(string name, int defeated, int total) → string`.

> **Note on `IconMarkup` surface:** before writing `TileDescriptor`, open `Assets/Scripts/UiLanguage/IconMarkup.cs` and use its **actual** public methods (e.g. `IconMarkup.Tag(IconConcept)`, `IconMarkup.CrystalTag(EmpowerType)`, `IconMarkup.Cost(...)`). The calls below assume `Tag` and `CrystalTag` exist per content-rules.md; adjust to the real names if they differ. Do **not** hand-roll `<sprite=…>`.

- [ ] **Step 1: Create the asmdef**

Create `Assets/Scripts/HexTooltip/ArchonsRise.HexTooltipInfo.asmdef`:

```json
{
    "name": "ArchonsRise.HexTooltipInfo",
    "rootNamespace": "",
    "references": ["ArchonsRise.UiLanguage", "ArchonsRise.SaveData"],
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

- [ ] **Step 2: Reference it from the EditMode tests asmdef**

Add `"ArchonsRise.HexTooltipInfo"` to the `references` array in `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`.

- [ ] **Step 3: Write the `HexDescriptor` struct**

Create `Assets/Scripts/HexTooltip/HexDescriptor.cs`:

```csharp
namespace ArchonsRise.HexTooltipInfo
{
    // What a hex occupant reports to the tooltip: one icon-marked line plus a
    // priority (highest wins when a cell is described by more than one source).
    // Pure data — no UnityEngine, no UI.
    public readonly struct HexDescriptor
    {
        public readonly string Line;
        public readonly int Priority;
        public HexDescriptor(string line, int priority) { Line = line; Priority = priority; }

        public bool IsEmpty => string.IsNullOrEmpty(Line);
        public static readonly HexDescriptor None = new HexDescriptor(null, int.MinValue);
    }
}
```

- [ ] **Step 4: Write the failing test**

Create `Assets/Tests/EditMode/TileDescriptorTests.cs`:

```csharp
using NUnit.Framework;
using ArchonsRise.HexTooltipInfo;

public class TileDescriptorTests
{
    [Test]
    public void Hotspot_ShowsChargeCount()
    {
        string s = TileDescriptor.Hotspot(EmpowerType.Red, 3);
        StringAssert.Contains("3", s);
        StringAssert.Contains("sprite", s); // an IconMarkup crystal tag is present
    }

    [Test]
    public void Hotspot_ShowsInfinityWhenUnlimited()
    {
        string s = TileDescriptor.Hotspot(EmpowerType.Green, -1);
        StringAssert.Contains("∞", s);
    }

    [Test]
    public void Hotspot_ShowsDepletedAtZero()
    {
        string s = TileDescriptor.Hotspot(EmpowerType.Purple, 0);
        StringAssert.Contains("epleted", s); // "Depleted"/"depleted"
    }

    [Test]
    public void Dungeon_ShowsProgress()
    {
        string s = TileDescriptor.Dungeon("Wyrm's Hollow", 1, 3);
        StringAssert.Contains("Wyrm's Hollow", s);
        StringAssert.Contains("1", s);
        StringAssert.Contains("3", s);
    }
}
```

- [ ] **Step 5: Run the test to verify it fails**

Run the EditMode suite filtered to `TileDescriptorTests`.
Expected: FAIL — `TileDescriptor` does not exist.

- [ ] **Step 6: Write the minimal implementation**

Create `Assets/Scripts/HexTooltip/TileDescriptor.cs` (adjust `IconMarkup` calls to the real API per Step-1 note):

```csharp
using ArchonsRise.UiLanguage;

namespace ArchonsRise.HexTooltipInfo
{
    // Pure builder of tooltip occupant lines (spec 2026-07-24, §1b). Every string
    // routes through IconMarkup so glyphs match the HUD; no hand-rolled sprite
    // tags. UnityEngine-free → mcs-testable.
    public static class TileDescriptor
    {
        // Priorities: a stand-on place outranks a passive tile when both claim a
        // cell (they never do in practice, but the tooltip picks deterministically).
        public const int PlacePriority = 30;
        public const int TilePriority = 20;

        public static string Hotspot(EmpowerType color, int remaining)
        {
            string gem = IconMarkup.CrystalTag(color);
            if (remaining == 0) return $"{gem} Depleted";
            string count = remaining < 0 ? "∞" : remaining.ToString();
            return $"{gem} ×{count}";
        }

        public static string Town(string name, IconConcept typeIcon, bool conquered)
        {
            string icon = IconMarkup.Tag(typeIcon);
            return conquered ? $"{icon} {name}" : $"{icon} {name} — guarded";
        }

        public static string Dungeon(string name, int defeated, int total)
        {
            string icon = IconMarkup.Tag(IconConcept.Dungeon);
            return $"{icon} {name} ({defeated}/{total})";
        }
    }
}
```

- [ ] **Step 7: Run the test to verify it passes**

Run the EditMode suite filtered to `TileDescriptorTests`.
Expected: PASS (4/4). If the `sprite`/tag assertion fails, fix the `IconMarkup` call names (Step-1 note), not the test's intent.

- [ ] **Step 8: Commit**

```bash
git add "Assets/Scripts/HexTooltip/" "Assets/Tests/EditMode/TileDescriptorTests.cs" "Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef"
git commit -m "feat: TileDescriptor + HexDescriptor pure tooltip strings (TDD)"
```

---

### Task 5: `IHexOccupant` interface + `HexOccupantRegistry`

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/GameBoardObjects/IHexOccupant.cs`
- Create: `Assets/Scripts/Managers/HexOccupantRegistry.cs`

**Interfaces:**
- Consumes: `HexDescriptor` (Task 4).
- Produces:
  - `interface IHexOccupant { Vector3Int Cell { get; } HexDescriptor Describe(); }`
  - `HexOccupantRegistry.Instance` (lazily-created scene singleton, `DungeonTracker` pattern); `.Register(IHexOccupant)`, `.Unregister(IHexOccupant)`, `.Best(Vector3Int cell, out HexDescriptor)` → true if any occupant claims the cell (returns the highest-priority descriptor), `.Occupied(Vector3Int cell) → bool`.

> This task is Unity glue (references `UnityEngine.Vector3Int`), so it is **editor-verified**, not mcs-tested. The pure descriptor logic it surfaces is already covered by Task 4.

- [ ] **Step 1: Write the interface**

Create `Assets/Scripts/GameObjectScripts/GameBoardObjects/IHexOccupant.cs`:

```csharp
using UnityEngine;
using ArchonsRise.HexTooltipInfo;

// Anything that sits on a hex cell and can describe itself to the tooltip
// (spec 2026-07-24, §1c). Tokens register with HexOccupantRegistry on Start so
// the tooltip and move-blocking discover new tile types with no HexInteractor
// edits. A future tile type integrates by implementing this + registering.
public interface IHexOccupant
{
    Vector3Int Cell { get; }
    HexDescriptor Describe();
}
```

- [ ] **Step 2: Write the registry**

Create `Assets/Scripts/Managers/HexOccupantRegistry.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using ArchonsRise.HexTooltipInfo;

// Cell -> occupants lookup so the tooltip / occupancy checks never scan the
// scene per frame (DungeonTracker singleton pattern). Scene-scoped: a new run
// starts blank. Multiple occupants per cell are tolerated; Best() returns the
// highest-priority descriptor.
public class HexOccupantRegistry : MonoBehaviour
{
    private readonly Dictionary<Vector3Int, List<IHexOccupant>> byCell = new();

    private static HexOccupantRegistry instance;
    public static HexOccupantRegistry Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("HexOccupantRegistry").AddComponent<HexOccupantRegistry>();
            return instance;
        }
    }

    public void Register(IHexOccupant occ)
    {
        if (!byCell.TryGetValue(occ.Cell, out var list))
        {
            list = new List<IHexOccupant>();
            byCell[occ.Cell] = list;
        }
        if (!list.Contains(occ)) list.Add(occ);
    }

    public void Unregister(IHexOccupant occ)
    {
        if (byCell.TryGetValue(occ.Cell, out var list))
            list.Remove(occ);
    }

    public bool Occupied(Vector3Int cell)
        => byCell.TryGetValue(cell, out var list) && list.Count > 0;

    public bool Best(Vector3Int cell, out HexDescriptor best)
    {
        best = HexDescriptor.None;
        if (!byCell.TryGetValue(cell, out var list)) return false;
        bool found = false;
        foreach (var occ in list)
        {
            var d = occ.Describe();
            if (d.IsEmpty) continue;
            if (!found || d.Priority > best.Priority) { best = d; found = true; }
        }
        return found;
    }
}
```

- [ ] **Step 3: Verify it compiles**

Return to Unity and let it recompile. Expected: no console errors. (No unit test — pure logic is covered in Task 4; this is scene glue.)

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/IHexOccupant.cs" "Assets/Scripts/Managers/HexOccupantRegistry.cs"
git commit -m "feat: IHexOccupant interface + HexOccupantRegistry"
```

---

### Task 6: `CrystalHotspotSO` + `HotspotTracker` + save wiring

**Files:**
- Create: `Assets/Scripts/GameScriptableObjectTypes/CrystalHotspotSO.cs`
- Create: `Assets/Scripts/Managers/HotspotTracker.cs`
- Modify: the serializer that builds/reads `RunState` (find via `grep`)

**Interfaces:**
- Consumes: `HotspotLedger` (Task 2), `HotspotState`.
- Produces: `HotspotTracker.Instance` (`DungeonTracker` pattern); `.Register(Vector3Int, string id, int charges)`, `.CanHarvest(Vector3Int)`, `.ColorAt(Vector3Int) → EmpowerType`, `.Harvest(Vector3Int)`, `.Remaining(Vector3Int) → int`, `.Export() → HotspotState[]`, `.ApplySave(HotspotState[])`. `CrystalHotspotSO` fields: `id`, `color` (`EmpowerType`), `charges` (int).

- [ ] **Step 1: Write `CrystalHotspotSO`**

Create `Assets/Scripts/GameScriptableObjectTypes/CrystalHotspotSO.cs` (subclass `AllCards` to match the content contract; confirm `AllCards`' namespace/using from a sibling SO like `DungeonsSO.cs`):

```csharp
using UnityEngine;

// A crystal-hotspot tile (spec 2026-07-24): stands on the map showing a fixed
// color; standing on it at End Turn grants 1 crystal of that color. charges
// decrement per harvest; -1 = unlimited (rich vein).
[CreateAssetMenu(fileName = "Crystal Hotspot", menuName = "ScriptableObjects/CrystalHotspot")]
public class CrystalHotspotSO : AllCards
{
    [Tooltip("Stable slug; save identity. Never rename.")]
    public string id;
    [Tooltip("Single color this tile yields.")]
    public EmpowerType color = EmpowerType.Red;
    [Tooltip("Payouts before dormancy. -1 = unlimited (rich vein).")]
    public int charges = 3;
}
```

- [ ] **Step 2: Write `HotspotTracker`**

Create `Assets/Scripts/Managers/HotspotTracker.cs`:

```csharp
using UnityEngine;
using ArchonsRise.Hotspots;
using ArchonsRise.SaveData;

// Runtime hotspot registry for the current run: wraps the pure HotspotLedger
// (DungeonTracker pattern). Also caches each cell's yielded color so the harvest
// hook needs only the cell. Scene-scoped; a new run starts blank.
public class HotspotTracker : MonoBehaviour
{
    private readonly HotspotLedger ledger = new HotspotLedger();
    private readonly System.Collections.Generic.Dictionary<Cell, EmpowerType> colors = new();

    private static HotspotTracker instance;
    public static HotspotTracker Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("HotspotTracker").AddComponent<HotspotTracker>();
            return instance;
        }
    }

    public void Register(Vector3Int cell, string id, int charges, EmpowerType color)
    {
        ledger.Register(ToCell(cell), id, charges);
        colors[ToCell(cell)] = color;
    }

    public bool CanHarvest(Vector3Int cell) => ledger.CanHarvest(ToCell(cell));
    public int Remaining(Vector3Int cell) => ledger.Remaining(ToCell(cell));
    public EmpowerType ColorAt(Vector3Int cell)
        => colors.TryGetValue(ToCell(cell), out var c) ? c : EmpowerType.None;

    public void Harvest(Vector3Int cell) => ledger.Harvest(ToCell(cell));

    public HotspotState[] Export() => ledger.Export();

    public void ApplySave(HotspotState[] hotspots)
    {
        if (hotspots == null) return;
        foreach (var h in hotspots)
            if (!ledger.ApplySavedState(h))
                Debug.LogWarning($"Hotspot restore: cell ({h.x},{h.y}) id '{h.hotspotId}' doesn't match the regenerated map — skipped.");
        RefreshTokenVisuals();
    }

    public void RefreshTokenVisuals()
    {
        foreach (var t in FindObjectsByType<CrystalHotspotToken>())
            t.RefreshVisual();
    }

    private static Cell ToCell(Vector3Int v) => new Cell(v.x, v.y);
}
```

> `CrystalHotspotToken` is Task 7; this reference compiles once Task 7 lands. If you implement strictly in order, temporarily comment the `RefreshTokenVisuals` body and restore it in Task 7 — or implement Task 7 before recompiling.

- [ ] **Step 3: Wire save export/restore**

Find the serializer that assembles `RunState` for save and applies it on load:

Run: `grep -rn "DungeonTracker.Instance.Export\|dungeons = \|ApplySave(.*dungeons" Assets/Scripts`

In the **export** path (where `run.dungeons = DungeonTracker.Instance.Export();` is set), add:

```csharp
            run.hotspots = HotspotTracker.Instance.Export();
```

In the **restore** path (where `DungeonTracker.Instance.ApplySave(run.dungeons, ...)` is called), add:

```csharp
            HotspotTracker.Instance.ApplySave(run.hotspots);
```

- [ ] **Step 4: Verify it compiles and the export path runs**

Return to Unity, recompile. Expected: no errors. (Full save/load round-trip is checked in Task 10's acceptance.)

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/GameScriptableObjectTypes/CrystalHotspotSO.cs" "Assets/Scripts/Managers/HotspotTracker.cs" Assets/Scripts
git commit -m "feat: CrystalHotspotSO + HotspotTracker + save wiring"
```

---

### Task 7: `CrystalHotspotRuleTile` + `CrystalHotspotToken`

**Files:**
- Create: `Assets/Scripts/TilemapScripts/CrystalHotspotRuleTile.cs`
- Create: `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalHotspotToken.cs`

**Interfaces:**
- Consumes: `HotspotTracker` (Task 6), `IHexOccupant`/`HexOccupantRegistry` (Task 5), `TileDescriptor` (Task 4).
- Produces: `CrystalHotspotToken { CrystalHotspotSO hotspotSO; Vector3Int gridPos; void RefreshVisual(); }` implementing `IHexOccupant`.

- [ ] **Step 1: Write the rule tile**

Create `Assets/Scripts/TilemapScripts/CrystalHotspotRuleTile.cs`:

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "Crystal Hotspot Rule Tile", menuName = "ScriptableObjects/Tiles/Crystal Hotspot Rule Tiles")]
public class CrystalHotspotRuleTile : HexRuleTile
{
}
```

- [ ] **Step 2: Write the token**

Create `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalHotspotToken.cs`:

```csharp
using UnityEngine;
using ArchonsRise.HexTooltipInfo;

// Map-side crystal-hotspot identity (spec 2026-07-24). Registers charges with
// HotspotTracker + itself with HexOccupantRegistry on Start. Passive: NO click
// handler — harvesting fires from the turn-end chain, not interaction. Visual
// states: live (colored crystal + charge pip) vs dormant (greyed, depleted).
public class CrystalHotspotToken : MonoBehaviour, IHexOccupant
{
    public CrystalHotspotSO hotspotSO;
    public Vector3Int gridPos;                 // assigned by GridGeneration at spawn
    [SerializeField] GameObject livePips;       // optional charge-count display
    [SerializeField] GameObject dormantMarker;  // active once depleted
    [SerializeField] SpriteRenderer crystalSprite; // tinted to hotspotSO.color

    public Vector3Int Cell => gridPos;

    void Start()
    {
        HotspotTracker.Instance.Register(gridPos, hotspotSO.id, hotspotSO.charges, hotspotSO.color);
        HexOccupantRegistry.Instance.Register(this);
        RefreshVisual();
    }

    void OnDestroy()
    {
        if (HexOccupantRegistry.Instance != null) HexOccupantRegistry.Instance.Unregister(this);
    }

    public HexDescriptor Describe()
        => new HexDescriptor(
            TileDescriptor.Hotspot(hotspotSO.color, HotspotTracker.Instance.Remaining(gridPos)),
            TileDescriptor.TilePriority);

    public void RefreshVisual()
    {
        bool live = HotspotTracker.Instance.CanHarvest(gridPos);
        if (dormantMarker != null) dormantMarker.SetActive(!live);
        if (livePips != null) livePips.SetActive(live);
        // (Optional) tint crystalSprite by hotspotSO.color via IconMarkup's hexes.
    }
}
```

- [ ] **Step 3: Verify it compiles**

Recompile in Unity. Expected: no errors, and `HotspotTracker.RefreshTokenVisuals` (Task 6) now resolves.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/TilemapScripts/CrystalHotspotRuleTile.cs" "Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalHotspotToken.cs"
git commit -m "feat: CrystalHotspotRuleTile + CrystalHotspotToken (passive, IHexOccupant)"
```

---

### Task 8: Harvest hook in `TurnPhaseController.EndTurnPressed`

**Files:**
- Modify: `Assets/Scripts/Managers/TurnPhaseController.cs`

**Interfaces:**
- Consumes: `HotspotTracker` (Task 6), the crystal-grant seam `crystals.CreateCrystal(EmpowerType)` (used by `Rewards`), `PlayerPosition`, the board `Grid`.

> The crystal is granted via the same seam `Rewards` uses: a `Crystals` component method `CreateCrystal(EmpowerType)`. Confirm the exact reference by opening `Rewards.cs` (it calls `crystals.CreateCrystal(color)`); reach the same `Crystals` instance from `TurnPhaseController` (e.g. `FindAnyObjectByType<Crystals>()`, cached in `Awake`).

- [ ] **Step 1: Add a private harvest helper**

In `Assets/Scripts/Managers/TurnPhaseController.cs`, add (cache `Grid`/`PlayerPosition`/`Crystals` in `Awake` if not already available):

```csharp
    // Free passive (spec 2026-07-24): if the player ends a turn parked on a live
    // crystal hotspot, grant 1 crystal of its color and spend a charge. No modal
    // and no RewardQueue — the HUD crystal count + the token pip are the feedback
    // (memory map-feedback-tooltip-and-log).
    private void HarvestHotspotIfParked()
    {
        var pp = FindAnyObjectByType<PlayerPosition>();
        var grid = FindAnyObjectByType<Grid>();
        if (pp == null || grid == null) return;
        var cell = grid.LocalToCell(pp.transform.position);
        if (!HotspotTracker.Instance.CanHarvest(cell)) return;

        var color = HotspotTracker.Instance.ColorAt(cell);
        FindAnyObjectByType<Crystals>().CreateCrystal(color);
        HotspotTracker.Instance.Harvest(cell);
        HotspotTracker.Instance.RefreshTokenVisuals();
    }
```

- [ ] **Step 2: Call it in `EndTurnPressed`**

In `EndTurnPressed`, add the call **before** `endTheTurn.Raise();` (so it happens while the turn is still resolving, before pools reset):

```csharp
        HarvestHotspotIfParked();
        endTheTurn.Raise(); // pools reset, hand top-up, TurnPlus (existing chain)
```

- [ ] **Step 3: Manual play-verification (editor)**

This is scene-behavior; verify in Play mode once Task 10 has placed a hotspot:
1. Move onto a hotspot cell, press End Turn.
2. Expected: the crystal HUD count for that color increases by 1; the token's charge pip drops; at 0 charges the token greys to dormant and further End Turns on it grant nothing.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/Managers/TurnPhaseController.cs"
git commit -m "feat: harvest crystal hotspot on End Turn when parked (free passive)"
```

---

### Task 9: HexTooltip enrichment — retrofit occupants + route through the registry

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonToken.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractor.cs`

**Interfaces:**
- Consumes: `IHexOccupant`/`HexOccupantRegistry` (Task 5), `TileDescriptor` (Task 4).
- Produces: `TownToken`, `DungeonToken` implement `IHexOccupant`; `HexInteractor.TooltipText` appends the occupant line; `PlaceOccupies` routes through the registry.

> Read each token first — `DungeonToken` already has `gridPos` and a `dungeonSO`; `TownToken` has `gridPos` and `townSO`. Reuse existing state (e.g. `DungeonTracker.Instance.DefeatedCount(gridPos)` for progress, `ConquestTracker` for town conquered-state) rather than adding fields.

- [ ] **Step 1: Make `DungeonToken` an `IHexOccupant`**

Add to `DungeonToken`'s class declaration `, IHexOccupant`, add `using ArchonsRise.HexTooltipInfo;`, register in `Start` (alongside the existing `DungeonTracker.Instance.Register`), and implement the members:

```csharp
    public Vector3Int Cell => gridPos;

    public HexDescriptor Describe()
        => new HexDescriptor(
            TileDescriptor.Dungeon(dungeonSO.cardName,
                DungeonTracker.Instance.DefeatedCount(gridPos), DungeonRules.DelveCount),
            TileDescriptor.PlacePriority);
```

Add to `Start()`: `HexOccupantRegistry.Instance.Register(this);` and add an `OnDestroy` unregister (mirror Task 7).

- [ ] **Step 2: Make `TownToken` an `IHexOccupant`**

Read `TownToken.cs` for its SO field name and place-type. Add `, IHexOccupant` + `using ArchonsRise.HexTooltipInfo;`, register in `Start`/`OnDestroy`, and implement:

```csharp
    public Vector3Int Cell => gridPos;

    public HexDescriptor Describe()
    {
        var icon = PlaceTypeIcon(townSO.placeType); // map Town/Keep/Castle -> IconConcept
        bool conquered = /* existing conquered check, e.g. ConquestTracker.Instance.IsConquered(gridPos) or guardians empty */;
        return new HexDescriptor(
            TileDescriptor.Town(townSO.cardName, icon, conquered),
            TileDescriptor.PlacePriority);
    }
```

Add a small local `PlaceTypeIcon(PlaceType) → IconConcept` mapping (`Town→IconConcept.Town`, `Keep→IconConcept.Keep`, `Castle→IconConcept.Castle`). Use the real conquered-state source already used elsewhere in `TownToken`/services gating.

- [ ] **Step 3: Route `HexInteractor.TooltipText` + `PlaceOccupies` through the registry**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractor.cs`, change `TooltipText` so an occupant line is appended (and shown even where it currently returns null). Replace the method's body tail:

```csharp
    string TooltipText(Vector3Int cell, HexAction verdict)
    {
        string exp = IconMarkup.Tag(IconConcept.Explore);
        string moveLine = MoveLine(cell, verdict, exp); // the existing switch, extracted
        string occupantLine = null;
        if (HexOccupantRegistry.Instance.Best(cell, out var d)) occupantLine = d.Line;

        if (string.IsNullOrEmpty(occupantLine)) return moveLine;
        if (string.IsNullOrEmpty(moveLine)) return occupantLine;
        return occupantLine + "\n" + moveLine;
    }
```

Extract the current `switch (verdict.Kind)` block into `string MoveLine(Vector3Int cell, HexAction verdict, string exp)` unchanged (it may still return null for the enemy/none cases). Then replace `PlaceOccupies`'s hardcoded scans:

```csharp
    protected bool PlaceOccupies(Vector3Int cell)
    {
        if (MapFog.IsHidden(cell)) return false;
        return HexOccupantRegistry.Instance.Occupied(cell);
    }
```

> `CrystalHotspotToken` is passive (movement onto it IS allowed — you must stand on it to harvest), unlike towns/dungeons which block move-dispatch. So `PlaceOccupies` must remain **place-only**. Give occupants a `BlocksMove` boolean (add `bool BlocksMove { get; }` to `IHexOccupant`; towns/dungeons `true`, hotspot `false`) and filter: `Occupied` → any occupant at the cell with `BlocksMove`. Update the three tokens accordingly.

- [ ] **Step 4: Add `BlocksMove` to the interface + implementers**

In `IHexOccupant.cs` add `bool BlocksMove { get; }`. `DungeonToken`/`TownToken`: `public bool BlocksMove => true;`. `CrystalHotspotToken`: `public bool BlocksMove => false;`. In `HexOccupantRegistry`, add `public bool Blocks(Vector3Int cell)` that returns true only if some occupant there has `BlocksMove`, and call **that** from `PlaceOccupies` (keep `Occupied` for any-occupant, `Best` for the tooltip which shows all).

- [ ] **Step 5: Manual play-verification (editor)**

Verify in Play mode (after Task 10 places tiles): hovering a town shows `<type> Name (— guarded/conquered)`; a dungeon shows `<dungeon> Name (n/3)`; a hotspot shows `<crystal> ×N` / `∞` / `Depleted`; moving onto a hotspot still works; moving onto a town/dungeon still dispatches the stand-on behavior (not a raw move).

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs" "Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonToken.cs" "Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractor.cs" "Assets/Scripts/GameObjectScripts/GameBoardObjects/IHexOccupant.cs" "Assets/Scripts/Managers/HexOccupantRegistry.cs"
git commit -m "feat: tooltip describes tile occupants via IHexOccupant registry"
```

---

### Task 10: Placement in `GridGeneration` + editor authoring (USER)

**Files:**
- Modify: `Assets/Scripts/TilemapScripts/GridGeneration.cs`
- USER editor: create the rule-tile `.asset`, the hotspot token prefab, and ≥4 `CrystalHotspotSO` assets (one per color); wire the new `GridGeneration` serialized fields.

**Interfaces:**
- Consumes: `SpawnRules.SeedZones`/`Spacing`, `CrystalHotspotToken` (Task 7), `CrystalHotspotSO` (Task 6).

- [ ] **Step 1: Add serialized fields + placement (mirror the dungeon block at `GridGeneration.cs:178-212`)**

Add fields near the dungeon ones (~line 24):

```csharp
    [SerializeField] TileBase hotspotTile;
    [SerializeField] GameObject hotspotTokenPrefab;
    [SerializeField] int hotspotCount = 6;         // tuning; TBD in playtest
    [SerializeField] int hotspotMinSpacing = 3;    // tuning; TBD in playtest
    [SerializeField] List<CrystalHotspotSO> hotspotPool = new();
```

After the dungeon placement block (after `GridGeneration.cs:212`), add:

```csharp
        // Crystal-hotspot placement (spec 2026-07-24): spaced like dungeons,
        // never on towns/dungeons or inside the start safe radius; SOs assigned
        // seed-randomly. Only charge state is saved (HotspotState pattern).
        if (hotspotTile != null && hotspotTokenPrefab != null && hotspotPool.Count > 0)
        {
            var hCandidates = new List<ArchonsRise.SaveData.Cell>();
            for (int x = 0; x < 20; x++)
                for (int y = 0; y < 20; y++)
                {
                    var pos = new Vector3Int(x, y);
                    if (!ground.HasTile(pos) || ground.GetTile(pos) == townTile || ground.GetTile(pos) == dungeonTile) continue;
                    var cell = new ArchonsRise.SaveData.Cell(x, y);
                    if (SpawnRules.Spacing(cell, new ArchonsRise.SaveData.Cell(0, 0)) < doomTuning.tuning.startSafeRadius) continue;
                    hCandidates.Add(cell);
                }
            var hotspotCells = SpawnRules.SeedZones(hCandidates, hotspotCount, hotspotMinSpacing, max => Rng(0, max));

            var hbag = new List<CrystalHotspotSO>(hotspotPool);
            foreach (var cell in hotspotCells)
            {
                if (hbag.Count == 0) hbag.AddRange(hotspotPool);
                var so = hbag[Rng(0, hbag.Count)];
                hbag.Remove(so);

                var tilePos = new Vector3Int(cell.x, cell.y);
                ground.SetTile(tilePos, hotspotTile);
                var token = Instantiate(hotspotTokenPrefab,
                    ground.CellToWorld(tilePos) + new Vector3(0, -1), Quaternion.identity, townParentObject);
                var placed = token.GetComponent<CrystalHotspotToken>();
                placed.hotspotSO = so;
                placed.gridPos = tilePos;
            }
        }
```

Also add `&& ground.GetTile(pos) != hotspotTile` to the later enemy/town/dungeon exclusion checks that currently list `townTile`/`dungeonTile` (so enemies/other tiles never overwrite a hotspot). Grep `== dungeonTile` in this file and mirror each check for `hotspotTile`.

- [ ] **Step 2: Commit the code**

```bash
git add "Assets/Scripts/TilemapScripts/GridGeneration.cs"
git commit -m "feat: seed crystal hotspots into the map (dungeon-spacing pattern)"
```

- [ ] **Step 3: USER editor authoring** (manual — see memory `manual-unity-edits-for-risky-changes`)

1. **Rule tile asset:** `Assets ▸ Create ▸ ScriptableObjects ▸ Tiles ▸ Crystal Hotspot Rule Tiles`; assign the hotspot sprite(s), matching how the Dungeon Rule Tile asset is set up.
2. **Hotspot token prefab:** duplicate the `Dungeon Token.prefab` as `Crystal Hotspot Token.prefab`; replace `DungeonToken` with `CrystalHotspotToken`; assign `crystalSprite`, `livePips`, `dormantMarker` children.
3. **Content assets:** `Assets ▸ Create ▸ ScriptableObjects ▸ CrystalHotspot` ×4+ — one per color (Red/Yellow/Green/Purple), each with a unique stable `id` (e.g. `hotspot_red`), its `color`, and `charges` (e.g. 3; author one as `-1` unlimited rich vein).
4. **Wire GridGeneration:** on the GridGeneration object in `GameBoard.unity`, assign `hotspotTile`, `hotspotTokenPrefab`, and populate `hotspotPool` with the SOs. Leave `hotspotCount`/`hotspotMinSpacing` at defaults.

- [ ] **Step 4: USER acceptance play-through**

1. New run → confirm hotspots appear, spaced, never on towns/dungeons or the start ring.
2. Park on one, End Turn → correct-color crystal added, pip drops; deplete a finite one → dormant; unlimited one never depletes.
3. Tooltip shows each occupant line (town/keep/castle/dungeon/hotspot).
4. Save mid-run after a partial harvest, quit, reload → charge counts and dormant state restored; a fresh (untouched) hotspot re-derives full.

---

### Task 11: Documentation updates

**Files:**
- Modify: `.claude/skills/archons-rise-design/content-rules.md`
- Modify: `.claude/skills/archons-rise-roadmap/milestones.md`, `status.md`, `decisions-log.md`

- [ ] **Step 1: Content-rules — add `CrystalHotspotSO` + the `IHexOccupant` note**

Add a `## Crystal Hotspot — CrystalHotspotSO` section (menu `ScriptableObjects/CrystalHotspot`; fields `id`, `color`, `charges` with the `-1` unlimited sentinel; passive End-Turn harvest, free), and a short note that map tokens describe themselves to the tooltip via `IHexOccupant` + `TileDescriptor`.

- [ ] **Step 2: Roadmap — record the milestone + decision**

In `milestones.md` add the shipped scope under a new **M2.15 — Crystal Hotspots (Plan 1)** entry (or the agreed number); update `status.md`'s "Exists" list; append a `decisions-log.md` entry dated 2026-07-24 summarizing: passive charge-limited harvest with `-1` unlimited sentinel, free (not the Action), no popup (tooltip/log direction), and the extensible `IHexOccupant` tooltip foundation.

- [ ] **Step 3: Commit**

```bash
git add ".claude/skills/archons-rise-design/content-rules.md" ".claude/skills/archons-rise-roadmap/milestones.md" ".claude/skills/archons-rise-roadmap/status.md" ".claude/skills/archons-rise-roadmap/decisions-log.md"
git commit -m "docs: content-rules + roadmap for crystal hotspots & hex tooltip foundation"
```

---

## Self-Review

**Spec coverage (Plan 1 scope):**
- §1 Crystal Hotspot content/tile/tracker/rules → Tasks 1,2,6,7. ✓
- §1 harvest trigger (free passive, End Turn) → Task 8. ✓
- §1 charge sentinel `-1` unlimited → Tasks 1,2 (tested). ✓
- §1 feedback: no popup, token visuals → Tasks 7,8 (`RefreshVisual`, no `ValidationMessage`). ✓
- §1b HexTooltip enrichment (occupant line, town/keep/castle/dungeon) → Tasks 4,9. ✓
- §1c `IHexOccupant` extensible model + registry → Tasks 5,9. ✓
- §4 save v8 (hotspots) → Tasks 2,3,6. ✓
- §4 placement seeded/spaced like dungeons → Task 10. ✓
- §4 pure tests (HotspotRules, TileDescriptor) → Tasks 1,4 (+ HotspotLedger). ✓
- §4 content-rules addition → Task 11. ✓
- **Deferred to Plan 2:** all Shrine sections (§2, §3), the shrine parts of §4 save (shrines[]/spawned-enemy reward → v9), and `TileDescriptor.Shrine`. Noted, not a gap.

**Placeholder scan:** No "TBD"/"handle edge cases"/"similar to Task N". The two `> Note` blocks point at real files to confirm an existing API name (`IconMarkup`, the `Crystals`/serializer seam) — verification instructions, not placeholders; each has a concrete fallback.

**Type consistency:** `HotspotRules.CanHarvest/NextCharges`, `HotspotLedger.Register(Cell,string,int)/Harvest/Export/ApplySavedState`, `HotspotState{x,y,hotspotId,remainingCharges}`, `HotspotTracker.Register(Vector3Int,string,int,EmpowerType)/CanHarvest/ColorAt/Harvest/Remaining/Export/ApplySave`, `IHexOccupant{Cell,Describe,BlocksMove}`, `HexDescriptor{Line,Priority}`, `TileDescriptor.Hotspot/Town/Dungeon` — names are consistent across the tasks that consume them. `schemaVersion` 7→8 consistent (SaveModels default + migrator bump + test).
