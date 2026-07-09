# Unit Gameplay & Recruitment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Units become configurable option-lists used through a card-style pop-out (with per-option crystal costs), and Influence can pay off or — with the Charismatic passive — recruit enemies; towns recruit through a choice panel at per-unit prices.

**Architecture:** Pure logic (`UnitOption`, `UnitPlaySelection`, `UnitNavRules`, `UnitOptionText`) in a new `ArchonsRise.UnitPlay` asmdef, TDD'd via the mcs CLI harness. Scene side mirrors the card pop-out: `UnitInspector` + thin views + `UnitInspectorNavController`, driven by a rewritten `UnitCommand` with symmetric undo. Recruiting: `Player.InfluenceEnemy` (combat) and a new `RecruitPanel` (towns), both funneling through `Player.AddUnit` and the existing influence-spend event. Save schema v4 → v5 (`unitExhausted` parallel to `unitIds`).

**Tech Stack:** Unity 6000.5.1f1, C#, NUnit (EditMode), Mono `mcs` CLI harness for pure logic, TextMeshPro, ScriptableObject event bus, Input System (GameControls).

**Spec:** `docs/superpowers/specs/2026-07-09-unit-gameplay-and-recruitment-design.md`

## Global Constraints

- **No git worktree** — Unity holds `Library/`; work on `master` like every prior milestone.
- **Never hand-edit scene/prefab YAML.** Scene, prefab, and cross-asset-reference wiring is done by the user in the editor from USER ACTION steps. Editing **fields of existing `.asset` ScriptableObjects** directly is allowed (established practice); creating new `.asset` files is done by the user in the editor so Unity assigns GUIDs/`.meta`s.
- **The Unity editor holds the compile lock** — batch-mode tests won't run while it's open. Pure logic is RED/GREEN-verified with the mcs harness; EditMode tests run in the editor's Test Runner at acceptance.
- **Unity generates `.meta` files** — never create them by hand; after adding `.cs` files, ask the user to focus the editor, then commit code + generated `.meta` together.
- Save schema **v5**; migration from v4 must default `unitExhausted` to empty (all units fresh).
- Crystal-cost matching reuses the card-empower rule: exact color or a wild (`isAll`) crystal; a cost of all-colors accepts any crystal; `EmpowerType.None` = free.
- Balance starting values (spec §6): unit recruit costs cheap 2–3 / standard 3–4 / premium 5+; a crystal-costed option ≈ 2× its free sibling; skill pool grows to 10 with Charismatic.
- **Enum appends only**: new `SkillCadence`/`SkillEffect` members go at the END (serialized ints).
- Commit messages: `feat:`/`test:`/`chore:`/`docs:` prefixes, ending with the Claude co-author line.

## mcs Harness (used by Tasks 1, 2, 9)

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

Run pattern (after each library compile): `& "$s\Runner.exe" "$s\<Lib>.dll"`.

---

# Phase 1 — Pure unit-play logic (TDD, mcs)

### Task 1: `UnitEffect` + `UnitOption` + `UnitOptionText` + `UnitPlaySelection`

**Files:**
- Create: `Assets/Scripts/Enums/Enums/UnitEffect.cs`
- Create: `Assets/Scripts/UnitPlay/ArchonsRise.UnitPlay.asmdef`
- Create: `Assets/Scripts/UnitPlay/UnitOption.cs`
- Create: `Assets/Scripts/UnitPlay/UnitOptionText.cs`
- Create: `Assets/Scripts/UnitPlay/UnitPlaySelection.cs`
- Create: `Assets/Tests/EditMode/UnitPlaySelectionTests.cs`
- Modify: `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` (add `"ArchonsRise.UnitPlay"` to `references`)

**Interfaces:**
- Consumes: `EmpowerType` + its `IsAllColors()` extension (ArchonsRise.Enums).
- Produces (later tasks call these exactly):
  - `enum UnitEffect { Attack, Defend, Explore, Influence, Siege, Heal, Crystallize }`
  - `class UnitOption { UnitEffect effect; int amount; EmpowerType grantColor; EmpowerType crystalCost; }`
  - `UnitOptionText.Describe(UnitOption) → string`
  - `UnitPlaySelection(IReadOnlyList<UnitOption> options, IReadOnlyList<bool> affordable)`;
    members `Count`, `SelectedIndex`, `Selected`, `Select(int)`, `IsAffordable(int)`, `CanUse`, `Describe(int)`

- [ ] **Step 1: Create the enum and asmdef**

`Assets/Scripts/Enums/Enums/UnitEffect.cs`:

```csharp
// What one unit option does. Attack..Siege add to the matching action pool;
// Heal removes wounds; Crystallize grants crystals of the option's grantColor.
public enum UnitEffect
{
    Attack,
    Defend,
    Explore,
    Influence,
    Siege,
    Heal,
    Crystallize,
}
```

`Assets/Scripts/UnitPlay/ArchonsRise.UnitPlay.asmdef`:

```json
{
    "name": "ArchonsRise.UnitPlay",
    "rootNamespace": "",
    "references": ["ArchonsRise.Enums"],
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

In `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`, add `"ArchonsRise.UnitPlay"` to the `references` array.

- [ ] **Step 2: Create `UnitOption`**

`Assets/Scripts/UnitPlay/UnitOption.cs`:

```csharp
// One authored option on a unit (spec 2026-07-09). crystalCost None = free;
// a color = requires 1 crystal of that color (wild satisfies, same rule as
// card empower); all-colors = any 1 crystal. grantColor only matters for
// Crystallize.
[System.Serializable]
public class UnitOption
{
    public UnitEffect effect;
    public int amount = 1;
    public EmpowerType grantColor;
    public EmpowerType crystalCost;
}
```

- [ ] **Step 3: Write the failing tests**

`Assets/Tests/EditMode/UnitPlaySelectionTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class UnitPlaySelectionTests
{
    static UnitOption Opt(UnitEffect e, int amount, EmpowerType cost = EmpowerType.None,
        EmpowerType grant = EmpowerType.None)
        => new UnitOption { effect = e, amount = amount, crystalCost = cost, grantColor = grant };

    [Test]
    public void Preselects_First_Affordable_Option()
    {
        var sel = new UnitPlaySelection(
            new List<UnitOption> { Opt(UnitEffect.Defend, 6, EmpowerType.Red), Opt(UnitEffect.Defend, 3) },
            new List<bool> { false, true });
        Assert.AreEqual(1, sel.SelectedIndex);
        Assert.IsTrue(sel.CanUse);
    }

    [Test]
    public void No_Affordable_Option_Selects_First_And_Blocks_Use()
    {
        var sel = new UnitPlaySelection(
            new List<UnitOption> { Opt(UnitEffect.Defend, 6, EmpowerType.Red) },
            new List<bool> { false });
        Assert.AreEqual(0, sel.SelectedIndex);
        Assert.IsFalse(sel.CanUse);
    }

    [Test]
    public void Select_Lands_On_Locked_Rows_But_CanUse_Stays_False()
    {
        var sel = new UnitPlaySelection(
            new List<UnitOption> { Opt(UnitEffect.Attack, 2), Opt(UnitEffect.Siege, 2, EmpowerType.Red) },
            new List<bool> { true, false });
        sel.Select(1);
        Assert.AreEqual(1, sel.SelectedIndex);
        Assert.IsFalse(sel.CanUse);
        sel.Select(0);
        Assert.IsTrue(sel.CanUse);
    }

    [Test]
    public void Select_Out_Of_Range_Is_Ignored()
    {
        var sel = new UnitPlaySelection(
            new List<UnitOption> { Opt(UnitEffect.Attack, 2) }, new List<bool> { true });
        sel.Select(5);
        Assert.AreEqual(0, sel.SelectedIndex);
        sel.Select(-1);
        Assert.AreEqual(0, sel.SelectedIndex);
    }

    [Test]
    public void Describe_Free_Stat_Option()
    {
        Assert.AreEqual("Attack 2", UnitOptionText.Describe(Opt(UnitEffect.Attack, 2)));
    }

    [Test]
    public void Describe_Costed_Option_Appends_Cost()
    {
        Assert.AreEqual("Defend 6 — 1 Red crystal",
            UnitOptionText.Describe(Opt(UnitEffect.Defend, 6, EmpowerType.Red)));
    }

    [Test]
    public void Describe_AnyColor_Cost()
    {
        var anyColor = EmpowerType.Red | EmpowerType.Yellow | EmpowerType.Green | EmpowerType.Purple;
        Assert.AreEqual("Heal 1 — 1 crystal (any color)",
            UnitOptionText.Describe(Opt(UnitEffect.Heal, 1, anyColor)));
    }

    [Test]
    public void Describe_Crystallize_Shows_Grant_Color()
    {
        Assert.AreEqual("Crystallize: 1 Yellow",
            UnitOptionText.Describe(Opt(UnitEffect.Crystallize, 1, EmpowerType.None, EmpowerType.Yellow)));
    }

    [Test]
    public void Empty_Options_Cannot_Use()
    {
        var sel = new UnitPlaySelection(new List<UnitOption>(), new List<bool>());
        Assert.AreEqual(-1, sel.SelectedIndex);
        Assert.IsFalse(sel.CanUse);
    }
}
```

- [ ] **Step 4: RED — compile tests against stubs and watch them fail**

Compile the library + tests with mcs (UnitOption/UnitEffect exist; `UnitPlaySelection`/`UnitOptionText` don't yet):

```powershell
$s = "<scratchpad>"
& $mcs -nologo -target:library "-out:$s\UnitPlay.dll" `
  Assets\Scripts\Enums\Enums\*.cs Assets\Scripts\ExtensionClass.cs `
  Assets\Scripts\UnitPlay\UnitOption.cs
```

Expected: the test compile FAILS with "The type or namespace name `UnitPlaySelection` could not be found". (If `ExtensionClass.cs` drags in UnityEngine, instead copy the `IsAllColors` extension into a tiny shim file in the scratchpad for harness builds only — do NOT commit the shim.)

- [ ] **Step 5: Implement `UnitOptionText`**

`Assets/Scripts/UnitPlay/UnitOptionText.cs`:

```csharp
// Row/label text for a unit option. UI-framework-free so it is mcs-testable
// and reusable by the pop-out rows, the Use bar, and the recruit panel.
public static class UnitOptionText
{
    public static string Describe(UnitOption o)
    {
        string body = o.effect == UnitEffect.Crystallize
            ? $"Crystallize: {o.amount} {o.grantColor}"
            : $"{o.effect} {o.amount}";

        if (o.crystalCost == EmpowerType.None) return body;
        string cost = o.crystalCost.IsAllColors() ? "1 crystal (any color)" : $"1 {o.crystalCost} crystal";
        return $"{body} — {cost}";
    }
}
```

- [ ] **Step 6: Implement `UnitPlaySelection`**

`Assets/Scripts/UnitPlay/UnitPlaySelection.cs`:

```csharp
using System.Collections.Generic;

// Pure state of one open unit pop-out: which row is selected and whether it
// can be used. Affordability is computed once at Open (the pop-out is modal,
// so crystal counts cannot change underneath it). Locked (unaffordable) rows
// are selectable — the player can focus them to read the cost — but CanUse
// stays false on them.
public class UnitPlaySelection
{
    readonly IReadOnlyList<UnitOption> _options;
    readonly IReadOnlyList<bool> _affordable;

    public int SelectedIndex { get; private set; }

    public UnitPlaySelection(IReadOnlyList<UnitOption> options, IReadOnlyList<bool> affordable)
    {
        _options = options;
        _affordable = affordable;
        SelectedIndex = options.Count == 0 ? -1 : 0;
        for (int i = 0; i < options.Count; i++)
            if (affordable[i]) { SelectedIndex = i; break; }
    }

    public int Count => _options.Count;
    public UnitOption Selected => SelectedIndex >= 0 ? _options[SelectedIndex] : null;
    public bool CanUse => SelectedIndex >= 0 && _affordable[SelectedIndex];

    public void Select(int index)
    {
        if (index < 0 || index >= _options.Count) return;
        SelectedIndex = index;
    }

    public bool IsAffordable(int index) => index >= 0 && index < _affordable.Count && _affordable[index];

    public string Describe(int index) => UnitOptionText.Describe(_options[index]);
}
```

- [ ] **Step 7: GREEN — compile and run via the harness**

```powershell
& $mcs -nologo -target:library "-out:$s\UnitPlay.dll" `
  <enum/extension sources as in Step 4> `
  Assets\Scripts\UnitPlay\UnitOption.cs Assets\Scripts\UnitPlay\UnitOptionText.cs Assets\Scripts\UnitPlay\UnitPlaySelection.cs
& $mcs -nologo -target:library "-out:$s\UnitPlayTests.dll" "-r:$s\UnitPlay.dll" "-r:$s\nunit.framework.dll" `
  Assets\Tests\EditMode\UnitPlaySelectionTests.cs
Copy-Item "$s\UnitPlay.dll" $s -Force
& "$s\Runner.exe" "$s\UnitPlayTests.dll"
```

Expected: `9 passed, 0 failed`.

- [ ] **Step 8: Commit**

```powershell
git add Assets/Scripts/Enums/Enums/UnitEffect.cs Assets/Scripts/UnitPlay/ Assets/Tests/EditMode/UnitPlaySelectionTests.cs Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef
git commit -m @'
feat: UnitOption model + UnitPlaySelection pure logic (TDD)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

(.meta files for the new folder/files come with the next editor-focus checkpoint; include them in the following commit.)

---

### Task 2: `UnitNavRules` (pure, TDD)

**Files:**
- Create: `Assets/Scripts/UnitPlay/UnitNavRules.cs`
- Test: `Assets/Tests/EditMode/UnitNavRulesTests.cs`

**Interfaces:**
- Consumes: nothing (leaf).
- Produces (Task 6 calls these exactly):
  - `UnitNavRules.UseSlot(int optionCount) → int` (== optionCount)
  - `UnitNavRules.Open(int optionCount) → int` (0, or UseSlot when no options)
  - `UnitNavRules.Move(int pos, int dy, int optionCount) → int` (dy > 0 is up)

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/UnitNavRulesTests.cs`:

```csharp
using NUnit.Framework;

public class UnitNavRulesTests
{
    [Test] public void Open_Focuses_First_Option() => Assert.AreEqual(0, UnitNavRules.Open(3));
    [Test] public void Open_With_No_Options_Focuses_Use() => Assert.AreEqual(UnitNavRules.UseSlot(0), UnitNavRules.Open(0));
    [Test] public void Down_Moves_To_Next_Option() => Assert.AreEqual(1, UnitNavRules.Move(0, -1, 3));
    [Test] public void Down_Past_Last_Option_Lands_On_Use() => Assert.AreEqual(3, UnitNavRules.Move(2, -1, 3));
    [Test] public void Down_On_Use_Stays() => Assert.AreEqual(3, UnitNavRules.Move(3, -1, 3));
    [Test] public void Up_From_Use_Returns_To_Last_Option() => Assert.AreEqual(2, UnitNavRules.Move(3, +1, 3));
    [Test] public void Up_At_Top_Stays() => Assert.AreEqual(0, UnitNavRules.Move(0, +1, 3));
    [Test] public void Zero_Delta_Stays() => Assert.AreEqual(1, UnitNavRules.Move(1, 0, 3));
}
```

- [ ] **Step 2: RED — compile via harness, expect "UnitNavRules could not be found"**

Same mcs pattern as Task 1 Step 4, adding the new test file. Expected: compile FAILS.

- [ ] **Step 3: Implement**

`Assets/Scripts/UnitPlay/UnitNavRules.cs`:

```csharp
// Pure gamepad focus for the unit pop-out: one vertical lane of option rows
// (0..optionCount-1) with the Use button as the final slot (== optionCount).
// dy follows InspectorNavRules' convention: +1 is up, -1 is down. Locked rows
// ARE focus targets (the player can read why they're locked); Use-ability is
// UnitPlaySelection's concern, not navigation's.
public static class UnitNavRules
{
    public static int UseSlot(int optionCount) => optionCount;

    public static int Open(int optionCount) => optionCount > 0 ? 0 : UseSlot(optionCount);

    public static int Move(int pos, int dy, int optionCount)
    {
        if (dy < 0) return pos < UseSlot(optionCount) ? pos + 1 : pos; // down
        if (dy > 0) return pos > 0 ? pos - 1 : pos;                   // up
        return pos;
    }
}
```

- [ ] **Step 4: GREEN — run via harness**

Recompile lib + tests, run `Runner.exe`. Expected: all Task 1 + Task 2 tests pass (`17 passed, 0 failed`).

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/UnitPlay/UnitNavRules.cs Assets/Tests/EditMode/UnitNavRulesTests.cs
git commit -m @'
feat: UnitNavRules pure pop-out navigation (TDD)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

# Phase 2 — SO rework, crystal plumbing, Player apply/revert, command

### Task 3: `UnitsSO` options + `CrystalInventory` pay/grant + `Player` unit-option methods + `UnitCommand`

**Files:**
- Modify: `Assets/Scripts/GameScriptableObjectTypes/UnitsSO.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`
- Modify: `Assets/Scripts/Managers/Commands/UnitCommand.cs`

**Interfaces:**
- Consumes: `UnitOption`, `UnitEffect` (Task 1); existing `PlayerHand.HealWound()/RestoreHealedWound()`, `Crystal.SetReserved/FlySpendThenHide/PopIn`, `ICommands`.
- Produces (Tasks 4, 7, 8 call these exactly):
  - `UnitsSO.options : List<UnitOption>`, `UnitsSO.influenceCost : int`
  - `CrystalInventory.CanPay(EmpowerType cost) → bool`
  - `CrystalInventory.SelectPayCrystal(EmpowerType cost) → Crystal`
  - `CrystalInventory.SpendUnitCrystal(Crystal c, Vector3 flyTarget)` / `RefundUnitCrystal()`
  - `CrystalInventory.UnitCrystallize(EmpowerType color)` / `UndoUnitCrystallize()`
  - `Player.AddUnit(UnitsSO so)`
  - `Player.ApplyUnitOption(Unit unit, UnitOption option)` / `Player.RevertUnitOption(Unit unit, UnitOption option)`
  - `new UnitCommand(Player, CrystalInventory, Unit, UnitOption, Crystal reservedOrNull)`

- [ ] **Step 1: Rework `UnitsSO`**

Replace the body of `Assets/Scripts/GameScriptableObjectTypes/UnitsSO.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Units", menuName = "ScriptableObjects/Units")]
public class UnitsSO : AllCards
{
    // The unit's authored options; the pop-out renders exactly these
    // (spec 2026-07-09). Using any option exhausts the unit for the round.
    public List<UnitOption> options = new();
    // Recruit price at towns (per-unit, replaces the town's flat recruitLevel).
    public int influenceCost;
    public Sprite sprite;
    public Color color;
    public char unitLetter;
}
```

(The legacy `attack/defend/explore/influence/siege/healAmount/numCrystals/cardType/empowerType` fields and `GetUnitStats()` are deleted; stale YAML keys in existing assets are ignored by Unity until the Task 10 content pass rewrites them.)

- [ ] **Step 2: Generalize crystal pay + add unit stacks in `CrystalInventory`**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs`:

Refactor `SelectEmpowerCrystal` to delegate, and add the new members below it:

```csharp
    public Crystal SelectEmpowerCrystal() => SelectPayCrystal(_card.cardSO.empowerType);

    // Generalized "find a crystal that satisfies this cost" — same preference
    // order as card empower: exact color first, wild as the fallback.
    public Crystal SelectPayCrystal(EmpowerType cost)
    {
        foreach (var crystal in crystalsInInventory)
            if (!crystal.isAll && ColorSatisfies(crystal.color, cost))
                return crystal;
        foreach (var crystal in crystalsInInventory)
            if (crystal.isAll)
                return crystal;
        return null;
    }

    public bool CanPay(EmpowerType cost)
        => cost == EmpowerType.None || SelectPayCrystal(cost) != null;

    // Unit option costs/grants get their own LIFO stacks (mirroring
    // playedCrystals / skillCreatedCrystals) so undo pops exactly what the
    // command pushed.
    public Stack<Crystal> unitSpentCrystals = new();
    public Stack<Crystal> unitCreatedCrystals = new();

    public void SpendUnitCrystal(Crystal crystal, Vector3 flyTarget)
    {
        unitSpentCrystals.Push(crystal);
        crystalsInInventory.Remove(crystal);
        crystal.SetReserved(false);
        crystal.FlySpendThenHide(flyTarget);
    }

    public void RefundUnitCrystal()
    {
        if (unitSpentCrystals.Count == 0) return;
        var crystal = unitSpentCrystals.Pop();
        crystal.gameObject.SetActive(true);
        crystalsInInventory.Add(crystal);
        crystal.PopIn();
    }

    public void UnitCrystallize(EmpowerType color)
    {
        unitCreatedCrystals.Push(CreateCrystal(color));
    }

    public void UndoUnitCrystallize()
    {
        if (unitCreatedCrystals.Count == 0) return;
        unitCreatedCrystals.Pop().RemoveCrystal();
    }
```

- [ ] **Step 3: Add unit-option apply/revert to `Player`; retire `PlayUnit`; extract `AddUnit`**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`:

Replace `RecruitUnit(TownToken)`'s body with an `AddUnit` extraction (the TownToken overload is deleted in Task 8):

```csharp
    public void AddUnit(UnitsSO so)
    {
        units.Add(so);
        var newUnit = Instantiate(unitPrefab, new Vector3(0, 0, 0), Quaternion.identity,
            GameObject.Find("Units").transform);
        newUnit.GetComponent<Unit>().unitSO = so;
    }

    public void RecruitUnit(TownToken town)
    {
        AddUnit(town.townSO.recruitableUnits[0]);
    }
```

Replace `PlayUnit(Unit unit)` entirely with:

```csharp
    // Applies ONE authored option (spec 2026-07-09). Crystal cost consumption
    // lives in UnitCommand (which owns the reserved crystal); this method only
    // applies the option's effect and the exhaust state.
    public void ApplyUnitOption(Unit unit, UnitOption option)
    {
        switch (option.effect)
        {
            case UnitEffect.Attack:    playerAttack    += option.amount; break;
            case UnitEffect.Defend:    playerDefend    += option.amount; break;
            case UnitEffect.Siege:     playerSiege     += option.amount; break;
            case UnitEffect.Explore:   playerExplore   += option.amount; GetCurrentExplore();   break;
            case UnitEffect.Influence: playerInfluence += option.amount; GetCurrentInfluence(); break;
            case UnitEffect.Heal:
            {
                var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
                for (int i = 0; i < option.amount; i++) hand.HealWound();
                break;
            }
            case UnitEffect.Crystallize:
            {
                var crystals = FindAnyObjectByType<CrystalInventory>();
                for (int i = 0; i < option.amount; i++) crystals.UnitCrystallize(option.grantColor);
                break;
            }
        }
        unit.transform.Rotate(0, 0, -90);
        unit.IsPlayed = true;
    }

    public void RevertUnitOption(Unit unit, UnitOption option)
    {
        switch (option.effect)
        {
            case UnitEffect.Attack:    playerAttack    -= option.amount; break;
            case UnitEffect.Defend:    playerDefend    -= option.amount; break;
            case UnitEffect.Siege:     playerSiege     -= option.amount; break;
            case UnitEffect.Explore:   playerExplore   -= option.amount; GetCurrentExplore();   break;
            case UnitEffect.Influence: playerInfluence -= option.amount; GetCurrentInfluence(); break;
            case UnitEffect.Heal:
            {
                var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
                for (int i = 0; i < option.amount; i++) hand.RestoreHealedWound();
                break;
            }
            case UnitEffect.Crystallize:
            {
                var crystals = FindAnyObjectByType<CrystalInventory>();
                for (int i = 0; i < option.amount; i++) crystals.UndoUnitCrystallize();
                break;
            }
        }
        unit.transform.Rotate(0, 0, 90);
        unit.IsPlayed = false;
    }
```

Note: `GameManager.playerHand` is the same reference `ApplySkillEffect` already uses — copy that exact access pattern.

- [ ] **Step 4: Rewrite `UnitCommand`**

Replace `Assets/Scripts/Managers/Commands/UnitCommand.cs` entirely:

```csharp
using UnityEngine;

// One unit-option use on the undo stack. Owns the reserved crystal for costed
// options: Execute consumes it (fly + hide), Undo refunds it. Effects go
// through Player.ApplyUnitOption / RevertUnitOption symmetrically.
public class UnitCommand : ICommands
{
    readonly Player _player;
    readonly CrystalInventory _crystals;
    readonly Unit _unit;
    readonly UnitOption _option;
    readonly Crystal _reserved; // null when the option is free

    public UnitCommand(Player player, CrystalInventory crystals, Unit unit, UnitOption option, Crystal reserved)
    {
        _player = player;
        _crystals = crystals;
        _unit = unit;
        _option = option;
        _reserved = reserved;
    }

    public void Execute()
    {
        if (_reserved != null) _crystals.SpendUnitCrystal(_reserved, _unit.transform.position);
        _player.ApplyUnitOption(_unit, _option);
    }

    public void Undo()
    {
        _player.RevertUnitOption(_unit, _option);
        if (_reserved != null) _crystals.RefundUnitCrystal();
    }
}
```

`Unit.cs` still references the old constructor — it is rewritten in Task 4; do Task 4 before asking the editor to compile.

- [ ] **Step 5: Commit (with Task 4 — they compile together)**

---

# Phase 3 — Unit pop-out UI

### Task 4: `UnitInspector` + views + `Unit` opens the pop-out + `GameManager.unitCanvas`

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitInspector.cs`
- Create: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitOptionRow.cs`
- Create: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitOptionList.cs`
- Create: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitUseBar.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs`
- Modify: `Assets/Scripts/Managers/GameManager.cs` (add `unitCanvas`)
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/HandFocusController.cs` (treat the unit pop-out as a pop-out)

**Interfaces:**
- Consumes: `UnitPlaySelection`, `UnitOptionText` (Task 1); `CrystalInventory.CanPay/SelectPayCrystal` and `UnitCommand` (Task 3); `InputContextState`.
- Produces:
  - `UnitInspector.Open(Unit)`, `.Close()`, `.SelectOption(int)`, `.Use()`, `.Selection`, `.Unit`, `event Action Changed`
  - `UnitOptionRow.Bind(...)` and `UnitOptionList.Rows` (Task 6 uses row rects for the focus outline)
  - `GameManager.unitCanvas : Canvas`

- [ ] **Step 1: Add `unitCanvas` to `GameManager`**

In `Assets/Scripts/Managers/GameManager.cs`, add next to `public Canvas cardCanvas;`:

```csharp
    public Canvas unitCanvas;
```

and in the same init block where `cardCanvas` is set active-but-disabled (lines ~51–52), add:

```csharp
        unitCanvas.gameObject.SetActive(true);
        unitCanvas.enabled = false;
```

- [ ] **Step 2: `UnitInspector`**

Create `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitInspector.cs`:

```csharp
using System;
using UnityEngine;
using DG.Tweening;

// Owns the in-progress unit use. Mirrors CardInspector: single source of truth
// the section views render from; Use routes through UnitCommand so undo and
// Player stat math stay symmetric. Affordability is computed once at Open —
// the pop-out is modal, so crystal counts can't change underneath it.
public class UnitInspector : MonoBehaviour
{
    [SerializeField] CanvasGroup boardScrim;
    [SerializeField] CanvasGroup popoutGroup;
    [SerializeField] float scrimAlpha = 0.6f;
    [SerializeField] float fadeTime = 0.2f;

    public UnitPlaySelection Selection { get; private set; }
    public Unit Unit { get; private set; }
    public event Action Changed;

    Crystal _reserved;

    public void Open(Unit unit)
    {
        Unit = unit;
        var inv = FindAnyObjectByType<CrystalInventory>();
        var options = unit.unitSO.options;
        var affordable = new bool[options.Count];
        for (int i = 0; i < options.Count; i++)
            affordable[i] = inv == null ? options[i].crystalCost == EmpowerType.None
                                        : inv.CanPay(options[i].crystalCost);

        Selection = new UnitPlaySelection(options, affordable);
        GameManager.Instance.unitCanvas.enabled = true;
        FadeIn();
        ReserveForSelected();
        InputContextState.Current = InputContext.Inspector;
        Raise();
    }

    public void Close()
    {
        ReleaseReservation();
        GameManager.Instance.unitCanvas.enabled = false;
        SnapClosed();
        Unit = null;
        Selection = null;
        InputContextState.Current = InputContext.Board;
    }

    public void SelectOption(int index)
    {
        if (Selection == null) return;
        ReleaseReservation();
        Selection.Select(index);
        ReserveForSelected();
        Raise();
    }

    public void Use()
    {
        if (Selection == null || !Selection.CanUse) return;
        var option = Selection.Selected;
        var inv = FindAnyObjectByType<CrystalInventory>();
        GameManager.Instance.commands.AddCommand(
            new UnitCommand(FindAnyObjectByType<Player>(), inv, Unit, option, _reserved));
        _reserved = null; // ownership passed to the command's consume/undo path
        Close();
    }

    // Reserve (dim) the crystal the selected option would spend, exactly like
    // CardInspector's empower reservation.
    void ReserveForSelected()
    {
        var option = Selection?.Selected;
        if (option == null || option.crystalCost == EmpowerType.None || !Selection.CanUse) return;
        var inv = FindAnyObjectByType<CrystalInventory>();
        _reserved = inv != null ? inv.SelectPayCrystal(option.crystalCost) : null;
        _reserved?.SetReserved(true);
    }

    void ReleaseReservation()
    {
        if (_reserved != null) { _reserved.SetReserved(false); _reserved = null; }
    }

    void FadeIn()
    {
        if (boardScrim != null)
        {
            boardScrim.DOKill();
            boardScrim.alpha = 0f;
            boardScrim.DOFade(scrimAlpha, fadeTime);
        }
        if (popoutGroup != null)
        {
            popoutGroup.DOKill();
            popoutGroup.alpha = 0f;
            popoutGroup.DOFade(1f, fadeTime);
        }
    }

    void SnapClosed()
    {
        if (boardScrim != null) { boardScrim.DOKill(); boardScrim.alpha = 0f; }
        if (popoutGroup != null) { popoutGroup.DOKill(); popoutGroup.alpha = 0f; }
    }

    void Raise() => Changed?.Invoke();
}
```

- [ ] **Step 3: Row + list + use bar views**

Create `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitOptionRow.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One option row: label, selected outline, locked dim. A locked row remains
// clickable/focusable (so its cost reads), but the Use bar refuses it.
public class UnitOptionRow : MonoBehaviour
{
    [SerializeField] public Button button;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Image selectedOutline;
    [SerializeField] CanvasGroup group;

    public void Bind(string text, bool selected, bool affordable)
    {
        label.text = text;
        if (selectedOutline != null) selectedOutline.enabled = selected;
        if (group != null) group.alpha = affordable ? 1f : 0.4f;
    }
}
```

Create `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitOptionList.cs`:

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Builds one UnitOptionRow per authored option when the pop-out opens and
// re-binds them on every inspector change. Rows are rebuilt per Open because
// option counts differ per unit.
public class UnitOptionList : MonoBehaviour
{
    [SerializeField] UnitInspector inspector;
    [SerializeField] Transform rowContainer;
    [SerializeField] GameObject rowPrefab;
    [SerializeField] TextMeshProUGUI unitName;

    readonly List<UnitOptionRow> rows = new();
    public IReadOnlyList<UnitOptionRow> Rows => rows;

    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Render()
    {
        var sel = inspector.Selection;
        if (sel == null) return;

        if (unitName != null && inspector.Unit != null)
            unitName.text = inspector.Unit.unitSO.cardName;

        while (rows.Count < sel.Count)
        {
            var row = Instantiate(rowPrefab, rowContainer).GetComponent<UnitOptionRow>();
            int captured = rows.Count;
            row.button.onClick.AddListener(() => inspector.SelectOption(captured));
            rows.Add(row);
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i < sel.Count;
            rows[i].gameObject.SetActive(active);
            if (active) rows[i].Bind(sel.Describe(i), sel.SelectedIndex == i, sel.IsAffordable(i));
        }
    }
}
```

Create `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitUseBar.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The Use button (label = live preview of the selected option) plus Back.
public class UnitUseBar : MonoBehaviour
{
    [SerializeField] UnitInspector inspector;
    [SerializeField] public Button useButton;
    [SerializeField] TextMeshProUGUI useLabel;
    [SerializeField] Button backButton;

    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        useButton.onClick.AddListener(() => inspector.Use());
        backButton.onClick.AddListener(() => inspector.Close());
    }

    void Render()
    {
        var sel = inspector.Selection;
        if (sel == null) return;
        useButton.interactable = sel.CanUse;
        useLabel.text = sel.CanUse
            ? $"USE · {sel.Describe(sel.SelectedIndex)}"
            : (sel.SelectedIndex >= 0 ? "Needs a crystal" : "No options");
    }
}
```

- [ ] **Step 4: `Unit` opens the pop-out**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs`, delete the `unitCommand` field and the `UnitEvent onClick_PerformUnitAction` field, and replace `OnPointerClick`:

```csharp
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isPlayed)
        {
            GameManager.Instance.ValidationMessage($"{unitSO.cardName} has already been played, undo to revert action.");
            return;
        }
        FindAnyObjectByType<UnitInspector>().Open(this);
    }
```

(`Start`, hover scale, `IsPlayed` stay unchanged.)

- [ ] **Step 5: Pause hand focus while the unit pop-out is open**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/HandFocusController.cs`, change the pop-out guard (line ~40) from:

```csharp
        if (gm.cardCanvas.enabled)
```

to:

```csharp
        if (gm.cardCanvas.enabled || gm.unitCanvas.enabled)
```

- [ ] **Step 6: USER ACTION — editor compile + `.meta` import**

Ask the user to focus the Unity editor and confirm the console is clean (compile errors would come from a missed rename). Then:

```powershell
git add -A
git commit -m @'
feat: unit pop-out (UnitInspector + views), option-based UnitsSO/UnitCommand

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 5: USER ACTION — build & wire the unit pop-out canvas, first playtest

**Files:** none (editor work; instructions only).

- [ ] **Step 1: Give the user these editor steps**

1. In `GameBoard.unity`, duplicate the CardMenuCanvas root's structure as a starting point OR create a new Canvas named **UnitMenuCanvas** (Screen Space - Camera, same camera & sorting as CardMenuCanvas). Add to it:
   - A full-screen **BoardScrim** Image (black, raycast target ON) with a CanvasGroup.
   - A centred **Popout** panel with a CanvasGroup containing: a TMP **UnitName** header, a vertical-layout **Options** container, and a bottom bar with **Use** and **Back** buttons (TMP labels).
   - A **UnitOptionRow prefab**: Button + TMP label + an outline Image (`selectedOutline`) + CanvasGroup; save under `Assets/Prefabs/`.
2. Add `UnitInspector`, `UnitOptionList`, `UnitUseBar` components to the canvas; wire every serialized field (scrim/popout groups, row container, row prefab, unit name, use/back buttons, inspector references).
3. On the scrim's Button (or an added Button component): OnClick → `UnitInspector.Close`.
4. Select the **GameManager** object and drag UnitMenuCanvas into the new `unitCanvas` field.
5. On the **Unit prefab** (`Assets/Prefabs/…` — the one `Player.unitPrefab` references): remove the now-unused `OnClick_PerformUnitAction` event wiring if the inspector shows a missing/none field. Nothing else changes.
6. The old `UnitListener`/`UnitEvent` scene wiring that pointed at `Player.PlayUnit` now shows a missing-method warning — remove that listener component/entry.

- [ ] **Step 2: USER ACTION — playtest checklist (report results back)**

- Click a unit → pop-out opens, board dims; Back / scrim click closes free.
- Legacy asset warning: until Task 10, old unit assets have NO options — pop-out should show "No options" with Use disabled (acceptable interim state).
- Temporarily give one unit asset an option in the inspector (e.g. Attack 2) → Use applies +2 Attack, unit turns sideways; Undo stands it back up and removes the stats; round end refreshes it.
- EditMode Test Runner: run all — expect green (UnitPlaySelection/UnitNavRules suites included).

- [ ] **Step 3: Commit scene/prefab changes** (user saves scene first): `git add -A; git commit -m "feat: unit pop-out scene wiring"` (+ co-author line).

---

# Phase 4 — Controller support

### Task 6: `UnitInspectorNavController` + `UnitsLane` (hand ↔ units)

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitInspectorNavController.cs`
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/UnitsLane.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/HandFocusController.cs`

**Interfaces:**
- Consumes: `UnitNavRules` (Task 2), `UnitInspector`/`UnitOptionList`/`UnitUseBar` (Task 4), `GameControls.Gameplay` actions (Navigate/Submit/Cancel), `InputContextState`.
- Produces: `UnitsLane.HasUnits : bool`, `UnitsLane.Enter()`, `UnitsLane.Exit()`; `HandFocusController.EnterFromUnits()`.

- [ ] **Step 1: Pop-out nav controller**

Create `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitInspectorNavController.cs` (mirrors `InspectorNavController`'s open/message-modal/latch patterns, one vertical lane):

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

// Gamepad/keyboard navigation for the unit pop-out: one vertical lane of
// option rows ending at Use (UnitNavRules). Moving focus onto a row selects it
// (focus == selection, per spec); Submit on Use fires it; Cancel closes.
public class UnitInspectorNavController : MonoBehaviour
{
    [SerializeField] UnitInspector inspector;
    [SerializeField] UnitOptionList optionList;
    [SerializeField] UnitUseBar useBar;
    [SerializeField] RectTransform focusOutline; // Image, raycastTarget off

    int _pos;
    bool _latched;
    bool _wasOpen;
    bool _messageWasUp;

    void Update()
    {
        bool open = GameManager.Instance != null && GameManager.Instance.unitCanvas.enabled
                    && inspector.Selection != null;
        if (!open)
        {
            _wasOpen = false;
            if (focusOutline != null) focusOutline.gameObject.SetActive(false);
            return;
        }

        if (GameManager.Instance.messageCanvas.enabled) { _messageWasUp = true; return; }
        if (_messageWasUp) { _messageWasUp = false; return; }

        if (!_wasOpen)
        {
            // First open frame: focus the selected row (or Use) and swallow the
            // Submit that opened the pop-out.
            _wasOpen = true;
            _pos = inspector.Selection.SelectedIndex >= 0
                ? inspector.Selection.SelectedIndex
                : UnitNavRules.UseSlot(inspector.Selection.Count);
            RenderOutline();
            return;
        }

        HandleNavigate();

        if (GameControls.Gameplay.Cancel.WasPressedThisFrame())
        {
            inspector.Close();
            return;
        }

        if (GameControls.Gameplay.Submit.WasPressedThisFrame())
        {
            if (_pos == UnitNavRules.UseSlot(inspector.Selection.Count)) inspector.Use();
            // Submit on a row is a no-op: focusing it already selected it.
            return;
        }

        RenderOutline();
    }

    void HandleNavigate()
    {
        Vector2 nav = GameControls.Gameplay.Navigate.ReadValue<Vector2>();
        if (Mathf.Abs(nav.y) < 0.5f) { _latched = false; return; }
        if (_latched) return;
        _latched = true;

        int next = UnitNavRules.Move(_pos, nav.y > 0 ? +1 : -1, inspector.Selection.Count);
        if (next == _pos) return;
        _pos = next;
        if (_pos < inspector.Selection.Count) inspector.SelectOption(_pos);
        RenderOutline();
    }

    void RenderOutline()
    {
        if (focusOutline == null) return;
        RectTransform target = _pos == UnitNavRules.UseSlot(inspector.Selection.Count)
            ? (RectTransform)useBar.useButton.transform
            : (RectTransform)optionList.Rows[_pos].transform;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            focusOutline.gameObject.SetActive(false);
            return;
        }
        focusOutline.SetParent(target, false);
        focusOutline.anchorMin = Vector2.zero;
        focusOutline.anchorMax = Vector2.one;
        focusOutline.offsetMin = new Vector2(-4f, -4f);
        focusOutline.offsetMax = new Vector2(4f, 4f);
        focusOutline.SetAsLastSibling();
        focusOutline.gameObject.SetActive(true);
    }
}
```

- [ ] **Step 2: `UnitsLane`**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/UnitsLane.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Gamepad focus lane over the unit tokens (the "Units" container). Entered
// from the hand fan (up past the hand); left/right cycles, down returns to
// the hand, Submit opens the unit pop-out, Cancel drops to Board. Focus
// visual reuses the tokens' hover scale.
public class UnitsLane : MonoBehaviour
{
    [SerializeField] HandFocusController hand;
    [SerializeField] UnitInspector inspector;

    int _index;
    bool _active;
    bool _latched;

    public bool HasUnits => CurrentUnits().Count > 0;
    public bool IsActive => _active;

    List<Unit> CurrentUnits()
    {
        var list = new List<Unit>(FindObjectsByType<Unit>());
        list.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        return list;
    }

    public void Enter()
    {
        var units = CurrentUnits();
        if (units.Count == 0) return;
        _active = true;
        _index = Mathf.Clamp(_index, 0, units.Count - 1);
        SetFocusVisual(units, _index);
        InputContextState.Current = InputContext.Fan;
    }

    public void Exit()
    {
        ClearFocusVisual();
        _active = false;
    }

    void Update()
    {
        if (!_active) return;
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.messageCanvas.enabled || gm.cardCanvas.enabled || gm.unitCanvas.enabled) return;

        var units = CurrentUnits();
        if (units.Count == 0) { ExitToHand(); return; }
        _index = Mathf.Clamp(_index, 0, units.Count - 1);

        Vector2 nav = GameControls.Gameplay.Navigate.ReadValue<Vector2>();
        if (nav.magnitude < 0.5f) _latched = false;
        else if (!_latched)
        {
            _latched = true;
            if (Mathf.Abs(nav.x) >= Mathf.Abs(nav.y))
            {
                int next = Mathf.Clamp(_index + (nav.x > 0 ? 1 : -1), 0, units.Count - 1);
                if (next != _index) { ClearFocusVisual(); _index = next; SetFocusVisual(units, _index); }
            }
            else if (nav.y < 0) { ExitToHand(); return; }
        }

        if (GameControls.Gameplay.Cancel.WasPressedThisFrame())
        {
            Exit();
            InputContextState.Current = InputContext.Board;
            return;
        }
        if (GameControls.Gameplay.Submit.WasPressedThisFrame())
        {
            var unit = units[_index];
            if (unit.IsPlayed)
                GameManager.Instance.ValidationMessage($"{unit.unitSO.cardName} has already been played, undo to revert action.");
            else
                inspector.Open(unit);
        }
    }

    void ExitToHand()
    {
        Exit();
        hand.EnterFromUnits();
    }

    void SetFocusVisual(List<Unit> units, int index)
    {
        units[index].transform.localScale = new Vector3(2, 2, 2);
    }

    void ClearFocusVisual()
    {
        foreach (var u in FindObjectsByType<Unit>())
            u.transform.localScale = Vector3.one;
    }
}
```

- [ ] **Step 3: Hand ↔ units crossing in `HandFocusController`**

Add a serialized lane reference and a public re-entry point, and extend `HandleNavigate` to cross up into the lane. In `HandFocusController`:

Add field:

```csharp
    [SerializeField] UnitsLane unitsLane;
```

Add after `RestorePadFocus()`:

```csharp
    // UnitsLane hands focus back when the player navigates down out of the lane.
    public void EnterFromUnits()
    {
        _owner = FocusOwner.Pad;
        RestorePadFocus();
    }
```

In `Update()`, add a lane guard right after the pop-out guards (the lane owns input while active):

```csharp
        if (unitsLane != null && unitsLane.IsActive) return;
```

In `HandleNavigate()`, change the latch block to also read the y axis and cross into the lane:

```csharp
        Vector2 nav = GameControls.Gameplay.Navigate.ReadValue<Vector2>();
        if (nav.magnitude < 0.5f) { _navLatched = false; return; }
        if (_navLatched) return;
        _navLatched = true;

        // Up from the fan crosses into the units lane (when any units exist).
        if (Mathf.Abs(nav.y) > Mathf.Abs(nav.x))
        {
            if (nav.y > 0 && _owner == FocusOwner.Pad && unitsLane != null && unitsLane.HasUnits)
            {
                layout.ClearFocus();
                unitsLane.Enter();
            }
            return;
        }
```

(keep the rest of the method — the wounds array, `HandNavRules.Step`, focus set — operating on `nav.x` exactly as today).

- [ ] **Step 4: USER ACTION — wiring + pad playtest**

1. Add `UnitInspectorNavController` to UnitMenuCanvas; wire inspector/optionList/useBar and add a focus-outline Image (copy the card pop-out's outline object).
2. Add `UnitsLane` to the **Units** container object; wire `hand` (HandFocusController) and `inspector`.
3. On the HandFocusController component, wire the new `unitsLane` field.
4. Playtest with pad: d-pad up from hand → unit focused; left/right cycles; A opens pop-out; d-pad drives rows (locked row focusable, Use disabled); A on Use plays; B closes; down returns to hand.

- [ ] **Step 5: Commit** (`feat: controller support for units lane and unit pop-out` + co-author line).

---

# Phase 5 — Recruiting flows

### Task 7: Passive skill kind + Charismatic gate

**Files:**
- Modify: `Assets/Scripts/Enums/Enums/SkillCadence.cs`
- Modify: `Assets/Scripts/Enums/Enums/SkillEffect.cs`
- Modify: `Assets/Scripts/GameObjectScripts/Leveling/SkillToken.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`

**Interfaces:**
- Produces: `SkillCadence.Passive`, `SkillEffect.RecruitEnemies`, `Player.HasCharismatic : bool`.

- [ ] **Step 1: Append enum members** (append-only — serialized ints):

`SkillCadence.cs`: add `Passive,` after `PerRound,`.
`SkillEffect.cs`: add `RecruitEnemies,` after `HealWound,`.

- [ ] **Step 2: Passives are not clickable**

In `SkillToken.OnPointerClick`, insert at the top:

```csharp
        if (skillSO.cadence == SkillCadence.Passive)
        {
            GameManager.Instance.ValidationMessage($"{skillSO.cardName} is always active.");
            return;
        }
```

- [ ] **Step 3: Gate + no-op effect on Player**

In `Player.cs` add near `Skills`:

```csharp
    // Charismatic passive: influenced enemies with a recruitedUnit join the army.
    public bool HasCharismatic => skills.Exists(s => s.effect == SkillEffect.RecruitEnemies);
```

In `ApplySkillEffect`'s switch add an explicit no-op arm:

```csharp
            case SkillEffect.RecruitEnemies: break; // passive — no activatable effect
```

(`RefreshSkills` needs no change: passives are never `SetUsed(true)`.)

- [ ] **Step 4: Commit** (`feat: passive skill kind + Charismatic gate` + co-author line).

---

### Task 8: Enemy influence — pay-to-leave / recruit

**Files:**
- Modify: `Assets/Scripts/GameScriptableObjectTypes/EnemiesSO.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/DisbandPanel.cs`

**Interfaces:**
- Consumes: `Player.AddUnit`, `Player.HasCharismatic`, `ArmyRules.NeedsDisband(int, int)`, existing `Player.Influence(int)` + `OnEnemyDefeat_GetRewards`.
- Produces: `EnemiesSO.recruitedUnit : UnitsSO`, `Player.InfluenceEnemy(EnemyCard)`, `DisbandPanel.OpenForHire(Action onDisbanded)`.

- [ ] **Step 1: `recruitedUnit` on `EnemiesSO`**

Add below `influenceCost`:

```csharp
    // Optional unit form: when set AND the player owns Charismatic, paying the
    // influence cost recruits this unit instead of just paying the enemy off.
    // Null = pay-to-leave only (spec 2026-07-09).
    public UnitsSO recruitedUnit;
```

- [ ] **Step 2: Generalize `DisbandPanel`**

Replace `Open`/`DisbandAndHire` in `DisbandPanel.cs` with:

```csharp
    System.Action _onDisbanded;

    public void Open(TownToken town)
    {
        // Town path: the same two events RecruitButton fires complete the hire.
        OpenForHire(() =>
        {
            townEvent.Raise(town);
            influenceCostEvent.Raise(town.townSO.recruitLevel);
        });
    }

    // Generic "make room, then continue": combat recruiting and the town panel
    // both pass their own continuation. Cancel never runs it.
    public void OpenForHire(System.Action onDisbanded)
    {
        _onDisbanded = onDisbanded;
        ClearEntries();
        panel.SetActive(true);

        foreach (var unit in FindObjectsByType<Unit>())
        {
            var go = Instantiate(entryButtonPrefab, entryContainer);
            go.GetComponentInChildren<TextMeshProUGUI>().text = unit.unitSO.cardName;
            var captured = unit;
            go.GetComponent<Button>().onClick.AddListener(() => DisbandAndContinue(captured));
            spawned.Add(go);
        }
    }

    void DisbandAndContinue(Unit unit)
    {
        var player = FindAnyObjectByType<Player>();
        player.DisbandUnit(unit);
        _onDisbanded?.Invoke();
        Close();
    }
```

and in `Close()` add `_onDisbanded = null;` before `panel.SetActive(false);`. Remove the now-unused `_town` field (Open no longer stores it).

- [ ] **Step 3: `Player.InfluenceEnemy`**

Add to `Player.cs` (below `ResolveAttack`):

```csharp
    // Influence resolution (spec 2026-07-09): pay the cost to end the fight
    // wound-free WITH defeat rewards; with Charismatic and a recruitedUnit the
    // same payment also adds the unit (rewards + unit). At the army cap the
    // disband picker runs first; cancelling it spends nothing.
    public void InfluenceEnemy(EnemyCard enemy)
    {
        if (!enemy.enemySO.canInfluence) return;
        int cost = enemy.enemySO.influenceCost;
        if (playerInfluence < cost)
        {
            GameManager.Instance.ValidationMessage($"You need {cost} Influence to sway {enemy.enemySO.cardName}.");
            return;
        }

        bool recruit = enemy.enemySO.recruitedUnit != null && HasCharismatic;
        if (recruit && ArmyRules.NeedsDisband(units.Count, ArmyCap))
        {
            FindAnyObjectByType<DisbandPanel>().OpenForHire(() => CompleteInfluence(enemy, true));
            return;
        }
        CompleteInfluence(enemy, recruit);
    }

    void CompleteInfluence(EnemyCard enemy, bool recruit)
    {
        if (recruit) AddUnit(enemy.enemySO.recruitedUnit);
        GameManager.Instance.ValidationMessage(recruit
            ? $"{enemy.enemySO.cardName} joins your army!"
            : $"{enemy.enemySO.cardName} departs peacefully.");
        Influence(enemy.enemySO.influenceCost); // spend + clear undo stack (standard for influence spends)
        OnEnemyDefeat_GetRewards.Raise(enemy);  // rewards + the defeat/cleanup chain; no counterattack ran = wound-free
    }
```

- [ ] **Step 4: Wire the influence button on `EnemyCard`**

In `EnemyCard.Start()`, replace the `canInfluence` block with:

```csharp
        var player = FindAnyObjectByType<Player>();
        if (enemySO.canInfluence)
        {
            bool recruit = enemySO.recruitedUnit != null && player != null && player.HasCharismatic;
            enemyInfluence.gameObject.SetActive(true);
            enemyInfluence.text = "<sprite=\"gem\" index=0> \n" + enemySO.influenceCost.ToString();
            influenceButtonText.text = (recruit ? "Recruit " : "Pay ")
                + "<sprite=\"gem\" index=0>" + enemySO.influenceCost.ToString();
            influenceButton.interactable = true;
            influenceButton.onClick.AddListener(() => player.InfluenceEnemy(this));
        }
        else
        {
            influenceButtonText.text = "Impossible";
            influenceButton.interactable = false;
        }
```

and in `EnableCombat(EnemyToken token)` add:

```csharp
        influenceButton.interactable = token.isAggro && enemySO.canInfluence;
```

- [ ] **Step 5: USER ACTION — disband panel above combat + playtest**

1. The DisbandPanel must render above the combat canvas when opened mid-fight: move its panel to a canvas whose sorting order is above CombatCanvas (or add a Canvas + GraphicRaycaster with override sorting on the panel root). Confirm the town flow still shows it.
2. Playtest: pay off a `canInfluence` enemy (needs a temp asset flagged in the inspector) → influence deducted, no wounds, reward granted, card resolves; with Charismatic (temp-add via `Player.AddSkill` in play mode or wait for Task 10's asset) and a `recruitedUnit` set → unit appears; at cap → disband picker; cancel → nothing spent.

- [ ] **Step 6: Commit** (`feat: enemy influence pay-off and Charismatic recruiting` + co-author line).

---

### Task 9: Town recruit panel + per-unit prices

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/TownMenuScripts/RecruitPanel.cs`
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/RecruitButton.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` (delete `RecruitUnit(TownToken)`)
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/DisbandPanel.cs` (delete `Open(TownToken)`)

**Interfaces:**
- Consumes: `Player.AddUnit`, `ArmyRules.NeedsDisband`, `DisbandPanel.OpenForHire`, `UnitOptionText.Describe`, the existing `influenceCostEvent` IntEvent asset (wired scene-side to `Player.Influence`).
- Produces: `RecruitPanel.Open(TownToken)`.

- [ ] **Step 1: `RecruitPanel`**

Create `Assets/Scripts/GameObjectScripts/TownMenuScripts/RecruitPanel.cs`:

```csharp
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Town hiring: pick WHICH unit at ITS OWN influence price (spec 2026-07-09;
// replaces the silent recruitableUnits[0] + flat recruitLevel flow). Mirrors
// DisbandPanel's build/clear pattern; unaffordable entries stay visible but
// disabled. Standard Buttons with default navigation, so the later towns
// controller pass needs no rework.
public class RecruitPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;             // root, inactive by default
    [SerializeField] Transform entryContainer;
    [SerializeField] GameObject entryButtonPrefab; // Button + TMP label
    [SerializeField] Button cancelButton;
    [SerializeField] DisbandPanel disbandPanel;
    [SerializeField] IntEvent influenceCostEvent;  // same asset as the old flow (→ Player.Influence)

    readonly List<GameObject> spawned = new();

    void Start()
    {
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(Close);
        panel.SetActive(false);
    }

    public void Open(TownToken town)
    {
        ClearEntries();
        panel.SetActive(true);
        var player = FindAnyObjectByType<Player>();

        foreach (var unit in town.townSO.recruitableUnits)
        {
            if (unit == null) continue;
            var go = Instantiate(entryButtonPrefab, entryContainer);
            string summary = string.Join(" / ", unit.options.Select(UnitOptionText.Describe));
            go.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{unit.cardName} — <sprite=\"gem\" index=0>{unit.influenceCost}\n<size=70%>{summary}</size>";
            go.GetComponent<Button>().interactable = player.playerInfluence >= unit.influenceCost;
            var captured = unit;
            go.GetComponent<Button>().onClick.AddListener(() => Pick(captured));
            spawned.Add(go);
        }
    }

    void Pick(UnitsSO unit)
    {
        var player = FindAnyObjectByType<Player>();
        if (ArmyRules.NeedsDisband(player.Units.Count, player.ArmyCap))
        {
            disbandPanel.OpenForHire(() => Hire(unit));
            Close();
            return;
        }
        Hire(unit);
        Close();
    }

    void Hire(UnitsSO unit)
    {
        FindAnyObjectByType<Player>().AddUnit(unit);
        influenceCostEvent.Raise(unit.influenceCost);
    }

    void Close()
    {
        ClearEntries();
        panel.SetActive(false);
    }

    void ClearEntries()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}
```

- [ ] **Step 2: Rework `RecruitButton`**

Replace the class body of `RecruitButton.cs`:

```csharp
public class RecruitButton : TownButtons
{
    [SerializeField] RecruitPanel recruitPanel;

    // Enabled when at least one listed unit is affordable (per-unit pricing —
    // the town's recruitLevel is retired as the price).
    bool AnyAffordable()
        => _town.townSO.recruitableUnits.Exists(u => u != null && u.influenceCost <= currentPlayerInfluence);

    private void Update()
    {
        if (_town is not null)
            thisButton.interactable = AnyAffordable();
    }

    public override void UpdateButtonText()
    {
        if (_town is not null)
        {
            buttonText.text = "Recruit";
            bool allowed = PlaceRules.AllowedServices(_town.townSO.placeType).HasFlag(PlaceService.Recruit);
            bool open = ConquestTracker.Instance.IsConquered(_town.gridPos);
            if (allowed && open)
            {
                thisButton.gameObject.SetActive(true);
                thisButton.interactable = AnyAffordable();
                thisButton.onClick.RemoveAllListeners();
                thisButton.onClick.AddListener(() => recruitPanel.Open(_town));
            }
            else
            {
                thisButton.gameObject.SetActive(false);
            }
        }
    }
}
```

- [ ] **Step 3: Delete the superseded paths**

- `Player.cs`: delete `RecruitUnit(TownToken town)` (keep `AddUnit`).
- `DisbandPanel.cs`: delete `Open(TownToken town)` and the `townEvent` field (now unused there).

- [ ] **Step 4: USER ACTION — build panel + rewire + playtest**

1. Build a **RecruitPanel** under the town canvas (copy DisbandPanel's panel structure: vertical layout + entry prefab + Cancel). Add the `RecruitPanel` component; wire panel/container/prefab/cancel/disbandPanel/influenceCostEvent (drag the SAME IntEvent asset RecruitButton used before — it stays wired to `Player.Influence`).
2. On the RecruitButton object: wire the new `recruitPanel` field. Remove the old `disbandPanel` reference if the inspector shows it stale.
3. Remove the scene `TownListener` entry that called `Player.RecruitUnit` (missing-method warning otherwise).
4. Playtest: recruit panel lists units with prices + option summaries; unaffordable disabled; hire deducts the unit's own cost; at cap the disband picker chains; cancel free.

- [ ] **Step 5: Commit** (`feat: town recruit panel with per-unit influence prices` + co-author line).

---

# Phase 6 — Saves

### Task 10: Schema v5 — `unitExhausted`

**Files:**
- Modify: `Assets/Scripts/SaveData/SaveModels.cs`
- Modify: `Assets/Scripts/SaveData/SaveMigrator.cs`
- Modify: `Assets/Scripts/Managers/DataManager.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` (`RebuildUnits` signature)
- Test: `Assets/Scripts/SaveData/Tests/SaveMigratorV5Tests.cs` (create)

**Interfaces:**
- Produces: `RunState.unitExhausted : bool[]` (parallel to `unitIds`), `SaveFile.schemaVersion == 5`, `Player.RebuildUnits(List<UnitsSO>, bool[] exhausted = null)`.

- [ ] **Step 1: Write the failing migrator tests**

Create `Assets/Scripts/SaveData/Tests/SaveMigratorV5Tests.cs` (mirror the V4 test file's conventions exactly — same namespace/usings as `SaveMigratorV4Tests.cs`):

```csharp
using System;
using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV5Tests
{
    static SaveFile V4File()
    {
        var f = new SaveFile { schemaVersion = 4 };
        f.run.unitIds = new[] { "unit_knight", "unit_scout" };
        f.run.unitExhausted = null; // absent in v4 JSON
        return f;
    }

    [Test]
    public void V4_File_Gets_Empty_UnitExhausted()
    {
        var m = SaveMigrator.Migrate(V4File());
        Assert.IsNotNull(m.run.unitExhausted);
        Assert.AreEqual(0, m.run.unitExhausted.Length);
    }

    [Test]
    public void V4_File_Version_Bumps_To_5()
    {
        Assert.AreEqual(5, SaveMigrator.Migrate(V4File()).schemaVersion);
    }

    [Test]
    public void V5_File_Is_Untouched()
    {
        var f = new SaveFile { schemaVersion = 5 };
        f.run.unitExhausted = new[] { true, false };
        var m = SaveMigrator.Migrate(f);
        Assert.AreEqual(5, m.schemaVersion);
        CollectionAssert.AreEqual(new[] { true, false }, m.run.unitExhausted);
    }
}
```

- [ ] **Step 2: RED — mcs-compile SaveData + tests, run harness**

```powershell
& $mcs -nologo -target:library "-out:$s\SaveData.dll" Assets\Scripts\SaveData\*.cs
```

Expected: FAILS — `unitExhausted` not defined.

- [ ] **Step 3: Model + migrator**

`SaveModels.cs`: change the version comment/field to:

```csharp
        // v5: adds RunState.unitExhausted (parallel to unitIds; mid-round saves
        // keep used units turned).
        public int schemaVersion = 5;
```

and in `RunState`, below `unitIds`:

```csharp
        // Parallel to unitIds: true = the unit was already used this round.
        public bool[] unitExhausted = Array.Empty<bool>();
```

`SaveMigrator.cs`: add before the final version bump:

```csharp
            // v4 -> v5: unitExhausted did not exist; absent means all units fresh.
            if (file.run.unitExhausted == null)
                file.run.unitExhausted = Array.Empty<bool>();
```

and change the bump to `if (file.schemaVersion < 5) file.schemaVersion = 5;`.

- [ ] **Step 4: GREEN — recompile + run harness.** Expected: V5 tests pass (plus existing migrator suites still green in the editor Test Runner later).

- [ ] **Step 5: Capture/restore**

`Player.cs` — extend `RebuildUnits`:

```csharp
    public void RebuildUnits(List<UnitsSO> unitSOs, bool[] exhausted = null)
    {
        foreach (var existing in FindObjectsByType<Unit>())
            Destroy(existing.gameObject);
        units.Clear();

        var unitsParent = GameObject.Find("Units");
        for (int i = 0; i < unitSOs.Count; i++)
        {
            var so = unitSOs[i];
            if (so == null) continue;
            units.Add(so);
            var newUnit = Instantiate(unitPrefab, new Vector3(0, 0, 0), Quaternion.identity,
                unitsParent?.transform);
            var unit = newUnit.GetComponent<Unit>();
            unit.unitSO = so;
            if (exhausted != null && i < exhausted.Length && exhausted[i])
            {
                unit.transform.Rotate(0, 0, -90);
                unit.IsPlayed = true;
            }
        }
    }
```

`DataManager.cs` — in `CaptureRunState`, replace `run.unitIds = UnitIds(player);` with a single-source capture (and delete/inline the old `UnitIds` helper if nothing else uses it):

```csharp
        var unitObjs = FindObjectsByType<Unit>();
        run.unitIds       = unitObjs.Select(u => u.unitSO.id).ToArray();
        run.unitExhausted = unitObjs.Select(u => u.IsPlayed).ToArray();
```

and in the restore path change `player.RebuildUnits(Units.Resolve(run.unitIds));` to:

```csharp
        player.RebuildUnits(Units.Resolve(run.unitIds), run.unitExhausted);
```

(Caution: `Units.Resolve` drops nothing — it returns one entry per id, null for unknown ids — so the parallel indexing holds. Verify that assumption in `ContentRegistry.Resolve` before committing; if it can skip entries, capture/restore must pair id+flag in one struct instead.)

- [ ] **Step 6: USER ACTION — editor Test Runner all-green; save/load playtest** (use a unit mid-round, save via quit, reload → unit still sideways; round end refreshes it).

- [ ] **Step 7: Commit** (`feat: save schema v5 — persist exhausted units` + co-author line).

---

# Phase 7 — Content pass + docs

### Task 11: Content — reworked units, recruitable enemies, Charismatic, towns

**Files:**
- Modify: `Assets/Scripts/ScriptableObjectData/Player/Rewards/Units/{Warrior,Knight,Scout,Merchant}.asset` (direct field edits)
- New assets (USER ACTION, editor-created): ~5 unit forms, ~4 new enemies, `Charismatic.asset` skill
- Modify (editor): existing town assets' `recruitableUnits`; `LevelRewardsSO` skill pool; recruitable flags on 1–2 existing enemies

- [ ] **Step 1: Rework the 4 existing unit assets (direct YAML field edits)**

For each asset, delete the legacy stat lines (`attack:` … `empowerType:` except keep `sprite/color/unitLetter`) and add `options`/`influenceCost`. Serialized enum ints: `UnitEffect` Attack=0 Defend=1 Explore=2 Influence=3 Siege=4 Heal=5 Crystallize=6; `EmpowerType` None=0 Red=1 Yellow=2 Green=4 Purple=8, any-color=15.

| Asset | options | influenceCost | cardDescription |
|---|---|---|---|
| Warrior | Attack 2 (free); Siege 2 — 1 Red | 3 | `Attack 2 / Siege 2 (1R)` |
| Knight | Defend 3 (free); Defend 6 — 1 Red | 4 | `Defend 3 / Defend 6 (1R)` |
| Scout | Explore 2 (free); Explore 4 — 1 Green | 2 | `Explore 2 / Explore 4 (1G)` |
| Merchant | Influence 2 (free); Crystallize 1 Yellow (free) | 3 | `Influence 2 / Crystallize Yellow` |

Example — Knight's new block:

```yaml
  options:
  - effect: 1
    amount: 3
    grantColor: 0
    crystalCost: 0
  - effect: 1
    amount: 6
    grantColor: 0
    crystalCost: 1
  influenceCost: 4
```

- [ ] **Step 2: USER ACTION — create the new assets in the editor**

Give the user exact values; they create via the asset menus (`ScriptableObjects/Units`, `ScriptableObjects/Cards/EnemyCards`, `ScriptableObjects/Skill`).

**Charismatic skill** (`Assets/Scripts/ScriptableObjectData/Player/Skills/Charismatic.asset`): id `skill_charismatic`, cardName `Charismatic`, description `You can recruit influenced enemies into your army.`, effect `RecruitEnemies`, magnitude 0, cadence `Passive`. Then add it to the LevelRewardsSO skill pool list (pool grows to 10).

**Unit forms** (menu `ScriptableObjects/Units`; ids `unit_<name>`; per-tier costs from balance):

| Unit | options | influenceCost |
|---|---|---|
| Footsoldier | Attack 2 (free); Defend 2 (free) | 3 |
| Mercenaries | Attack 3 (free); Attack 5 — 1 Red | 4 |
| Mystic | Heal 1 (free); Crystallize 1 Purple — 1 crystal any color (cost 15) | 5 |
| Gryphon Rider | Explore 3 (free); Siege 3 — 1 Green | 5 |
| Ogre Brute | Attack 4 (free); Defend 5 — 1 Purple | 6 |

**Recruitable enemies** (menu `ScriptableObjects/Cards/EnemyCards`; before authoring stats, open 2 existing enemies per tier and match their HP/Attack bands; `defeatRewards` = same RewardsSO lists as a same-tier existing enemy):

| Enemy | tier | canInfluence | influenceCost | recruitedUnit |
|---|---|---|---|---|
| Mercenary Band | 1 | true | 4 | Mercenaries |
| Wandering Mystic | 2 | true | 5 | Mystic |
| Gryphon Rider (enemy) | 2 | true | 5 | Gryphon Rider |
| Ogre Brute | 3 | true | 6 | Ogre Brute |

Also edit **Bandit Footsoldier** (existing): `canInfluence: 1`, `influenceCost: 3`, `recruitedUnit` → Footsoldier.

**Towns**: in each Town/Keep/Castle asset's `recruitableUnits`, curate 2–3 units (Towns: Scout/Merchant/Warrior; Keeps: Warrior/Knight/Footsoldier; Castles: Knight + two premium forms).

- [ ] **Step 3: USER ACTION — Tools > Archon's Rise > Rebuild Content Registry**, then confirm no duplicate-id warnings.

- [ ] **Step 4: Full playtest** (spec §7 checklist): pay-to-leave; recruit with Charismatic (+cap, +cancel); costed option with/without matching crystal (wild fallback); undo every option kind (stat/Siege/Heal/Crystallize incl. crystal refund); exhaust across turn/round/save-load; town per-unit pricing.

- [ ] **Step 5: Commit** (`feat: unit & recruit content pass — options, recruitable enemies, Charismatic` + co-author line).

---

### Task 12: Design bible + decisions log + roadmap

**Files:**
- Modify: `.claude/skills/archons-rise-design/mechanics.md` (Units become option-lists; enemy influence resolution; passive skills; recruit section updated to per-unit prices)
- Modify: `.claude/skills/archons-rise-design/content-rules.md` (new `UnitsSO` table: `options`/`influenceCost`; `EnemiesSO.recruitedUnit`; `SkillsSO.cadence` gains Passive; `UnitEffect` enum listed)
- Modify: `.claude/skills/archons-rise-design/balance.md` (unit recruit-cost bands 2–3/3–4/5+; costed option ≈ 2× free sibling; skill pool 10)
- Modify: `.claude/skills/archons-rise-roadmap/decisions-log.md` (append the six decisions from the spec's final section, dated 2026-07-09)
- Modify: `.claude/skills/archons-rise-roadmap/milestones.md` (insert "M2.75 — Unit gameplay & recruitment" between M2.5 and M3 with scope + acceptance: "units play through the option pop-out incl. crystal-costed options; enemies can be paid off / recruited with Charismatic; towns recruit via panel at per-unit prices")

- [ ] **Step 1: Make the five doc edits above** (keep each file's existing voice/format; content-rules tables mirror the actual script fields — scripts win on conflict).

- [ ] **Step 2: Commit** (`docs: design bible + roadmap updates for unit gameplay & recruitment` + co-author line).

---

## Self-Review (done at write time)

- **Spec coverage:** data model → T1/T3/T7/T8; pop-out+undo → T3/T4/T5; recruiting → T8/T9; controller → T2/T6; saves → T10; content → T11; docs → T12. Rewards-on-influence, cancel-disband-cancels-all, wild-crystal matching, exhausted-state persistence all present.
- **Type consistency:** `UnitOption.crystalCost/grantColor`, `UnitPlaySelection.CanUse/Describe(int)`, `CrystalInventory.CanPay/SelectPayCrystal`, `Player.ApplyUnitOption/RevertUnitOption/AddUnit/HasCharismatic/InfluenceEnemy`, `DisbandPanel.OpenForHire`, `UnitNavRules.UseSlot/Open/Move` used identically across tasks.
- **Known risk flagged in-task:** `ContentRegistry.Resolve` null-vs-skip behavior (T10 Step 5 caution); mcs shim for `IsAllColors` if `ExtensionClass.cs` is Unity-bound (T1 Step 4).
