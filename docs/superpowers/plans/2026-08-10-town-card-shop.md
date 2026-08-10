# Town Card Shop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Places that list cards for sale offer a Cards action that spends influence and opens the existing reward picker with three unique cards from that place's list.

**Architecture:** A new pure `ShopRules.PickUnique` becomes the single selection path for every card offer in the game. `TownsSO.purchasableCards` is the gate (place type no longer decides), `TownsSO.cardLevel` is the price, and both UI routes — the place fan and the town canvas — call one `TownToken.BuyCards()` that spends, commits the visit action, then hands off to `Rewards.OfferTownCards`.

**Tech Stack:** Unity 6000.5.1f1, C# 9, NUnit EditMode tests, asmdef-partitioned pure classes.

**Spec:** `docs/superpowers/specs/2026-08-10-town-card-shop-design.md`

## Global Constraints

- **Comments are sparse.** The spec is the commentary. Comment only where the code alone would mislead — a non-obvious ordering requirement, or a deliberate deviation from a nearby pattern. Do not restate the spec in the code. This overrides the surrounding files' existing heavy comment density; do not match it.
- **Never open a reward canvas directly.** Every modal goes through `RewardQueue.Instance.Enqueue`. Callers of a self-enqueueing method must not wrap it in their own `Enqueue`.
- **Never hand-edit scene or prefab YAML.** Scene/asset wiring is the user's manual step (Task 5).
- **Spend influence through `Player.Influence(int)`**, not a serialized `IntEvent`. There are two influence `IntEvent`s and only `AdjustPlayerInfluence` deducts; `GetCurrentInfluence` only rebroadcasts to the HUD.
- **The Unity editor is open and holds the project lock**, so `-runTests` in batch mode will not work. Verification is the two CLI harnesses built in Task 1.
- Unity editor path: `C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data`.
- New `.cs` files have no `.meta` until Unity next refreshes. Commit the `.cs` immediately; commit the `.meta` whenever it appears.

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Scripts/Rewards/ShopRules.cs` (create) | Pure unique-selection. Generic, Unity-free, rng injected. |
| `Assets/Tests/EditMode/ShopRulesTests.cs` (create) | `string`-typed tests for the above. |
| `Assets/Scripts/GameScriptableObjectTypes/TownsSO.cs` (modify) | Adds `purchasableCards`. |
| `Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs` (modify) | Adds `OfferTownCards`; `OfferCardChoice` adopts the unique picker. |
| `Assets/Scripts/Places/PlaceAction.cs` (modify) | `TownActionSnapshot` gains `SellsCards` / `CardCost`. |
| `Assets/Scripts/Places/PlaceActionRules.cs` (modify) | Cards slot gates on the list, not the place type; stops being a stub. |
| `Assets/Scripts/Places/PlaceRules.cs` (modify) | Castle stops granting `PlaceService.Cards`. |
| `Assets/Tests/EditMode/PlaceActionRulesTests.cs` (modify) | Cards-slot coverage. |
| `Assets/Tests/EditMode/PlaceRulesTests.cs` (modify) | Castle no longer reports Cards. |
| `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs` (modify) | `BuyCards()`, the one purchase path; fan snapshot + dispatch. |
| `Assets/Scripts/GameObjectScripts/TownMenuScripts/CardButton.cs` (modify) | Canvas route; delegates to `BuyCards()`. |

---

### Task 1: `ShopRules.PickUnique` + the CLI test harnesses

**Files:**
- Create: `Assets/Scripts/Rewards/ShopRules.cs`
- Test: `Assets/Tests/EditMode/ShopRulesTests.cs`
- Scratch (not committed): `<scratchpad>/Runner.cs`, `<scratchpad>/CompileCheck.ps1`

`<scratchpad>` below means your session's scratchpad directory. Set `$SP` to it once at the start of each PowerShell block and reuse it; the harness files are built once here and re-run by later tasks.

**Interfaces:**
- Consumes: nothing.
- Produces: `public static List<T> ShopRules.PickUnique<T>(IReadOnlyList<T> pool, int count, Func<int,int> rng) where T : class` — returns at most `count` distinct non-null entries in draw order; fewer when the distinct pool is smaller; empty for a null/empty pool or `count <= 0`. `rng(exclusiveMax)` must return `[0, exclusiveMax)`.

- [ ] **Step 1: Build the two CLI harnesses**

Write `<scratchpad>/Runner.cs` — a reflection runner for `[Test]` methods:

```csharp
using System;
using System.Reflection;

public static class Runner
{
    public static int Main(string[] args)
    {
        int pass = 0, fail = 0;
        foreach (var path in args)
        {
            var asm = Assembly.LoadFrom(path);
            foreach (var type in asm.GetTypes())
            {
                if (type.IsAbstract || !type.IsClass) continue;
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    bool isTest = false;
                    foreach (var a in m.GetCustomAttributes(true))
                        if (a.GetType().Name == "TestAttribute") isTest = true;
                    if (!isTest) continue;

                    try
                    {
                        m.Invoke(Activator.CreateInstance(type), null);
                        pass++;
                        Console.WriteLine("PASS " + type.Name + "." + m.Name);
                    }
                    catch (Exception e)
                    {
                        fail++;
                        var inner = e.InnerException ?? e;
                        Console.WriteLine("FAIL " + type.Name + "." + m.Name + " : " + inner.Message);
                    }
                }
            }
        }
        Console.WriteLine(pass + " passed, " + fail + " failed");
        return fail == 0 ? 0 : 1;
    }
}
```

Write `<scratchpad>/CompileCheck.ps1` — compile-checks Unity assemblies while the editor holds the lock:

```powershell
param(
    [Parameter(Mandatory = $true)][string]$RepoRoot,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)][string[]]$Assemblies
)

$ErrorActionPreference = 'Stop'
$editor = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data"
$dotnet = Join-Path $editor "NetCoreRuntime\dotnet.exe"
$csc = Join-Path $editor "DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll"

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

function Decode([string]$s) {
    $s.Replace('&apos;', "'").Replace('&quot;', '"').Replace('&amp;', '&').Replace('&lt;', '<').Replace('&gt;', '>')
}

$failed = $false
foreach ($asm in $Assemblies) {
    $proj = Join-Path $RepoRoot "$asm.csproj"
    if (-not (Test-Path $proj)) { Write-Host "MISSING PROJECT $proj"; $failed = $true; continue }
    $xml = Get-Content $proj -Raw

    $sources = @()
    foreach ($m in [regex]::Matches($xml, '<Compile Include="([^"]+)"')) {
        $p = Decode $m.Groups[1].Value
        if (-not [System.IO.Path]::IsPathRooted($p)) { $p = Join-Path $RepoRoot $p }
        $sources += $p
    }

    $refs = @()
    foreach ($m in [regex]::Matches($xml, '<HintPath>([^<]+)</HintPath>')) {
        $refs += (Decode $m.Groups[1].Value)
    }
    foreach ($m in [regex]::Matches($xml, '<ProjectReference Include="([^"]+)"')) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension((Decode $m.Groups[1].Value))
        $fresh = Join-Path $OutDir "$name.dll"
        if (Test-Path $fresh) { $refs += $fresh }
        else { $refs += (Join-Path $RepoRoot "Library\ScriptAssemblies\$name.dll") }
    }

    $rsp = Join-Path $OutDir "$asm.rsp"
    $lines = @('-nostdlib+', '-noconfig', '-target:library', '-langversion:9.0', '-define:UNITY_EDITOR', '-nowarn:0169,0414,0649')
    $lines += "-out:`"$(Join-Path $OutDir "$asm.dll")`""
    foreach ($r in $refs) { $lines += "-r:`"$r`"" }
    foreach ($s in $sources) { $lines += "`"$s`"" }
    Set-Content -Path $rsp -Value $lines -Encoding utf8

    Write-Host "=== $asm ($($sources.Count) sources, $($refs.Count) refs) ==="
    & $dotnet $csc "@$rsp"
    if ($LASTEXITCODE -ne 0) { $failed = $true; Write-Host "COMPILE FAILED: $asm" }
}

if ($failed) { Write-Host "RESULT: FAILED"; exit 1 }
Write-Host "RESULT: OK"
exit 0
```

Then build `Runner.exe` and stage nunit next to it:

```powershell
$SP = "<scratchpad>"
$repo = "C:\Users\Dave's Comp\source\repos\Archon's Rise"
$mbe = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge"
$mono = "$mbe\bin\mono.exe"; $mcs = "$mbe\lib\mono\4.5\mcs.exe"
Copy-Item "$repo\Library\PackageCache\com.unity.ext.nunit@44f7d31723bd\net472\unity-custom\nunit.framework.dll" "$SP\nunit.framework.dll" -Force
& $mono $mcs -nologo "-out:$SP\Runner.exe" "$SP\Runner.cs"
```

Expected: no output, exit 0, and `$SP\Runner.exe` exists.

Never invoke `mcs.bat` (it breaks on the spaces in `C:\Program Files\...`), and never run the bare `Runner.exe` (the mcs build links Mono's mscorlib — it must run under `mono.exe`).

- [ ] **Step 2: Write the failing test**

Create `Assets/Tests/EditMode/ShopRulesTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ShopRulesTests
{
    static Func<int, int> Seq(params int[] values)
    {
        int i = 0;
        return _ => values[Math.Min(i++, values.Length - 1)];
    }

    static List<string> Pool(params string[] entries) => new List<string>(entries);

    [Test]
    public void PicksRequestedCount()
    {
        var picked = ShopRules.PickUnique(Pool("A", "B", "C", "D"), 3, _ => 0);
        Assert.AreEqual(3, picked.Count);
    }

    [Test]
    public void ZeroRngWalksThePoolInOrder()
    {
        var picked = ShopRules.PickUnique(Pool("A", "B", "C", "D"), 3, _ => 0);
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, picked);
    }

    [Test]
    public void ScriptedRngProducesADeterministicSelection()
    {
        var picked = ShopRules.PickUnique(Pool("A", "B", "C", "D"), 3, Seq(3, 0, 0));
        CollectionAssert.AreEqual(new[] { "D", "B", "C" }, picked);
    }

    [Test]
    public void DuplicateEntriesAreCollapsed()
    {
        var picked = ShopRules.PickUnique(Pool("A", "A", "B"), 3, _ => 0);
        CollectionAssert.AreEqual(new[] { "A", "B" }, picked);
    }

    [Test]
    public void NeverRepeatsAnEntryAcrossManyRolls()
    {
        var rand = new System.Random(20260810);
        var pool = Pool("A", "B", "A", "C", "B", "D", "E");
        for (int i = 0; i < 200; i++)
        {
            var picked = ShopRules.PickUnique(pool, 3, max => rand.Next(max));
            Assert.AreEqual(3, picked.Count);
            CollectionAssert.AllItemsAreUnique(picked);
        }
    }

    [Test]
    public void ShortPoolReturnsWhatItHas()
    {
        var picked = ShopRules.PickUnique(Pool("A", "B"), 3, _ => 0);
        Assert.AreEqual(2, picked.Count);
    }

    [Test]
    public void NullEntriesAreDropped()
    {
        var picked = ShopRules.PickUnique(Pool("A", null, "B"), 3, _ => 0);
        CollectionAssert.AreEqual(new[] { "A", "B" }, picked);
    }

    [Test]
    public void EmptyPoolReturnsEmpty()
    {
        Assert.AreEqual(0, ShopRules.PickUnique(Pool(), 3, _ => 0).Count);
    }

    [Test]
    public void NullPoolReturnsEmpty()
    {
        Assert.AreEqual(0, ShopRules.PickUnique<string>(null, 3, _ => 0).Count);
    }

    [Test]
    public void ZeroCountReturnsEmpty()
    {
        Assert.AreEqual(0, ShopRules.PickUnique(Pool("A", "B"), 0, _ => 0).Count);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

```powershell
$SP = "<scratchpad>"
$repo = "C:\Users\Dave's Comp\source\repos\Archon's Rise"
$mbe = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge"
$mono = "$mbe\bin\mono.exe"; $mcs = "$mbe\lib\mono\4.5\mcs.exe"
& $mono $mcs -nologo -target:library "-out:$SP\Shop.dll" "-r:$SP\nunit.framework.dll" "$repo\Assets\Scripts\Rewards\ShopRules.cs" "$repo\Assets\Tests\EditMode\ShopRulesTests.cs"
```

Expected: FAIL to compile — `error CS2001: Source file '...ShopRules.cs' could not be found.`

- [ ] **Step 4: Write the implementation**

Create `Assets/Scripts/Rewards/ShopRules.cs`:

```csharp
using System;
using System.Collections.Generic;

public static class ShopRules
{
    public static List<T> PickUnique<T>(IReadOnlyList<T> pool, int count, Func<int, int> rng)
        where T : class
    {
        var picked = new List<T>();
        if (pool == null || count <= 0) return picked;

        var distinct = new List<T>();
        for (int i = 0; i < pool.Count; i++)
        {
            var entry = pool[i];
            if (entry == null) continue;
            if (!Contains(distinct, entry)) distinct.Add(entry);
        }

        int take = Math.Min(count, distinct.Count);
        for (int i = 0; i < take; i++)
        {
            int j = i + rng(distinct.Count - i);
            var swap = distinct[j];
            distinct[j] = distinct[i];
            distinct[i] = swap;
            picked.Add(swap);
        }
        return picked;
    }

    static bool Contains<T>(List<T> list, T entry) where T : class
    {
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], entry)) return true;
        return false;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```powershell
$SP = "<scratchpad>"
$repo = "C:\Users\Dave's Comp\source\repos\Archon's Rise"
$mbe = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge"
$mono = "$mbe\bin\mono.exe"; $mcs = "$mbe\lib\mono\4.5\mcs.exe"
& $mono $mcs -nologo -target:library "-out:$SP\Shop.dll" "-r:$SP\nunit.framework.dll" "$repo\Assets\Scripts\Rewards\ShopRules.cs" "$repo\Assets\Tests\EditMode\ShopRulesTests.cs"
if ($?) { & $mono "$SP\Runner.exe" "$SP\Shop.dll" }
```

Expected: 10 `PASS` lines and `10 passed, 0 failed`.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Rewards/ShopRules.cs" "Assets/Tests/EditMode/ShopRulesTests.cs"
git commit -m "feat: add ShopRules.PickUnique for unique card offers"
```

---

### Task 2: `purchasableCards` + the offer path

**Files:**
- Modify: `Assets/Scripts/GameScriptableObjectTypes/TownsSO.cs:29`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs:89-104`

**Interfaces:**
- Consumes: `ShopRules.PickUnique<T>(IReadOnlyList<T>, int, Func<int,int>)` from Task 1.
- Produces: `public List<CardsSO> TownsSO.purchasableCards` (field, initialized to an empty list) and `public void Rewards.OfferTownCards(TownsSO town)` (self-enqueueing on `RewardQueue`; never wrap it in another `Enqueue`).

There is no EditMode test in this task: `Rewards` is a `MonoBehaviour` and `TownsSO` a `ScriptableObject`, neither reachable from the pure harness. The gate is the Roslyn compile-check plus Task 5's in-editor play test.

- [ ] **Step 1: Add the field to `TownsSO`**

In `Assets/Scripts/GameScriptableObjectTypes/TownsSO.cs`, add below `public List<UnitsSO> recruitableUnits;`:

```csharp
    // Non-empty = this place sells cards, at cardLevel influence per purchase.
    public List<CardsSO> purchasableCards = new List<CardsSO>();
```

- [ ] **Step 2: Replace the with-replacement sampling in `OfferCardChoice`**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs`, replace these lines inside the `RewardQueue.Instance.Enqueue` lambda:

```csharp
            var candidates = new List<CardsSO>();
            for (int i = 0; i < 3; i++)
                candidates.Add(pool[Random.Range(0, pool.Count)]);
```

with:

```csharp
            var candidates = ShopRules.PickUnique(pool, 3, max => Random.Range(0, max));
```

`pool` is the `tuning.CardPool(tier)` local already declared above the `Enqueue`. Leave the `RewardQueue`/`rewardCanvas.Offer` structure and the `onClosed` handling exactly as they are.

- [ ] **Step 3: Add `OfferTownCards`**

In the same file, add immediately after `OfferCardChoice`:

```csharp
    // Pay-then-pick: the caller has already charged the purchase, so a skip forfeits it.
    public void OfferTownCards(TownsSO town)
    {
        if (town == null) return;
        var pool = town.purchasableCards;
        if (pool == null || pool.Count == 0) return;

        RewardQueue.Instance.Enqueue(done =>
        {
            var candidates = ShopRules.PickUnique(pool, 3, max => Random.Range(0, max));
            rewardCanvas.Offer(candidates,
                so => { deck.AddCard(so, toTop: true); done(); },
                () => done());
        });
    }
```

- [ ] **Step 4: Compile-check the changed assemblies**

```powershell
$SP = "<scratchpad>"
$repo = "C:\Users\Dave's Comp\source\repos\Archon's Rise"
& "$SP\CompileCheck.ps1" $repo "$SP\out" ArchonsRise.Enums ArchonsRise.UiLanguage ArchonsRise.Places ArchonsRise.Rewards Assembly-CSharp
```

Expected: `RESULT: OK` on the last line, no `error CS` lines. Warnings (`CS2023`, `CS1701`, `CS0618` from `ShrineTracker.cs`) are pre-existing and expected.

If the run reports `CS0103: The name 'ShopRules' does not exist`, it means Unity has not refreshed and `ArchonsRise.Rewards.csproj` does not list the new file yet. Append it to that assembly's response file and re-run just that compile:

```powershell
$SP = "<scratchpad>"
$repo = "C:\Users\Dave's Comp\source\repos\Archon's Rise"
$editor = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data"
Add-Content -Path "$SP\out\ArchonsRise.Rewards.rsp" -Value "`"$repo\Assets\Scripts\Rewards\ShopRules.cs`"" -Encoding utf8
& "$editor\NetCoreRuntime\dotnet.exe" "$editor\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll" "@$SP\out\ArchonsRise.Rewards.rsp"
& "$SP\CompileCheck.ps1" $repo "$SP\out" Assembly-CSharp
```

Expected: `RESULT: OK`.

- [ ] **Step 5: Re-run the Task 1 tests to confirm nothing regressed**

```powershell
$SP = "<scratchpad>"
$repo = "C:\Users\Dave's Comp\source\repos\Archon's Rise"
$mbe = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge"
$mono = "$mbe\bin\mono.exe"; $mcs = "$mbe\lib\mono\4.5\mcs.exe"
& $mono $mcs -nologo -target:library "-out:$SP\Shop.dll" "-r:$SP\nunit.framework.dll" "$repo\Assets\Scripts\Rewards\ShopRules.cs" "$repo\Assets\Tests\EditMode\ShopRulesTests.cs"
if ($?) { & $mono "$SP\Runner.exe" "$SP\Shop.dll" }
```

Expected: `10 passed, 0 failed`.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/GameScriptableObjectTypes/TownsSO.cs" "Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs"
git commit -m "feat: offer unique town card purchases through the reward canvas"
```

---

### Task 3: The Cards slot gates on the card list

**Files:**
- Modify: `Assets/Scripts/Places/PlaceAction.cs:24-53`
- Modify: `Assets/Scripts/Places/PlaceActionRules.cs:35-37`
- Modify: `Assets/Scripts/Places/PlaceRules.cs:15`
- Test: `Assets/Tests/EditMode/PlaceActionRulesTests.cs`
- Test: `Assets/Tests/EditMode/PlaceRulesTests.cs:19-24`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `TownActionSnapshot(PlaceType placeType, bool conquered, int guardiansRemaining, int influence, int healCost, int crystalCost, bool sellsCards, int cardCost, bool anyUnitAffordable, bool visitCanAct, bool hasMenu)` — the two new parameters sit between `crystalCost` and `anyUnitAffordable`. Task 4's `TownToken.BuildActions` must pass them by name.

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/EditMode/PlaceActionRulesTests.cs`, replace the `Town` helper at the top with:

```csharp
    // A conquered Town with everything affordable and the action unspent.
    static TownActionSnapshot Town(PlaceType type = PlaceType.Town, bool conquered = true,
        int guardiansRemaining = 0, int influence = 99, int healCost = 3, int crystalCost = 2,
        bool sellsCards = false, int cardCost = 5,
        bool anyUnitAffordable = true, bool visitCanAct = true, bool hasMenu = true)
        => new TownActionSnapshot(type, conquered, guardiansRemaining, influence, healCost,
            crystalCost, sellsCards, cardCost, anyUnitAffordable, visitCanAct, hasMenu);
```

Replace the whole `ConqueredCastle_IncludesCardsButDisabled` test with these six:

```csharp
    [Test]
    public void SellingTown_ShowsCardsBetweenHealAndCrystal()
    {
        var actions = PlaceActionRules.ForTown(Town(sellsCards: true));
        Assert.AreEqual(5, actions.Count);
        Assert.AreEqual(PlaceActionId.Recruit, actions[0].Id);
        Assert.AreEqual(PlaceActionId.Heal, actions[1].Id);
        Assert.AreEqual(PlaceActionId.Cards, actions[2].Id);
        Assert.AreEqual(PlaceActionId.Crystal, actions[3].Id);
        Assert.AreEqual(PlaceActionId.OpenMenu, actions[4].Id);
    }

    [Test]
    public void CardsShowsItsInfluenceCostAndUnlocksWhenAffordable()
    {
        var actions = PlaceActionRules.ForTown(Town(sellsCards: true, influence: 5, cardCost: 5));
        var cards = actions.Find(a => a.Id == PlaceActionId.Cards);
        Assert.AreEqual(IconConcept.Card, cards.Icon);
        Assert.AreEqual(IconConcept.Influence, cards.CostIcon);
        Assert.AreEqual(5, cards.CostAmount);
        Assert.IsTrue(cards.Enabled);
    }

    [Test]
    public void CardsLocksBelowItsCost()
    {
        var actions = PlaceActionRules.ForTown(Town(sellsCards: true, influence: 4, cardCost: 5));
        Assert.IsFalse(actions.Find(a => a.Id == PlaceActionId.Cards).Enabled);
    }

    [Test]
    public void CardsLocksOnceTheActionIsSpent()
    {
        var actions = PlaceActionRules.ForTown(Town(sellsCards: true, visitCanAct: false));
        Assert.IsFalse(actions.Find(a => a.Id == PlaceActionId.Cards).Enabled);
    }

    [Test]
    public void NoCardList_NoCardsSlot()
    {
        foreach (PlaceType type in System.Enum.GetValues(typeof(PlaceType)))
        {
            var actions = PlaceActionRules.ForTown(Town(type, sellsCards: false));
            Assert.AreEqual(0, actions.FindAll(a => a.Id == PlaceActionId.Cards).Count,
                type + " must not offer Cards without a card list");
        }
    }

    [Test]
    public void AnyPlaceTypeWithAListSellsCards()
    {
        foreach (PlaceType type in System.Enum.GetValues(typeof(PlaceType)))
        {
            var actions = PlaceActionRules.ForTown(Town(type, sellsCards: true));
            Assert.AreEqual(1, actions.FindAll(a => a.Id == PlaceActionId.Cards).Count,
                type + " must offer Cards when it has a card list");
        }
    }
```

In the same file, change `ActionSpent_ServicesLockButMenuStaysOpen` so it covers the Cards slot too — replace its first line:

```csharp
        var actions = PlaceActionRules.ForTown(Town(visitCanAct: false));
```

with:

```csharp
        var actions = PlaceActionRules.ForTown(Town(sellsCards: true, visitCanAct: false));
```

In `Assets/Tests/EditMode/PlaceRulesTests.cs`, replace `AllowedServices_Castle_RecruitHealCardsCrystal` with:

```csharp
    [Test]
    public void AllowedServices_Castle_RecruitHealCrystal()
    {
        Assert.AreEqual(PlaceService.Recruit | PlaceService.Heal | PlaceService.Crystal,
            PlaceRules.AllowedServices(PlaceType.Castle));
    }

    [Test]
    public void AllowedServices_NoPlaceTypeGrantsCards()
    {
        foreach (PlaceType type in System.Enum.GetValues(typeof(PlaceType)))
            Assert.IsFalse(PlaceRules.AllowedServices(type).HasFlag(PlaceService.Cards),
                $"{type} must not gate Cards — the card list does");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```powershell
$SP = "<scratchpad>"
$repo = "C:\Users\Dave's Comp\source\repos\Archon's Rise"
$mbe = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge"
$mono = "$mbe\bin\mono.exe"; $mcs = "$mbe\lib\mono\4.5\mcs.exe"
$P = "$repo\Assets\Scripts\Places"; $T = "$repo\Assets\Tests\EditMode"
& $mono $mcs -nologo -target:library "-out:$SP\Places.dll" "-r:$SP\nunit.framework.dll" `
  "$P\PlaceRules.cs" "$P\PlaceType.cs" "$P\PlaceService.cs" "$P\PlaceAction.cs" "$P\PlaceActionId.cs" "$P\PlaceActionRules.cs" `
  "$repo\Assets\Scripts\UiLanguage\IconConcept.cs" "$T\PlaceRulesTests.cs" "$T\PlaceActionRulesTests.cs"
if ($?) { & $mono "$SP\Runner.exe" "$SP\Places.dll" }
```

Expected: FAIL to compile — `error CS1729: 'TownActionSnapshot' does not contain a constructor that takes 11 arguments`.

- [ ] **Step 3: Add the snapshot fields**

In `Assets/Scripts/Places/PlaceAction.cs`, inside `TownActionSnapshot`, add after the `CrystalCost` field:

```csharp
    public readonly bool SellsCards;
    public readonly int CardCost;
```

and replace the constructor with:

```csharp
    public TownActionSnapshot(PlaceType placeType, bool conquered, int guardiansRemaining,
        int influence, int healCost, int crystalCost, bool sellsCards, int cardCost,
        bool anyUnitAffordable, bool visitCanAct, bool hasMenu)
    {
        PlaceType = placeType;
        Conquered = conquered;
        GuardiansRemaining = guardiansRemaining;
        Influence = influence;
        HealCost = healCost;
        CrystalCost = crystalCost;
        SellsCards = sellsCards;
        CardCost = cardCost;
        AnyUnitAffordable = anyUnitAffordable;
        VisitCanAct = visitCanAct;
        HasMenu = hasMenu;
    }
```

- [ ] **Step 4: Gate the Cards slot on the list**

In `Assets/Scripts/Places/PlaceActionRules.cs`, replace this block (it keeps its position between Heal and Crystal):

```csharp
            // M2 stub: the slot is shown so the place reports itself honestly,
            // but buying is disabled until the purchase economics land.
            if ((allowed & PlaceService.Cards) != 0)
                list.Add(new PlaceAction(PlaceActionId.Cards, IconConcept.Card,
                    null, 0, false));
```

with:

```csharp
            if (s.SellsCards)
                list.Add(new PlaceAction(PlaceActionId.Cards, IconConcept.Card,
                    IconConcept.Influence, s.CardCost,
                    s.Influence >= s.CardCost && s.VisitCanAct));
```

In `Assets/Scripts/Places/PlaceRules.cs`, replace the Castle line:

```csharp
            case PlaceType.Castle: return PlaceService.Recruit | PlaceService.Heal | PlaceService.Cards | PlaceService.Crystal;
```

with:

```csharp
            case PlaceType.Castle: return PlaceService.Recruit | PlaceService.Heal | PlaceService.Crystal;
```

Leave the `PlaceService.Cards` enum member in place — removing it would renumber the flags.

- [ ] **Step 5: Run the tests to verify they pass**

```powershell
$SP = "<scratchpad>"
$repo = "C:\Users\Dave's Comp\source\repos\Archon's Rise"
$mbe = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge"
$mono = "$mbe\bin\mono.exe"; $mcs = "$mbe\lib\mono\4.5\mcs.exe"
$P = "$repo\Assets\Scripts\Places"; $T = "$repo\Assets\Tests\EditMode"
& $mono $mcs -nologo -target:library "-out:$SP\Places.dll" "-r:$SP\nunit.framework.dll" `
  "$P\PlaceRules.cs" "$P\PlaceType.cs" "$P\PlaceService.cs" "$P\PlaceAction.cs" "$P\PlaceActionId.cs" "$P\PlaceActionRules.cs" `
  "$repo\Assets\Scripts\UiLanguage\IconConcept.cs" "$T\PlaceRulesTests.cs" "$T\PlaceActionRulesTests.cs"
if ($?) { & $mono "$SP\Runner.exe" "$SP\Places.dll" }
```

Expected: every line `PASS`, ending `30 passed, 0 failed` (8 `PlaceRulesTests` + 22 `PlaceActionRulesTests`). `Assembly-CSharp` will not compile until Task 4 updates `TownToken` — that is expected here and is not a reason to change this task.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Places/PlaceAction.cs" "Assets/Scripts/Places/PlaceActionRules.cs" "Assets/Scripts/Places/PlaceRules.cs" "Assets/Tests/EditMode/PlaceActionRulesTests.cs" "Assets/Tests/EditMode/PlaceRulesTests.cs"
git commit -m "feat: gate the Cards slot on a place's card list"
```

---

### Task 4: `BuyCards()` and both UI routes

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs:40-103`
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/CardButton.cs`

**Interfaces:**
- Consumes: `TownsSO.purchasableCards`, `Rewards.OfferTownCards(TownsSO)` (Task 2); the 11-argument `TownActionSnapshot` constructor (Task 3).
- Produces: `public void TownToken.BuyCards()` — spends `townSO.cardLevel` influence, commits the visit action, then opens the picker. Both UI routes call it and nothing else.

Both files are `MonoBehaviour`s, so the gate is the Roslyn compile-check plus Task 5's play test.

- [ ] **Step 1: Feed the snapshot's new fields**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs`, inside `BuildActions`, add these two named arguments to the `TownActionSnapshot` construction, between `crystalCost:` and `anyUnitAffordable:`:

```csharp
            sellsCards: townSO.purchasableCards != null && townSO.purchasableCards.Count > 0,
            cardCost: townSO.cardLevel,
```

- [ ] **Step 2: Add `BuyCards` and dispatch to it**

In the same file, replace the stub case:

```csharp
            case PlaceActionId.Cards:
                break; // M2 stub: the slot renders locked and does nothing
```

with:

```csharp
            case PlaceActionId.Cards:
                BuyCards();
                break;
```

and add this method next to `OpenCrystalPopout`:

```csharp
    // The charge and the turn's action land BEFORE the picker opens, so skipping
    // the pick forfeits the influence rather than refunding it.
    public void BuyCards()
    {
        if (PlayerStats != null) PlayerStats.Influence(townSO.cardLevel);
        if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
        var rewards = FindAnyObjectByType<Rewards>(FindObjectsInactive.Include);
        if (rewards != null) rewards.OfferTownCards(townSO);
    }
```

- [ ] **Step 3: Replace the `CardButton` stub**

Replace the entire contents of `Assets/Scripts/GameObjectScripts/TownMenuScripts/CardButton.cs` with:

```csharp
using UnityEngine;

public class CardButton : TownButtons
{
    private void Update()
    {
        if (_town is not null)
        {
            if (currentPlayerInfluence < _town.townSO.cardLevel || !CanActThisVisit)
                thisButton.interactable = false;
            SyncLock();
        }
    }

    public override void UpdateButtonText()
    {
        if (_town is null) return;

        buttonText.text =
            $"{IconMarkup.Tag(IconConcept.Card)} Cards — {IconMarkup.Cost(IconConcept.Influence, _town.townSO.cardLevel)}";
        bool sells = _town.townSO.purchasableCards != null && _town.townSO.purchasableCards.Count > 0;
        bool open = ConquestTracker.Instance.IsConquered(_town.gridPos);
        if (sells && open)
        {
            thisButton.gameObject.SetActive(true);
            thisButton.interactable = currentPlayerInfluence >= _town.townSO.cardLevel;
            thisButton.onClick.RemoveAllListeners();
            thisButton.onClick.AddListener(() => _town.BuyCards());
        }
        else
        {
            thisButton.gameObject.SetActive(false);
        }
        SyncLock();
    }
}
```

- [ ] **Step 4: Compile-check every affected assembly**

```powershell
$SP = "<scratchpad>"
$repo = "C:\Users\Dave's Comp\source\repos\Archon's Rise"
& "$SP\CompileCheck.ps1" $repo "$SP\out" ArchonsRise.Enums ArchonsRise.UiLanguage ArchonsRise.Places ArchonsRise.Rewards Assembly-CSharp Assembly-CSharp-Editor
```

Expected: `RESULT: OK`, no `error CS` lines. Pre-existing warnings (`CS2023`, `CS1701`, `CS0618` from `ShrineTracker.cs`) are fine.

- [ ] **Step 5: Re-run every pure test**

```powershell
$SP = "<scratchpad>"
$repo = "C:\Users\Dave's Comp\source\repos\Archon's Rise"
$mbe = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge"
$mono = "$mbe\bin\mono.exe"; $mcs = "$mbe\lib\mono\4.5\mcs.exe"
$P = "$repo\Assets\Scripts\Places"; $T = "$repo\Assets\Tests\EditMode"
& $mono $mcs -nologo -target:library "-out:$SP\Shop.dll" "-r:$SP\nunit.framework.dll" "$repo\Assets\Scripts\Rewards\ShopRules.cs" "$T\ShopRulesTests.cs"
if ($?) { & $mono $mcs -nologo -target:library "-out:$SP\Places.dll" "-r:$SP\nunit.framework.dll" `
  "$P\PlaceRules.cs" "$P\PlaceType.cs" "$P\PlaceService.cs" "$P\PlaceAction.cs" "$P\PlaceActionId.cs" "$P\PlaceActionRules.cs" `
  "$repo\Assets\Scripts\UiLanguage\IconConcept.cs" "$T\PlaceRulesTests.cs" "$T\PlaceActionRulesTests.cs" }
if ($?) { & $mono "$SP\Runner.exe" "$SP\Shop.dll" "$SP\Places.dll" }
```

Expected: `40 passed, 0 failed` (10 `ShopRulesTests` + 8 `PlaceRulesTests` + 22 `PlaceActionRulesTests`).

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs" "Assets/Scripts/GameObjectScripts/TownMenuScripts/CardButton.cs"
git commit -m "feat: wire the town card shop to the place fan and town menu"
```

---

### Task 5: Author the shops and verify in the editor

**Files:**
- Modify (by hand, in the Unity editor): `Assets/Scripts/ScriptableObjectData/Non-Player/Towns/*.asset`

**Interfaces:**
- Consumes: everything from Tasks 1–4.
- Produces: no code. A playable card shop and a verified end-to-end flow.

This task is the user's, done in the Unity editor. Do not hand-edit the `.asset` YAML — hand these instructions over and wait.

- [ ] **Step 1: Hand the user the authoring instructions**

> The code is in. Unity will recompile when you focus the editor — the Console should be clean.
>
> 1. Pick the places that should sell cards. In the Project window, open
>    `Assets/Scripts/ScriptableObjectData/Non-Player/Towns/` and select an asset
>    (e.g. `CastleBrune`).
> 2. In the Inspector there is a new **Purchasable Cards** list. Set its size and drag
>    `CardsSO` assets into the slots. Three or more gives a full picker; one or two
>    still works and shows that many options.
> 3. Check **Card Level** on the same asset — that is now the influence price per
>    purchase. Currently authored: CastleBrune 5, Garth Barracks 6, SirensGateKeep 5,
>    Rags Town 7, **Merchant Village 0** (a 0 there means a free shop).
> 4. Save the project (Ctrl+S).
>
> Then tell me the Console is clean and which places you set up, and walk through the
> play test below.

- [ ] **Step 2: Play test — the fan route**

Ask the user to confirm each of these, and report any that fail:

1. Stand on a conquered card-selling place. The fan shows a **card** icon with an influence cost badge, sitting between Heal and Crystal.
2. With influence below the price, the slot is dimmed and unclickable.
3. With enough influence, pressing it deducts exactly the price from the HUD influence readout and opens the card picker with **three different cards**, all drawn from that place's list.
4. Picking a card adds it to the deck (it draws next) and closes the picker.
5. Re-entering the place the same turn: every service slot is now dimmed — the visit's action was spent by the purchase.

- [ ] **Step 3: Play test — the skip, the canvas route, and the non-sellers**

1. Buy again on a later turn and press **Skip** on the picker. Influence stays spent — no refund — and the picker closes cleanly.
2. Open the place's full menu (the ledger slot) and confirm the **Cards** button appears there too, showing the same icon-and-cost label, and that pressing it does exactly what the fan slot does.
3. Stand on a place with an empty `purchasableCards` list — including a **Castle**, which used to show a locked "Cards (soon)" slot. Neither the fan nor the town menu shows a Cards entry at all.
4. Kill an enemy that grants a card reward and confirm the three offered cards are all different.

- [ ] **Step 4: Commit the authored assets and any generated `.meta` files**

```bash
git status --short
git add "Assets/Scripts/ScriptableObjectData/Non-Player/Towns" "Assets/Scripts/Rewards/ShopRules.cs.meta" "Assets/Tests/EditMode/ShopRulesTests.cs.meta"
git commit -m "content: stock the card-selling places"
```

If either `.meta` path does not exist yet, drop it from the `git add` — Unity writes it on the next refresh, and it can ride along with a later commit.

---

## Verification Summary

| Gate | Command | Expected |
|---|---|---|
| Pure rules | the Task 4 Step 5 block | `39 passed, 0 failed` |
| Compilation | `CompileCheck.ps1 ... Assembly-CSharp Assembly-CSharp-Editor` | `RESULT: OK` |
| Behavior | Task 5 Steps 2–3 | every numbered item confirmed by the user |
