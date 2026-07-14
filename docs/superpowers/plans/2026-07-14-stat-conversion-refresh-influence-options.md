# Stat Conversion, Unit Refresh & Influence-Costed Unit Options Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three additive strategy mechanics: opt-in 1:1 stat conversion on cards/skills, a budgeted mid-round unit Refresh with a reusable unit-picker modal, and influence-costed unit options.

**Architecture:** Pure rules first (`ConvertRules` in `ArchonsRise.CardPlay`, `RefreshRules` in `ArchonsRise.UnitPlay`), TDD'd via the mcs CLI harness. Scene side mirrors existing patterns: `ConvertBanner` is a ChoiceBanner-style inspector section, `UnitPickerPanel` follows DisbandPanel's canvas/button/continuation shape, influence costs ride the existing `UnitCommand` apply/revert symmetry. No save schema change.

**Tech Stack:** Unity 6000.5.1f1, C#, NUnit (EditMode), Mono `mcs` CLI harness for pure logic, TextMeshPro, ScriptableObject event bus.

**Spec:** `docs/superpowers/specs/2026-07-14-stat-conversion-refresh-influence-options-design.md`

## Global Constraints

- **No git worktree** — Unity holds `Library/`; work on `master` like every prior milestone.
- **Never hand-edit scene/prefab YAML.** All scene, prefab, and canvas wiring is done by the user in the editor from the USER ACTION steps. `.asmdef` JSON edits are allowed. Content `.asset` files are authored by the user in the editor.
- **The Unity editor holds the compile lock** — batch-mode tests won't run while it's open. Pure logic is RED/GREEN-verified with the mcs harness; EditMode tests run in the editor's Test Runner at acceptance. MonoBehaviour code is verified by the editor compiling cleanly when the user focuses it.
- **Unity generates `.meta` files** — never create them by hand; after adding `.cs` files, ask the user to focus the editor, then commit code + generated `.meta` together.
- **mcs is C# ~7** — no tuples or `out var` in files it compiles (pure rules + their tests). Unity-only files may use modern C#.
- Spec rules (verbatim): conversion is always **1:1**; conversion touches the **four action stats only** (Attack/Defend/Explore/Influence — Siege/Heal/Crystal/Wound never participate); conversion is **opt-in at play time**; `convertTo` must never be flagged in `convertFrom`; a card cannot be both `isChoice` and a converter; Refresh N is a **budget across multiple units**, each pick deducting the unit's `influenceCost`; unspent refresh budget is **lost**; refresh **fizzles** when no spent unit is affordable at play time; a unit option costs **crystal OR influence OR free — never both**; **no save schema bump**.
- The unit picker is **not** a reward modal — it opens directly (DisbandPanel precedent), never through `RewardQueue`.
- Commit messages: `feat:`/`test:`/`chore:`/`docs:` prefixes, ending with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

## mcs Harness (used by Tasks 1, 2, 4, 8)

One-time per session. The reflection runner lives at `<scratchpad>\Runner.cs` (create if absent):

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

Run pattern (after each library compile): `& "$s\Runner.exe" "$s\<Lib>.dll"`.

---

### Task 1: ConvertRules (pure conversion math)

**Files:**
- Create: `Assets/Scripts/CardPlay/ConvertRules.cs`
- Test: `Assets/Tests/EditMode/ConvertRulesTests.cs`

**Interfaces:**
- Consumes: `StatType` (existing `[Flags]` enum in `ArchonsRise.Enums`).
- Produces: `static class ConvertRules` — `StatType[] ActionStats`; `int IndexOf(StatType single)` (0–3 pool index, −1 otherwise); `bool IsValid(StatType from, StatType to)`; `int[] Moved(int[] pools, StatType from, StatType to)` (pool order `[attack, defend, influence, explore]`); `int MovedTotal(int[] moved)`; `string Describe(StatType from, StatType to)`. Used by Tasks 3, 5, 6, 7.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/ConvertRulesTests.cs`:

```csharp
using NUnit.Framework;

public class ConvertRulesTests
{
    static readonly StatType AllActions =
        StatType.Attack | StatType.Defend | StatType.Influence | StatType.Explore;

    [Test]
    public void IsValid_SingleActionSourceToActionTarget()
    {
        Assert.IsTrue(ConvertRules.IsValid(StatType.Defend, StatType.Attack));
    }

    [Test]
    public void IsValid_MultiSourceToTarget()
    {
        Assert.IsTrue(ConvertRules.IsValid(
            StatType.Attack | StatType.Defend | StatType.Explore, StatType.Influence));
    }

    [Test]
    public void IsValid_RejectsTargetInSources()
    {
        Assert.IsFalse(ConvertRules.IsValid(AllActions, StatType.Influence));
    }

    [Test]
    public void IsValid_RejectsNoneSource()
    {
        Assert.IsFalse(ConvertRules.IsValid(StatType.None, StatType.Attack));
    }

    [Test]
    public void IsValid_RejectsSiegeTarget()
    {
        Assert.IsFalse(ConvertRules.IsValid(StatType.Attack, StatType.Siege));
    }

    [Test]
    public void IsValid_RejectsSiegeSource()
    {
        Assert.IsFalse(ConvertRules.IsValid(StatType.Siege, StatType.Attack));
    }

    [Test]
    public void IsValid_RejectsNonActionSourceMixedIn()
    {
        Assert.IsFalse(ConvertRules.IsValid(StatType.Defend | StatType.Heal, StatType.Attack));
    }

    [Test]
    public void IsValid_RejectsMultiFlagTarget()
    {
        Assert.IsFalse(ConvertRules.IsValid(StatType.Explore, StatType.Attack | StatType.Defend));
    }

    [Test]
    public void IndexOf_MapsPoolOrder()
    {
        Assert.AreEqual(0, ConvertRules.IndexOf(StatType.Attack));
        Assert.AreEqual(1, ConvertRules.IndexOf(StatType.Defend));
        Assert.AreEqual(2, ConvertRules.IndexOf(StatType.Influence));
        Assert.AreEqual(3, ConvertRules.IndexOf(StatType.Explore));
        Assert.AreEqual(-1, ConvertRules.IndexOf(StatType.Siege));
    }

    [Test]
    public void Moved_DrainsSingleSourceLeavesTargetZero()
    {
        int[] pools = { 2, 9, 4, 1 }; // atk, def, inf, exp
        int[] moved = ConvertRules.Moved(pools, StatType.Defend, StatType.Attack);
        Assert.AreEqual(new[] { 0, 9, 0, 0 }, moved);
    }

    [Test]
    public void Moved_DrainsAllFlaggedSources()
    {
        int[] pools = { 2, 3, 4, 5 };
        int[] moved = ConvertRules.Moved(pools,
            StatType.Attack | StatType.Defend | StatType.Explore, StatType.Influence);
        Assert.AreEqual(new[] { 2, 3, 0, 5 }, moved);
    }

    [Test]
    public void Moved_EmptyPoolsMoveNothing()
    {
        int[] moved = ConvertRules.Moved(new[] { 0, 0, 0, 0 }, StatType.Defend, StatType.Attack);
        Assert.AreEqual(new[] { 0, 0, 0, 0 }, moved);
    }

    [Test]
    public void Moved_InvalidConversionMovesNothing()
    {
        int[] moved = ConvertRules.Moved(new[] { 5, 5, 5, 5 }, StatType.None, StatType.Attack);
        Assert.AreEqual(new[] { 0, 0, 0, 0 }, moved);
    }

    [Test]
    public void MovedTotal_Sums()
    {
        Assert.AreEqual(10, ConvertRules.MovedTotal(new[] { 2, 3, 0, 5 }));
    }

    [Test]
    public void Describe_SingleSource()
    {
        Assert.AreEqual("Convert all Defend → Attack",
            ConvertRules.Describe(StatType.Defend, StatType.Attack));
    }

    [Test]
    public void Describe_MultiSource()
    {
        Assert.AreEqual("Convert all Attack, Defend, Explore → Influence",
            ConvertRules.Describe(StatType.Attack | StatType.Defend | StatType.Explore,
                StatType.Influence));
    }
}
```

- [ ] **Step 2: Run the harness to verify it fails (RED)**

```powershell
& $mcs -nologo -target:library "-out:$s\Convert.dll" "-r:$nunit" "Assets\Scripts\Enums\Enums\StatType.cs" "Assets\Tests\EditMode\ConvertRulesTests.cs"
```

Expected: compile errors — `ConvertRules` does not exist.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/CardPlay/ConvertRules.cs`:

```csharp
using System.Collections.Generic;

// Pure conversion math (spec 2026-07-14). Conversion is always 1:1 and only
// moves the four action pools — Siege/Heal/Crystal/Wound never participate.
// Pool arrays use the CardPlaySelection order [attack, defend, influence,
// explore]. Unity-free so it is mcs-CLI-testable.
public static class ConvertRules
{
    public static readonly StatType[] ActionStats =
        { StatType.Attack, StatType.Defend, StatType.Influence, StatType.Explore };

    const StatType ActionMask =
        StatType.Attack | StatType.Defend | StatType.Influence | StatType.Explore;

    // Index of a single action flag in the pools array; -1 for anything else.
    public static int IndexOf(StatType single)
    {
        for (int i = 0; i < ActionStats.Length; i++)
            if (ActionStats[i] == single) return i;
        return -1;
    }

    // Authorable when: target is exactly one action stat, sources are one or
    // more action stats, and the target is not among the sources.
    public static bool IsValid(StatType from, StatType to)
    {
        if (IndexOf(to) < 0) return false;
        if (from == StatType.None) return false;
        if ((from & ~ActionMask) != 0) return false;
        if (from.HasFlag(to)) return false;
        return true;
    }

    // Per-pool amounts the conversion moves: each flagged source drains fully;
    // the target index stays 0 (the caller adds MovedTotal to the target).
    public static int[] Moved(int[] pools, StatType from, StatType to)
    {
        var moved = new int[4];
        if (!IsValid(from, to)) return moved;
        for (int i = 0; i < ActionStats.Length; i++)
            if (from.HasFlag(ActionStats[i]) && pools[i] > 0)
                moved[i] = pools[i];
        return moved;
    }

    public static int MovedTotal(int[] moved)
    {
        int total = 0;
        for (int i = 0; i < moved.Length; i++) total += moved[i];
        return total;
    }

    // Banner / description text, e.g. "Convert all Defend → Attack".
    public static string Describe(StatType from, StatType to)
    {
        var parts = new List<string>();
        for (int i = 0; i < ActionStats.Length; i++)
            if (from.HasFlag(ActionStats[i])) parts.Add(ActionStats[i].ToString());
        return "Convert all " + string.Join(", ", parts.ToArray()) + " → " + to;
    }
}
```

- [ ] **Step 4: Run the harness to verify it passes (GREEN)**

```powershell
& $mcs -nologo -target:library "-out:$s\Convert.dll" "-r:$nunit" "Assets\Scripts\Enums\Enums\StatType.cs" "Assets\Scripts\CardPlay\ConvertRules.cs" "Assets\Tests\EditMode\ConvertRulesTests.cs"
& "$s\Runner.exe" "$s\Convert.dll"
```

Expected: `16 passed, 0 failed`.

- [ ] **Step 5: USER ACTION — focus the Unity editor** so it compiles and generates the two `.meta` files. Console must show no errors.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/CardPlay/ConvertRules.cs Assets/Scripts/CardPlay/ConvertRules.cs.meta Assets/Tests/EditMode/ConvertRulesTests.cs Assets/Tests/EditMode/ConvertRulesTests.cs.meta
git commit -m @'
feat: ConvertRules - pure 1:1 stat conversion math

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 2: RefreshRules (pure refresh-budget math)

**Files:**
- Create: `Assets/Scripts/UnitPlay/RefreshRules.cs`
- Test: `Assets/Tests/EditMode/RefreshRulesTests.cs`

**Interfaces:**
- Consumes: nothing beyond System.
- Produces: `static class RefreshRules` — `int PickCost(int influenceCost)` (min 1, so the budget always shrinks and the picker terminates); `bool CanPick(bool exhausted, int influenceCost, int remaining)`. Used by Tasks 8, 9.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/RefreshRulesTests.cs`:

```csharp
using NUnit.Framework;

public class RefreshRulesTests
{
    [Test]
    public void PickCost_UsesInfluenceCost()
    {
        Assert.AreEqual(3, RefreshRules.PickCost(3));
    }

    [Test]
    public void PickCost_FloorsAtOne()
    {
        Assert.AreEqual(1, RefreshRules.PickCost(0));
        Assert.AreEqual(1, RefreshRules.PickCost(-2));
    }

    [Test]
    public void CanPick_ExhaustedAndAffordable()
    {
        Assert.IsTrue(RefreshRules.CanPick(true, 3, 3));
    }

    [Test]
    public void CanPick_RejectsReadyUnit()
    {
        Assert.IsFalse(RefreshRules.CanPick(false, 3, 6));
    }

    [Test]
    public void CanPick_RejectsOverBudget()
    {
        Assert.IsFalse(RefreshRules.CanPick(true, 4, 3));
    }

    [Test]
    public void CanPick_ZeroCostUnitNeedsBudgetOfOne()
    {
        Assert.IsTrue(RefreshRules.CanPick(true, 0, 1));
        Assert.IsFalse(RefreshRules.CanPick(true, 0, 0));
    }
}
```

- [ ] **Step 2: Run the harness to verify it fails (RED)**

```powershell
& $mcs -nologo -target:library "-out:$s\Refresh.dll" "-r:$nunit" "Assets\Tests\EditMode\RefreshRulesTests.cs"
```

Expected: compile errors — `RefreshRules` does not exist.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/UnitPlay/RefreshRules.cs`:

```csharp
// Pure refresh-budget math (spec 2026-07-14). Refresh N is a budget spent
// across exhausted units; each pick deducts the unit's recruit influenceCost.
// Unity-free so it is mcs-CLI-testable.
public static class RefreshRules
{
    // A unit never refreshes for free: an authored cost below 1 counts as 1 so
    // every pick shrinks the budget and the picker always terminates.
    public static int PickCost(int influenceCost)
    {
        return influenceCost < 1 ? 1 : influenceCost;
    }

    public static bool CanPick(bool exhausted, int influenceCost, int remaining)
    {
        return exhausted && PickCost(influenceCost) <= remaining;
    }
}
```

- [ ] **Step 4: Run the harness to verify it passes (GREEN)**

```powershell
& $mcs -nologo -target:library "-out:$s\Refresh.dll" "-r:$nunit" "Assets\Scripts\UnitPlay\RefreshRules.cs" "Assets\Tests\EditMode\RefreshRulesTests.cs"
& "$s\Runner.exe" "$s\Refresh.dll"
```

Expected: `6 passed, 0 failed`.

- [ ] **Step 5: USER ACTION — focus the Unity editor**; clean compile, `.meta` files generated.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/UnitPlay/RefreshRules.cs Assets/Scripts/UnitPlay/RefreshRules.cs.meta Assets/Tests/EditMode/RefreshRulesTests.cs Assets/Tests/EditMode/RefreshRulesTests.cs.meta
git commit -m @'
feat: RefreshRules - pure unit-refresh budget math

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 3: Enum + ScriptableObject plumbing

**Files:**
- Modify: `Assets/Scripts/Enums/Enums/StatType.cs`
- Modify: `Assets/Scripts/Enums/Enums/SkillEffect.cs`
- Modify: `Assets/Scripts/GameScriptableObjectTypes/CardsSO.cs`
- Modify: `Assets/Scripts/GameScriptableObjectTypes/SkillsSO.cs`
- Modify: `Assets/Scripts/UnitPlay/UnitOption.cs`
- Modify: `Assets/Scripts/GameScriptableObjectTypes/UnitsSO.cs`

**Interfaces:**
- Consumes: `ConvertRules.IsValid(StatType, StatType)` (Task 1).
- Produces: `StatType.Refresh = 256`; `SkillEffect.ConvertStat`, `SkillEffect.RefreshUnits` (appended); `CardsSO.convertTo/convertFrom/convertRequiresEmpower/refresh/empowerRefresh` + `int ReturnRefresh(bool isEmpowered)`; `SkillsSO.convertFrom/convertTo`; `UnitOption.influenceCost` (int). Used by every later task.

- [ ] **Step 1: Append the enum members**

`StatType.cs` — append after `Siege = 128`:

```csharp
    Siege = 128,
    // Immediate effect flag (like Heal/Crystal, not a per-turn pool): the card
    // readies spent units via the refresh picker (spec 2026-07-14).
    Refresh = 256
```

`SkillEffect.cs` — append after `RecruitEnemies` (enum is append-only):

```csharp
    RecruitEnemies,
    // Converts banked action pools 1:1 (SkillsSO.convertFrom -> convertTo).
    ConvertStat,
    // Opens the refresh picker with `magnitude` as the budget.
    RefreshUnits,
```

- [ ] **Step 2: Extend CardsSO**

Add fields after `isChoice` and the two methods after `ReturnSiege` in `Assets/Scripts/GameScriptableObjectTypes/CardsSO.cs`:

```csharp
    public bool isChoice;
    [Header("Conversion (spec 2026-07-14)")]
    public StatType convertTo;          // None = this card has no conversion
    public StatType convertFrom;        // action flags only; never contains convertTo
    public bool convertRequiresEmpower; // true = conversion only offered on the empowered play
    [Header("Refresh (spec 2026-07-14)")]
    public int refresh;
    public int empowerRefresh;
```

```csharp
    public int ReturnRefresh(bool isEmpowered)
    {
        if (cardType.HasFlag(StatType.Refresh))
            if (isEmpowered) return empowerRefresh;
            else return refresh;
        else
        return 0;
    }

    void OnValidate()
    {
        if (convertTo != StatType.None)
        {
            if (isChoice)
                Debug.LogWarning($"{name}: a card cannot be both isChoice and a converter.", this);
            if (!ConvertRules.IsValid(convertFrom, convertTo))
                Debug.LogWarning($"{name}: conversion must target one action stat, draw from action stats, and never include the target in its sources.", this);
        }
        if ((refresh > 0 || empowerRefresh > 0) && !cardType.HasFlag(StatType.Refresh))
            Debug.LogWarning($"{name}: refresh values need the Refresh flag on cardType.", this);
    }
```

- [ ] **Step 3: Extend SkillsSO**

After `cadence` in `Assets/Scripts/GameScriptableObjectTypes/SkillsSO.cs`:

```csharp
    public SkillCadence cadence;
    // Only meaningful for SkillEffect.ConvertStat (spec 2026-07-14).
    public StatType convertFrom;
    public StatType convertTo;
```

- [ ] **Step 4: Extend UnitOption + UnitsSO validation**

`Assets/Scripts/UnitPlay/UnitOption.cs` — add after `crystalCost` (and update the header comment):

```csharp
// One authored option on a unit (spec 2026-07-09). crystalCost None = free;
// a color = requires 1 crystal of that color (wild satisfies, same rule as
// card empower); all-colors = any 1 crystal. grantColor only matters for
// Crystallize. influenceCost (spec 2026-07-14) is an in-turn Influence price;
// an option costs a crystal OR influence, never both.
[System.Serializable]
public class UnitOption
{
    public UnitEffect effect;
    public int amount = 1;
    public EmpowerType grantColor;
    public EmpowerType crystalCost;
    public int influenceCost;
}
```

`Assets/Scripts/GameScriptableObjectTypes/UnitsSO.cs` — add (or merge into an existing) `OnValidate`:

```csharp
    void OnValidate()
    {
        if (options == null) return;
        foreach (var o in options)
            if (o != null && o.crystalCost != EmpowerType.None && o.influenceCost > 0)
                Debug.LogWarning($"{name}: an option may cost a crystal OR influence, not both.", this);
    }
```

(Read the file first; keep whatever fields/validation already exist.)

- [ ] **Step 5: USER ACTION — focus the Unity editor.** Clean compile, no console warnings from existing assets (none set the new fields yet).

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Enums/Enums/StatType.cs Assets/Scripts/Enums/Enums/SkillEffect.cs Assets/Scripts/GameScriptableObjectTypes/CardsSO.cs Assets/Scripts/GameScriptableObjectTypes/SkillsSO.cs Assets/Scripts/UnitPlay/UnitOption.cs Assets/Scripts/GameScriptableObjectTypes/UnitsSO.cs
git commit -m @'
feat: convert/refresh/influence-cost fields on cards, skills, and unit options

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 4: CardSnapshot + CardPlaySelection convert state (TDD)

**Files:**
- Modify: `Assets/Scripts/CardPlay/CardSnapshot.cs`
- Modify: `Assets/Scripts/CardPlay/CardPlaySelection.cs`
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs` (Snapshot factory only)
- Test: `Assets/Tests/EditMode/CardPlaySelectionTests.cs` (extend)

**Interfaces:**
- Consumes: `CardSnapshot` ctor call sites — run `Grep "new CardSnapshot" Assets/Scripts Assets/Tests` and update every site that should pass conversion data (optional params keep the rest compiling).
- Produces: `CardSnapshot.ConvertTo/ConvertFrom/ConvertRequiresEmpower` (readonly fields, optional ctor params defaulting to `None/None/false`); `CardPlaySelection.ConvertOn { get; }`, `void SetConvert(bool)`, `bool HasConversion`, `bool CanConvert()`, `bool EffectiveConvert()`; `IsPlayable()` now also true for `StatType.Refresh`. Used by Tasks 5, 6.

- [ ] **Step 1: Write the failing tests** — append to `Assets/Tests/EditMode/CardPlaySelectionTests.cs` (match the file's existing helper style; the snapshot helper below shows the full optional-param signature):

```csharp
    static CardSnapshot Converter(bool requiresEmpower)
    {
        // Defend 3 / empower Defend 5, Convert all Defend -> Attack (Shield Bash shape)
        return new CardSnapshot(StatType.Defend, EmpowerType.Red, false,
            0, 3, 0, 0,
            0, 5, 0, 0,
            StatType.Attack, StatType.Defend, requiresEmpower);
    }

    [Test]
    public void Convert_DefaultsOff()
    {
        var sel = new CardPlaySelection(Converter(false));
        Assert.IsFalse(sel.ConvertOn);
        Assert.IsTrue(sel.HasConversion);
        Assert.IsTrue(sel.CanConvert());
        Assert.IsFalse(sel.EffectiveConvert());
    }

    [Test]
    public void Convert_OptInArms()
    {
        var sel = new CardPlaySelection(Converter(false));
        sel.SetConvert(true);
        Assert.IsTrue(sel.EffectiveConvert());
    }

    [Test]
    public void Convert_NoConversionCardCannotConvert()
    {
        var sel = new CardPlaySelection(new CardSnapshot(
            StatType.Defend, EmpowerType.None, false, 0, 3, 0, 0, 0, 5, 0, 0));
        Assert.IsFalse(sel.HasConversion);
        sel.SetConvert(true);
        Assert.IsFalse(sel.EffectiveConvert());
    }

    [Test]
    public void Convert_LockedWhileImprovising()
    {
        var sel = new CardPlaySelection(Converter(false));
        sel.SetConvert(true);
        sel.SetImproviseStat(StatType.Attack);
        Assert.IsFalse(sel.CanConvert());
        Assert.IsFalse(sel.EffectiveConvert());
    }

    [Test]
    public void Convert_RequiresEmpowerGatesUntilEmpowered()
    {
        var sel = new CardPlaySelection(Converter(true));
        sel.SetConvert(true);
        Assert.IsFalse(sel.CanConvert());
        sel.SetEmpowered(true);
        Assert.IsTrue(sel.CanConvert());
        Assert.IsTrue(sel.EffectiveConvert());
    }

    [Test]
    public void IsPlayable_RefreshOnlyCardIsPlayable()
    {
        var sel = new CardPlaySelection(new CardSnapshot(
            StatType.Refresh, EmpowerType.Green, false, 0, 0, 0, 0, 0, 0, 0, 0));
        Assert.IsTrue(sel.IsPlayable());
    }
```

- [ ] **Step 2: Run the harness to verify it fails (RED)**

```powershell
& $mcs -nologo -target:library "-out:$s\Sel.dll" "-r:$nunit" "Assets\Scripts\Enums\Enums\StatType.cs" "Assets\Scripts\Enums\Enums\EmpowerType.cs" "Assets\Scripts\CardPlay\PlayMode.cs" "Assets\Scripts\CardPlay\CardSnapshot.cs" "Assets\Scripts\CardPlay\CardPlaySelection.cs" "Assets\Tests\EditMode\CardPlaySelectionTests.cs"
```

(If `EmpowerType.cs` has Unity-only dependencies, include whatever extra enum files the existing CardPlaySelectionTests harness compile already needs — check how CardSnapshotTests were verified previously and mirror that file list.)

Expected: compile errors — new ctor params and members do not exist.

- [ ] **Step 3: Implement**

`CardSnapshot.cs` — add three readonly fields and optional ctor params (existing call sites compile unchanged):

```csharp
public readonly struct CardSnapshot
{
    public readonly StatType CardType;
    public readonly EmpowerType EmpowerType;
    public readonly bool IsChoice;
    public readonly int Attack, Defend, Influence, Explore;
    public readonly int EmpowerAttack, EmpowerDefend, EmpowerInfluence, EmpowerExplore;
    public readonly StatType ConvertTo;             // None = no conversion
    public readonly StatType ConvertFrom;
    public readonly bool ConvertRequiresEmpower;

    public CardSnapshot(StatType cardType, EmpowerType empowerType, bool isChoice,
        int attack, int defend, int influence, int explore,
        int empowerAttack, int empowerDefend, int empowerInfluence, int empowerExplore,
        StatType convertTo = StatType.None, StatType convertFrom = StatType.None,
        bool convertRequiresEmpower = false)
    {
        CardType = cardType;
        EmpowerType = empowerType;
        IsChoice = isChoice;
        Attack = attack; Defend = defend; Influence = influence; Explore = explore;
        EmpowerAttack = empowerAttack; EmpowerDefend = empowerDefend;
        EmpowerInfluence = empowerInfluence; EmpowerExplore = empowerExplore;
        ConvertTo = convertTo;
        ConvertFrom = convertFrom;
        ConvertRequiresEmpower = convertRequiresEmpower;
    }
    // BaseOf / EmpowerOf unchanged
```

`CardPlaySelection.cs` — add convert state (ctor initializes `ConvertOn = false`), and the Refresh playability line:

```csharp
    public bool ConvertOn { get; private set; }

    public void SetConvert(bool value) => ConvertOn = value;

    public bool HasConversion => _card.ConvertTo != StatType.None;

    // Opt-in gate (spec 2026-07-14): never while improvising, and an
    // empower-gated conversion needs the play to actually be empowered.
    public bool CanConvert() =>
        HasConversion && Mode != PlayMode.Improvise
        && (!_card.ConvertRequiresEmpower || EffectiveEmpowered());

    public bool EffectiveConvert() => ConvertOn && CanConvert();
```

In `IsPlayable()` add after the Heal line:

```csharp
        if (_card.CardType.HasFlag(StatType.Refresh)) return true;
```

`CardInspector.Snapshot` — pass the new fields:

```csharp
    static CardSnapshot Snapshot(CardsSO so) =>
        new CardSnapshot(so.cardType, so.empowerType, so.isChoice,
            so.attack, so.defend, so.influence, so.explore,
            so.empowerAttack, so.empowerDefend, so.empowerInfluence, so.empowerExplore,
            so.convertTo, so.convertFrom, so.convertRequiresEmpower);
```

- [ ] **Step 4: Run the harness to verify it passes (GREEN)** — same compile as Step 2 plus `ConvertRules` is NOT needed here. Expected: all existing + 7 new tests pass, `0 failed`.

- [ ] **Step 5: USER ACTION — focus the Unity editor**; clean compile.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/CardPlay/CardSnapshot.cs Assets/Scripts/CardPlay/CardPlaySelection.cs Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs Assets/Tests/EditMode/CardPlaySelectionTests.cs
git commit -m @'
feat: opt-in convert state on the card play selection

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 5: Player conversion apply/revert + Card fields + inspector Play hook

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs`

**Interfaces:**
- Consumes: `ConvertRules.Moved/MovedTotal/IndexOf` (Task 1); `CardPlaySelection.EffectiveConvert()` (Task 4).
- Produces: `Card.ConvertOn` (bool), `Card.ConvertMoved` (int[]), both `[System.NonSerialized] public`; `Player.ShiftPools(int[] moved, StatType to, int sign)` (private, reused by Task 7); `CardInspector.SetConvert(bool)` (public, called by Task 6's banner). Conversion fires only on the Normal play path — validation forbids `isChoice` converters and improvised plays never convert.

- [ ] **Step 1: Card fields** — add to `Card.cs` beside `isEmpowered`:

```csharp
    // Conversion state for the in-flight play (spec 2026-07-14). ConvertOn is
    // set by the inspector before the PlayCommand executes; ConvertMoved holds
    // the per-pool amounts the play actually moved so undo restores exactly.
    [System.NonSerialized] public bool ConvertOn;
    [System.NonSerialized] public int[] ConvertMoved;
```

- [ ] **Step 2: Player apply/revert** — add to `Player.cs`:

```csharp
    // Conversion rider (spec 2026-07-14): only the Normal play path converts —
    // validation forbids isChoice converters and improvise never converts.
    void ApplyCardConversion(Card card)
    {
        if (!card.ConvertOn) return;
        var so = card.cardSO;
        int[] pools = { playerAttack, playerDefend, playerInfluence, playerExplore };
        var moved = ConvertRules.Moved(pools, so.convertFrom, so.convertTo);
        card.ConvertMoved = moved;
        ShiftPools(moved, so.convertTo, +1);
        PulseConvert(moved, so.convertTo);
    }

    void RevertCardConversion(Card card)
    {
        if (card.ConvertMoved == null) return;
        ShiftPools(card.ConvertMoved, card.cardSO.convertTo, -1);
        card.ConvertMoved = null;
    }

    // sign +1: drain each source pool by moved[i], add the total to the target.
    // sign -1: exact inverse. Safe under LIFO undo: nothing touches the pools
    // between execute and undo without itself being undone first.
    void ShiftPools(int[] moved, StatType to, int sign)
    {
        int total = ConvertRules.MovedTotal(moved);
        playerAttack    -= sign * moved[0];
        playerDefend    -= sign * moved[1];
        playerInfluence -= sign * moved[2];
        playerExplore   -= sign * moved[3];
        int target = ConvertRules.IndexOf(to);
        if      (target == 0) playerAttack    += sign * total;
        else if (target == 1) playerDefend    += sign * total;
        else if (target == 2) playerInfluence += sign * total;
        else if (target == 3) playerExplore   += sign * total;
        GetCurrentInfluence();
        GetCurrentExplore();
    }

    // Pulse every drained source icon plus the target (apply only, like the
    // unit-option pulse; undo shows the stat count-down instead).
    void PulseConvert(int[] moved, StatType to)
    {
        foreach (var icon in FindObjectsByType<PlayerIcon>())
        {
            for (int i = 0; i < moved.Length; i++)
                if (moved[i] > 0) icon.AnimateStat(ConvertRules.ActionStats[i]);
            icon.AnimateStat(to);
        }
    }
```

Wire into `PlayCard` (conversion applies after the card's own stats land; reverts before they are unassigned):

```csharp
    public void PlayCard(Card card)
    {
        if(!card.IsPlayed)
        {
            AssignPlayerStats(card.cardSO.GetCardStats(card.IsEmpowered));
            EmpowerCrystalCheck(card);
            onPlay_TriggerAdditionalEffects.Raise(card);
            ApplyCardConversion(card);
        }
        else if(card.IsPlayed)
        {
            RevertCardConversion(card);
            UnAssignPlayerStats(card.cardSO.GetCardStats(card.IsEmpowered));
            UndoEmpower(card);
            onPlay_TriggerAdditionalEffects.Raise(card);
        }
    }
```

- [ ] **Step 3: CardInspector** — add the banner's entry point and arm the card in `Play()`:

```csharp
    public void SetConvert(bool value)      { Selection?.SetConvert(value); Raise(); }
```

In `Play()`, right after `Card.IsEmpowered = Selection.EffectiveEmpowered();`:

```csharp
        Card.ConvertOn = Selection.EffectiveConvert();
```

(Always assigned — true or false — so a card replayed later can never carry a stale opt-in.)

- [ ] **Step 4: USER ACTION — focus the Unity editor**; clean compile. No behavior change is visible yet (no converter card exists and no banner sets `SetConvert`).

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs
git commit -m @'
feat: conversion apply/revert on the card play path

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 6: ConvertBanner section + wiring + Shield Bash

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ConvertBanner.cs`
- Wiring + content: USER ACTION (card canvas scene wiring; Shield Bash asset)

**Interfaces:**
- Consumes: `CardInspector.Selection.HasConversion/CanConvert()/ConvertOn`, `CardInspector.SetConvert(bool)` (Tasks 4–5), `ConvertRules.Describe` (Task 1).
- Produces: the visible opt-in UI; the first converter card asset.

- [ ] **Step 1: Write ConvertBanner**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Opt-in conversion section (spec 2026-07-14). Shows only for converter cards;
// the toggle arms "convert all X → Y" on the in-progress play. Locks (dim +
// reason) while Improvise is active or while an empower-gated conversion isn't
// empowered. Same lifetime-subscription pattern as ChoiceBanner: Render hides
// root (self), so Awake/OnDestroy survive self-deactivation.
public class ConvertBanner : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;              // banner container to show/hide
    [SerializeField] Toggle convertToggle;
    [SerializeField] TextMeshProUGUI label;        // "Convert all Defend → Attack"
    [SerializeField] GameObject lockedReason;
    [SerializeField] TextMeshProUGUI lockedReasonText;

    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        convertToggle.onValueChanged.AddListener(v => inspector.SetConvert(v));
    }

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool show = sel != null && card != null && sel.HasConversion;
        root.SetActive(show);
        if (!show) return;

        label.text = ConvertRules.Describe(card.cardSO.convertFrom, card.cardSO.convertTo);

        bool can = sel.CanConvert();
        convertToggle.SetIsOnWithoutNotify(sel.ConvertOn && can);
        convertToggle.interactable = can;
        if (lockedReason != null) lockedReason.SetActive(!can);
        if (!can && lockedReasonText != null)
            lockedReasonText.text = sel.Mode == PlayMode.Improvise
                ? "Locked while improvising"
                : "Empower to unlock";
    }
}
```

- [ ] **Step 2: USER ACTION — focus editor** (compile + `.meta`), then wire the banner in the card menu canvas:

1. In the card pop-out hierarchy (where **ChoiceBanner** lives), duplicate the ChoiceBanner container GameObject; rename the copy **ConvertBanner**; delete its StatSegment children.
2. On the duplicate, remove the `ChoiceBanner` component and add **ConvertBanner**.
3. Add children: a **Toggle** (checkbox style, from the UI kit used elsewhere), a **TextMeshProUGUI** label next to it, and a small **LockedReason** child (background + TMP text), initially inactive.
4. Wire ConvertBanner's fields: `inspector` → the CardInspector component (same object ChoiceBanner points at), `root` → the ConvertBanner GameObject itself, `convertToggle`, `label`, `lockedReason`, `lockedReasonText`.
5. Position it below the ChoiceBanner slot so both never show at once (a card is never both).

- [ ] **Step 3: USER ACTION — author Shield Bash** (`Assets/Scripts/ScriptableObjectData/Player/Cards/`): Create → ScriptableObjects → Cards → PlayerCards, name **Shield Bash**. `cardName` "Shield Bash", description "Defend 3. Empower: Defend 5 — may convert all Defend to Attack." Fields: `defend 3`, `empowerDefend 5`, `cardType = Defend`, `empowerType = Red`, `convertTo = Attack`, `convertFrom = Defend`, `convertRequiresEmpower = true`, everything else 0/false. Add it to a reward card pool (`RewardTuningSO` tier list) or temporarily to the starting hand for testing.

- [ ] **Step 4: USER ACTION — play test** (Play mode): banner hidden on ordinary cards; on Shield Bash it shows "Convert all Defend → Attack" locked with "Empower to unlock"; empowering unlocks the toggle; playing with toggle on converts the whole Defend pool into Attack (HUD icons pulse); **undo** restores Defend/Attack and the crystal exactly; playing with toggle off just adds Defend; improvising Shield Bash locks the banner.

- [ ] **Step 5: Commit** (code + meta + scene + asset):

```powershell
git add Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ConvertBanner.cs Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ConvertBanner.cs.meta Assets/Scenes "Assets/Scripts/ScriptableObjectData/Player/Cards"
git commit -m @'
feat: ConvertBanner opt-in UI + Shield Bash converter card

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 7: ConvertStat skill effect

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/Leveling/SkillToken.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`
- Content: USER ACTION (Tactician skill asset)

**Interfaces:**
- Consumes: `ShiftPools`/`PulseConvert` (Task 5), `SkillsSO.convertFrom/convertTo` + `SkillEffect.ConvertStat` (Task 3).
- Produces: `SkillToken.ConvertMoved` (`[System.NonSerialized] public int[]`); `Player.ApplySkillEffect` signature becomes `(SkillsSO skill, int sign, SkillToken token)`.

- [ ] **Step 1: SkillToken snapshot field** — add beside `skillSO`:

```csharp
    // Per-activation conversion snapshot (spec 2026-07-14): the sign-flip undo
    // pattern can't reverse a conversion, so the applied amounts live here.
    [System.NonSerialized] public int[] ConvertMoved;
```

- [ ] **Step 2: Player** — pass the token through and add the case. Change both call sites in `PerformSkillAction`:

```csharp
            ApplySkillEffect(token.skillSO, +1, token);
            ...
            ApplySkillEffect(token.skillSO, -1, token);
```

Change the signature to `private void ApplySkillEffect(SkillsSO skill, int sign, SkillToken token)` and add before the `RecruitEnemies` case:

```csharp
            case SkillEffect.ConvertStat:
            {
                if (sign > 0)
                {
                    int[] pools = { playerAttack, playerDefend, playerInfluence, playerExplore };
                    token.ConvertMoved = ConvertRules.Moved(pools, skill.convertFrom, skill.convertTo);
                    ShiftPools(token.ConvertMoved, skill.convertTo, +1);
                    PulseConvert(token.ConvertMoved, skill.convertTo);
                }
                else if (token.ConvertMoved != null)
                {
                    ShiftPools(token.ConvertMoved, skill.convertTo, -1);
                    token.ConvertMoved = null;
                }
                break;
            }
```

- [ ] **Step 3: USER ACTION — focus editor** (clean compile), then author **Tactician** (Create → ScriptableObjects → Skill): `cardName` "Tactician", description "Convert all Defend to Attack.", `effect = ConvertStat`, `cadence = PerRound`, `convertFrom = Defend`, `convertTo = Attack`, `magnitude` irrelevant (leave 1). Add it to the `LevelRewardsSO` skill pool.

- [ ] **Step 4: USER ACTION — play test:** level up to a skill pick offering Tactician (or temporarily grant it), bank some Defend, click the skill — Defend pool becomes Attack, token exhausts; undo restores pools and readies the token; converting an empty Defend pool is a harmless no-op that still exhausts (undo reverts).

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/GameObjectScripts/Leveling/SkillToken.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs "Assets/Scripts/ScriptableObjectData"
git commit -m @'
feat: ConvertStat skill effect with per-activation undo snapshot

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 8: UnitPickerPanel + refresh flow (card + skill)

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs`
- Modify: `Assets/Scripts/GameObjectScripts/Leveling/SkillToken.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`

**Interfaces:**
- Consumes: `RefreshRules.PickCost/CanPick` (Task 2), `CardsSO.ReturnRefresh` + `SkillEffect.RefreshUnits` (Task 3).
- Produces: `UnitPickerPanel.OpenForRefresh(int budget, System.Action<Unit> onPick)`; `Card.RefreshedUnits` / `SkillToken.RefreshedUnits` (`public readonly List<Unit>`); `Player.ReadyUnit(Unit)` / `Player.ExhaustUnit(Unit)` (private helpers, also DRY-ing the existing rotate/IsPlayed pairs).

- [ ] **Step 1: Write UnitPickerPanel**

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Modal "ready a spent unit" picker (spec 2026-07-14). DisbandPanel's shape:
// own Canvas toggled on/off, one button per unit, continuation callback. Opens
// with a refresh budget; only exhausted units list, entries over the remaining
// budget show disabled, each pick deducts the unit's influenceCost (min 1) and
// readies it via the callback. Done — or nothing left affordable — closes.
// Not a reward modal: opens directly, never through RewardQueue.
[RequireComponent(typeof(Canvas))]
public class UnitPickerPanel : MonoBehaviour
{
    [SerializeField] Transform entryContainer;     // vertical layout for unit buttons
    [SerializeField] GameObject entryButtonPrefab; // Button + TMP label
    [SerializeField] Button doneButton;
    [SerializeField] TextMeshProUGUI titleLabel;   // "Refresh — 3 left"

    System.Action<Unit> _onPick;
    int _remaining;
    readonly List<GameObject> spawned = new();

    Canvas _canvas;
    Canvas Canvas => _canvas ??= GetComponent<Canvas>();

    void Start()
    {
        doneButton.onClick.RemoveAllListeners();
        doneButton.onClick.AddListener(Close);
        Canvas.enabled = false; // start closed regardless of the authored state
    }

    public void OpenForRefresh(int budget, System.Action<Unit> onPick)
    {
        _onPick = onPick;
        _remaining = budget;
        Canvas.enabled = true;
        Rebuild();
    }

    void Rebuild()
    {
        ClearEntries();
        if (titleLabel != null) titleLabel.text = $"Refresh — {_remaining} left";

        bool any = false;
        foreach (var unit in FindObjectsByType<Unit>())
        {
            if (!unit.IsPlayed) continue; // only spent units list
            var go = Instantiate(entryButtonPrefab, entryContainer);
            int cost = RefreshRules.PickCost(unit.unitSO.influenceCost);
            go.GetComponentInChildren<TextMeshProUGUI>().text = $"{unit.unitSO.cardName} — {cost}";
            bool pickable = RefreshRules.CanPick(unit.IsPlayed, unit.unitSO.influenceCost, _remaining);
            var button = go.GetComponent<Button>();
            button.interactable = pickable;
            if (pickable)
            {
                any = true;
                var captured = unit;
                button.onClick.AddListener(() => Pick(captured));
            }
            spawned.Add(go);
        }
        if (!any) Close(); // unspent budget is lost (spec) — nothing left to buy
    }

    void Pick(Unit unit)
    {
        _remaining -= RefreshRules.PickCost(unit.unitSO.influenceCost);
        _onPick?.Invoke(unit);
        Rebuild(); // unit stood up, so it drops off the list; budget re-renders
    }

    void Close()
    {
        ClearEntries();
        _onPick = null;
        Canvas.enabled = false;
    }

    void ClearEntries()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}
```

- [ ] **Step 2: Record-keeping fields** — `Card.cs` (beside `ConvertMoved`) and `SkillToken.cs` (beside `ConvertMoved`), both need `using System.Collections.Generic;` if absent:

```csharp
    // Units this play readied (spec 2026-07-14) so undo re-exhausts exactly them.
    public readonly List<Unit> RefreshedUnits = new();
```

- [ ] **Step 3: Player refresh flow** — add helpers and DRY the existing rotate pairs:

```csharp
    // The one rotate/IsPlayed pair, shared by round refresh, unit options, and
    // the refresh picker so exhaust visuals can never drift apart.
    void ReadyUnit(Unit unit)   { unit.transform.Rotate(0, 0, 90);  unit.IsPlayed = false; }
    void ExhaustUnit(Unit unit) { unit.transform.Rotate(0, 0, -90); unit.IsPlayed = true; }

    bool AnyRefreshable(int budget)
    {
        foreach (var unit in FindObjectsByType<Unit>())
            if (RefreshRules.CanPick(unit.IsPlayed, unit.unitSO.influenceCost, budget)) return true;
        return false;
    }

    // Refresh rider (spec 2026-07-14): open the picker with the play's budget.
    // Picks ready units immediately and are recorded on the card so a later
    // undo of this play re-exhausts exactly those units. Fizzles (no picker)
    // when nothing affordable is spent at play time.
    void BeginCardRefresh(Card card)
    {
        card.RefreshedUnits.Clear();
        int budget = card.cardSO.ReturnRefresh(card.IsEmpowered);
        if (budget <= 0 || !AnyRefreshable(budget)) return;
        var panel = FindAnyObjectByType<UnitPickerPanel>();
        if (panel == null) return;
        panel.OpenForRefresh(budget, unit =>
        {
            ReadyUnit(unit);
            card.RefreshedUnits.Add(unit);
        });
    }

    void RevertCardRefresh(Card card)
    {
        foreach (var unit in card.RefreshedUnits) ExhaustUnit(unit);
        card.RefreshedUnits.Clear();
    }
```

Replace the bodies that duplicate the rotate pairs: in `RefreshUnits()` use `ReadyUnit(unit)`; at the end of `ApplyUnitOption` use `ExhaustUnit(unit)`; at the end of `RevertUnitOption` use `ReadyUnit(unit)`.

Wire into `PlayCard` (refresh is the last apply step; reverted first):

```csharp
        if(!card.IsPlayed)
        {
            AssignPlayerStats(card.cardSO.GetCardStats(card.IsEmpowered));
            EmpowerCrystalCheck(card);
            onPlay_TriggerAdditionalEffects.Raise(card);
            ApplyCardConversion(card);
            BeginCardRefresh(card);
        }
        else if(card.IsPlayed)
        {
            RevertCardRefresh(card);
            RevertCardConversion(card);
            UnAssignPlayerStats(card.cardSO.GetCardStats(card.IsEmpowered));
            UndoEmpower(card);
            onPlay_TriggerAdditionalEffects.Raise(card);
        }
```

Add the skill case to `ApplySkillEffect` (after `ConvertStat`):

```csharp
            case SkillEffect.RefreshUnits:
            {
                if (sign > 0)
                {
                    token.RefreshedUnits.Clear();
                    if (AnyRefreshable(skill.magnitude))
                        FindAnyObjectByType<UnitPickerPanel>()?.OpenForRefresh(skill.magnitude, unit =>
                        {
                            ReadyUnit(unit);
                            token.RefreshedUnits.Add(unit);
                        });
                }
                else
                {
                    foreach (var unit in token.RefreshedUnits) ExhaustUnit(unit);
                    token.RefreshedUnits.Clear();
                }
                break;
            }
```

- [ ] **Step 4: USER ACTION — focus editor**; clean compile (panel not yet in scene — `FindAnyObjectByType` returns null and the refresh safely no-ops).

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs.meta Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs Assets/Scripts/GameObjectScripts/Leveling/SkillToken.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs
git commit -m @'
feat: UnitPickerPanel + budgeted refresh flow for cards and skills

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 9: Picker canvas wiring + Mobilize card

**Files:**
- Wiring + content: USER ACTION only

- [ ] **Step 1: USER ACTION — build the picker canvas** (mirror the DisbandPanel object):

1. Duplicate the **DisbandPanel** canvas GameObject; rename **UnitPickerPanel**.
2. Swap the `DisbandPanel` component for **UnitPickerPanel**; wire `entryContainer` (the vertical layout), `entryButtonPrefab` (reuse DisbandPanel's entry button prefab), `doneButton` (relabel the Cancel button "DONE"), and add a title TMP text wired to `titleLabel`.
3. Ensure the canvas has a **full-screen raycast-blocking background image** (semi-transparent) so the board, hand, undo, and End Turn are unreachable while it is open — it is modal.
4. Canvas settings match DisbandPanel (Screen Space - Camera, sorting above the board UI).

- [ ] **Step 2: USER ACTION — author Mobilize** (PlayerCards asset): `cardName` "Mobilize", description "Refresh 3. Empower: Refresh 6. (Ready spent units up to their recruit cost.)", `cardType = Explore | Refresh` with `explore 1` / `empowerExplore 1` (the small secondary stat so it is never a dead play), `refresh 3`, `empowerRefresh 6`, `empowerType = Green`. Add to a reward pool and/or starting hand for testing.

- [ ] **Step 3: USER ACTION — play test:** recruit 2+ units, use them (exhaust), play Mobilize → picker lists spent units with costs, over-budget entries disabled, picking stands a unit up and shrinks the budget, Done closes; the readied unit can act again this round; **undo of Mobilize** re-exhausts the picked units and refunds the crystal if empowered; playing Mobilize with no spent units fizzles (no picker, Explore still lands); a per-round skill authored with `RefreshUnits` (optional quick asset) behaves the same.

- [ ] **Step 4: Commit** (scene + assets):

```powershell
git add Assets/Scenes "Assets/Scripts/ScriptableObjectData"
git commit -m @'
feat: unit picker canvas wiring + Mobilize refresh card

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 10: Influence-costed unit options

**Files:**
- Modify: `Assets/Scripts/UnitPlay/UnitOptionText.cs`
- Modify: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitInspector.cs`
- Modify: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitUseBar.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`
- Test: `Assets/Tests/EditMode/UnitOptionTextTests.cs` (create)

**Interfaces:**
- Consumes: `UnitOption.influenceCost` (Task 3), `Player.PlayerInfluence`.
- Produces: influence price rendering, affordability lock, undoable spend inside `ApplyUnitOption`/`RevertUnitOption` (never `Player.Influence()`, which clears the undo stack — that is for permanent purchases).

- [ ] **Step 1: Write the failing tests** — `Assets/Tests/EditMode/UnitOptionTextTests.cs`:

```csharp
using NUnit.Framework;

public class UnitOptionTextTests
{
    static UnitOption Opt(UnitEffect effect, int amount, EmpowerType crystal, int influence)
    {
        var o = new UnitOption();
        o.effect = effect;
        o.amount = amount;
        o.crystalCost = crystal;
        o.influenceCost = influence;
        return o;
    }

    [Test]
    public void Describe_FreeOption()
    {
        Assert.AreEqual("Attack 2", UnitOptionText.Describe(Opt(UnitEffect.Attack, 2, EmpowerType.None, 0)));
    }

    [Test]
    public void Describe_CrystalCostUnchanged()
    {
        Assert.AreEqual("Attack 4 — 1 Red crystal",
            UnitOptionText.Describe(Opt(UnitEffect.Attack, 4, EmpowerType.Red, 0)));
    }

    [Test]
    public void Describe_InfluenceCost()
    {
        Assert.AreEqual("Attack 5 — 3 Influence",
            UnitOptionText.Describe(Opt(UnitEffect.Attack, 5, EmpowerType.None, 3)));
    }
}
```

- [ ] **Step 2: Run the harness to verify it fails (RED)**

```powershell
& $mcs -nologo -target:library "-out:$s\OptText.dll" "-r:$nunit" "Assets\Scripts\Enums\Enums\EmpowerType.cs" "Assets\Scripts\Enums\Enums\UnitEffect.cs" "Assets\Scripts\ExtensionClass.cs" "Assets\Scripts\UnitPlay\UnitOption.cs" "Assets\Scripts\UnitPlay\UnitOptionText.cs" "Assets\Tests\EditMode\UnitOptionTextTests.cs"
```

(If `ExtensionClass.cs` — home of `IsAllColors()` — pulls in Unity types, extract just that check's file list the way prior harness compiles did; adjust until only the influence test fails: `Describe_InfluenceCost` red, others green.)

- [ ] **Step 3: Implement** — `UnitOptionText.Describe`:

```csharp
    public static string Describe(UnitOption o)
    {
        string body = o.effect == UnitEffect.Crystallize
            ? $"Crystallize: {o.amount} {o.grantColor}"
            : $"{o.effect} {o.amount}";

        if (o.influenceCost > 0) return $"{body} — {o.influenceCost} Influence";
        if (o.crystalCost == EmpowerType.None) return body;
        string cost = o.crystalCost.IsAllColors() ? "1 crystal (any color)" : $"1 {o.crystalCost} crystal";
        return $"{body} — {cost}";
    }
```

- [ ] **Step 4: Run the harness to verify it passes (GREEN)** — same compile; expected `3 passed, 0 failed`.

- [ ] **Step 5: Affordability + spend** —

`UnitInspector.Open`: replace the affordability loop:

```csharp
        var player = FindAnyObjectByType<Player>();
        var options = unit.unitSO.options;
        var affordable = new bool[options.Count];
        for (int i = 0; i < options.Count; i++)
        {
            bool crystalOk = inv == null ? options[i].crystalCost == EmpowerType.None
                                         : inv.CanPay(options[i].crystalCost);
            bool influenceOk = player == null || player.PlayerInfluence >= options[i].influenceCost;
            affordable[i] = crystalOk && influenceOk;
        }
```

`Player.ApplyUnitOption` — first line of the method body:

```csharp
        // Influence-costed option (spec 2026-07-14): an in-turn tactical spend,
        // so it stays undoable — never Player.Influence(), which clears the
        // stack for permanent purchases.
        if (option.influenceCost > 0) { playerInfluence -= option.influenceCost; GetCurrentInfluence(); }
```

`Player.RevertUnitOption` — first line of the method body:

```csharp
        if (option.influenceCost > 0) { playerInfluence += option.influenceCost; GetCurrentInfluence(); }
```

`UnitUseBar.Render` — replace the hardcoded "Needs a crystal":

```csharp
        useLabel.text = sel.CanUse
            ? $"USE · {sel.Describe(sel.SelectedIndex)}"
            : (sel.SelectedIndex >= 0
                ? (sel.Selected != null && sel.Selected.influenceCost > 0 ? "Needs influence" : "Needs a crystal")
                : "No options");
```

- [ ] **Step 6: USER ACTION — focus editor** (clean compile), then author the mercenary row: open the mercenary-style `UnitsSO`, add an option `effect = Attack, amount = 5, influenceCost = 3` (crystalCost None). Play test: the pop-out shows "Attack 5 — 3 Influence"; with < 3 Influence the row is dimmed/locked and Use shows "Needs influence"; with ≥ 3 it applies +5 Attack and deducts 3 Influence (HUD updates); undo refunds both and readies the unit.

- [ ] **Step 7: Commit**

```powershell
git add Assets/Scripts/UnitPlay/UnitOptionText.cs Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitInspector.cs Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitUseBar.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs Assets/Tests/EditMode/UnitOptionTextTests.cs Assets/Tests/EditMode/UnitOptionTextTests.cs.meta "Assets/Scripts/ScriptableObjectData"
git commit -m @'
feat: influence-costed unit options with undoable spend

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 11: Remaining content, docs, and acceptance

**Files:**
- Content: USER ACTION (Rally to the Banner)
- Modify: `.claude/skills/archons-rise-design/mechanics.md`, `.claude/skills/archons-rise-design/content-rules.md`, `.claude/skills/archons-rise-design/balance.md`
- Modify: `.claude/skills/archons-rise-roadmap/decisions-log.md`, `.claude/skills/archons-rise-roadmap/milestones.md`

- [ ] **Step 1: USER ACTION — author Rally to the Banner** (PlayerCards): `cardName` "Rally to the Banner", description "Influence 2. Empower: Influence 3 — may convert all Attack, Defend and Explore to Influence." `influence 2`, `empowerInfluence 3`, `cardType = Influence`, `empowerType = Purple`, `convertTo = Influence`, `convertFrom = Attack | Defend | Explore`, `convertRequiresEmpower = true`. Add to a reward pool. Play test: toggling convert dumps all three pools into Influence.

- [ ] **Step 2: Update the design bible.**
  - `mechanics.md`: add a **Conversion** paragraph under Stats (1:1, opt-in, action stats only, Siege excluded); extend **Units** with mid-round Refresh (budget = influence costs, picker, undo) and influence-costed options; extend **Skills** with the two new effects.
  - `content-rules.md`: document the new `CardsSO` fields (`convertTo/convertFrom/convertRequiresEmpower/refresh/empowerRefresh`, Refresh flag rule, isChoice-XOR-convert rule, target-not-in-sources rule), `SkillsSO.convertFrom/convertTo`, `UnitOption.influenceCost` (one cost type per option), and `StatType.Refresh = 256` / the two `SkillEffect` members in the Enums section.
  - `balance.md`: converter cards price ~1 point under vanilla same-tier cards; refresh base ≈ one cheap unit, empowered ≈ two cheap or one elite; influence option price ≈ recruit-value of the stat burst.

- [ ] **Step 3: Roadmap** — append to `decisions-log.md` (decision 2026-07-14: the six locked decisions from the spec table) and add a milestone entry to `milestones.md` for this feature with its acceptance line.

- [ ] **Step 4: USER ACTION — acceptance.** Run the full EditMode suite in the editor Test Runner (all green, including the three new test files). Full play-test pass: Shield Bash convert + undo; Rally multi-source convert; Tactician skill convert + undo; Mobilize refresh (multi-pick, over-budget lock, fizzle, undo); mercenary influence option (lock, spend, undo); save/load mid-run still restores units/skills/exhaust states (no schema change to verify beyond a smoke load).

- [ ] **Step 5: Commit**

```powershell
git add .claude/skills/archons-rise-design .claude/skills/archons-rise-roadmap "Assets/Scripts/ScriptableObjectData"
git commit -m @'
docs: design bible + roadmap for conversion, refresh, and influence options

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```
