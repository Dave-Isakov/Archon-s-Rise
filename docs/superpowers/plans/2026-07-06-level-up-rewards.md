# M2.4 Level-Up Rewards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Level-ups pay out from a data-driven reward table: skill picks (1 of 3), card picks (existing reward screen), +HP, +hand size, +army size — with an exhaustible skill bar and an army cap with disband-to-hire.

**Architecture:** Pure rules (`LevelRules`, `ArmyRules`) in a new `ArchonsRise.Leveling` asmdef, TDD'd via the mcs CLI harness. Content is ScriptableObjects (`SkillsSO`, `LevelRewardsSO`). Runtime MonoBehaviours (skill bar, level-up modal, disband panel) follow the existing event/command patterns. Save schema goes v2 → v3 (owned + exhausted skill ids only; hand/army derived from level).

**Tech Stack:** Unity 6000.5.1f1, C#, NUnit (EditMode), Mono `mcs` CLI harness for pure logic, TextMeshPro, the project's ScriptableObject event system + `ICommands` undo stack.

**Spec:** `docs/superpowers/specs/2026-07-06-level-up-rewards-design.md`

## Global Constraints

- **No git worktree** — Unity holds `Library/`; work on `master` like every prior milestone.
- **Never hand-edit scene/prefab YAML.** All scene, prefab, and asset wiring is done by the user in the editor from the USER ACTION steps. `.asmdef` JSON may be edited directly.
- **The Unity editor holds the compile lock** — batch-mode tests won't run while it's open. Pure logic is RED/GREEN-verified with the mcs harness; EditMode tests run in the editor's Test Runner at acceptance (Task 11).
- **Unity generates `.meta` files** — never create them by hand; after adding `.cs` files, focus the editor and let it import, then commit code + generated `.meta` together.
- Save schema v3; migration from v2 must default new fields (no skills, sizes derived from level).
- Baselines (from `balance.md`): hand size **5** (PlayerSO), army cap **1**, HP **2**. Exp curve stays `expToNextLevel += playerLevel + 12`.
- Commit messages: `feat:`/`test:`/`chore:` prefixes, ending with the Claude co-author line, as in recent history.

## mcs Harness (used by Tasks 1–3)

One-time per session. The reflection runner lives at `<scratchpad>/Runner.cs` (create if absent):

```csharp
using System;
using System.Linq;
using System.Reflection;

class Runner
{
    static int Main(string[] args)
    {
        var asm = Assembly.LoadFrom(args[0]);
        int pass = 0, fail = 0;
        foreach (var t in asm.GetTypes())
        {
            foreach (var m in t.GetMethods().Where(x =>
                x.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute")))
            {
                var inst = Activator.CreateInstance(t);
                try { m.Invoke(inst, null); Console.WriteLine($"PASS {t.Name}.{m.Name}"); pass++; }
                catch (TargetInvocationException e)
                { Console.WriteLine($"FAIL {t.Name}.{m.Name}: {e.InnerException.Message}"); fail++; }
            }
        }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail > 0 ? 1 : 0;
    }
}
```

Setup (PowerShell, repo root; `<scratchpad>` = the session scratchpad dir):

```powershell
$mcs = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge\bin\mcs.bat"
$nunit = (Get-ChildItem "Library\PackageCache\com.unity.ext.nunit*\net472\unity-custom\nunit.framework.dll").FullName
$s = "<scratchpad>"
Copy-Item $nunit $s -Force
& $mcs -nologo "-out:$s\Runner.exe" "$s\Runner.cs"
```

---

### Task 1: Leveling asmdef + `LevelRewardEntry` + `LevelRules` (pure, TDD)

**Files:**
- Create: `Assets/Scripts/Leveling/ArchonsRise.Leveling.asmdef`
- Create: `Assets/Scripts/Leveling/LevelRewardEntry.cs`
- Create: `Assets/Scripts/Leveling/LevelRules.cs`
- Test: `Assets/Tests/EditMode/LevelRulesTests.cs`
- Modify: `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` (add reference)

**Interfaces:**
- Consumes: nothing (leaf).
- Produces (later tasks call these exactly):
  - `class LevelRewardEntry { int level; int hpBonus; int handSizeBonus; int armySizeBonus; int skillPicks; int cardPicks; }`
  - `LevelRules.RewardsFor(int level, IReadOnlyList<LevelRewardEntry> entries) → LevelRewardEntry` (null if none)
  - `LevelRules.DerivedHandSize(int baseHandSize, int level, IReadOnlyList<LevelRewardEntry> entries) → int`
  - `LevelRules.DerivedArmyCap(int level, IReadOnlyList<LevelRewardEntry> entries) → int` (base `LevelRules.BaseArmyCap = 1`)
  - `LevelRules.CarriedExp(int exp, int expToNextLevel) → int`
  - `LevelRules.DrawSkillChoices<T>(IReadOnlyList<T> pool, ICollection<T> owned, System.Random rng, int count = 3) → List<T>`

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/LevelRulesTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class LevelRulesTests
{
    static List<LevelRewardEntry> Table() => new List<LevelRewardEntry>
    {
        new LevelRewardEntry { level = 2, skillPicks = 1 },
        new LevelRewardEntry { level = 3, hpBonus = 1, cardPicks = 1 },
        new LevelRewardEntry { level = 4, handSizeBonus = 1, armySizeBonus = 1 },
        new LevelRewardEntry { level = 7, skillPicks = 1, armySizeBonus = 1 },
    };

    [Test]
    public void RewardsFor_ReturnsMatchingEntryOrNull()
    {
        Assert.AreEqual(1, LevelRules.RewardsFor(2, Table()).skillPicks);
        Assert.AreEqual(1, LevelRules.RewardsFor(3, Table()).cardPicks);
        Assert.IsNull(LevelRules.RewardsFor(5, Table()));   // no entry for 5
        Assert.IsNull(LevelRules.RewardsFor(99, Table()));  // past the table
    }

    [Test]
    public void DerivedHandSize_SumsBonusesUpToLevel()
    {
        Assert.AreEqual(5, LevelRules.DerivedHandSize(5, 1, Table()));
        Assert.AreEqual(5, LevelRules.DerivedHandSize(5, 3, Table()));  // bonus is at 4
        Assert.AreEqual(6, LevelRules.DerivedHandSize(5, 4, Table()));
        Assert.AreEqual(6, LevelRules.DerivedHandSize(5, 9, Table()));  // no later bonuses
    }

    [Test]
    public void DerivedArmyCap_StartsAtOneAndSums()
    {
        Assert.AreEqual(1, LevelRules.DerivedArmyCap(1, Table()));
        Assert.AreEqual(2, LevelRules.DerivedArmyCap(4, Table()));
        Assert.AreEqual(3, LevelRules.DerivedArmyCap(7, Table()));
        Assert.AreEqual(3, LevelRules.DerivedArmyCap(20, Table()));
    }

    [Test]
    public void CarriedExp_KeepsOverflowAndClampsAtZero()
    {
        Assert.AreEqual(3, LevelRules.CarriedExp(18, 15)); // overflow carries
        Assert.AreEqual(0, LevelRules.CarriedExp(15, 15)); // exact
        Assert.AreEqual(0, LevelRules.CarriedExp(10, 15)); // defensive clamp
    }

    [Test]
    public void DrawSkillChoices_ExcludesOwnedNoDuplicates()
    {
        var pool = new List<string> { "a", "b", "c", "d", "e" };
        var owned = new List<string> { "b", "d" };
        var rng = new System.Random(42);
        var picks = LevelRules.DrawSkillChoices(pool, owned, rng, 3);

        Assert.AreEqual(3, picks.Count);
        CollectionAssert.AllItemsAreUnique(picks);
        CollectionAssert.IsNotSubsetOf(new[] { "b" }, picks);
        CollectionAssert.IsNotSubsetOf(new[] { "d" }, picks);
    }

    [Test]
    public void DrawSkillChoices_ReturnsFewerWhenPoolRunsDry()
    {
        var pool = new List<string> { "a", "b" };
        var rng = new System.Random(1);
        Assert.AreEqual(2, LevelRules.DrawSkillChoices(pool, new List<string>(), rng, 3).Count);
        Assert.AreEqual(0, LevelRules.DrawSkillChoices(pool, new List<string> { "a", "b" }, rng, 3).Count);
    }
}
```

- [ ] **Step 2: Run the harness to verify it fails (RED)**

```powershell
& $mcs -nologo -target:library "-out:$s\Leveling.dll" "-r:$nunit" "Assets\Scripts\Leveling\LevelRewardEntry.cs" "Assets\Scripts\Leveling\LevelRules.cs" "Assets\Tests\EditMode\LevelRulesTests.cs"
```

Expected: FAILS — source files missing / `CS0246 LevelRules`.

- [ ] **Step 3: Write the minimal implementation**

Create `Assets/Scripts/Leveling/LevelRewardEntry.cs`:

```csharp
// One row of the level reward table. Plain serializable data — no UnityEngine —
// so LevelRules stays testable from the CLI mcs harness. All fields are counts,
// never booleans: every reward knob is tunable per level in the inspector.
[System.Serializable]
public class LevelRewardEntry
{
    public int level;
    public int hpBonus;
    public int handSizeBonus;
    public int armySizeBonus;
    public int skillPicks;
    public int cardPicks;
}
```

Create `Assets/Scripts/Leveling/LevelRules.cs`:

```csharp
using System.Collections.Generic;

// Pure leveling rules. No scene/Unity dependency (mirrors CombatRules /
// PlaceRules). Hand size and army cap are DERIVED from level + table — never
// stored — so saves stay lean and can't drift out of sync with the table.
public static class LevelRules
{
    public const int BaseArmyCap = 1;

    // The table row for this exact level; null when the level grants nothing.
    public static LevelRewardEntry RewardsFor(int level, IReadOnlyList<LevelRewardEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].level == level) return entries[i];
        return null;
    }

    public static int DerivedHandSize(int baseHandSize, int level, IReadOnlyList<LevelRewardEntry> entries)
    {
        int size = baseHandSize;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].level <= level) size += entries[i].handSizeBonus;
        return size;
    }

    public static int DerivedArmyCap(int level, IReadOnlyList<LevelRewardEntry> entries)
    {
        int cap = BaseArmyCap;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].level <= level) cap += entries[i].armySizeBonus;
        return cap;
    }

    // Exp past the threshold carries into the next level (the old code reset to
    // 0 and discarded overflow). Clamped for safety against bad saved values.
    public static int CarriedExp(int exp, int expToNextLevel)
    {
        int carried = exp - expToNextLevel;
        return carried < 0 ? 0 : carried;
    }

    // Up to `count` distinct random picks from the pool, excluding owned.
    // Generic so it's Unity-free; callers pass SkillsSO lists, tests pass strings.
    public static List<T> DrawSkillChoices<T>(IReadOnlyList<T> pool, ICollection<T> owned,
        System.Random rng, int count = 3)
    {
        var candidates = new List<T>();
        for (int i = 0; i < pool.Count; i++)
            if (!owned.Contains(pool[i]) && !candidates.Contains(pool[i]))
                candidates.Add(pool[i]);

        var picks = new List<T>();
        while (picks.Count < count && candidates.Count > 0)
        {
            int idx = rng.Next(candidates.Count);
            picks.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }
        return picks;
    }
}
```

Create `Assets/Scripts/Leveling/ArchonsRise.Leveling.asmdef`:

```json
{
    "name": "ArchonsRise.Leveling",
    "rootNamespace": "",
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

In `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`, add `"ArchonsRise.Leveling"` to the `references` array (after `"ArchonsRise.Hand"`).

- [ ] **Step 4: Run the harness to verify it passes (GREEN)**

```powershell
& $mcs -nologo -target:library "-out:$s\Leveling.dll" "-r:$nunit" "Assets\Scripts\Leveling\LevelRewardEntry.cs" "Assets\Scripts\Leveling\LevelRules.cs" "Assets\Tests\EditMode\LevelRulesTests.cs"
& "$s\Runner.exe" "$s\Leveling.dll"
```

Expected: `6 passed, 0 failed`.

- [ ] **Step 5: USER ACTION — focus Unity, confirm console compiles clean, then commit**

```bash
git add Assets/Scripts/Leveling Assets/Tests/EditMode/LevelRulesTests.cs Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef
git add Assets/Scripts/Leveling.meta Assets/Tests/EditMode/LevelRulesTests.cs.meta 2>/dev/null || true
git commit -m "feat: pure LevelRules + LevelRewardEntry in new Leveling asmdef"
```

---

### Task 2: `ArmyRules` (pure, TDD)

**Files:**
- Create: `Assets/Scripts/Leveling/ArmyRules.cs`
- Test: `Assets/Tests/EditMode/ArmyRulesTests.cs`

**Interfaces:**
- Produces: `ArmyRules.CanRecruit(int unitCount, int cap) → bool`, `ArmyRules.NeedsDisband(int unitCount, int cap) → bool` (used by Task 8's RecruitButton/DisbandPanel).

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/ArmyRulesTests.cs`:

```csharp
using NUnit.Framework;

public class ArmyRulesTests
{
    [Test]
    public void CanRecruit_OnlyBelowCap()
    {
        Assert.IsTrue(ArmyRules.CanRecruit(0, 1));
        Assert.IsFalse(ArmyRules.CanRecruit(1, 1));
        Assert.IsFalse(ArmyRules.CanRecruit(2, 1)); // over-cap (bad state) still blocks
    }

    [Test]
    public void NeedsDisband_AtOrAboveCap()
    {
        Assert.IsFalse(ArmyRules.NeedsDisband(0, 1));
        Assert.IsTrue(ArmyRules.NeedsDisband(1, 1));
        Assert.IsTrue(ArmyRules.NeedsDisband(3, 2));
    }
}
```

- [ ] **Step 2: Run the harness to verify it fails (RED)**

```powershell
& $mcs -nologo -target:library "-out:$s\Army.dll" "-r:$nunit" "Assets\Scripts\Leveling\ArmyRules.cs" "Assets\Tests\EditMode\ArmyRulesTests.cs"
```

Expected: FAILS — `ArmyRules.cs` missing.

- [ ] **Step 3: Write the minimal implementation**

Create `Assets/Scripts/Leveling/ArmyRules.cs`:

```csharp
// Pure army-cap rules (mirrors TurnButtonGate). The cap itself comes from
// LevelRules.DerivedArmyCap; these gates decide what the Recruit flow does.
public static class ArmyRules
{
    public static bool CanRecruit(int unitCount, int cap) => unitCount < cap;

    // At (or somehow above) cap, hiring requires disbanding an existing unit.
    public static bool NeedsDisband(int unitCount, int cap) => unitCount >= cap;
}
```

- [ ] **Step 4: Run the harness to verify it passes (GREEN)**

```powershell
& $mcs -nologo -target:library "-out:$s\Army.dll" "-r:$nunit" "Assets\Scripts\Leveling\ArmyRules.cs" "Assets\Tests\EditMode\ArmyRulesTests.cs"
& "$s\Runner.exe" "$s\Army.dll"
```

Expected: `2 passed, 0 failed`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Leveling/ArmyRules.cs Assets/Tests/EditMode/ArmyRulesTests.cs
git commit -m "feat: pure ArmyRules recruit/disband gates"
```

---

### Task 3: Save schema v3 + migrator (pure, TDD)

**Files:**
- Modify: `Assets/Scripts/SaveData/SaveModels.cs`
- Modify: `Assets/Scripts/SaveData/SaveMigrator.cs`
- Modify: `Assets/Scripts/Managers/DataManager.cs:206` and `:242,:246` (remove handSize use, bump capture version)
- Test: `Assets/Scripts/SaveData/Tests/SaveMigratorV3Tests.cs`

**Interfaces:**
- Produces: `PlayerState.ownedSkillIds : string[]`, `PlayerState.exhaustedSkillIds : string[]` (Task 9 fills them). `PlayerState.handSize` is **deleted** (derived from level per spec).

- [ ] **Step 1: Write the failing test**

Create `Assets/Scripts/SaveData/Tests/SaveMigratorV3Tests.cs`:

```csharp
using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV3Tests
{
    [Test]
    public void MigratesV2_DefaultsSkillArraysAndBumpsVersion()
    {
        var file = new SaveFile { schemaVersion = 2 };
        file.run.player.ownedSkillIds = null;      // v2 json has no such keys
        file.run.player.exhaustedSkillIds = null;

        var migrated = SaveMigrator.Migrate(file);

        Assert.AreEqual(3, migrated.schemaVersion);
        Assert.IsNotNull(migrated.run.player.ownedSkillIds);
        Assert.IsEmpty(migrated.run.player.ownedSkillIds);
        Assert.IsNotNull(migrated.run.player.exhaustedSkillIds);
        Assert.IsEmpty(migrated.run.player.exhaustedSkillIds);
    }

    [Test]
    public void MigrationIsIdempotentOnV3()
    {
        var file = new SaveFile();
        file.run.player.ownedSkillIds = new[] { "skill-envoy" };
        var migrated = SaveMigrator.Migrate(file);
        Assert.AreEqual(3, migrated.schemaVersion);
        Assert.AreEqual(new[] { "skill-envoy" }, migrated.run.player.ownedSkillIds);
    }
}
```

- [ ] **Step 2: Run the harness to verify it fails (RED)**

```powershell
& $mcs -nologo -target:library "-out:$s\SaveV3.dll" "-r:$nunit" "Assets\Scripts\SaveData\SaveModels.cs" "Assets\Scripts\SaveData\SaveMigrator.cs" "Assets\Scripts\SaveData\Tests\SaveMigratorV3Tests.cs"
```

Expected: FAILS — `CS1061: 'PlayerState' does not contain a definition for 'ownedSkillIds'`.

- [ ] **Step 3: Update the models and migrator**

In `Assets/Scripts/SaveData/SaveModels.cs`:

1. `SaveFile` — change the version comment and default:

```csharp
    [Serializable]
    public class SaveFile
    {
        // v3 (M2.4): adds PlayerState.ownedSkillIds/exhaustedSkillIds; removes
        // PlayerState.handSize (hand size and army cap derive from level via
        // the LevelRewardsSO table, so storing them could only drift).
        public int schemaVersion = 3;
        public RunState run = new RunState();
    }
```

2. `PlayerState` — remove `public int handSize;` and add the two arrays:

```csharp
    [Serializable]
    public class PlayerState
    {
        public int hp;
        public int level;
        public int exp;
        public int expToNextLevel;
        public int attack;
        public int defend;
        public int influence;
        public int explore;
        public string[] ownedSkillIds = Array.Empty<string>();
        public string[] exhaustedSkillIds = Array.Empty<string>();
        public float[] position = new float[3];
    }
```

In `Assets/Scripts/SaveData/SaveMigrator.cs`, replace `Migrate` with:

```csharp
        public static SaveFile Migrate(SaveFile file)
        {
            // v1 -> v2: places array did not exist; absent means nothing conquered.
            if (file.run.places == null)
                file.run.places = Array.Empty<PlaceConquest>();

            // v2 -> v3: skill arrays did not exist; absent means no skills owned.
            // (handSize was dropped from the model: JsonUtility ignores the stale
            // key in old files, and the value is derived from level on load.)
            if (file.run.player.ownedSkillIds == null)
                file.run.player.ownedSkillIds = Array.Empty<string>();
            if (file.run.player.exhaustedSkillIds == null)
                file.run.player.exhaustedSkillIds = Array.Empty<string>();

            if (file.schemaVersion < 3)
                file.schemaVersion = 3;
            return file;
        }
```

In `Assets/Scripts/Managers/DataManager.cs` (keeps the project compiling — Player still has its field until Task 5):
- Delete line 206: `player.PlayerHandSize  = run.player.handSize;`
- Delete line 246: `run.player.handSize      = player.PlayerHandSize;`
- Change line 242 to: `var file = new SaveFile { schemaVersion = 3 };`

- [ ] **Step 4: Run the harness to verify it passes (GREEN)**

```powershell
& $mcs -nologo -target:library "-out:$s\SaveV3.dll" "-r:$nunit" "Assets\Scripts\SaveData\SaveModels.cs" "Assets\Scripts\SaveData\SaveMigrator.cs" "Assets\Scripts\SaveData\Tests\SaveMigratorV3Tests.cs"
& "$s\Runner.exe" "$s\SaveV3.dll"
```

Expected: `2 passed, 0 failed`.

- [ ] **Step 5: USER ACTION — focus Unity, confirm clean compile. Commit**

```bash
git add Assets/Scripts/SaveData/SaveModels.cs Assets/Scripts/SaveData/SaveMigrator.cs Assets/Scripts/SaveData/Tests/SaveMigratorV3Tests.cs Assets/Scripts/Managers/DataManager.cs
git commit -m "feat: save schema v3 - skill ids in, stored handSize out"
```

---

### Task 4: Skill enums, `SkillsSO`, `LevelRewardsSO`, `SkillEvent` trio, `SkillCommand`

**Files:**
- Create: `Assets/Scripts/Enums/Enums/SkillEffect.cs`
- Create: `Assets/Scripts/Enums/Enums/SkillCadence.cs`
- Create: `Assets/Scripts/GameScriptableObjectTypes/SkillsSO.cs`
- Create: `Assets/Scripts/GameScriptableObjectTypes/LevelRewardsSO.cs`
- Create: `Assets/Scripts/GameEvents/EventTypes/EventTypes/SkillEvent.cs`
- Create: `Assets/Scripts/GameEvents/UnityEvents/UnitySkillEvent.cs`
- Create: `Assets/Scripts/GameEvents/Listener/SkillListener.cs`
- Create: `Assets/Scripts/Managers/Commands/SkillCommand.cs`

**Interfaces:**
- Consumes: `LevelRewardEntry` (Task 1), `AllCards` base (`id`, `cardName`, `cardDescription`), `BaseGameEvent<T>` / `BaseGameEventListener` pattern, `ICommands`.
- Produces: `SkillsSO` (fields below), `LevelRewardsSO.SkillPool : IReadOnlyList<SkillsSO>`, `LevelRewardsSO.Entries : IReadOnlyList<LevelRewardEntry>`, `SkillEvent : BaseGameEvent<SkillToken>`, `SkillCommand(SkillEvent, SkillToken)`. **Note:** `SkillToken` (the MonoBehaviour) is created in Task 6 — this task's `SkillEvent`, `UnitySkillEvent`, `SkillListener`, and `SkillCommand` reference it, so **Tasks 4–6 compile together**; commit at the end of Task 6. Steps stay separated for review clarity.

- [ ] **Step 1: Write the enums**

`Assets/Scripts/Enums/Enums/SkillEffect.cs`:

```csharp
// What activating a skill does. Stat gains feed the same per-turn pools as
// cards/units; GainCrystal and HealWound reuse the existing crystal / heal paths.
public enum SkillEffect
{
    GainAttack,
    GainDefend,
    GainInfluence,
    GainExplore,
    GainCrystal,
    HealWound,
}
```

`Assets/Scripts/Enums/Enums/SkillCadence.cs`:

```csharp
// How often a used skill refreshes. PerTurn = weak effects, refresh at turn
// end. PerRound = strong effects (crystals, healing), refresh at round end.
public enum SkillCadence
{
    PerTurn,
    PerRound,
}
```

- [ ] **Step 2: Write the ScriptableObject types**

`Assets/Scripts/GameScriptableObjectTypes/SkillsSO.cs` (inherits `AllCards` for the stable `id` + name/description, like `UnitsSO`):

```csharp
using UnityEngine;

// A level-up skill: an activatable, exhaustible ability on the skill bar.
// id (from AllCards) is the stable save identity — never rename ids.
[CreateAssetMenu(fileName = "Skill", menuName = "ScriptableObjects/Skill")]
public class SkillsSO : AllCards
{
    public Sprite icon;
    public SkillEffect effect;
    public int magnitude = 1;
    // Only meaningful for SkillEffect.GainCrystal.
    public EmpowerType crystalColor;
    public SkillCadence cadence;
}
```

`Assets/Scripts/GameScriptableObjectTypes/LevelRewardsSO.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

// THE level reward table: one asset drives all level-up payouts, so every
// balance change during playtesting is an inspector edit on this asset.
[CreateAssetMenu(fileName = "LevelRewards", menuName = "ScriptableObjects/LevelRewards")]
public class LevelRewardsSO : ScriptableObject
{
    [SerializeField] List<SkillsSO> skillPool = new();
    [SerializeField] List<LevelRewardEntry> entries = new();

    public IReadOnlyList<SkillsSO> SkillPool => skillPool;
    public IReadOnlyList<LevelRewardEntry> Entries => entries;
}
```

- [ ] **Step 3: Write the event trio + command**

`Assets/Scripts/GameEvents/EventTypes/EventTypes/SkillEvent.cs`:

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "New Skill Event", menuName = "Game Events/Skill Event")]
public class SkillEvent : BaseGameEvent<SkillToken>
{
}
```

`Assets/Scripts/GameEvents/UnityEvents/UnitySkillEvent.cs`:

```csharp
using UnityEngine.Events;

[System.Serializable] public class UnitySkillEvent : UnityEvent<SkillToken> { }
```

`Assets/Scripts/GameEvents/Listener/SkillListener.cs`:

```csharp
public class SkillListener : BaseGameEventListener<SkillToken, SkillEvent, UnitySkillEvent> { }
```

`Assets/Scripts/Managers/Commands/SkillCommand.cs` (mirrors `UnitCommand` — Execute/Undo raise the same toggle event):

```csharp
public class SkillCommand : ICommands
{
    SkillToken _token;
    SkillEvent _skillAction;

    public SkillCommand(SkillEvent skillEvent, SkillToken token)
    {
        _skillAction = skillEvent;
        _token = token;
    }

    public void Execute()
    {
        _skillAction.Raise(_token);
    }

    public void Undo()
    {
        _skillAction.Raise(_token);
    }
}
```

- [ ] **Step 4: Continue to Task 5 (no commit yet — `SkillToken` lands in Task 6)**

---

### Task 5: Player skill state, effects, derived sizes; CrystalInventory skill stack

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs`
- Modify: `Assets/Scripts/Managers/GameManager.cs:95-101` (`RoundPlus`)

**Interfaces:**
- Consumes: `SkillsSO`, `SkillCadence`, `SkillEffect` (Task 4), `LevelRules` (Task 1), `SkillToken` (Task 6: `skillSO`, `IsUsed`, `SetUsed(bool)`, `Bind(SkillsSO)`).
- Produces (Tasks 6–9 call these exactly):
  - `Player.Skills : IReadOnlyList<SkillsSO>`
  - `Player.LevelRewards : LevelRewardsSO`
  - `Player.PlayerHandSize : int` (get-only, derived) — **setter removed**
  - `Player.ArmyCap : int` (get-only, derived)
  - `Player.AddSkill(SkillsSO)`
  - `Player.RebuildSkills(List<SkillsSO> skills, HashSet<string> exhaustedIds)`
  - `Player.RefreshSkills(bool includePerRound)`
  - `Player.PerformSkillAction(SkillToken token)` (the SkillEvent listener target)
  - `CrystalInventory.SkillCrystallize(EmpowerType)`, `CrystalInventory.UndoSkillCrystallize()`

- [ ] **Step 1: Player changes**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`:

1. Replace the field `private int playerHandSize = 5;` (line 15) with nothing (deleted), and delete the `Awake()` body line `playerHandSize = player.PlayerHandSize;` (the empty `Awake` can be removed entirely).
2. Add fields after the `units` list (line 27):

```csharp
    [SerializeField] LevelRewardsSO levelRewards;
    [SerializeField] List<SkillsSO> skills = new();
```

3. Replace the `PlayerHandSize` property (line 29) and add the new accessors next to `Units` (line 41):

```csharp
    // Derived, never stored: base size from PlayerSO plus every table bonus at
    // or below the current level. Same derivation on load, so saves can't drift.
    public int PlayerHandSize => LevelRules.DerivedHandSize(player.PlayerHandSize, playerLevel, levelRewards.Entries);
    public int ArmyCap => LevelRules.DerivedArmyCap(playerLevel, levelRewards.Entries);
    public LevelRewardsSO LevelRewards => levelRewards;
    public IReadOnlyList<SkillsSO> Skills => skills;
```

4. Add the skill methods after `PlayUnit` (line 326):

```csharp
    // SkillEvent listener target. Toggles like PlayUnit: the same event fires on
    // command Execute and Undo, so IsUsed decides apply vs revert.
    public void PerformSkillAction(SkillToken token)
    {
        if (!token.IsUsed)
        {
            ApplySkillEffect(token.skillSO, +1);
            token.SetUsed(true);
        }
        else
        {
            ApplySkillEffect(token.skillSO, -1);
            token.SetUsed(false);
        }
    }

    private void ApplySkillEffect(SkillsSO skill, int sign)
    {
        switch (skill.effect)
        {
            case SkillEffect.GainAttack:    playerAttack    += sign * skill.magnitude; break;
            case SkillEffect.GainDefend:    playerDefend    += sign * skill.magnitude; break;
            case SkillEffect.GainInfluence: playerInfluence += sign * skill.magnitude; GetCurrentInfluence(); break;
            case SkillEffect.GainExplore:   playerExplore   += sign * skill.magnitude; GetCurrentExplore(); break;
            case SkillEffect.GainCrystal:
            {
                var crystals = FindAnyObjectByType<CrystalInventory>();
                for (int i = 0; i < skill.magnitude; i++)
                {
                    if (sign > 0) crystals.SkillCrystallize(skill.crystalColor);
                    else          crystals.UndoSkillCrystallize();
                }
                break;
            }
            case SkillEffect.HealWound:
            {
                var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
                for (int i = 0; i < skill.magnitude; i++)
                {
                    if (sign > 0) hand.HealWound();
                    else          hand.RestoreHealedWound();
                }
                break;
            }
        }
    }

    public void AddSkill(SkillsSO skill)
    {
        skills.Add(skill);
        var bar = FindAnyObjectByType<SkillBar>();
        if (bar != null) bar.AddToken(skill);
    }

    // Save/load path (mirrors RebuildUnits): wipe tokens + list, re-add each
    // owned skill, restore exhausted state by id.
    public void RebuildSkills(List<SkillsSO> skillSOs, HashSet<string> exhaustedIds)
    {
        var bar = FindAnyObjectByType<SkillBar>();
        if (bar != null) bar.Clear();
        skills.Clear();

        foreach (var so in skillSOs)
        {
            if (so == null) continue;
            skills.Add(so);
            var token = bar != null ? bar.AddToken(so) : null;
            if (token != null && exhaustedIds.Contains(so.id))
                token.SetUsed(true);
        }
    }

    // Cadence refresh. Turn end refreshes per-turn skills; round end refreshes
    // everything. Safe against the undo stack: End Turn / End Round clear the
    // command stack before their events fire, so no skill command is undoable
    // by the time this runs.
    public void RefreshSkills(bool includePerRound)
    {
        foreach (var token in FindObjectsByType<SkillToken>(FindObjectsSortMode.None))
        {
            if (!token.IsUsed) continue;
            if (token.skillSO.cadence == SkillCadence.PerTurn || includePerRound)
                token.SetUsed(false);
        }
    }
```

5. In `TurnEnd()` (line 338), add as the last line: `RefreshSkills(false);`

- [ ] **Step 2: GameManager round refresh**

In `GameManager.RoundPlus()` (line 95), after `player.RefreshUnits();` add:

```csharp
        if (player != null) player.RefreshSkills(true);
```

- [ ] **Step 3: CrystalInventory skill stack**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs`, add after the `playerCreatedCrystal` field (line 17):

```csharp
    // Crystals granted by skill activations, so a skill undo removes exactly
    // the crystals it created (mirrors playerCreatedCrystal for Crystallize
    // cards; command-stack LIFO order keeps push/pop pairs matched).
    public Stack<Crystal> skillCreatedCrystals = new();
```

And add after `PurchaseTownCrystal` (line 197):

```csharp
    public void SkillCrystallize(EmpowerType color)
    {
        skillCreatedCrystals.Push(CreateCrystal(color));
    }

    public void UndoSkillCrystallize()
    {
        if (skillCreatedCrystals.Count == 0) return;
        skillCreatedCrystals.Pop().RemoveCrystal();
    }
```

- [ ] **Step 4: Continue to Task 6 (compiles once SkillToken/SkillBar exist)**

---

### Task 6: `SkillToken` + `SkillBar` + scene wiring

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/Leveling/SkillToken.cs`
- Create: `Assets/Scripts/GameObjectScripts/Leveling/SkillBar.cs`

**Interfaces:**
- Consumes: `SkillsSO`, `SkillEvent`, `SkillCommand` (Task 4), `GameManager.Instance.commands`.
- Produces: `SkillToken.skillSO`, `SkillToken.IsUsed`, `SkillToken.SetUsed(bool)`, `SkillToken.Bind(SkillsSO)`, `SkillBar.AddToken(SkillsSO) → SkillToken`, `SkillBar.Clear()`.

- [ ] **Step 1: Write SkillToken**

`Assets/Scripts/GameObjectScripts/Leveling/SkillToken.cs` (mirrors `Unit`'s click→command pattern; exhaust is a dim overlay instead of a rotation):

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillToken : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Image icon;
    // Semi-transparent cover enabled while exhausted.
    [SerializeField] Image dimOverlay;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] SkillEvent onClick_PerformSkillAction;
    public SkillsSO skillSO;
    public bool IsUsed { get; private set; }

    public void Bind(SkillsSO so)
    {
        skillSO = so;
        gameObject.name = so.cardName;
        if (icon != null && so.icon != null) icon.sprite = so.icon;
        if (label != null) label.text = so.cardName;
        SetUsed(false);
    }

    public void SetUsed(bool used)
    {
        IsUsed = used;
        if (dimOverlay != null) dimOverlay.enabled = used;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsUsed)
        {
            GameManager.Instance.commands.AddCommand(new SkillCommand(onClick_PerformSkillAction, this));
        }
        else
        {
            string refresh = skillSO.cadence == SkillCadence.PerTurn ? "next turn" : "next round";
            GameManager.Instance.ValidationMessage($"{skillSO.cardName} is exhausted until {refresh}. Undo to revert if it was just used.");
        }
    }
}
```

- [ ] **Step 2: Write SkillBar**

`Assets/Scripts/GameObjectScripts/Leveling/SkillBar.cs`:

```csharp
using UnityEngine;

// The persistent panel of owned skills. Pure UI container: Player owns the
// skill list; this only spawns/clears the clickable tokens.
public class SkillBar : MonoBehaviour
{
    [SerializeField] GameObject skillTokenPrefab;

    public SkillToken AddToken(SkillsSO skill)
    {
        var go = Instantiate(skillTokenPrefab, transform);
        var token = go.GetComponent<SkillToken>();
        token.Bind(skill);
        return token;
    }

    public void Clear()
    {
        foreach (var token in GetComponentsInChildren<SkillToken>())
            Destroy(token.gameObject);
    }
}
```

- [ ] **Step 3: USER ACTION — Unity wiring (Tasks 4–6 compile check + scene work)**

Focus Unity, wait for import, confirm the console is clean. Then:

1. **SkillEvent asset:** Project window → `Assets/Scripts/GameEvents/Events` → right-click → Create → Game Events → Skill Event → name it `onSkillClick_PerformSkillAction`.
2. **SkillToken prefab:** In the game scene's main UI canvas, create a UI → Image named `SkillToken` (~72×72). Add two children: an Image named `Icon` (stretch to fill), and an Image named `DimOverlay` (stretch to fill, black, alpha ≈ 0.6, Raycast Target OFF), plus a child TextMeshPro named `Label` (small, bottom-anchored). Add the `SkillToken` component to the root; drag `Icon` → icon, `DimOverlay` → dimOverlay, `Label` → label, and the `onSkillClick_PerformSkillAction` asset → onClick_PerformSkillAction. Disable `DimOverlay`'s Image component (unchecked = not exhausted). Drag the root into `Assets/Prefabs` (or the folder your other UI prefabs use) to make it a prefab, then delete it from the scene.
3. **SkillBar panel:** In the main UI canvas, add an empty UI object named `SkillBar` docked along the screen edge near the unit area, with a Horizontal Layout Group (spacing ~8, child force expand off). Add the `SkillBar` component; drag the SkillToken prefab → skillTokenPrefab.
4. **Skill listener:** On the `Player` GameObject, Add Component → `SkillListener`. Game Event = `onSkillClick_PerformSkillAction`; Unity Event Response → + → drag `Player` → function `Player.PerformSkillAction`.
5. Report: console clean, prefab + panel + listener created.

- [ ] **Step 4: Commit (Tasks 4+5+6 together)**

```bash
git add Assets/Scripts/Enums Assets/Scripts/GameScriptableObjectTypes/SkillsSO.cs Assets/Scripts/GameScriptableObjectTypes/LevelRewardsSO.cs Assets/Scripts/GameEvents Assets/Scripts/Managers/Commands/SkillCommand.cs Assets/Scripts/GameObjectScripts/Leveling Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs Assets/Scripts/Managers/GameManager.cs
git commit -m "feat: skills - SO types, event/command plumbing, player state, skill bar"
```

(Include any newly generated `.meta` files and the scene file the user saved.)

---

### Task 7: Level-up flow — `PlayerLevelUp` rewrite, `LevelUpController`, `LevelUpModal`, card picks

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/Leveling/LevelUpController.cs`
- Create: `Assets/Scripts/GameObjectScripts/Leveling/LevelUpModal.cs`
- Create: `Assets/Scripts/GameObjectScripts/Leveling/SkillChoiceButton.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs:347-353` (`PlayerLevelUp`)
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs:63-74` (`OfferCardChoice`)
- Modify: `Assets/Scripts/Managers/DataManager.cs:313-329` (`IsSettledState`)

**Interfaces:**
- Consumes: `LevelRules.RewardsFor/CarriedExp/DrawSkillChoices` (Task 1), `Player.AddSkill/Skills/LevelRewards` (Task 5), `RewardCanvas.Offer` (existing).
- Produces: `LevelUpController.EnqueueLevelRewards(int level, LevelRewardEntry entry)`, `LevelUpController.Busy : bool`, `LevelUpModal.Offer(IReadOnlyList<SkillsSO>, Action<SkillsSO>)`, `Rewards.OfferCardChoice(System.Action onClosed = null)` (now public).

- [ ] **Step 1: Rewrite `PlayerLevelUp` in Player.cs**

Replace the existing method (line 347) with:

```csharp
    public void PlayerLevelUp()
    {
        playerLevel++;
        // Overflow exp carries into the next level (the old reset-to-0 discarded
        // it). Update() keeps polling, so back-to-back level-ups fire one per
        // frame and their reward queues chain in order.
        playerExp = LevelRules.CarriedExp(playerExp, expToNextLevel);
        expToNextLevel = expToNextLevel + playerLevel + 12;

        var entry = LevelRules.RewardsFor(playerLevel, levelRewards.Entries);
        if (entry != null) playerHP += entry.hpBonus;

        var controller = FindAnyObjectByType<LevelUpController>();
        if (controller != null) controller.EnqueueLevelRewards(playerLevel, entry);
    }
```

- [ ] **Step 2: Write LevelUpController**

`Assets/Scripts/GameObjectScripts/Leveling/LevelUpController.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

// Runs the level-up payout queue. Rewards resolve strictly in order — skill
// pick(s), then card pick(s), then whatever the next pending level enqueued —
// one modal at a time. Fixed bonuses (HP/hand/army) never enter the queue:
// Player applies HP directly and the sizes are derived.
public class LevelUpController : MonoBehaviour
{
    [SerializeField] Player player;
    [SerializeField] LevelUpModal modal;
    [SerializeField] Rewards rewards;

    readonly Queue<System.Action> pending = new();
    readonly System.Random rng = new System.Random();
    bool resolving;

    // Save gate: mid-payout is not a settled state.
    public bool Busy => resolving || pending.Count > 0;

    public void EnqueueLevelRewards(int level, LevelRewardEntry entry)
    {
        GameManager.Instance.ValidationMessage($"You reached level {level}!");
        if (entry == null) return;

        for (int i = 0; i < entry.skillPicks; i++) pending.Enqueue(OfferSkillPick);
        for (int i = 0; i < entry.cardPicks; i++) pending.Enqueue(OfferCardPick);
        TryNext();
    }

    void TryNext()
    {
        if (resolving || pending.Count == 0) return;
        // Wait for the level-up announcement (or any other message) to be
        // dismissed before opening a pick screen on top of it.
        if (GameManager.Instance.messageCanvas.enabled) { Invoke(nameof(TryNext), 0.25f); return; }
        resolving = true;
        pending.Dequeue().Invoke();
    }

    void OfferSkillPick()
    {
        var choices = LevelRules.DrawSkillChoices(player.LevelRewards.SkillPool,
            new List<SkillsSO>(player.Skills), rng, 3);
        if (choices.Count == 0) { Done(); return; } // pool exhausted: skip the pick
        modal.Offer(choices, chosen => { player.AddSkill(chosen); Done(); });
    }

    void OfferCardPick()
    {
        rewards.OfferCardChoice(Done);
    }

    void Done()
    {
        resolving = false;
        TryNext();
    }
}
```

- [ ] **Step 3: Write LevelUpModal + SkillChoiceButton**

`Assets/Scripts/GameObjectScripts/Leveling/LevelUpModal.cs` (mirrors `RewardCanvas`: fixed slots, double-resolution guard):

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelUpModal : MonoBehaviour
{
    [SerializeField] Canvas canvas;
    [SerializeField] SkillChoiceButton[] choiceSlots = new SkillChoiceButton[3];
    Action<SkillsSO> onChosen;
    bool resolved;

    public bool IsOpen => canvas != null && canvas.enabled;

    public void Offer(IReadOnlyList<SkillsSO> skills, Action<SkillsSO> onChosen)
    {
        this.onChosen = onChosen;
        resolved = false;
        canvas.enabled = true;

        for (int i = 0; i < choiceSlots.Length; i++)
        {
            bool active = i < skills.Count;
            choiceSlots[i].gameObject.SetActive(active);
            if (active) choiceSlots[i].Bind(skills[i], Choose);
        }
    }

    void Choose(SkillsSO chosen)
    {
        if (resolved) return;
        resolved = true;
        canvas.enabled = false;
        onChosen?.Invoke(chosen);
    }
}
```

`Assets/Scripts/GameObjectScripts/Leveling/SkillChoiceButton.cs`:

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillChoiceButton : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image icon;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI descriptionText;

    public void Bind(SkillsSO skill, Action<SkillsSO> onClick)
    {
        if (icon != null && skill.icon != null) icon.sprite = skill.icon;
        nameText.text = skill.cardName;
        descriptionText.text = skill.cardDescription;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(skill));
    }
}
```

- [ ] **Step 4: Make `Rewards.OfferCardChoice` public with a completion callback**

Replace `OfferCardChoice` in `Rewards.cs` (line 63):

```csharp
    // Card pick: choose 1 of 3 from the curated pool. Public because level-ups
    // grant the same pick (LevelUpController); onClosed lets the caller queue
    // the next reward after the screen resolves (chosen OR skipped).
    public void OfferCardChoice(System.Action onClosed = null)
    {
        // Draw from the curated rewardPool, NOT DataManager.Cards (which now
        // includes starting cards + Wound for save/load resolution).
        if (rewardPool == null || rewardPool.Count == 0) { onClosed?.Invoke(); return; }

        var candidates = new List<CardsSO>();
        for (int i = 0; i < 3; i++)
            candidates.Add(rewardPool[Random.Range(0, rewardPool.Count)]);

        rewardCanvas.Offer(candidates,
            so => { deck.AddCard(so, toTop: true); onClosed?.Invoke(); },
            () => onClosed?.Invoke());
    }
```

(The existing `Grant` call `OfferCardChoice();` still compiles via the optional arg.)

- [ ] **Step 5: Gate saving during payout**

In `DataManager.IsSettledState()` (line 313), after the card-list canvas check add:

```csharp
        // No level-up payout mid-flight (skill modal open or picks queued).
        var levelUp = FindAnyObjectByType<LevelUpController>();
        if (levelUp != null && levelUp.Busy) return false;
        var levelUpModal = FindAnyObjectByType<LevelUpModal>();
        if (levelUpModal != null && levelUpModal.IsOpen) return false;
```

- [ ] **Step 6: USER ACTION — Unity wiring**

1. **Level-Up canvas:** duplicate the structure of the card reward canvas: create a new Canvas named `LevelUpCanvas` (same render settings as `CardRewardCanvas`, enabled = false, GameObject active). Add a dark full-screen Image backdrop, a TMP title ("Choose a skill"), and three child panels named `SkillChoice1..3`, each with: Button (background), child `Icon` Image, child `Name` TMP, child `Description` TMP. Put a `SkillChoiceButton` component on each panel and wire its button/icon/nameText/descriptionText.
2. **Modal + controller:** Add an empty GameObject `LevelUpFlow` under the managers object. Add `LevelUpModal` (canvas = `LevelUpCanvas`, choiceSlots = the three `SkillChoiceButton`s) and `LevelUpController` (player = Player, modal = the LevelUpModal, rewards = the existing Rewards object).
3. Enter Play Mode briefly: no console errors on load.
4. Report done.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/Leveling Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs Assets/Scripts/Managers/DataManager.cs
git commit -m "feat: level-up payout queue - skill pick modal + card picks + exp carry-over"
```

(Plus generated `.meta` files and the saved scene.)

---

### Task 8: Army cap — RecruitButton gate, DisbandPanel, `Player.DisbandUnit`

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/TownMenuScripts/DisbandPanel.cs`
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/RecruitButton.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` (add `DisbandUnit`)

**Interfaces:**
- Consumes: `ArmyRules` (Task 2), `Player.ArmyCap/Units` (Task 5), `TownButtons` protected fields (`_town`, `townEvent`, `influenceCostEvent`).
- Produces: `Player.DisbandUnit(Unit unit)`, `DisbandPanel.Open(TownToken town)`.

- [ ] **Step 1: Add `Player.DisbandUnit`**

In `Player.cs`, after `RecruitUnit` (line 278):

```csharp
    // Disband-to-hire: removes one unit to make room at the army cap. A played
    // unit keeps its stat contribution for this turn (it fought its last
    // battle); pools reset at turn end anyway. The town flow clears the undo
    // stack right after hiring, so no stale UnitCommand can reference it.
    public void DisbandUnit(Unit unit)
    {
        units.Remove(unit.unitSO);
        Destroy(unit.gameObject);
    }
```

- [ ] **Step 2: Write DisbandPanel**

`Assets/Scripts/GameObjectScripts/TownMenuScripts/DisbandPanel.cs`:

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Your army is full" flow: pick an existing unit to disband, then the hire
// completes through the exact same events RecruitButton fires (atomic: no
// state where influence is spent without a unit). Cancel is free.
public class DisbandPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;            // root, inactive by default
    [SerializeField] Transform entryContainer;    // vertical layout for unit buttons
    [SerializeField] GameObject entryButtonPrefab; // Button + TMP label
    [SerializeField] Button cancelButton;
    [SerializeField] TownEvent townEvent;          // same asset RecruitButton raises
    [SerializeField] IntEvent influenceCostEvent;  // same asset RecruitButton raises

    TownToken _town;
    readonly List<GameObject> spawned = new();

    void Start()
    {
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(Close);
        panel.SetActive(false);
    }

    public void Open(TownToken town)
    {
        _town = town;
        ClearEntries();
        panel.SetActive(true);

        foreach (var unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            var go = Instantiate(entryButtonPrefab, entryContainer);
            go.GetComponentInChildren<TextMeshProUGUI>().text = unit.unitSO.cardName;
            var captured = unit;
            go.GetComponent<Button>().onClick.AddListener(() => DisbandAndHire(captured));
            spawned.Add(go);
        }
    }

    void DisbandAndHire(Unit unit)
    {
        var player = FindAnyObjectByType<Player>();
        player.DisbandUnit(unit);
        // Same two events the normal Recruit click raises: hire + spend.
        townEvent.Raise(_town);
        influenceCostEvent.Raise(_town.townSO.recruitLevel);
        Close();
    }

    void Close()
    {
        ClearEntries();
        panel.SetActive(false);
        _town = null;
    }

    void ClearEntries()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}
```

- [ ] **Step 3: Gate RecruitButton on the cap**

Replace the body of `RecruitButton.UpdateButtonText()` listener wiring (`Assets/Scripts/GameObjectScripts/TownMenuScripts/RecruitButton.cs`) — full new file:

```csharp
using UnityEngine;

public class RecruitButton : TownButtons
{
    [SerializeField] DisbandPanel disbandPanel;

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
                    thisButton.onClick.AddListener(Recruit);
            }
            else
            {
                thisButton.gameObject.SetActive(false);
            }
        }
    }

    // At the army cap the hire needs a disband first; below it, the original
    // two-event flow runs unchanged.
    private void Recruit()
    {
        var player = FindAnyObjectByType<Player>();
        if (ArmyRules.NeedsDisband(player.Units.Count, player.ArmyCap))
        {
            disbandPanel.Open(_town);
            return;
        }
        townEvent.Raise(_town);
        influenceCostEvent.Raise(_town.townSO.recruitLevel);
    }
}
```

- [ ] **Step 4: USER ACTION — Unity wiring**

1. Under the town canvas, add a panel `DisbandPanel` (inactive-styled like other town sub-panels): dark backdrop, TMP title "Your army is full — disband a unit to hire", an empty child `Entries` with a Vertical Layout Group, and a `Cancel` Button.
2. Create a small prefab `DisbandEntryButton`: a Button with a TMP label child.
3. Add the `DisbandPanel` component to the panel root: panel = the root, entryContainer = `Entries`, entryButtonPrefab = `DisbandEntryButton`, cancelButton = `Cancel`, townEvent + influenceCostEvent = **the same event assets** already assigned on the RecruitButton component (check them there).
4. On the `RecruitButton` component, assign disbandPanel = the new DisbandPanel.
5. Play Mode: recruit once (cap 1), try recruiting again → the disband panel opens; Cancel closes it free; picking the unit swaps armies and spends Influence.
6. Report results.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/TownMenuScripts Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs
git commit -m "feat: army cap with disband-to-hire recruit flow"
```

---

### Task 9: DataManager — skill registry, capture, restore

**Files:**
- Modify: `Assets/Scripts/Managers/DataManager.cs`

**Interfaces:**
- Consumes: `PlayerState.ownedSkillIds/exhaustedSkillIds` (Task 3), `Player.Skills/RebuildSkills` (Task 5), `SkillToken.IsUsed/skillSO` (Task 6), `ContentRegistry<T>` (existing).
- Produces: `DataManager.Skills : ContentRegistry<SkillsSO>`, `allSkills : SkillsSO[]` (inspector field the user fills in Task 10).

- [ ] **Step 1: Add the registry**

In `DataManager.cs`:

1. After `public UnitsSO[] allUnits;` (line 34) add:

```csharp
    public SkillsSO[] allSkills;
```

2. After `public ContentRegistry<UnitsSO> Units { get; private set; }` (line 37) add:

```csharp
    public ContentRegistry<SkillsSO> Skills { get; private set; }
```

3. In `BuildRegistries()` (line 55), after the Units line add:

```csharp
            Skills = new ContentRegistry<SkillsSO>(allSkills, s => s.id);
```

- [ ] **Step 2: Restore skills on load**

In `RestoreNow()`, after `player.RebuildUnits(Units.Resolve(run.unitIds));` (line 223) add:

```csharp
        player.RebuildSkills(Skills.Resolve(run.player.ownedSkillIds),
            new HashSet<string>(run.player.exhaustedSkillIds));
```

- [ ] **Step 3: Capture skills on save**

In `CaptureRunState()`, after `run.unitIds = UnitIds(player);` (line 262) add:

```csharp
        run.player.ownedSkillIds     = SkillIds(player);
        run.player.exhaustedSkillIds = ExhaustedSkillIds();
```

And add the helpers after `UnitIds` (line 299):

```csharp
    private static string[] SkillIds(Player player)
    {
        var ids = new List<string>();
        if (player == null) return ids.ToArray();
        foreach (var s in player.Skills)
            if (s != null) ids.Add(s.id);
        return ids.ToArray();
    }

    // Exhaust state lives on the tokens (mirrors how unit exhaustion lives on
    // Unit); saving happens only at settled states, so tokens are authoritative.
    private static string[] ExhaustedSkillIds()
    {
        var ids = new List<string>();
        foreach (var token in FindObjectsByType<SkillToken>(FindObjectsSortMode.None))
            if (token.IsUsed && token.skillSO != null) ids.Add(token.skillSO.id);
        return ids.ToArray();
    }
```

- [ ] **Step 4: USER ACTION — focus Unity, clean compile. Commit**

```bash
git add Assets/Scripts/Managers/DataManager.cs
git commit -m "feat: save/load owned and exhausted skills (schema v3)"
```

---

### Task 10: Content authoring (USER, in the editor)

**Files (created via the editor, then committed):**
- `Assets/Scripts/ScriptableObjectData/Player/Skills/` — 9 `SkillsSO` assets
- `Assets/Scripts/ScriptableObjectData/Player/LevelRewards.asset` — 1 `LevelRewardsSO`

**Interfaces:**
- Consumes: the Create menus from Task 4.
- Produces: the authored content every runtime piece reads; `Player.levelRewards` and `DataManager.allSkills` assigned.

- [ ] **Step 1: USER ACTION — create the 9 skill assets**

In `Assets/Scripts/ScriptableObjectData/Player/`, create folder `Skills`. For each row: right-click → Create → ScriptableObjects → Skill, then fill:

| Asset name | id | cardName | effect | magnitude | crystalColor | cadence | cardDescription |
|---|---|---|---|---|---|---|---|
| Drillmaster | `skill-drillmaster` | Drillmaster | GainAttack | 1 | None | PerTurn | +1 Attack, once per turn. |
| Shieldwall | `skill-shieldwall` | Shieldwall | GainDefend | 1 | None | PerTurn | +1 Defend, once per turn. |
| Envoy | `skill-envoy` | Envoy | GainInfluence | 1 | None | PerTurn | +1 Influence, once per turn. |
| Pathfinder | `skill-pathfinder` | Pathfinder | GainExplore | 1 | None | PerTurn | +1 Explore, once per turn. |
| CrystallizeRed | `skill-crystallize-red` | Crystallize: Red | GainCrystal | 1 | Red | PerRound | Gain 1 Red crystal, once per round. |
| CrystallizeYellow | `skill-crystallize-yellow` | Crystallize: Yellow | GainCrystal | 1 | Yellow | PerRound | Gain 1 Yellow crystal, once per round. |
| CrystallizeGreen | `skill-crystallize-green` | Crystallize: Green | GainCrystal | 1 | Green | PerRound | Gain 1 Green crystal, once per round. |
| CrystallizePurple | `skill-crystallize-purple` | Crystallize: Purple | GainCrystal | 1 | Purple | PerRound | Gain 1 Purple crystal, once per round. |
| FieldMedic | `skill-field-medic` | Field Medic | HealWound | 1 | None | PerRound | Heal 1 Wound from your hand, once per round. |

Icons: reuse any existing stat icons for now (tune later); leave empty if none fits — tokens fall back to the label.

- [ ] **Step 2: USER ACTION — create the LevelRewards table asset**

Create → ScriptableObjects → LevelRewards in `Assets/Scripts/ScriptableObjectData/Player/`, named `LevelRewards`. Skill Pool = all 9 skill assets. Entries (9 rows; unlisted fields = 0):

| level | hpBonus | handSizeBonus | armySizeBonus | skillPicks | cardPicks |
|---|---|---|---|---|---|
| 2 | 0 | 0 | 0 | 1 | 0 |
| 3 | 1 | 0 | 0 | 0 | 1 |
| 4 | 0 | 1 | 1 | 0 | 0 |
| 5 | 0 | 0 | 0 | 1 | 0 |
| 6 | 1 | 0 | 0 | 0 | 1 |
| 7 | 0 | 0 | 1 | 1 | 0 |
| 8 | 0 | 1 | 0 | 0 | 0 |
| 9 | 1 | 0 | 0 | 1 | 0 |
| 10 | 0 | 1 | 1 | 0 | 1 |

- [ ] **Step 3: USER ACTION — assign references**

1. On the `Player` GameObject: levelRewards = the `LevelRewards` asset.
2. On the `DataManager` object: allSkills = all 9 skill assets.
3. Save the scene.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/ScriptableObjectData/Player/Skills" "Assets/Scripts/ScriptableObjectData/Player/LevelRewards.asset" 
git commit -m "content: 9 starter skills + level reward table (levels 2-10)"
```

(Plus `.meta` files and the saved scene.)

---

### Task 11: Acceptance

**Files:**
- Modify: `.claude/skills/archons-rise-roadmap/milestones.md` (mark M2.4 done when passing)

- [ ] **Step 1: USER ACTION — run EditMode tests**

Unity → Window → General → Test Runner → EditMode → Run All. Expected: all green, including `LevelRulesTests` (6), `ArmyRulesTests` (2), `SaveMigratorV3Tests` (2).

- [ ] **Step 2: USER ACTION — manual acceptance checklist (Play Mode)**

1. **Exp overflow:** set the Player's `playerExp` in the inspector just below `expToNextLevel`, defeat an enemy granting +3 → level-up fires once, leftover exp shows on the bar (not 0, unless exact).
2. **Level 2 skill pick:** modal shows 3 distinct skills; picking one adds a token to the skill bar; the modal cannot double-fire.
3. **Skill use + undo:** click the skill → effect applies (stat/crystal/heal), token dims; Undo → effect reverts, token undims. Exhausted click shows the validation message.
4. **Cadence:** per-turn skill refreshes after End Turn; per-round (Crystallize) stays exhausted through turns and refreshes on End Round.
5. **Level 3 card pick:** after the level-up message, the card reward screen opens; chosen card enters the deck; Skip also closes cleanly and the queue continues.
6. **Level 4 sizes:** hand tops up to 6 at next turn end; a second unit can be recruited.
7. **Army cap:** at cap, Recruit opens the disband panel; Cancel is free; disband-then-hire swaps the unit and spends Influence.
8. **Save/load:** save with an owned + exhausted skill, quit to menu, load → skills, exhaust state, hand size, and army cap all match; an old v2 save loads with no skills and cap 1 + table bonuses for its level.

- [ ] **Step 3: Record results, fix anything red, then mark M2.4 done in milestones.md and commit**

```bash
git add .claude/skills/archons-rise-roadmap/milestones.md
git commit -m "docs: mark M2.4 level-up rewards done"
```

---

## Self-Review Notes

- **Spec coverage:** reward table (T10), SkillsSO/LevelRewardsSO (T4), pick-1-of-3 modal (T7), card picks reusing RewardCanvas (T7), skill bar exhaust/refresh (T5/T6), undo via command stack (T4/T6), army cap + disband-to-hire (T2/T8), derived hand/army (T1/T5), schema v3 + migration (T3/T9), exp overflow (T1/T7), pure-rule tests (T1/T2/T3), acceptance (T11). Out-of-scope items from the spec have no tasks, as intended.
- **Type consistency:** `SkillToken.skillSO` (public field), `IsUsed`/`SetUsed` naming used identically in Tasks 5, 6, 9. `LevelRewardEntry` field names match between Tasks 1, 4, 7, 10. `OfferCardChoice(System.Action)` matches Tasks 7's controller and Rewards change.
- **Compile-unit note:** Tasks 4–6 form one compile unit (SkillEvent/SkillCommand reference SkillToken); they share a single commit at Task 6. Tasks 3, 7, 8, 9 each compile standalone.
