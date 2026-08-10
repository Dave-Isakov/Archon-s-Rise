# Hand / Unit Lane Swap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the bottom of the screen into one bar holding two interchangeable fan lanes — units on the left, cards on the right — with exactly one focused at a time and a single left/right rail walking across both.

**Architecture:** `HandFanLayout` is renamed `FanLane` and generalised over an `IFanItem` interface that `Card` and `Unit` both implement, so one component lays out, focuses and hit-tests either lane. `HandFocusController` and `UnitsLane` collapse into a single `BarFocusController` that owns lane focus, the rail position, mouse hit-testing and Submit/Cancel. Cross-lane navigation is not special-cased: a new pure `BarRailRules` flattens both lanes into one virtual list and delegates the stepping to the existing `HandNavRules`.

**Tech Stack:** Unity 6000.5.1f1, C# 9, Unity Input System, DOTween (incl. `DOTweenModuleUI`), NUnit EditMode tests under `ArchonsRise.Tests.EditMode`.

**Spec:** `docs/superpowers/specs/2026-08-10-hand-unit-lane-swap-design.md`

## Global Constraints

- Unity editor is normally open and holds the project lock, so `Unity.exe -runTests -batchmode` will fail. Pure classes are verified with the Mono/mcs harness built in Task 1; MonoBehaviour edits are verified with the Roslyn compile check built in Task 3.
- Pure logic lives in `Assets/Scripts/Hand/` (asmdef `ArchonsRise.Hand`, already listed in `ArchonsRise.Tests.EditMode.asmdef` — **no asmdef changes are needed in this plan**). MonoBehaviours that touch `Player`/`GameManager`/`PlayerHand` must stay under `Assets/Scripts/GameObjectScripts/` (Assembly-CSharp); an asmdef folder cannot reference Assembly-CSharp back.
- Never hand-edit `.unity` or `.prefab` YAML. All scene and prefab changes are done by the user in the editor from the guide produced in Task 7.
- New `.cs` files have no `.meta` until Unity next imports them. Commit the source in its task; commit the generated `.meta` files afterwards (repo convention — see commit `ba69ee4`).
- Keep comment density low. The spec is the commentary; comment only non-obvious decisions.
- Shared fan geometry values (both lanes): spread `66`, spacing `120`, arc drop `40`, `MaxWidth 900`, focus lift `40`, focus scale `1.3`, dim `0.86`.
- Lane poses: card lane focused `x = 0`, parked `x = +520`; unit lane focused `x = -430`, parked `x = -760`; parked scale `0.55`, parked alpha `0.5`; tween `0.18s` `Ease.OutCubic`.

---

## File Structure

**Created:**
- `Assets/Scripts/Hand/BarRailRules.cs` — pure cross-lane rail stepping (`BarLane`, `RailPos`, `BarRailRules`).
- `Assets/Scripts/GameObjectScripts/PlayerScripts/IFanItem.cs` — the contract `Card` and `Unit` share.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/BarFocusController.cs` — the single input owner.
- `Assets/Tests/EditMode/BarRailRulesTests.cs`
- `.superpowers/scratch/lane-swap/` — test harness and compile-check scripts (gitignored).
- `docs/superpowers/plans/2026-08-10-hand-unit-lane-swap-wiring.md` — editor steps for the user.

**Renamed:**
- `Assets/Scripts/GameObjectScripts/PlayerScripts/HandFanLayout.cs` → `FanLane.cs` (**with its `.cs.meta`**, preserving the GUID).

**Modified:**
- `Assets/Scripts/Hand/FanSlot.cs` — `FanSettings.MaxWidth`.
- `Assets/Scripts/Hand/FanMath.cs` — width cap.
- `Assets/Tests/EditMode/FanMathTests.cs` — cap tests.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs` — implements `IFanItem`, click offered to the bar.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs` — implements `IFanItem`, exhaust tint, hover handlers removed.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs` — field type.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` — exhaust rotation removed, lane relayout notifications.

**Deleted:**
- `Assets/Scripts/GameObjectScripts/PlayerScripts/UnitsLane.cs`
- `Assets/Scripts/GameObjectScripts/PlayerScripts/HandFocusController.cs`

---

## Task 1: Fan width cap

**Files:**
- Modify: `Assets/Scripts/Hand/FanSlot.cs`
- Modify: `Assets/Scripts/Hand/FanMath.cs`
- Test: `Assets/Tests/EditMode/FanMathTests.cs`
- Create: `.superpowers/scratch/lane-swap/Runner.cs`, `.superpowers/scratch/lane-swap/run-pure-tests.ps1`

**Interfaces:**
- Consumes: nothing.
- Produces: `FanSettings.MaxWidth` (public float field, default `900f`). `FanMath.Solve(int count, FanSettings s)` keeps its existing signature and return type `FanSlot[]`.

- [ ] **Step 1: Create the pure-test harness runner**

Create `.superpowers/scratch/lane-swap/Runner.cs`:

```csharp
using System;
using System.Linq;
using System.Reflection;

public static class Runner
{
    public static int Main(string[] args)
    {
        var asm = Assembly.LoadFrom(args[0]);
        int pass = 0, fail = 0;
        foreach (var type in asm.GetTypes())
        {
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!m.GetCustomAttributes(true).Any(a => a.GetType().Name == "TestAttribute")) continue;
                try
                {
                    m.Invoke(Activator.CreateInstance(type), null);
                    pass++;
                    Console.WriteLine("PASS " + type.Name + "." + m.Name);
                }
                catch (TargetInvocationException ex)
                {
                    fail++;
                    Console.WriteLine("FAIL " + type.Name + "." + m.Name + " : " + ex.InnerException.Message);
                }
            }
        }
        Console.WriteLine("--- " + pass + " passed, " + fail + " failed");
        return fail == 0 ? 0 : 1;
    }
}
```

- [ ] **Step 2: Create the harness driver script**

Create `.superpowers/scratch/lane-swap/run-pure-tests.ps1`:

```powershell
param([Parameter(Mandatory=$true)][string[]]$Sources)
$ErrorActionPreference = "Stop"
$scratch   = $PSScriptRoot
$mbe       = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge"
$mono      = "$mbe\bin\mono.exe"
$mcs       = "$mbe\lib\mono\4.5\mcs.exe"
$unityCore = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll"
$nunitSrc  = (Get-ChildItem "Library\PackageCache\com.unity.ext.nunit*\net472\unity-custom\nunit.framework.dll").FullName
$nunit     = "$scratch\nunit.framework.dll"

Copy-Item $nunitSrc $nunit -Force

if (-not (Test-Path "$scratch\Runner.exe")) {
    & $mono $mcs -nologo -target:exe "-out:$scratch\Runner.exe" "-r:$nunit" "$scratch\Runner.cs"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

& $mono $mcs -nologo -target:library "-out:$scratch\Tests.dll" "-r:$nunit" "-r:$unityCore" $Sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $mono "$scratch\Runner.exe" "$scratch\Tests.dll"
exit $LASTEXITCODE
```

Note: `mcs.bat` must never be invoked directly — the spaces in `C:\Program Files\...` break it. The script runs `mcs.exe` under `mono.exe` for that reason.

- [ ] **Step 3: Write the failing tests**

In `Assets/Tests/EditMode/FanMathTests.cs`, change the `Settings()` helper to include `MaxWidth` and append the four new tests. The full helper becomes:

```csharp
    static FanSettings Settings() => new FanSettings
    {
        SpreadDegrees = 66f,
        CardSpacing = 120f,
        ArcDrop = 40f,
        MaxWidth = 900f
    };

    static float Span(FanSlot[] slots) =>
        slots[slots.Length - 1].AnchoredPosition.x - slots[0].AnchoredPosition.x;
```

Append inside the class:

```csharp
    [Test]
    public void Span_IsUncappedBelowTheThreshold()
    {
        // 8 cards need 7 * 120 = 840 <= 900, so spacing is untouched.
        Assert.AreEqual(840f, Span(FanMath.Solve(8, Settings())), 0.001f);
    }

    [Test]
    public void Span_CompressesAboveTheThreshold()
    {
        // 12 cards would need 11 * 120 = 1320; the cap pulls the span back to 900.
        Assert.AreEqual(900f, Span(FanMath.Solve(12, Settings())), 0.001f);
    }

    [Test]
    public void Span_NeverExceedsMaxWidth()
    {
        for (int count = 2; count <= 20; count++)
            Assert.LessOrEqual(Span(FanMath.Solve(count, Settings())), 900.001f,
                "count " + count + " overran MaxWidth");
    }

    [Test]
    public void MaxWidthOfZero_DisablesTheCap()
    {
        var s = Settings();
        s.MaxWidth = 0f;
        Assert.AreEqual(1320f, Span(FanMath.Solve(12, s)), 0.001f);
    }
```

- [ ] **Step 4: Run the tests to verify they fail**

Run from the repo root:

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources @(
  "Assets\Scripts\Hand\FanSlot.cs",
  "Assets\Scripts\Hand\FanMath.cs",
  "Assets\Tests\EditMode\FanMathTests.cs")
```

Expected: compilation fails with `CS0117: 'FanSettings' does not contain a definition for 'MaxWidth'`.

- [ ] **Step 5: Add `MaxWidth` to `FanSettings`**

In `Assets/Scripts/Hand/FanSlot.cs`, replace the `FanSettings` class body:

```csharp
// Tunable fan geometry. Plain fields so FanLane can serialize it.
[System.Serializable]
public class FanSettings
{
    public float SpreadDegrees = 66f; // total fan angle -> edges sit at ±33°
    public float CardSpacing = 120f;  // horizontal px between adjacent card centers
    public float ArcDrop = 40f;       // px the edge cards sit below the center card
    public float MaxWidth = 900f;     // cap on the centre-to-centre span; <= 0 disables
}
```

- [ ] **Step 6: Cap the spacing in `FanMath`**

Replace the body of `Assets/Scripts/Hand/FanMath.cs`:

```csharp
using UnityEngine;

// Pure fan-arc solver. Given a card count and geometry, returns each card's
// local position + tilt. Index 0 is the leftmost card. No scene dependency.
public static class FanMath
{
    public static FanSlot[] Solve(int count, FanSettings s)
    {
        var slots = new FanSlot[count < 0 ? 0 : count];
        if (count <= 0) return slots;

        float spacing = EffectiveSpacing(count, s);

        for (int i = 0; i < count; i++)
        {
            // t in [-0.5, 0.5]; single card -> 0 (centred).
            float t = count == 1 ? 0f : (float)i / (count - 1) - 0.5f;

            float x = (i - (count - 1) * 0.5f) * spacing;
            float y = -s.ArcDrop * (2f * t) * (2f * t); // parabolic dip, edges lowest
            float tilt = -t * s.SpreadDegrees;           // leftmost -> +half-spread

            slots[i] = new FanSlot(new Vector2(x, y), tilt);
        }
        return slots;
    }

    // MaxWidth caps the centre-to-centre span, not the total pixel width — the
    // solver knows slot positions, not item dimensions. An oversized hand
    // tightens rather than sprawling off screen.
    static float EffectiveSpacing(int count, FanSettings s)
    {
        if (count <= 1 || s.MaxWidth <= 0f) return s.CardSpacing;
        return Mathf.Min(s.CardSpacing, s.MaxWidth / (count - 1));
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources @(
  "Assets\Scripts\Hand\FanSlot.cs",
  "Assets\Scripts\Hand\FanMath.cs",
  "Assets\Tests\EditMode\FanMathTests.cs")
```

Expected: `--- 9 passed, 0 failed` (5 pre-existing + 4 new).

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Hand/FanSlot.cs Assets/Scripts/Hand/FanMath.cs Assets/Tests/EditMode/FanMathTests.cs
git commit -m "feat: cap the fan's centre-to-centre span at MaxWidth"
```

---

## Task 2: `BarRailRules`

**Files:**
- Create: `Assets/Scripts/Hand/BarRailRules.cs`
- Test: `Assets/Tests/EditMode/BarRailRulesTests.cs`

**Interfaces:**
- Consumes: `HandNavRules.Step(int current, int direction, IReadOnlyList<bool> wounds)` and `HandNavRules.ClampAfterChange(int previous, IReadOnlyList<bool> wounds)`, both existing and unchanged. In both, a `true` flag means "skip this entry".
- Produces:
  - `enum BarLane { Units, Cards }`
  - `readonly struct RailPos { BarLane Lane; int Index; bool IsNone; static RailPos None; }` with constructor `RailPos(BarLane lane, int index)`.
  - `BarRailRules.Step(RailPos current, int direction, IReadOnlyList<bool> unitsBlocked, IReadOnlyList<bool> cardsBlocked) -> RailPos`
  - `BarRailRules.ClampAfterChange(RailPos previous, IReadOnlyList<bool> unitsBlocked, IReadOnlyList<bool> cardsBlocked) -> RailPos`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/BarRailRulesTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class BarRailRulesTests
{
    // false == selectable. Units are the LEFT lane, cards the RIGHT lane.
    static List<bool> Open(int count) => new List<bool>(new bool[count]);

    static void AssertAt(RailPos pos, BarLane lane, int index)
    {
        Assert.IsFalse(pos.IsNone, "expected a position, got none");
        Assert.AreEqual(lane, pos.Lane);
        Assert.AreEqual(index, pos.Index);
    }

    [Test]
    public void StepLeft_FromLeftmostCard_LandsOnRightmostUnit()
    {
        var pos = BarRailRules.Step(new RailPos(BarLane.Cards, 0), -1, Open(3), Open(4));
        AssertAt(pos, BarLane.Units, 2);
    }

    [Test]
    public void StepRight_FromRightmostUnit_LandsOnLeftmostCard()
    {
        var pos = BarRailRules.Step(new RailPos(BarLane.Units, 2), +1, Open(3), Open(4));
        AssertAt(pos, BarLane.Cards, 0);
    }

    [Test]
    public void StepLeft_FromLeftmostUnit_WrapsToRightmostCard()
    {
        var pos = BarRailRules.Step(new RailPos(BarLane.Units, 0), -1, Open(3), Open(4));
        AssertAt(pos, BarLane.Cards, 3);
    }

    [Test]
    public void StepRight_FromRightmostCard_WrapsToLeftmostUnit()
    {
        var pos = BarRailRules.Step(new RailPos(BarLane.Cards, 3), +1, Open(3), Open(4));
        AssertAt(pos, BarLane.Units, 0);
    }

    [Test]
    public void Step_SkipsBlockedEntries()
    {
        // The rightmost unit is exhausted, so crossing left lands on the one before it.
        var units = new List<bool> { false, false, true };
        var pos = BarRailRules.Step(new RailPos(BarLane.Cards, 0), -1, units, Open(4));
        AssertAt(pos, BarLane.Units, 1);
    }

    [Test]
    public void Step_WithEmptyUnitLane_StaysInCards()
    {
        var pos = BarRailRules.Step(new RailPos(BarLane.Cards, 0), -1, Open(0), Open(3));
        AssertAt(pos, BarLane.Cards, 2);
    }

    [Test]
    public void Step_WithEveryUnitBlocked_SkipsTheWholeLane()
    {
        var units = new List<bool> { true, true };
        var pos = BarRailRules.Step(new RailPos(BarLane.Cards, 0), -1, units, Open(3));
        AssertAt(pos, BarLane.Cards, 2);
    }

    [Test]
    public void Step_WithNothingSelectable_ReturnsNone()
    {
        var units = new List<bool> { true };
        var cards = new List<bool> { true, true };
        Assert.IsTrue(BarRailRules.Step(new RailPos(BarLane.Cards, 0), -1, units, cards).IsNone);
    }

    [Test]
    public void Step_FromNone_LandsNearTheMiddleOfTheBar()
    {
        // Combined length 6; the first press lands at flat index 3 == cards[1].
        var pos = BarRailRules.Step(RailPos.None, +1, Open(2), Open(4));
        AssertAt(pos, BarLane.Cards, 1);
    }

    [Test]
    public void ClampAfterChange_CanLandAcrossTheBoundary()
    {
        // Both cards became wounds; the nearest survivor is in the unit lane.
        var units = new List<bool> { false, false };
        var cards = new List<bool> { true, true };
        var pos = BarRailRules.ClampAfterChange(new RailPos(BarLane.Cards, 0), units, cards);
        AssertAt(pos, BarLane.Units, 1);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources @(
  "Assets\Scripts\Hand\HandNavRules.cs",
  "Assets\Tests\EditMode\BarRailRulesTests.cs")
```

Expected: compilation fails with `CS0246: The type or namespace name 'RailPos' could not be found`.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Hand/BarRailRules.cs`:

```csharp
using System.Collections.Generic;

public enum BarLane { Units, Cards }

// Where focus sits on the bar. Index < 0 means nothing is focusable.
public readonly struct RailPos
{
    public readonly BarLane Lane;
    public readonly int Index;

    public RailPos(BarLane lane, int index) { Lane = lane; Index = index; }

    public bool IsNone => Index < 0;
    public static RailPos None => new RailPos(BarLane.Cards, -1);
}

// Pure rail rules for the bottom bar. The bar is two lanes — units on the left,
// cards on the right — walked as one left/right continuum. Flattening them into
// a single virtual list is what makes crossing the boundary an ordinary step:
// HandNavRules already skips blocked entries and wraps, and it does both here
// over the whole bar rather than one lane. `true` in a mask means "skip".
public static class BarRailRules
{
    public static RailPos Step(RailPos current, int direction,
        IReadOnlyList<bool> unitsBlocked, IReadOnlyList<bool> cardsBlocked)
    {
        int flat = HandNavRules.Step(ToFlat(current, unitsBlocked.Count), direction,
            Combine(unitsBlocked, cardsBlocked));
        return FromFlat(flat, unitsBlocked.Count, cardsBlocked.Count);
    }

    public static RailPos ClampAfterChange(RailPos previous,
        IReadOnlyList<bool> unitsBlocked, IReadOnlyList<bool> cardsBlocked)
    {
        int flat = HandNavRules.ClampAfterChange(ToFlat(previous, unitsBlocked.Count),
            Combine(unitsBlocked, cardsBlocked));
        return FromFlat(flat, unitsBlocked.Count, cardsBlocked.Count);
    }

    static List<bool> Combine(IReadOnlyList<bool> units, IReadOnlyList<bool> cards)
    {
        var combined = new List<bool>(units.Count + cards.Count);
        for (int i = 0; i < units.Count; i++) combined.Add(units[i]);
        for (int i = 0; i < cards.Count; i++) combined.Add(cards[i]);
        return combined;
    }

    static int ToFlat(RailPos pos, int unitCount)
    {
        if (pos.IsNone) return -1;
        return pos.Lane == BarLane.Units ? pos.Index : unitCount + pos.Index;
    }

    static RailPos FromFlat(int flat, int unitCount, int cardCount)
    {
        if (flat < 0 || flat >= unitCount + cardCount) return RailPos.None;
        return flat < unitCount
            ? new RailPos(BarLane.Units, flat)
            : new RailPos(BarLane.Cards, flat - unitCount);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources @(
  "Assets\Scripts\Hand\HandNavRules.cs",
  "Assets\Scripts\Hand\BarRailRules.cs",
  "Assets\Tests\EditMode\BarRailRulesTests.cs")
```

Expected: `--- 10 passed, 0 failed`.

- [ ] **Step 5: Confirm the existing nav tests still pass**

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources @(
  "Assets\Scripts\Hand\HandNavRules.cs",
  "Assets\Tests\EditMode\HandNavRulesTests.cs")
```

Expected: all pass, 0 failed. `HandNavRules` was not modified; this proves it.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Hand/BarRailRules.cs Assets/Tests/EditMode/BarRailRulesTests.cs
git commit -m "feat: add BarRailRules for cross-lane rail stepping"
```

---

## Task 3: `IFanItem` and the `FanLane` rename

**Files:**
- Rename: `Assets/Scripts/GameObjectScripts/PlayerScripts/HandFanLayout.cs` → `FanLane.cs` (and its `.cs.meta`)
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/IFanItem.cs`
- Create: `.superpowers/scratch/lane-swap/compile-check.ps1`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/HandFocusController.cs`

**Interfaces:**
- Consumes: `FanMath.Solve`, `FanSettings` (Task 1).
- Produces:
  - `interface IFanItem { RectTransform Rect { get; } CanvasGroup Group { get; } bool Selectable { get; } void Activate(); }`
  - `class FanLane : MonoBehaviour` with `Transform Container`, `IFanItem Focused`, `void SetFocus(IFanItem)`, `void ClearFocus()`, `void Relayout(IReadOnlyList<IFanItem>)`, `List<IFanItem> InLane()`, `IFanItem HitTest(Vector2 screenPos)`.

- [ ] **Step 1: Create the Roslyn compile-check script**

Create `.superpowers/scratch/lane-swap/compile-check.ps1`:

```powershell
param([string]$Proj = "Assembly-CSharp.csproj")
$ErrorActionPreference = "Stop"
$scratch = $PSScriptRoot
$root    = (Get-Location).Path
$editor  = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor"
$dotnet  = "$editor\Data\NetCoreRuntime\dotnet.exe"
$csc     = "$editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll"

$lines = New-Object System.Collections.Generic.List[string]

Select-Xml -Path $Proj -XPath "//*[local-name()='Compile']/@Include" |
  ForEach-Object { $lines.Add('"' + [System.Net.WebUtility]::HtmlDecode($_.Node.Value) + '"') }

Select-Xml -Path $Proj -XPath "//*[local-name()='Reference']/*[local-name()='HintPath']" |
  ForEach-Object { $lines.Add('"-r:' + [System.Net.WebUtility]::HtmlDecode($_.Node.InnerText) + '"') }

Select-Xml -Path $Proj -XPath "//*[local-name()='ProjectReference']/@Include" |
  ForEach-Object {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($_.Node.Value)
    $lines.Add('"-r:' + "$root\Library\ScriptAssemblies\$name.dll" + '"')
  }

if ($lines.Count -eq 0) { throw "No compile items found in $Proj — the csproj shape changed." }

$rsp = "$scratch\compile.rsp"
Set-Content -Path $rsp -Value $lines -Encoding utf8

& $dotnet $csc -nostdlib+ -noconfig -target:library -langversion:9.0 -define:UNITY_EDITOR `
  "-out:$scratch\out.dll" "@$rsp"
exit $LASTEXITCODE
```

Missing `ProjectReference` resolution is the usual cause of a flood of `CS0246` on the project's own types — that third block is why it is there.

- [ ] **Step 2: Rename the layout file, preserving its GUID**

```bash
git mv Assets/Scripts/GameObjectScripts/PlayerScripts/HandFanLayout.cs Assets/Scripts/GameObjectScripts/PlayerScripts/FanLane.cs
git mv Assets/Scripts/GameObjectScripts/PlayerScripts/HandFanLayout.cs.meta Assets/Scripts/GameObjectScripts/PlayerScripts/FanLane.cs.meta
```

Both moves are required. Moving only the `.cs` makes Unity mint a new GUID, which leaves `Hand.prefab` with a missing script reference and silently drops its serialized fan settings.

- [ ] **Step 3: Create `IFanItem`**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/IFanItem.cs`:

```csharp
using UnityEngine;

// One entry in a fan lane. Card and Unit both satisfy it, so FanLane lays out,
// focuses and hit-tests either lane with no branching on type.
public interface IFanItem
{
    RectTransform Rect { get; }
    CanvasGroup Group { get; }
    bool Selectable { get; }  // false for wounds and exhausted units
    void Activate();          // Submit, or a click inside the focused lane
}
```

- [ ] **Step 4: Rewrite `FanLane` against `IFanItem`**

Replace the whole of `Assets/Scripts/GameObjectScripts/PlayerScripts/FanLane.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

// Applies FanMath slots to the live items of one lane. Driven by whoever owns
// the lane's order. Geometry only: focus is WRITTEN by BarFocusController (the
// single owner of focus policy); this component renders it and answers slot
// hit-tests.
public class FanLane : MonoBehaviour
{
    [SerializeField] FanSettings fan = new FanSettings();
    [SerializeField] float focusLift = 40f;
    [SerializeField] float focusScale = 1.3f;
    [SerializeField] float dimBrightness = 0.86f;

    IFanItem _focused;
    IReadOnlyList<IFanItem> _last;

    public Transform Container => transform;
    public IFanItem Focused => Alive(_focused) ? _focused : null;

    // An interface reference bypasses Unity's null-equality overload, so a
    // destroyed item would still read as non-null. Everything that dereferences
    // an IFanItem goes through here.
    static bool Alive(IFanItem item) => item is MonoBehaviour mb && mb != null;

    public void SetFocus(IFanItem item)
    {
        if (ReferenceEquals(item, _focused)) return;
        _focused = item;
        if (_last != null) Relayout(_last);
    }

    public void ClearFocus() => SetFocus(null);

    public void Relayout(IReadOnlyList<IFanItem> orderedItems)
    {
        _last = orderedItems;
        var inLane = InLane();

        var slots = FanMath.Solve(inLane.Count, fan);
        for (int i = 0; i < inLane.Count; i++)
            Apply(inLane[i], slots[i], ReferenceEquals(inLane[i], _focused));

        if (Alive(_focused) && _focused.Rect.parent == transform)
            _focused.Rect.SetAsLastSibling();
    }

    // The items currently physically in the fan (parented here, active), in order.
    public List<IFanItem> InLane()
    {
        var inLane = new List<IFanItem>();
        if (_last == null) return inLane;
        foreach (var item in _last)
            if (Alive(item) && item.Rect.parent == transform && item.Rect.gameObject.activeSelf)
                inLane.Add(item);
        return inLane;
    }

    // Topmost selectable item whose SLOT rect (not its lifted position) contains
    // the screen point; checking the slot prevents the pointer-exit feedback loop
    // that occurs when the lifted item moves out from under the cursor.
    public IFanItem HitTest(Vector2 screenPos)
    {
        if (_last == null) return null;
        var container = (RectTransform)transform;
        var cam = GetComponentInParent<Canvas>()?.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                container, screenPos, cam, out var local))
            return null;

        var inLane = InLane();
        var slots = FanMath.Solve(inLane.Count, fan);

        // Front-to-back so the topmost (last sibling) item wins on overlap.
        for (int i = inLane.Count - 1; i >= 0; i--)
        {
            if (!inLane[i].Selectable) continue;

            var slotPos = slots[i].AnchoredPosition;
            var half = inLane[i].Rect.rect.size * 0.5f;

            if (local.x >= slotPos.x - half.x && local.x <= slotPos.x + half.x &&
                local.y >= slotPos.y - half.y && local.y <= slotPos.y + half.y)
                return inLane[i];
        }
        return null;
    }

    void Apply(IFanItem item, FanSlot slot, bool focused)
    {
        var rt = item.Rect;
        if (focused)
        {
            rt.anchoredPosition = slot.AnchoredPosition + new Vector2(0f, focusLift);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * focusScale;
        }
        else
        {
            rt.anchoredPosition = slot.AnchoredPosition;
            rt.localRotation = Quaternion.Euler(0f, 0f, slot.TiltZ);
            rt.localScale = Vector3.one;
        }

        var cg = item.Group;
        if (cg != null)
            cg.alpha = (focused || !Alive(_focused)) ? 1f : dimBrightness;
    }
}
```

- [ ] **Step 5: Make `Card` an `IFanItem`**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs`, change the class declaration:

```csharp
public class Card : MonoBehaviour, IPointerClickHandler, IFanItem
```

Add these members immediately after the `InDiscard` property (around line 69):

```csharp
    CanvasGroup _group;
    public RectTransform Rect => (RectTransform)transform;
    public CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();
    public bool Selectable => cardSO != null && cardSO.cardType != StatType.Wound;
    public void Activate() => ToggleInspect();
```

And update the stale layout lookup at line 162:

```csharp
        var hand = GameManager.Instance.playerHand.GetComponentInChildren<FanLane>();
```

- [ ] **Step 6: Make `Unit` an `IFanItem`**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs`, change the class declaration and add the members. The declaration becomes:

```csharp
public class Unit : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IFanItem
```

Add after the `IsPlayed` property:

```csharp
    CanvasGroup _group;
    public RectTransform Rect => (RectTransform)transform;
    public CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();
    public bool Selectable => !isPlayed;

    public void Activate()
    {
        var inspector = FindAnyObjectByType<UnitInspector>();
        if (inspector != null) inspector.Open(this);
    }
```

The pointer-enter/exit handlers stay for now; Task 5 removes them along with `UnitsLane`.

- [ ] **Step 7: Update `PlayerHand` and `HandFocusController` to the new types**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs` line 16:

```csharp
    [SerializeField] FanLane handLayout;
```

`Relayout()` needs no change: `List<Card>` converts to `IReadOnlyList<IFanItem>` through interface covariance.

In `Assets/Scripts/GameObjectScripts/PlayerScripts/HandFocusController.cs` line 10:

```csharp
    [SerializeField] FanLane layout;
```

Replace the three blocks that reach through `Card` (this file is deleted in Task 5; these edits only keep the project compiling until then). In `HandleNavigate`:

```csharp
        var cards = layout.InLane();
        var blocked = new bool[cards.Count];
        for (int i = 0; i < cards.Count; i++)
            blocked[i] = !cards[i].Selectable;

        int current = layout.Focused != null ? cards.IndexOf(layout.Focused) : -1;
        int next = HandNavRules.Step(current, nav.x > 0 ? +1 : -1, blocked);
```

In `HandleSubmit`:

```csharp
        layout.Focused.Activate();
```

In `KeepPadFocusValid` and `RestorePadFocus`, replace `layout.InHand()` with `layout.InLane()` and both wound-flag loops with the `Selectable` form above, keeping the `HandNavRules` calls as they are.

- [ ] **Step 8: Verify the assembly compiles**

```powershell
.\.superpowers\scratch\lane-swap\compile-check.ps1 -Proj Assembly-CSharp.csproj
```

Expected: exit code 0 and no `CS` diagnostics. `Assembly-CSharp.csproj` will not list the new `IFanItem.cs` until Unity regenerates it, so add it by hand to the response file if `CS0246: IFanItem` appears — or focus the Unity editor once to force a reimport and re-run.

- [ ] **Step 9: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlayerScripts/
git commit -m "refactor: generalise the hand fan into FanLane over IFanItem"
```

---

## Task 4: Lane pose (focused / parked)

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/FanLane.cs`

**Interfaces:**
- Consumes: `FanLane` from Task 3.
- Produces: `FanLane.SetPose(bool parked, bool instant = false)` and `bool FanLane.IsParked { get; }`.

- [ ] **Step 1: Add the pose fields and API to `FanLane`**

Add `using DG.Tweening;` to the top of `FanLane.cs`, then add these fields after `dimBrightness`:

```csharp
    [Header("Lane pose")]
    [SerializeField] CanvasGroup laneGroup;             // on this container, not the items
    [SerializeField] Vector2 focusedPos = new Vector2(0f, -300f);
    [SerializeField] Vector2 parkedPos = new Vector2(520f, -330f);
    [SerializeField] float parkedScale = 0.55f;
    [SerializeField] float parkedAlpha = 0.5f;
    [SerializeField] float poseTween = 0.18f;

    bool _parked;
```

and these members after `ClearFocus()`:

```csharp
    public bool IsParked => _parked;

    // The focused lane holds the middle of the bar at full size; the other tucks
    // to its own edge, scaled and dimmed but still readable and still clickable.
    // parkedPos.y is its own value because localScale shrinks about the centre
    // pivot, which lifts a parked lane off the bar baseline unless y compensates.
    public void SetPose(bool parked, bool instant = false)
    {
        if (_parked == parked && !instant) return;
        _parked = parked;

        var rt = (RectTransform)transform;
        Vector2 pos = parked ? parkedPos : focusedPos;
        float scale = parked ? parkedScale : 1f;
        float alpha = parked ? parkedAlpha : 1f;

        rt.DOKill();
        if (laneGroup != null) laneGroup.DOKill();

        if (instant || poseTween <= 0f)
        {
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one * scale;
            if (laneGroup != null) laneGroup.alpha = alpha;
            return;
        }

        rt.DOAnchorPos(pos, poseTween).SetEase(Ease.OutCubic);
        rt.DOScale(Vector3.one * scale, poseTween).SetEase(Ease.OutCubic);
        if (laneGroup != null) laneGroup.DOFade(alpha, poseTween).SetEase(Ease.OutCubic);
    }
```

The `DOKill()` pair is what stops rapid swapping from stacking transforms.

- [ ] **Step 2: Verify the assembly compiles**

```powershell
.\.superpowers\scratch\lane-swap\compile-check.ps1 -Proj Assembly-CSharp.csproj
```

Expected: exit code 0, no `CS` diagnostics. `DOAnchorPos` and `DOFade` come from `Assets/Plugins/Demigiant/DOTween/Modules/DOTweenModuleUI.cs`, which is present and already used by `CardInspector` and `Toast`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlayerScripts/FanLane.cs
git commit -m "feat: give FanLane a focused/parked pose"
```

---

## Task 5: `BarFocusController`

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/BarFocusController.cs`
- Delete: `Assets/Scripts/GameObjectScripts/PlayerScripts/HandFocusController.cs` (+ `.meta`)
- Delete: `Assets/Scripts/GameObjectScripts/PlayerScripts/UnitsLane.cs` (+ `.meta`)
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs`

**Interfaces:**
- Consumes: `BarRailRules.Step` / `ClampAfterChange`, `RailPos`, `BarLane` (Task 2); `FanLane.InLane()`, `HitTest`, `SetFocus`, `ClearFocus`, `Focused`, `SetPose` (Tasks 3–4); `IFanItem.Selectable` / `Activate()`.
- Produces:
  - `BarFocusController.Instance` (static, nullable)
  - `bool TryClaimClick(IFanItem item)` — true when the click was consumed as a lane swap
  - `void RelayoutUnits()` — rebuilds the unit lane from its children and re-clamps

- [ ] **Step 1: Write `BarFocusController`**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/BarFocusController.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The single writer of bar focus. Two lanes — units left, cards right — are
// walked as one rail, so crossing the lane boundary IS the swap; there is no
// separate swap input and no hand-off between controllers. Mouse claims focus
// only when the pointer actually MOVES (delta != 0); gamepad/keyboard claims it
// on a Navigate press — last input wins.
public class BarFocusController : MonoBehaviour
{
    public static BarFocusController Instance { get; private set; }

    [SerializeField] FanLane unitLane;
    [SerializeField] FanLane cardLane;

    enum FocusOwner { None, Mouse, Pad }
    FocusOwner _owner = FocusOwner.None;
    Vector2 _lastMousePos;
    bool _navLatched;
    bool _inspectorWasOpen;
    RailPos _lastPos = RailPos.None;
    BarLane _focusedLane = BarLane.Cards;

    void Awake()
    {
        Instance = this;
        cardLane.SetPose(parked: false, instant: true);
        unitLane.SetPose(parked: true, instant: true);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    FanLane Lane(BarLane lane) => lane == BarLane.Units ? unitLane : cardLane;
    List<IFanItem> Items(BarLane lane) => Lane(lane).InLane();

    static List<bool> Blocked(List<IFanItem> items)
    {
        var blocked = new List<bool>(items.Count);
        foreach (var item in items) blocked.Add(!item.Selectable);
        return blocked;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.cardCanvas.enabled || gm.unitCanvas.enabled)
        {
            // Pop-out open: the bar shows no item focus and consumes no input.
            // _owner and the lane pose are kept so both restore on close.
            if (!_inspectorWasOpen) { _inspectorWasOpen = true; ClearItemFocus(); }
            return;
        }

        if (_inspectorWasOpen)
        {
            // First frame after the pop-out closed. Restore pad focus and swallow
            // this frame's input so the Cancel/Submit press that closed the
            // pop-out cannot also act here.
            _inspectorWasOpen = false;
            if (_owner == FocusOwner.Pad) RestorePadFocus();
            return;
        }

        if (gm.mainMenuCanvas.enabled || gm.cardListCanvas.enabled) return;

        // Map mode: the per-frame hit-test would focus items the player is
        // panning over, and arrow keys are pan input there, not rail navigation.
        if (InputContextState.Current == InputContext.Map) return;

        if (_owner == FocusOwner.Pad) KeepPadFocusValid();
        HandleMouse();
        HandleNavigate();
        HandleCancel();
        HandleSubmit(); // last: opening the inspector must be this frame's final act
    }

    void HandleMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 pos = mouse.position.ReadValue();
        if (pos == _lastMousePos) return;
        _lastMousePos = pos;

        // Hover acts only inside the focused lane; a parked lane responds to
        // clicks, never to the cursor passing over it.
        var lane = Lane(_focusedLane);
        var hit = lane.HitTest(pos);
        if (hit != null)
        {
            _owner = FocusOwner.Mouse;
            lane.SetFocus(hit);
            _lastPos = new RailPos(_focusedLane, Items(_focusedLane).IndexOf(hit));
        }
        else if (_owner == FocusOwner.Mouse)
        {
            // Only a mouse-claimed focus is cleared by the mouse leaving; drifting
            // the mouse must not clear a pad-claimed focus.
            _owner = FocusOwner.None;
            ClearItemFocus();
        }
    }

    void HandleNavigate()
    {
        Vector2 nav = GameControls.Gameplay.Navigate.ReadValue<Vector2>();
        // Latch so one press = one step (sticks and held d-pads report every frame).
        if (nav.magnitude < 0.5f) { _navLatched = false; return; }
        if (_navLatched) return;
        _navLatched = true;

        // Up/down no longer cross lanes — left/right is the whole rail.
        if (Mathf.Abs(nav.y) > Mathf.Abs(nav.x)) return;

        var units = Items(BarLane.Units);
        var cards = Items(BarLane.Cards);
        var next = BarRailRules.Step(_lastPos, nav.x > 0 ? +1 : -1, Blocked(units), Blocked(cards));
        if (next.IsNone) return;

        _owner = FocusOwner.Pad;
        ApplyPos(next);
    }

    void HandleCancel()
    {
        if (_owner != FocusOwner.Pad) return;
        if (!GameControls.Gameplay.Cancel.WasPressedThisFrame()) return;
        _owner = FocusOwner.None;
        _lastPos = RailPos.None;
        ClearItemFocus();
        InputContextState.Current = InputContext.Board;
    }

    void HandleSubmit()
    {
        if (!GameControls.Gameplay.Submit.WasPressedThisFrame()) return;
        var focused = Lane(_focusedLane).Focused;
        if (focused == null) return;
        focused.Activate();
    }

    // Card and Unit offer their click here first. A click on an item in a PARKED
    // lane is consumed as swap-and-select: the lanes swap, focus lands on (or
    // beside) the clicked item, and its pop-out does NOT open. A click in the
    // focused lane is not claimed, so the normal open path runs.
    public bool TryClaimClick(IFanItem item)
    {
        if (InputContextState.MapOpen) return false;
        var gm = GameManager.Instance;
        if (gm == null) return false;
        if (gm.cardCanvas.enabled || gm.unitCanvas.enabled ||
            gm.mainMenuCanvas.enabled || gm.cardListCanvas.enabled) return false;

        if (!Locate(item, out var lane, out int index)) return false; // not in a lane
        if (lane == _focusedLane) return false;

        _owner = FocusOwner.Mouse;

        // Clamp inside the CLICKED lane, not across the bar: the player aimed at
        // a lane, so a wound or exhausted unit still buys them that lane.
        int landing = HandNavRules.ClampAfterChange(index, Blocked(Items(lane)));
        if (landing < 0)
        {
            SetFocusedLane(lane);
            ClearItemFocus();
            return true;
        }
        ApplyPos(new RailPos(lane, landing));
        return true;
    }

    // Rebuilds the unit lane from its children in sibling order. Called by Player
    // whenever the army or its exhaust state changes, because IsPlayed feeds
    // IFanItem.Selectable and the rail's mask is stale until it runs.
    public void RelayoutUnits()
    {
        if (unitLane == null) return;
        var items = new List<IFanItem>();
        foreach (Transform child in unitLane.Container)
        {
            var unit = child.GetComponent<Unit>();
            if (unit != null) items.Add(unit);
        }
        unitLane.Relayout(items);
        if (_owner == FocusOwner.Pad) KeepPadFocusValid();
    }

    bool Locate(IFanItem item, out BarLane lane, out int index)
    {
        index = Items(BarLane.Units).IndexOf(item);
        if (index >= 0) { lane = BarLane.Units; return true; }
        index = Items(BarLane.Cards).IndexOf(item);
        if (index >= 0) { lane = BarLane.Cards; return true; }
        lane = BarLane.Cards;
        return false;
    }

    void ApplyPos(RailPos pos)
    {
        if (pos.IsNone) { ClearItemFocus(); return; }
        var items = Items(pos.Lane);
        if (pos.Index >= items.Count) return;

        SetFocusedLane(pos.Lane);
        Lane(pos.Lane).SetFocus(items[pos.Index]);
        Lane(pos.Lane == BarLane.Units ? BarLane.Cards : BarLane.Units).ClearFocus();
        _lastPos = pos;
        InputContextState.Current = InputContext.Fan;
    }

    void SetFocusedLane(BarLane lane)
    {
        if (_focusedLane == lane) return;
        _focusedLane = lane;
        cardLane.SetPose(parked: lane != BarLane.Cards);
        unitLane.SetPose(parked: lane != BarLane.Units);
    }

    void ClearItemFocus()
    {
        unitLane.ClearFocus();
        cardLane.ClearFocus();
    }

    // After draw/discard/heal/play/recruit/exhaust the focused item may have left
    // the bar; keep pad focus on the nearest survivor instead of letting it vanish.
    // The clamp is free to cross the lane boundary, and lane focus follows it.
    void KeepPadFocusValid()
    {
        var focused = Lane(_focusedLane).Focused;
        if (focused != null && Items(_focusedLane).Contains(focused))
        {
            _lastPos = new RailPos(_focusedLane, Items(_focusedLane).IndexOf(focused));
            return;
        }
        RestorePadFocus();
    }

    void RestorePadFocus()
    {
        var units = Items(BarLane.Units);
        var cards = Items(BarLane.Cards);
        var next = BarRailRules.ClampAfterChange(_lastPos, Blocked(units), Blocked(cards));
        if (next.IsNone)
        {
            _owner = FocusOwner.None;
            _lastPos = RailPos.None;
            ClearItemFocus();
            InputContextState.Current = InputContext.Board;
            return;
        }
        ApplyPos(next);
    }
}
```

- [ ] **Step 2: Offer clicks to the bar from `Card`**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs`, replace line 102:

```csharp
    public void OnPointerClick(PointerEventData eventData)
    {
        // A maximized card has been reparented out of the lane, so it is never
        // claimed and this still closes the inspector.
        if (BarFocusController.Instance != null &&
            BarFocusController.Instance.TryClaimClick(this)) return;
        ToggleInspect();
    }
```

- [ ] **Step 3: Rewrite `Unit`'s pointer handling**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs`, change the declaration to drop the hover interfaces:

```csharp
public class Unit : MonoBehaviour, IPointerClickHandler, IFanItem
```

Replace `OnPointerClick` and delete `OnPointerEnter` / `OnPointerExit` entirely:

```csharp
    public void OnPointerClick(PointerEventData eventData)
    {
        if (InputContextState.MapOpen) return; // map mode: look, don't touch
        if (BarFocusController.Instance != null &&
            BarFocusController.Instance.TryClaimClick(this)) return;
        if (isPlayed)
        {
            GameLog.Instance.Post($"{unitSO.cardName} has already been played, undo to revert action.");
            return;
        }
        FindAnyObjectByType<UnitInspector>().Open(this);
    }
```

- [ ] **Step 4: Delete the two superseded controllers**

```bash
git rm Assets/Scripts/GameObjectScripts/PlayerScripts/HandFocusController.cs
git rm Assets/Scripts/GameObjectScripts/PlayerScripts/HandFocusController.cs.meta
git rm Assets/Scripts/GameObjectScripts/PlayerScripts/UnitsLane.cs
git rm Assets/Scripts/GameObjectScripts/PlayerScripts/UnitsLane.cs.meta
```

Nothing outside these two files referenced `UnitsLane.FocusOutlineOver` or `HideOutline` — the comment in `UnitsLane` claiming the pop-out's nav controller reuses them is stale.

- [ ] **Step 5: Verify the assembly compiles**

```powershell
.\.superpowers\scratch\lane-swap\compile-check.ps1 -Proj Assembly-CSharp.csproj
```

Expected: exit code 0, no `CS` diagnostics. Deleted files may still appear in the stale `Assembly-CSharp.csproj`; if `CS2001: Source file could not be found` names `HandFocusController.cs` or `UnitsLane.cs`, focus the Unity editor once so it regenerates the csproj, then re-run.

- [ ] **Step 6: Commit**

```bash
git add -A Assets/Scripts/GameObjectScripts/PlayerScripts/
git commit -m "refactor: collapse hand and unit focus into BarFocusController"
```

---

## Task 6: Exhaustion as a tint

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`

**Interfaces:**
- Consumes: `BarFocusController.Instance.RelayoutUnits()` (Task 5).
- Produces: `Unit.IsPlayed` setter now also applies the exhaust tint. No new public API.

- [ ] **Step 1: Route the tint through `Unit.IsPlayed`**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs`, add the serialized colour beside the other fields:

```csharp
    [SerializeField] Color exhaustedGrey = new Color(0.55f, 0.55f, 0.55f, 1f);
```

Replace the `IsPlayed` property and `Start`:

```csharp
    // Exhaustion used to be a -90 rotation, which FanLane would overwrite with the
    // slot tilt on the next relayout. It is now a grey tint — the same language
    // wounds use — applied here so no caller can drift from it.
    public bool IsPlayed
    {
        get => isPlayed;
        set { isPlayed = value; ApplyExhaustTint(); }
    }

    void Start()
    {
        unitLetter.text = unitSO.cardName.ToString();
        unitText.text = unitSO.cardDescription;
        ApplyExhaustTint();
    }

    void ApplyExhaustTint()
    {
        if (image == null || unitSO == null) return;
        image.color = isPlayed ? exhaustedGrey : unitSO.color;
    }
```

- [ ] **Step 2: Drop the rotations and notify the lane in `Player`**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`, replace lines 465–468:

```csharp
    // The one IsPlayed pair, shared by round refresh, unit options, and the
    // refresh picker so exhaust visuals can never drift apart. The tint lives in
    // Unit.IsPlayed; the relayout refreshes the rail's selectable mask.
    void ReadyUnit(Unit unit)   { unit.IsPlayed = false; BarFocusController.Instance?.RelayoutUnits(); }
    void ExhaustUnit(Unit unit) { unit.IsPlayed = true;  BarFocusController.Instance?.RelayoutUnits(); }
```

In `RebuildUnits`, replace lines 446–450:

```csharp
            if (exhausted != null && i < exhausted.Length && exhausted[i])
                unit.IsPlayed = true;
        }
        BarFocusController.Instance?.RelayoutUnits();
```

(The closing brace shown is the existing `for` loop's; the relayout call goes after it, before the method's closing brace.)

In `AddUnit`, append after `newUnit.GetComponent<Unit>().unitSO = so;`:

```csharp
        BarFocusController.Instance?.RelayoutUnits();
```

In `DisbandUnit`, append after `Destroy(unit.gameObject);`:

```csharp
        BarFocusController.Instance?.RelayoutUnits();
```

- [ ] **Step 3: Verify the assembly compiles**

```powershell
.\.superpowers\scratch\lane-swap\compile-check.ps1 -Proj Assembly-CSharp.csproj
```

Expected: exit code 0, no `CS` diagnostics.

- [ ] **Step 4: Confirm no rotation calls survive**

```bash
grep -n "Rotate(0, 0" Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs
```

Expected: no output.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs
git commit -m "feat: show unit exhaustion as a grey tint instead of a rotation"
```

---

## Task 7: Editor wiring and the acceptance pass

**Files:**
- Create: `docs/superpowers/plans/2026-08-10-hand-unit-lane-swap-wiring.md`

**Interfaces:**
- Consumes: everything from Tasks 1–6.
- Produces: nothing in code. Deliverable is the guide plus a signed-off acceptance run.

- [ ] **Step 1: Write the wiring guide**

Create `docs/superpowers/plans/2026-08-10-hand-unit-lane-swap-wiring.md` containing exactly these steps for the user to perform in the Unity editor:

```markdown
# Lane swap — editor wiring

## 1. Unit prefab (`Assets/Prefabs/Unit.prefab`)
- Set the root `RectTransform` width/height to match the card prefab's.
- Re-lay the portrait, name and description children onto the card-shaped face.
- Add a `CanvasGroup` component to the root (FanLane dims items through it).
- Set the new `Exhausted Grey` colour field on the `Unit` component (default 0.55 grey is fine).

## 2. Units container (scene `GameBoard`, object `Units`)
- Delete the `Grid Layout Group` component — `FanLane` writes positions directly.
- Delete the `UnitsLane` component (its script is gone) and the focus-outline child object.
- Add `FanLane`. Set: Spread 66, Card Spacing 120, Arc Drop 40, Max Width 900,
  Focus Lift 40, Focus Scale 1.3, Dim Brightness 0.86.
- Add a `CanvasGroup` to `Units` and drag it into FanLane's `Lane Group` field.
- Pose: Focused Pos `(-430, -300)`, Parked Pos `(-760, -330)`, Parked Scale 0.55,
  Parked Alpha 0.5, Pose Tween 0.18.

## 3. Hand container (the object carrying the old `HandFanLayout`, under `Hand.prefab`)
- The component is now called `FanLane` and keeps its serialized values (the GUID was preserved).
- Add a `CanvasGroup` to the same object and drag it into `Lane Group`.
- Set Max Width 900. Pose: Focused Pos `(0, -300)`, Parked Pos `(520, -330)`,
  Parked Scale 0.55, Parked Alpha 0.5, Pose Tween 0.18.

## 4. Bar controller
- On the object that used to carry `HandFocusController` (it now has a missing script), remove the
  missing-script entry and add `BarFocusController`.
- Drag the `Units` object into `Unit Lane` and the hand fan container into `Card Lane`.

## 5. Check for missing references
- Open the scene and the Hand/Unit prefabs; confirm no component shows "Missing (Mono Script)".
- Confirm `PlayerHand`'s `Hand Layout` field still points at the hand `FanLane`.
```

- [ ] **Step 2: Commit the guide**

```bash
git add docs/superpowers/plans/2026-08-10-hand-unit-lane-swap-wiring.md
git commit -m "docs: add editor wiring guide for the lane swap"
```

- [ ] **Step 3: Hand the guide to the user and wait**

Ask the user to perform the wiring steps and to commit the generated `.meta` files for `BarRailRules.cs`, `IFanItem.cs` and `BarFocusController.cs` once Unity has imported them. Do not proceed until they confirm.

- [ ] **Step 4: Run the acceptance pass with the user**

Walk the user through spec §10 and record the result of each item:

1. Cards focused: the unit lane sits small and dimmed on the left, the card fan holds the middle, and moving the mouse over the map changes neither.
2. Clicking a unit in the parked lane swaps the lanes, lifts that unit, and opens nothing; clicking it again opens its pop-out.
3. On a pad, holding left from the middle of the hand walks off the leftmost card onto the rightmost unit and the lanes swap in one motion; continuing left past the leftmost unit wraps to the rightmost card.
4. Wounds and exhausted units are stepped over by both mouse hover and pad nav; exhausted units read as greyed, not rotated.
5. A 12-card hand (hand size plus wounds) stays on screen and does not touch the parked unit lane.
6. Playing the last playable card with a ready unit left moves pad focus into the unit lane rather than dropping to Board.
7. Opening and closing a card or unit pop-out leaves the same lane focused as before.

- [ ] **Step 5: Record the decision in the roadmap**

Append a dated entry to `.claude/skills/archons-rise-roadmap/decisions-log.md` naming the lane-swap design, the approach chosen (one bar, two lanes, one rail) and the exhaust-visual change, then commit.

```bash
git add .claude/skills/archons-rise-roadmap/decisions-log.md
git commit -m "docs: log the hand/unit lane swap decision"
```

---

## Self-Review Notes

**Spec coverage:** §3.1 → Tasks 1–2. §3.2 → Task 3. §3.3 → Task 5. §3.4 → Task 5 steps 2–3. §4 geometry → Task 4 + Task 7 wiring. §5 exhaustion → Task 6. §6 input → Task 5. §7 edge cases → carried by the guards preserved in Task 5's `Update` and by `KeepPadFocusValid`/`RestorePadFocus`. §8 tests → Tasks 1–2. §9 manual work → Task 7. §10 acceptance → Task 7 step 4.

**Known gap accepted:** §7's *"Wound arrives while units are focused"* has no dedicated code — it falls out of `PlayerHand.Relayout` touching only the card lane, and is covered by acceptance item 5 rather than a test.
