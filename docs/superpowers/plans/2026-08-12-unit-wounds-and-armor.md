# Unit Wounds and Armor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the player commit units to soak the Defend-phase counterattack, taking a wound in their stead, and rework healing through the unit picker.

**Architecture:** A unit's `armorClass` is summed into `defendLeft` before the existing `EnemyTraitRules` pipeline runs, so Swift's cap, Brutal's surcharge, Warlord's aura and per-enemy blocking are all untouched. Leech and Toxic are the only two exceptions. `UnitPickerPanel` gains two new modes (wound, heal) beside the existing refresh mode. Unit wound state persists via a new `int[]` in the save at schema v14.

**Tech Stack:** Unity 6000.5.1f1, C#, NUnit (EditMode), mcs/mono CLI pure-test harness.

**Spec:** [`docs/superpowers/specs/2026-08-12-unit-wounds-and-armor-design.md`](../specs/2026-08-12-unit-wounds-and-armor-design.md)

## Global Constraints

- **Pure rules are Unity-free** so they run in the mcs CLI harness. Anything under `Assets/Scripts/CardPlay/`, `Assets/Scripts/UnitPlay/`, `Assets/Scripts/Places/` and `Assets/Scripts/SaveData/` must not reference `UnityEngine` types unless it already does.
- **No new asmdefs.** Every new pure class goes in a folder that already has one (`ArchonsRise.CardPlay`, `ArchonsRise.UnitPlay`, `ArchonsRise.Places`, `ArchonsRise.SaveData`).
- **Never hand-roll a `<sprite=…>` literal or a bare-number cost.** All glyphs and costs go through `IconMarkup`. No new icons are needed by this plan.
- **Locked / unaffordable = `UiLock.Apply(canvasGroup, true)`** (alpha 0.4) *on top of* `Button.interactable = false`.
- **Never hand-edit scene or prefab YAML.** Prefab/scene work is written as step-by-step instructions for the human to perform in the editor.
- **Unit wounds never count toward wound-out.** `PlayerHand.TotalWoundCount` is not modified by any task in this plan.
- **`WoundDestination` stays two-valued.** Units do not receive wound cards.

### Running tests

The Unity editor holds a lock that blocks batch-mode `-runTests`. Pure classes are verified with the CLI harness instead. **Run from the repo root:**

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources <comma-separated .cs paths>
```

Pass every source the test file needs to compile (the rule under test plus its dependencies plus the test file). MonoBehaviour changes cannot be tested this way; those tasks end with a compile check and a human verification step, and say so explicitly rather than pretending otherwise.

---

## Task 1: `HealRules` gains a "is there anything to heal" predicate

Pure, independent, and the foundation of the town-heal bug fix. Nothing else depends on it except Tasks 2 and 12.

**Files:**
- Modify: `Assets/Scripts/CardPlay/HealRules.cs`
- Test: `Assets/Tests/EditMode/HealRulesTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `HealRules.HealableCount(int handWounds, IReadOnlyList<int> unitWoundCounts) -> int` and `HealRules.CanHeal(int handWounds, IReadOnlyList<int> unitWoundCounts) -> bool`.

- [ ] **Step 1: Write the failing tests**

Append to `Assets/Tests/EditMode/HealRulesTests.cs`, inside the existing `HealRulesTests` class:

```csharp
    // A wounded unit is healable even when the hand is clean — that is the whole
    // point of the town-heal guard being rebuilt as part of this feature.
    [Test]
    public void HealableCount_SumsHandWoundsAndUnitWounds()
    {
        Assert.AreEqual(0, HealRules.HealableCount(0, new int[0]));
        Assert.AreEqual(4, HealRules.HealableCount(4, new int[0]));
        Assert.AreEqual(3, HealRules.HealableCount(0, new[] { 1, 2 }));
        Assert.AreEqual(7, HealRules.HealableCount(4, new[] { 1, 2 }));
    }

    // Healthy units contribute nothing; a null list is treated as "no army".
    [Test]
    public void HealableCount_IgnoresHealthyUnitsAndNullList()
    {
        Assert.AreEqual(0, HealRules.HealableCount(0, new[] { 0, 0, 0 }));
        Assert.AreEqual(2, HealRules.HealableCount(2, null));
    }

    [Test]
    public void CanHeal_IsTrueWhenAnythingIsWounded()
    {
        Assert.IsFalse(HealRules.CanHeal(0, new[] { 0, 0 }));
        Assert.IsTrue(HealRules.CanHeal(1, new[] { 0, 0 }));
        Assert.IsTrue(HealRules.CanHeal(0, new[] { 0, 1 }));
    }
```

Add `using System.Collections.Generic;` to the top of the file if it is not already there.

- [ ] **Step 2: Run tests to verify they fail**

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources Assets\Scripts\Enums\Enums\StatType.cs,Assets\Scripts\CardPlay\HealRules.cs,Assets\Tests\EditMode\HealRulesTests.cs
```

Expected: compile error `CS0117: 'HealRules' does not contain a definition for 'HealableCount'`.

- [ ] **Step 3: Implement**

Append inside `HealRules` in `Assets/Scripts/CardPlay/HealRules.cs`:

```csharp
    // How many wounds exist anywhere a heal can reach: the hand, plus every
    // wounded unit's wound count. Unit wounds are NOT part of wound-out
    // (PlayerHand.TotalWoundCount), but they ARE part of "is a heal worth
    // anything", which is what gates the town heal service.
    public static int HealableCount(int handWounds, IReadOnlyList<int> unitWoundCounts)
    {
        int total = handWounds > 0 ? handWounds : 0;
        if (unitWoundCounts != null)
            for (int i = 0; i < unitWoundCounts.Count; i++)
                if (unitWoundCounts[i] > 0) total += unitWoundCounts[i];
        return total;
    }

    public static bool CanHeal(int handWounds, IReadOnlyList<int> unitWoundCounts)
        => HealableCount(handWounds, unitWoundCounts) > 0;
```

Add `using System.Collections.Generic;` as the first line of the file.

- [ ] **Step 4: Run tests to verify they pass**

Same command as Step 2. Expected: all `HealRulesTests` pass, including the pre-existing `HealCount` tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CardPlay/HealRules.cs Assets/Tests/EditMode/HealRulesTests.cs
git commit -m "Add HealRules.HealableCount/CanHeal predicate"
```

---

## Task 2: Town heal slot gates on necessity, not just affordability

**Files:**
- Modify: `Assets/Scripts/Places/PlaceAction.cs:24-57` (`TownActionSnapshot`)
- Modify: `Assets/Scripts/Places/PlaceActionRules.cs:28-31`
- Test: `Assets/Tests/EditMode/PlaceActionRulesTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1 (this is the snapshot layer; the predicate is called by the caller in Task 3).
- Produces: `TownActionSnapshot` constructor gains an `anyWoundToHeal` parameter **after `anyUnitAffordable`**, giving the final order: `(placeType, conquered, guardiansRemaining, influence, healCost, crystalCost, sellsCards, cardCost, anyUnitAffordable, anyWoundToHeal, visitCanAct, hasMenu)`. New readonly field `AnyWoundToHeal`.

- [ ] **Step 1: Write the failing test**

Append to `Assets/Tests/EditMode/PlaceActionRulesTests.cs`, inside the test class. Note the helper below builds a full snapshot — check the existing file for a builder helper first and reuse it if one exists, adding the new argument to it instead of duplicating.

```csharp
    // The heal slot must gate on NECESSITY as well as affordability. Before this
    // change a player with a clean hand could pay the influence AND the turn's
    // action for nothing, unundoably.
    [Test]
    public void Town_HealSlotDisabled_WhenNothingIsWounded()
    {
        var s = new TownActionSnapshot(
            placeType: PlaceType.Town, conquered: true, guardiansRemaining: 0,
            influence: 10, healCost: 2, crystalCost: 2, sellsCards: false, cardCost: 0,
            anyUnitAffordable: true, anyWoundToHeal: false, visitCanAct: true, hasMenu: true);

        var heal = PlaceActionRules.ForTown(s).Find(a => a.Id == PlaceActionId.Heal);
        Assert.IsFalse(heal.Enabled, "heal must be locked when nothing is wounded");
    }

    [Test]
    public void Town_HealSlotEnabled_WhenWoundedAndAffordable()
    {
        var s = new TownActionSnapshot(
            placeType: PlaceType.Town, conquered: true, guardiansRemaining: 0,
            influence: 10, healCost: 2, crystalCost: 2, sellsCards: false, cardCost: 0,
            anyUnitAffordable: true, anyWoundToHeal: true, visitCanAct: true, hasMenu: true);

        var heal = PlaceActionRules.ForTown(s).Find(a => a.Id == PlaceActionId.Heal);
        Assert.IsTrue(heal.Enabled);
    }

    // Necessity does not override the other two gates.
    [Test]
    public void Town_HealSlotDisabled_WhenWoundedButBroke()
    {
        var s = new TownActionSnapshot(
            placeType: PlaceType.Town, conquered: true, guardiansRemaining: 0,
            influence: 1, healCost: 2, crystalCost: 2, sellsCards: false, cardCost: 0,
            anyUnitAffordable: true, anyWoundToHeal: true, visitCanAct: true, hasMenu: true);

        var heal = PlaceActionRules.ForTown(s).Find(a => a.Id == PlaceActionId.Heal);
        Assert.IsFalse(heal.Enabled);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources Assets\Scripts\Places\PlaceType.cs,Assets\Scripts\Places\PlaceService.cs,Assets\Scripts\Places\PlaceActionId.cs,Assets\Scripts\Places\PlaceAction.cs,Assets\Scripts\Places\PlaceRules.cs,Assets\Scripts\Places\PlaceActionRules.cs,Assets\Scripts\UiLanguage\IconConcept.cs,Assets\Tests\EditMode\PlaceActionRulesTests.cs
```

Expected: compile error — `TownActionSnapshot` has no `anyWoundToHeal` parameter.

- [ ] **Step 3: Add the snapshot field**

In `Assets/Scripts/Places/PlaceAction.cs`, add the field after `AnyUnitAffordable`:

```csharp
    public readonly bool AnyUnitAffordable;
    // Necessity, not affordability. A heal with nothing to heal spends the
    // influence AND the visit's action for no effect, and a town visit is not
    // undoable — so the slot must refuse to light at all.
    public readonly bool AnyWoundToHeal;
```

Update the constructor signature and body:

```csharp
    public TownActionSnapshot(PlaceType placeType, bool conquered, int guardiansRemaining,
        int influence, int healCost, int crystalCost, bool sellsCards, int cardCost,
        bool anyUnitAffordable, bool anyWoundToHeal, bool visitCanAct, bool hasMenu)
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
        AnyWoundToHeal = anyWoundToHeal;
        VisitCanAct = visitCanAct;
        HasMenu = hasMenu;
    }
```

- [ ] **Step 4: Gate the heal slot**

In `Assets/Scripts/Places/PlaceActionRules.cs`, replace lines 28-31:

```csharp
            if ((allowed & PlaceService.Heal) != 0)
                list.Add(new PlaceAction(PlaceActionId.Heal, IconConcept.Heal,
                    IconConcept.Influence, s.HealCost,
                    s.Influence >= s.HealCost && s.AnyWoundToHeal && s.VisitCanAct));
```

- [ ] **Step 5: Run tests to verify they pass**

Same command as Step 2. Expected: all `PlaceActionRulesTests` pass, including pre-existing ones.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Places/PlaceAction.cs Assets/Scripts/Places/PlaceActionRules.cs Assets/Tests/EditMode/PlaceActionRulesTests.cs
git commit -m "Gate town heal slot on necessity, not just affordability"
```

---

## Task 3: Town heal commit refuses, and both call sites pass the new flag

Layer 2 of the guard — the layer that actually makes the guarantee. Layer 1 (Task 2) only lets the player see it coming.

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs:47-76`
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/HealButton.cs`

**Interfaces:**
- Consumes: `HealRules.CanHeal` (Task 1), `TownActionSnapshot(..., anyWoundToHeal, ...)` (Task 2).
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Add a shared helper for "is anything wounded"**

Unit wound state does not exist until Task 5, so this helper reads hand wounds only for now and Task 6 extends it. Add to `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs`, inside the class:

```csharp
    // Necessity gate for the heal service. Unit wounds join this in Task 6 —
    // until then a clean hand is the only "nothing to heal" case, which is
    // exactly the bug being fixed.
    static bool AnythingToHeal()
    {
        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        int handWounds = 0;
        foreach (var c in hand.cardsInPlay)
            if (c != null && c.cardSO != null && c.cardSO.cardType == StatType.Wound) handWounds++;
        return HealRules.CanHeal(handWounds, null);
    }
```

- [ ] **Step 2: Pass the flag into the snapshot**

In `TownToken.cs`, in the snapshot construction around line 47-59, add the argument after `anyUnitAffordable`:

```csharp
            anyUnitAffordable: anyUnitAffordable,
            anyWoundToHeal: AnythingToHeal(),
```

- [ ] **Step 3: Refuse at the commit**

In `TownToken.cs`, replace the `PlaceActionId.Heal` case (lines 70-76):

```csharp
            case PlaceActionId.Heal:
                // Layer 2 of the guard: refuse BEFORE raising anything. The fan
                // slot is already dimmed, but this is the layer that makes the
                // guarantee — no rebind, controller nav or stale click can spend
                // the influence and the visit's action on a no-op.
                if (!AnythingToHeal())
                {
                    GameLog.Instance.Post("You don't require healing — nobody is wounded.");
                    break;
                }
                // Same three effects the old HealButton wired, in the same order.
                if (healTownEvent != null) healTownEvent.Raise(this);
                if (healInfluenceCostEvent != null) healInfluenceCostEvent.Raise(townSO.healLevel);
                if (TurnPhaseController.Instance != null)
                    TurnPhaseController.Instance.CommitVisitAction();
                break;
```

- [ ] **Step 4: Determine whether `HealButton.cs` is still live**

```powershell
Select-String -Path "Assets\Scenes\*.unity" -Pattern "HealButton" -SimpleMatch | Select-Object -First 5
```

If there are no matches, `HealButton.cs` is dead code superseded by the fan path — delete `Assets/Scripts/GameObjectScripts/TownMenuScripts/HealButton.cs` and its `.meta`, and skip Step 5. If there ARE matches, keep it and do Step 5.

- [ ] **Step 5: Guard `HealButton` too (only if Step 4 found it live)**

In `Assets/Scripts/GameObjectScripts/TownMenuScripts/HealButton.cs`, change the `Update` gate on line 10:

```csharp
            if(currentPlayerInfluence < _town.townSO.healLevel || !CanActThisVisit || !TownToken.AnythingToHeal())
                thisButton.interactable = false;
```

and change `AnythingToHeal()` in `TownToken.cs` from `static bool` to `public static bool`.

- [ ] **Step 6: Compile-check**

The Unity editor holds the lock, so verify the assembly compiles rather than running the editor:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\Tools\Roslyn\csc.dll" --help > $null
```

If that path does not resolve, open the project in Unity and confirm the console shows **zero** compile errors. Either way, do not proceed until the assembly builds clean.

- [ ] **Step 7: Human verification**

Ask the user to confirm in the editor:
1. Stand on a conquered Town with a clean hand and no wounded units → the Heal fan slot renders dimmed and does nothing when clicked.
2. Take a wound, revisit → the slot lights and heals normally, spending the influence and the visit's action.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs Assets/Scripts/GameObjectScripts/TownMenuScripts/
git commit -m "Refuse town heal when nothing is wounded"
```

---

## Task 4: `UnitsSO.armorClass`

**Files:**
- Modify: `Assets/Scripts/GameScriptableObjectTypes/UnitsSO.cs`

**Interfaces:**
- Produces: `UnitsSO.armorClass` (`int`, ≥ 0).

- [ ] **Step 1: Add the field**

In `Assets/Scripts/GameScriptableObjectTypes/UnitsSO.cs`, after `influenceCost`:

```csharp
    // How much of the Defend-phase counterattack this unit soaks when committed
    // as a shield (spec 2026-08-12). Armor IS Defend — it is summed into the
    // Defend pool before the existing counterattack comparison runs. 0 = this
    // unit cannot shield at all (support units, casters).
    public int armorClass;
```

- [ ] **Step 2: Clamp it in `OnValidate`**

Replace the body of `OnValidate`:

```csharp
    void OnValidate()
    {
        if (armorClass < 0) armorClass = 0;
        if (options == null) return;
        foreach (var o in options)
            if (o != null && o.crystalCost != EmpowerType.None && o.influenceCost > 0)
                Debug.LogWarning($"{name}: an option may cost a crystal OR influence, not both.", this);
    }
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameScriptableObjectTypes/UnitsSO.cs
git commit -m "Add UnitsSO.armorClass"
```

---

## Task 5: `Unit` wound state and visuals

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs`

**Interfaces:**
- Consumes: `UnitsSO.armorClass` (Task 4).
- Produces: `Unit.WoundCount` (`int` get/set, clamped 0–2), `Unit.IsWounded` (`bool`), `Unit.ArmorClass` (`int`). `Unit.Selectable` now also requires `!IsWounded`.

- [ ] **Step 1: Replace the file**

Rewrite `Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Unit : MonoBehaviour, IPointerClickHandler, IFanItem
{
    [SerializeField] Image image;
    [SerializeField] public UnitsSO unitSO;
    [SerializeField] TextMeshProUGUI unitLetter;
    [SerializeField] TextMeshProUGUI unitText;
    [SerializeField] Color exhaustedGrey = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] Color woundedRed = new Color(0.65f, 0.18f, 0.18f, 1f);
    [SerializeField] GameObject woundDecal;              // toggled with IsWounded
    [SerializeField] TextMeshProUGUI woundCountLabel;    // shown only at 2 wounds
    private bool isPlayed = false;
    private int woundCount = 0;

    // Exhaustion used to be a -90 rotation, which FanLane would overwrite with the
    // slot tilt on the next relayout. It is now a grey tint — the same language
    // wounds use — applied here so no caller can drift from it.
    public bool IsPlayed
    {
        get => isPlayed;
        set { isPlayed = value; ApplyStateTint(); }
    }

    // 0-2. Only Toxic produces 2 (spec 2026-08-12 §3.2); every other route adds
    // one. Clamped here so no caller can author a third state the heal picker
    // and the decal label do not know how to render.
    public int WoundCount
    {
        get => woundCount;
        set { woundCount = Mathf.Clamp(value, 0, 2); ApplyStateTint(); }
    }

    public bool IsWounded => woundCount > 0;
    public int ArmorClass => unitSO != null ? unitSO.armorClass : 0;

    CanvasGroup _group;
    public RectTransform Rect => (RectTransform)transform;
    public CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();
    public bool Selectable => !isPlayed && !IsWounded;

    public void Activate()
    {
        var inspector = FindAnyObjectByType<UnitInspector>();
        if (inspector != null) inspector.Open(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InputContextState.MapOpen) return; // map mode: look, don't touch
        if (BarFocusController.Instance != null &&
            BarFocusController.Instance.TryClaimClick(this)) return;
        if (IsWounded)
        {
            GameLog.Instance.Post($"{unitSO.cardName} is wounded and cannot act until healed.");
            return;
        }
        if (isPlayed)
        {
            GameLog.Instance.Post($"{unitSO.cardName} has already been played, undo to revert action.");
            return;
        }
        FindAnyObjectByType<UnitInspector>().Open(this);
    }

    void Start()
    {
        unitLetter.text = unitSO.cardName.ToString();
        unitText.text = unitSO.cardDescription;
        ApplyStateTint();
    }

    // Wounded outranks exhausted: a wounded unit is unusable for the rest of the
    // run until healed, while exhaustion clears at round start, so the more
    // durable state is the one the player needs to read at a glance.
    void ApplyStateTint()
    {
        if (image == null || unitSO == null) return;
        if (IsWounded)      image.color = woundedRed;
        else if (isPlayed)  image.color = exhaustedGrey;
        else                image.color = unitSO.color;

        if (woundDecal != null) woundDecal.SetActive(IsWounded);
        if (woundCountLabel != null)
        {
            woundCountLabel.gameObject.SetActive(woundCount > 1);
            woundCountLabel.text = woundCount.ToString();
        }
    }
}
```

- [ ] **Step 2: Compile-check**

Open Unity (or run the Roslyn compile check) and confirm the assembly builds with zero errors. `woundDecal` and `woundCountLabel` are optional serialized references — null-guarded — so the prefab can stay unwired until Task 14.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlayerScripts/Unit.cs
git commit -m "Add Unit wound state, red tint and decal hooks"
```

---

## Task 6: Wounded units drop out of the refresh picker and the heal necessity gate

**Files:**
- Modify: `Assets/Scripts/UnitPlay/RefreshRules.cs`
- Modify: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs:51-69`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs:466-477`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs` (`AnythingToHeal`)
- Test: `Assets/Tests/EditMode/RefreshRulesTests.cs`

**Interfaces:**
- Consumes: `Unit.IsWounded`, `Unit.WoundCount` (Task 5); `HealRules.CanHeal` (Task 1).
- Produces: `RefreshRules.CanPick(bool exhausted, bool wounded, int influenceCost, int remaining) -> bool` — **signature changed**, `wounded` inserted as the second parameter. `Player.WoundUnit(Unit)` / `Player.HealUnit(Unit, int)`.

- [ ] **Step 1: Write the failing test**

Append to `Assets/Tests/EditMode/RefreshRulesTests.cs`:

```csharp
    // A wounded unit is not "spent" — it is out of the run until healed. Mobilize
    // must not offer it, or the budget buys a unit that still cannot act and the
    // card's fizzle check reports work it cannot do.
    [Test]
    public void CanPick_RefusesWoundedUnits()
    {
        Assert.IsTrue(RefreshRules.CanPick(exhausted: true, wounded: false, influenceCost: 2, remaining: 3));
        Assert.IsFalse(RefreshRules.CanPick(exhausted: true, wounded: true, influenceCost: 2, remaining: 3));
    }
```

Update every pre-existing `RefreshRules.CanPick(` call in this file to pass `wounded: false` as the second argument.

- [ ] **Step 2: Run tests to verify they fail**

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources Assets\Scripts\UnitPlay\RefreshRules.cs,Assets\Tests\EditMode\RefreshRulesTests.cs
```

Expected: compile error — `CanPick` takes 3 arguments, not 4.

- [ ] **Step 3: Change the rule**

Replace `CanPick` in `Assets/Scripts/UnitPlay/RefreshRules.cs`:

```csharp
    // Wounded is a separate axis from exhausted: a wounded unit is out of the run
    // until healed, so readying it would be a no-op the player paid for.
    public static bool CanPick(bool exhausted, bool wounded, int influenceCost, int remaining)
    {
        return exhausted && !wounded && PickCost(influenceCost) <= remaining;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Same command as Step 2. Expected: all `RefreshRulesTests` pass.

- [ ] **Step 5: Update the two production callers**

In `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs`, line 53 and line 58:

```csharp
            if (!unit.IsPlayed || unit.IsWounded) continue; // only spent, unwounded units list
```

```csharp
            bool pickable = RefreshRules.CanPick(unit.IsPlayed, unit.IsWounded, unit.unitSO.influenceCost, _remaining);
```

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`, line 475:

```csharp
            if (RefreshRules.CanPick(unit.IsPlayed, unit.IsWounded, unit.unitSO.influenceCost, budget)) return true;
```

- [ ] **Step 6: Add the wound/heal write path**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`, after `ExhaustUnit` (line 470):

```csharp
    // Wound state gets the same single write path exhaustion has, relayout
    // included — BarFocusController's selectable mask reads Unit.Selectable,
    // which now depends on both.
    public void WoundUnit(Unit unit, int wounds)
    {
        unit.WoundCount += wounds;
        BarFocusController.Instance?.RelayoutUnits();
    }

    public void HealUnit(Unit unit, int wounds)
    {
        unit.WoundCount -= wounds;
        BarFocusController.Instance?.RelayoutUnits();
    }
```

- [ ] **Step 7: Extend the heal necessity gate to unit wounds**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs`, replace `AnythingToHeal`:

```csharp
    // Necessity gate for the heal service. Counts hand wounds AND unit wounds —
    // a player with a clean hand and a downed Knight still needs the service.
    static bool AnythingToHeal()
    {
        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        int handWounds = 0;
        foreach (var c in hand.cardsInPlay)
            if (c != null && c.cardSO != null && c.cardSO.cardType == StatType.Wound) handWounds++;

        var units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        var unitWounds = new int[units.Length];
        for (int i = 0; i < units.Length; i++) unitWounds[i] = units[i].WoundCount;

        return HealRules.CanHeal(handWounds, unitWounds);
    }
```

- [ ] **Step 8: Show wound state in the disband picker**

Spec §6.5: disbanding a wounded unit and rehiring is the Influence-priced healing channel, so the
player must be able to see which units are wounded before choosing. In
`Assets/Scripts/GameObjectScripts/TownMenuScripts/DisbandPanel.cs`, replace line 44:

```csharp
            // Wound state is load-bearing here: disband-and-rehire IS the
            // Influence-priced heal channel (spec 2026-08-12 §6.5), so choosing
            // blind would make the whole channel a guess.
            go.GetComponentInChildren<TextMeshProUGUI>().text = unit.IsWounded
                ? $"{unit.unitSO.cardName} — {IconMarkup.Cost(IconConcept.Wound, unit.WoundCount)}"
                : unit.unitSO.cardName;
```

- [ ] **Step 9: Compile-check, then commit**

Confirm the assembly builds with zero errors.

```bash
git add Assets/Scripts/UnitPlay/RefreshRules.cs Assets/Tests/EditMode/RefreshRulesTests.cs Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs Assets/Scripts/GameObjectScripts/TownMenuScripts/DisbandPanel.cs
git commit -m "Exclude wounded units from refresh; add wound/heal write path"
```

---

## Task 7: Persist unit wounds — save schema v13 → v14

**Files:**
- Modify: `Assets/Scripts/SaveData/SaveModels.cs:8-20,38`
- Modify: `Assets/Scripts/SaveData/SaveMigrator.cs:109-125`
- Modify: `Assets/Scripts/Managers/DataManager.cs:337,401-405`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs:433-453`
- Create: `Assets/Scripts/SaveData/Tests/SaveMigratorV14Tests.cs`
- Modify: 12 existing migrator test files (see Step 5)

**Interfaces:**
- Consumes: `Unit.WoundCount` (Task 5).
- Produces: `RunState.unitWounds` (`int[]`); `Player.RebuildUnits(List<UnitsSO> unitSOs, bool[] exhausted = null, int[] wounds = null)`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Scripts/SaveData/Tests/SaveMigratorV14Tests.cs`:

```csharp
using System;
using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV14Tests
{
    // v13 -> v14: unitWounds did not exist; absent means every unit is healthy.
    [Test]
    public void V13_GetsEmptyUnitWounds()
    {
        var f = new SaveFile { schemaVersion = 13 };
        f.run.unitWounds = null; // absent in v13 JSON

        var m = SaveMigrator.Migrate(f);

        Assert.IsNotNull(m.run.unitWounds);
        Assert.AreEqual(0, m.run.unitWounds.Length);
        Assert.AreEqual(14, m.schemaVersion);
    }

    [Test]
    public void V14_IsUnchanged()
    {
        var f = new SaveFile { schemaVersion = 14 };
        f.run.unitWounds = new[] { 0, 2, 1 };

        var m = SaveMigrator.Migrate(f);

        CollectionAssert.AreEqual(new[] { 0, 2, 1 }, m.run.unitWounds);
        Assert.AreEqual(14, m.schemaVersion);
    }

    // Migration is idempotent — running it twice must not re-fire the v14 step.
    [Test]
    public void Migrate_IsIdempotent()
    {
        var f = new SaveFile { schemaVersion = 13 };
        var once = SaveMigrator.Migrate(f);
        var twice = SaveMigrator.Migrate(once);

        Assert.AreEqual(14, twice.schemaVersion);
        Assert.IsNotNull(twice.run.unitWounds);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources Assets\Scripts\SaveData\SaveModels.cs,Assets\Scripts\SaveData\MapDelta.cs,Assets\Scripts\SaveData\SaveMigrator.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV14Tests.cs
```

Expected: compile error — `RunState` has no `unitWounds`.

- [ ] **Step 3: Add the field and bump the version**

In `Assets/Scripts/SaveData/SaveModels.cs`, add to the version comment block after the v13 line:

```csharp
        // v14: adds RunState.unitWounds (unit wound counts, 0-2). Units can now
        // soak the Defend-phase counterattack and stay wounded until healed
        // (2026-08-12), which persists across rounds and therefore across saves.
        public int schemaVersion = 14;
```

Add the field right after `unitExhausted` (line 38):

```csharp
        // Parallel to unitIds: true = the unit was already used this round.
        public bool[] unitExhausted = Array.Empty<bool>();
        // Parallel to unitIds: 0-2 wounds. NOT part of wound-out — unit wounds
        // trade run-loss pressure for army capability (spec 2026-08-12 §2).
        public int[] unitWounds = Array.Empty<int>();
```

- [ ] **Step 4: Add the migration**

In `Assets/Scripts/SaveData/SaveMigrator.cs`, insert immediately before `return file;` (line 125):

```csharp
            // v13 -> v14: unitWounds did not exist; absent means every unit is
            // healthy, which is exactly the state a pre-v14 run was in.
            if (file.schemaVersion < 14)
            {
                if (file.run.unitWounds == null)
                    file.run.unitWounds = Array.Empty<int>();
                file.schemaVersion = 14;
            }
```

- [ ] **Step 5: Bump every existing version assertion**

12 files assert the current version. Replace `Assert.AreEqual(13,` with `Assert.AreEqual(14,` in each:

```powershell
Get-ChildItem "Assets\Scripts\SaveData\Tests\*.cs" | ForEach-Object {
    (Get-Content $_.FullName -Raw) -replace 'Assert\.AreEqual\(13, ', 'Assert.AreEqual(14, ' |
        Set-Content $_.FullName -Encoding utf8
}
```

Then verify none were missed:

```powershell
Select-String -Path "Assets\Scripts\SaveData\Tests\*.cs" -Pattern "AreEqual\(13, "
```

Expected: **no output.** (`SaveMigratorV13Tests` legitimately keeps `schemaVersion = 13` as *input* on lines 9, 26 and 41 — those are `new SaveFile { schemaVersion = 13 }`, not assertions, and the regex above does not touch them.)

- [ ] **Step 6: Run all migrator tests**

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources Assets\Scripts\SaveData\SaveModels.cs,Assets\Scripts\SaveData\MapDelta.cs,Assets\Scripts\SaveData\SaveMigrator.cs,Assets\Scripts\SaveData\Tests\SaveMigratorTests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV3Tests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV4Tests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV5Tests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV6Tests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV7Tests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV8Tests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV9Tests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV10Tests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV11Tests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV12Tests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV13Tests.cs,Assets\Scripts\SaveData\Tests\SaveMigratorV14Tests.cs
```

Expected: all pass.

- [ ] **Step 7: Capture and restore the state**

In `Assets/Scripts/Managers/DataManager.cs`, replace lines 401-405:

```csharp
        // Single-source capture so unitIds[i], unitExhausted[i] and unitWounds[i]
        // always pair: all three come from the same Unit-object iteration.
        var unitObjs = FindObjectsByType<Unit>();
        run.unitIds       = System.Array.ConvertAll(unitObjs, u => u.unitSO.id);
        run.unitExhausted = System.Array.ConvertAll(unitObjs, u => u.IsPlayed);
        run.unitWounds    = System.Array.ConvertAll(unitObjs, u => u.WoundCount);
```

Replace line 337:

```csharp
        player.RebuildUnits(Units.Resolve(run.unitIds), run.unitExhausted, run.unitWounds);
```

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`, replace `RebuildUnits` (lines 433-453):

```csharp
    public void RebuildUnits(List<UnitsSO> unitSOs, bool[] exhausted = null, int[] wounds = null)
    {
        // Clear any existing Unit GameObjects (including the placeholder created in Awake) and the list.
        foreach (var existing in FindObjectsByType<Unit>())
            Destroy(existing.gameObject);
        units.Clear();

        var unitsParent = GameObject.Find("Units");
        for (int i = 0; i < unitSOs.Count; i++)
        {
            var so = unitSOs[i];
            if (so == null) continue;
            units.Add(so);
            var newUnit = Instantiate(unitPrefab, unitsParent?.transform);
            var unit = newUnit.GetComponent<Unit>();
            unit.unitSO = so;
            if (exhausted != null && i < exhausted.Length && exhausted[i])
                unit.IsPlayed = true;
            if (wounds != null && i < wounds.Length && wounds[i] > 0)
                unit.WoundCount = wounds[i];
        }
        BarFocusController.Instance?.RelayoutUnits();
    }
```

- [ ] **Step 8: Compile-check, then commit**

```bash
git add Assets/Scripts/SaveData/ Assets/Scripts/Managers/DataManager.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs
git commit -m "Persist unit wounds; save schema v14"
```

---

## Task 8: The soak math

The heart of the feature, and fully pure. Nothing here knows what a `Unit` is — it takes an int.

**Files:**
- Modify: `Assets/Scripts/CardPlay/EnemyTraitRules.cs`
- Create: `Assets/Tests/EditMode/EnemyTraitShelterTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `struct CounterattackOutcome { int HandWounds; int DiscardWounds; int CrystalsStolen; int WoundsPerCommittedUnit; }`
  - `EnemyTraitRules.Resolve(CounterattackPreview p, int defendLeft, int soak, int toughness, EnemyTraitTuning t, int committedUnits) -> CounterattackOutcome`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/EnemyTraitShelterTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

// Unit shelter (spec 2026-08-12 §3). Armor is Defend: soak is summed into
// defendLeft before the existing pipeline runs. Leech and Toxic are the only
// two exceptions, and they exist because they care WHO holds the shield.
public class EnemyTraitShelterTests
{
    static EnemyTraitTuning Tuning() => new EnemyTraitTuning();

    static List<EnemyCombatant> Roster(params EnemyCombatant[] e) => new List<EnemyCombatant>(e);

    static EnemyCombatant Enemy(int attack, int hp, EnemyTrait traits = EnemyTrait.None)
        => new EnemyCombatant { Attack = attack, HP = hp, Traits = traits, Blocked = false };

    // §1.1 compatibility guarantee: with nothing committed, every value is what
    // the shipped pipeline already produced. This is the test that lets the
    // feature ship without re-tuning a single enemy.
    [Test]
    public void ZeroCommitted_MatchesShippedPipeline()
    {
        var roster = Roster(Enemy(4, 3, EnemyTrait.Toxic), Enemy(2, 2, EnemyTrait.Leech));
        var t = Tuning();
        var p = EnemyTraitRules.BuildPreview(roster, t);

        var o = EnemyTraitRules.Resolve(p, defendLeft: 1, soak: 0, toughness: 2, t: t, committedUnits: 0);

        Assert.AreEqual(EnemyTraitRules.HandWounds(p, 1, 2), o.HandWounds);
        Assert.AreEqual(EnemyTraitRules.DiscardWounds(p, 1, 2, t), o.DiscardWounds);
        Assert.AreEqual(EnemyTraitRules.CrystalsStolen(p, 1, 2, t), o.CrystalsStolen);
    }

    // §3.3 example A: Attack 5, Defend 0, Toughness 2, Scout AC 2.
    [Test]
    public void Soak_ReducesHandWounds()
    {
        var roster = Roster(Enemy(5, 4));
        var t = Tuning();
        var p = EnemyTraitRules.BuildPreview(roster, t);

        var bare = EnemyTraitRules.Resolve(p, 0, soak: 0, toughness: 2, t: t, committedUnits: 0);
        var held = EnemyTraitRules.Resolve(p, 0, soak: 2, toughness: 2, t: t, committedUnits: 1);

        Assert.AreEqual(3, bare.HandWounds);
        Assert.AreEqual(2, held.HandWounds);
        Assert.AreEqual(1, held.WoundsPerCommittedUnit);
    }

    // §3.3 example B: Toxic transfers to the unit, Leech reads pre-soak.
    [Test]
    public void ToxicTransfers_AndLeechIgnoresSoak()
    {
        var roster = Roster(Enemy(4, 3, EnemyTrait.Toxic), Enemy(2, 2, EnemyTrait.Leech));
        var t = Tuning();
        var p = EnemyTraitRules.BuildPreview(roster, t);

        var bare = EnemyTraitRules.Resolve(p, 1, soak: 0, toughness: 2, t: t, committedUnits: 0);
        var held = EnemyTraitRules.Resolve(p, 1, soak: 3, toughness: 2, t: t, committedUnits: 1);

        Assert.AreEqual(3, bare.HandWounds);
        Assert.AreEqual(2, bare.DiscardWounds);
        Assert.AreEqual(1, bare.CrystalsStolen);

        Assert.AreEqual(1, held.HandWounds);
        Assert.AreEqual(0, held.DiscardWounds, "toxic transfers to the units");
        Assert.AreEqual(1, held.CrystalsStolen, "leech reads the PRE-soak number");
        Assert.AreEqual(2, held.WoundsPerCommittedUnit, "toxic makes it 2 per unit");
    }

    // Toxic only transfers when somebody actually stepped in.
    [Test]
    public void ToxicDoesNotTransfer_WhenNobodyIsCommitted()
    {
        var roster = Roster(Enemy(4, 3, EnemyTrait.Toxic));
        var t = Tuning();
        var p = EnemyTraitRules.BuildPreview(roster, t);

        var o = EnemyTraitRules.Resolve(p, 0, soak: 0, toughness: 2, t: t, committedUnits: 0);

        Assert.Greater(o.DiscardWounds, 0);
    }

    // §3.3 example C: soaking past the threat short-circuits Effective at <= 0,
    // so Brutal's surcharge never lands.
    [Test]
    public void SoakingToZero_KillsBrutalSurcharge()
    {
        var roster = Roster(Enemy(3, 3, EnemyTrait.Brutal));
        var t = Tuning();
        var p = EnemyTraitRules.BuildPreview(roster, t);

        var bare = EnemyTraitRules.Resolve(p, 2, soak: 0, toughness: 2, t: t, committedUnits: 0);
        var held = EnemyTraitRules.Resolve(p, 2, soak: 3, toughness: 2, t: t, committedUnits: 1);

        Assert.AreEqual(2, bare.HandWounds);
        Assert.AreEqual(0, held.HandWounds);
    }

    // Pins the SHIPPED Toxic behaviour so the doc correction in Task 15 is made
    // against verified fact: discard copies are ADDITIVE, not diverted. If this
    // test ever fails, mechanics.md was right and the code is the bug.
    [Test]
    public void Toxic_AddsDiscardCopies_RatherThanDivertingFromHand()
    {
        var toxic = Roster(Enemy(4, 3, EnemyTrait.Toxic));
        var plain = Roster(Enemy(4, 3));
        var t = Tuning();

        var pToxic = EnemyTraitRules.BuildPreview(toxic, t);
        var pPlain = EnemyTraitRules.BuildPreview(plain, t);

        Assert.AreEqual(EnemyTraitRules.HandWounds(pPlain, 0, 2),
                        EnemyTraitRules.HandWounds(pToxic, 0, 2),
                        "hand wounds are unchanged by Toxic — the copies are extra");
        Assert.Greater(EnemyTraitRules.DiscardWounds(pToxic, 0, 2, t), 0);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```powershell
.\.superpowers\scratch\lane-swap\run-pure-tests.ps1 -Sources Assets\Scripts\Enums\Enums\EnemyTrait.cs,Assets\Scripts\CardPlay\CombatRules.cs,Assets\Scripts\CardPlay\EnemyCombatant.cs,Assets\Scripts\CardPlay\EnemyTraitRules.cs,Assets\Tests\EditMode\EnemyTraitShelterTests.cs
```

Expected: compile error — `EnemyTraitRules` has no `Resolve`.

If instead the error is about `EnemyCombatant`'s initialiser (field names differ from the test helper), open `Assets/Scripts/CardPlay/EnemyCombatant.cs` and fix the `Enemy(...)` helper to match the real field names before continuing.

- [ ] **Step 3: Implement**

Append to `Assets/Scripts/CardPlay/EnemyTraitRules.cs`, inside the `EnemyTraitRules` class:

```csharp
    // The whole counterattack in one call (spec 2026-08-12 §3.2).
    //
    // The design principle is that unit armor IS Defend — so `soak` is simply
    // added to defendLeft and every existing trait behaviour follows for free.
    // Exactly two traits are exceptions, and both are here rather than buried
    // in a call site:
    //   Leech — reads the PRE-soak number. The shield does not stop it draining
    //           YOU, which makes it the anti-wall trait.
    //   Toxic — transfers. Its discard copies become 0 and each committed unit
    //           takes 2 wounds instead of 1: the venom goes into the body that
    //           took the hit.
    //
    // With committedUnits == 0, soak is 0 and neither exception fires, so every
    // value is identical to the shipped pipeline (§1.1).
    public static CounterattackOutcome Resolve(CounterattackPreview p, int defendLeft, int soak,
        int toughness, EnemyTraitTuning t, int committedUnits)
    {
        bool anyToxic = p.ToxicContribution > 0;
        bool sheltered = committedUnits > 0;

        return new CounterattackOutcome
        {
            HandWounds     = HandWounds(p, defendLeft + soak, toughness),
            DiscardWounds  = sheltered && anyToxic ? 0 : DiscardWounds(p, defendLeft, toughness, t),
            CrystalsStolen = CrystalsStolen(p, defendLeft, toughness, t),
            WoundsPerCommittedUnit = anyToxic ? 2 : 1,
        };
    }
```

Append at the end of the file, beside `CounterattackPreview`:

```csharp
// Everything one counterattack does, so CombatController makes a single call
// instead of four and can never apply three of the four (spec 2026-08-12 §3.2).
// WoundsPerCommittedUnit is meaningless when nothing was committed; the caller
// applies it per committed unit, so a zero-length loop drops it harmlessly.
public struct CounterattackOutcome
{
    public int HandWounds;
    public int DiscardWounds;
    public int CrystalsStolen;
    public int WoundsPerCommittedUnit;
}
```

- [ ] **Step 4: Run to verify it passes**

Same command as Step 2. Expected: all 6 tests pass.

If `Toxic_AddsDiscardCopies_RatherThanDivertingFromHand` **fails**, stop and report it: the shipped code diverts rather than adds, `mechanics.md` was correct, and Task 15's doc correction must be dropped.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Tests/EditMode/EnemyTraitShelterTests.cs
git commit -m "Add unit-shelter soak math to the counterattack pipeline"
```

---

## Task 9: `UnitPickerPanel` wound mode

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs`

**Interfaces:**
- Consumes: `Unit.IsWounded`, `Unit.ArmorClass` (Task 5); `EnemyTraitRules.Resolve`, `CounterattackOutcome` (Task 8).
- Produces: `UnitPickerPanel.OpenForWounds(CounterattackPreview preview, int defendLeft, int toughness, EnemyTraitTuning tuning, System.Action<IReadOnlyList<Unit>> onConfirm)`.

- [ ] **Step 1: Split `Close` into a dismiss request and a real teardown**

In `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs`, replace `Close()` (lines 83-89):

```csharp
    // Public so the ClickOffCatcher can bind to it — this is now a DISMISS
    // REQUEST, not a teardown. Refresh and heal modes forfeit their remaining
    // budget and close, which is the shipped rule. WOUND MODE REFUSES: the
    // player must not be able to click off the counterattack (spec 2026-07-29
    // §6.4 flagged this exact hazard). Closing wound mode goes through the
    // take-hit row, which calls CloseInternal directly.
    public void Close()
    {
        if (_mode == PickerMode.Wound)
        {
            GameLog.Instance.Post("Choose who takes the hit, then confirm.");
            return;
        }
        CloseInternal();
    }

    void CloseInternal()
    {
        AnyOpen = false;
        ClearEntries();
        _onPick = null;
        _onConfirm = null;
        _committed.Clear();
        _mode = PickerMode.Refresh;
        Canvas.enabled = false;
    }
```

- [ ] **Step 2: Add the mode state**

Add to the field block near the top of the class (after `readonly List<GameObject> spawned = new();`):

```csharp
    enum PickerMode { Refresh, Wound, Heal }
    PickerMode _mode = PickerMode.Refresh;

    // Wound mode only. Selection is non-destructive — nothing commits until the
    // take-hit row is clicked — so a picked unit can be un-picked.
    readonly List<Unit> _committed = new();
    System.Action<IReadOnlyList<Unit>> _onConfirm;
    CounterattackPreview _preview;
    int _defendLeft, _toughness;
    EnemyTraitTuning _tuning;
```

Add `using System.Collections.Generic;` if missing (it is already there).

Set the mode in the existing `OpenForRefresh`, as its first line after `AnyOpen = true;`:

```csharp
        _mode = PickerMode.Refresh;
```

- [ ] **Step 3: Add the wound-mode entry point and its rebuild**

Append to the class:

```csharp
    // Opens the "who takes this hit?" picker (spec 2026-08-12 §4). Every row is
    // a body that can absorb the counterattack, and the player is the last one.
    public void OpenForWounds(CounterattackPreview preview, int defendLeft, int toughness,
        EnemyTraitTuning tuning, System.Action<IReadOnlyList<Unit>> onConfirm)
    {
        AnyOpen = true;
        _mode = PickerMode.Wound;
        _preview = preview;
        _defendLeft = defendLeft;
        _toughness = toughness;
        _tuning = tuning;
        _onConfirm = onConfirm;
        _committed.Clear();
        Canvas.enabled = true;
        RebuildWounds();
    }

    int CommittedSoak()
    {
        int soak = 0;
        foreach (var u in _committed) if (u != null) soak += u.ArmorClass;
        return soak;
    }

    CounterattackOutcome CurrentOutcome()
        => EnemyTraitRules.Resolve(_preview, _defendLeft, CommittedSoak(),
                                   _toughness, _tuning, _committed.Count);

    void RebuildWounds()
    {
        ClearEntries();

        var outcome = CurrentOutcome();
        int reduced = _preview.UnblockedThreat - CommittedSoak();
        if (reduced < 0) reduced = 0;

        if (titleLabel != null)
            titleLabel.text = CommittedSoak() > 0
                ? $"{IconMarkup.Tag(IconConcept.Attack)}{_preview.UnblockedThreat} -> {IconMarkup.Tag(IconConcept.Attack)}{reduced}"
                : $"{IconMarkup.Tag(IconConcept.Attack)}{_preview.UnblockedThreat}";

        // Unit rows. Eligibility is a single filter: not already wounded.
        // Exhausted units qualify — taking a hit is not USING the unit.
        foreach (var unit in FindObjectsByType<Unit>())
        {
            if (unit.IsWounded) continue;

            var go = Instantiate(entryButtonPrefab, entryContainer);
            bool picked = _committed.Contains(unit);
            // A unit with no armor can never help; once the shortfall is already
            // zero, nothing further can either. Both lock rather than disappear,
            // so the player can read WHY.
            bool pickable = picked || (unit.ArmorClass > 0 && outcome.HandWounds > 0);

            go.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{(picked ? "> " : "")}{unit.unitSO.cardName} — {IconMarkup.Cost(IconConcept.Defend, unit.ArmorClass)}";

            var button = go.GetComponent<Button>();
            button.interactable = pickable;
            UiLock.Apply(go.GetComponent<CanvasGroup>(), !pickable);
            if (pickable)
            {
                var captured = unit;
                button.onClick.AddListener(() => TogglePick(captured));
            }
            spawned.Add(go);
        }

        // The take-hit row: always last, always live, and the only exit. It is a
        // ROW rather than a Confirm button so the panel reads as one homogeneous
        // question with the player as one of the answers.
        var takeGo = Instantiate(entryButtonPrefab, entryContainer);
        var takeLabel = takeGo.GetComponentInChildren<TextMeshProUGUI>();
        takeLabel.text = $"Take {outcome.HandWounds} {IconMarkup.Tag(IconConcept.Wound)}";
        takeLabel.color = new Color(0.90f, 0.20f, 0.20f); // CombatButtons.takeHitColor
        var takeButton = takeGo.GetComponent<Button>();
        takeButton.interactable = true;
        UiLock.Apply(takeGo.GetComponent<CanvasGroup>(), false);
        takeButton.onClick.AddListener(ConfirmWounds);
        spawned.Add(takeGo);
    }

    void TogglePick(Unit unit)
    {
        if (!_committed.Remove(unit)) _committed.Add(unit);
        RebuildWounds();
    }

    void ConfirmWounds()
    {
        var result = new List<Unit>(_committed);
        var callback = _onConfirm;
        CloseInternal();
        callback?.Invoke(result);
    }
```

- [ ] **Step 4: Compile-check**

Confirm the assembly builds with zero errors. If `IconConcept.Attack` or `IconConcept.Defend` do not exist under those names, run:

```powershell
Select-String -Path "Assets\Scripts\UiLanguage\IconConcept.cs" -Pattern "Attack|Defend|Wound"
```

and use the real member names.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs
git commit -m "Add wound mode to UnitPickerPanel"
```

---

## Task 10: `ResolveDefend` becomes two-stage

**Files:**
- Modify: `Assets/Scripts/Managers/CombatController.cs:546-593`

**Interfaces:**
- Consumes: `EnemyTraitRules.Resolve`, `CounterattackOutcome` (Task 8); `UnitPickerPanel.OpenForWounds` (Task 9); `Player.WoundUnit` (Task 6).
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Replace `ResolveDefend`**

In `Assets/Scripts/Managers/CombatController.cs`, replace the whole `ResolveDefend` method (lines 546-593):

```csharp
    // Defend (Defend -> Attack, spec 2026-07-22, extended 2026-08-12): resolve
    // the summed survivor counterattack. The player may first commit units to
    // soak it — armor IS Defend, bought with a unit wound instead of a card —
    // so this is now two-stage: open the picker, apply in the callback.
    public void ResolveDefend()
    {
        if (Phase != CombatPhase.Defend) return;
        var player = FindAnyObjectByType<Player>();

        var preview = Preview();
        int defendLeft = player.PlayerDefend;
        int toughness = player.PlayerToughness;

        // Skip the picker when it could not change anything: nothing is coming
        // (Effective short-circuits at shortfall <= 0, so Toxic and Leech are
        // both zero too), or no unit is eligible to step in. Both fall through
        // to the unsheltered path, which satisfies wound-plumbing contract
        // rule 7 by never opening.
        var bare = EnemyTraitRules.Resolve(preview, defendLeft, 0, toughness, Tuning, 0);
        var picker = FindAnyObjectByType<UnitPickerPanel>();
        if (bare.HandWounds == 0 || picker == null || !AnyShelterAvailable())
        {
            ApplyCounterattack(preview, defendLeft, toughness, new List<Unit>());
            return;
        }

        picker.OpenForWounds(preview, defendLeft, toughness, Tuning,
            committed => ApplyCounterattack(preview, defendLeft, toughness, committed));
    }

    static bool AnyShelterAvailable()
    {
        foreach (var unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
            if (!unit.IsWounded && unit.ArmorClass > 0) return true;
        return false;
    }

    // Stage two: everything the single-stage ResolveDefend used to do, in the
    // same order, plus the unit wounds the player chose to take.
    void ApplyCounterattack(CounterattackPreview preview, int defendLeft, int toughness,
        IReadOnlyList<Unit> committed)
    {
        var player = FindAnyObjectByType<Player>();

        int soak = 0;
        foreach (var u in committed) if (u != null) soak += u.ArmorClass;

        var outcome = EnemyTraitRules.Resolve(preview, defendLeft, soak, toughness,
                                              Tuning, committed.Count);

        // The placement list is the seam (spec 2026-07-29 §6.2). Units never
        // receive a wound CARD — the soak happened before the wound math — so
        // this still produces only Hand and Discard.
        var placements = WoundPlacementRules.Place(outcome.HandWounds, outcome.DiscardWounds);
        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        foreach (var dest in placements) hand.AddWound(dest);

        int wounds = placements.Count;

        foreach (var u in committed)
            if (u != null) player.WoundUnit(u, outcome.WoundsPerCommittedUnit);

        // Taking the group counterattack reads on the avatar (spec D4).
        if (wounds > 0 && PlayerAvatar.Instance != null)
            PlayerAvatar.Instance.Play(AvatarState.Hurt);

        player.PlayerDefend = Mathf.Max(0, player.PlayerDefend - preview.UnblockedThreat);
        GameManager.Instance.commands.ClearStack();   // taking the hit is a commit point

        if (committed.Count > 0)
        {
            var names = new List<string>();
            foreach (var u in committed) if (u != null) names.Add(u.unitSO.cardName);
            GameLog.Instance.Post(outcome.WoundsPerCommittedUnit > 1
                ? $"{string.Join(", ", names)} take the blow — and the venom with it."
                : $"{string.Join(", ", names)} take the blow in your stead.");
        }
        if (wounds > 0)
            GameLog.Instance.Post($"The enemies strike back! You are wounded {wounds} times.");
        if (outcome.DiscardWounds > 0)
            GameLog.Instance.Post($"Venom festers — {outcome.DiscardWounds} wound(s) rot in your discard.");
        if (outcome.CrystalsStolen > 0)
            GameLog.Instance.Post($"Leeched! You lose {outcome.CrystalsStolen} crystal(s).");

        if (outcome.CrystalsStolen > 0)
        {
            var crystals = FindAnyObjectByType<CrystalInventory>();
            if (crystals != null)
            {
                int n = Mathf.Min(outcome.CrystalsStolen, crystals.crystalsInInventory.Count);
                for (int i = 0; i < n; i++) crystals.crystalsInInventory[0].RemoveCrystal();
            }
        }
        ClearBlocks();

        SetPhase(CombatPhase.Attack);
    }
```

- [ ] **Step 2: Compile-check**

Confirm the assembly builds with zero errors.

- [ ] **Step 3: Human verification**

Ask the user to confirm in the editor:
1. Start a fight where the counterattack would wound, with a unit that has `armorClass > 0` → pressing the red Advance button opens the picker.
2. Clicking off the picker does nothing and logs "Choose who takes the hit, then confirm."
3. Picking a unit lowers the `Take N` row; clicking the same unit again raises it back.
4. Clicking `Take N` applies exactly that many wounds and turns each picked unit red.
5. A fight where Defend already covers the threat opens **no** picker.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Managers/CombatController.cs
git commit -m "Route the Defend counterattack through the unit shelter picker"
```

---

## Task 11: `UnitPickerPanel` heal mode

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs`

**Interfaces:**
- Consumes: `Unit.WoundCount` (Task 5).
- Produces: `struct HealTarget { Unit Unit; int Cost; }` (`Unit == null` means the hand-wounds row) and `UnitPickerPanel.OpenForHeal(int budget, int handWounds, System.Action<HealTarget> onPick)`.

- [ ] **Step 1: Add the target type**

Create the type at the bottom of `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs`, outside the class:

```csharp
// One pick from the heal picker. A null Unit is the "wounds in hand" row —
// modelling the hand as just another target keeps the panel homogeneous and
// lets the caller's undo snapshot record both kinds the same way.
public readonly struct HealTarget
{
    public readonly Unit Unit;
    public readonly int Cost;
    public HealTarget(Unit unit, int cost) { Unit = unit; Cost = cost; }
}
```

- [ ] **Step 2: Add the entry point**

Append to the `UnitPickerPanel` class:

```csharp
    System.Action<HealTarget> _onHealPick;
    int _handWounds;

    // Heal mode (spec 2026-08-12 §6.1). One list: hand wounds and wounded units
    // compete for one budget, so the allocation is an explicit decision every
    // time. Mobilize semantics otherwise — click-off dismisses and unspent
    // budget is lost, because unlike wound mode there is nothing here the
    // player must not escape.
    public void OpenForHeal(int budget, int handWounds, System.Action<HealTarget> onPick)
    {
        AnyOpen = true;
        _mode = PickerMode.Heal;
        _remaining = budget;
        _handWounds = handWounds;
        _onHealPick = onPick;
        Canvas.enabled = true;
        RebuildHeal();
    }

    void RebuildHeal()
    {
        ClearEntries();
        if (titleLabel != null)
            titleLabel.text = $"{IconMarkup.Tag(IconConcept.Heal)} Heal — {_remaining} left";

        bool any = false;

        if (_handWounds > 0)
        {
            var go = Instantiate(entryButtonPrefab, entryContainer);
            bool pickable = _remaining >= 1;
            go.GetComponentInChildren<TextMeshProUGUI>().text =
                $"Wounds in hand  x{_handWounds} — {IconMarkup.Cost(IconConcept.Heal, 1)}";
            var button = go.GetComponent<Button>();
            button.interactable = pickable;
            UiLock.Apply(go.GetComponent<CanvasGroup>(), !pickable);
            if (pickable)
            {
                any = true;
                button.onClick.AddListener(() => PickHeal(new HealTarget(null, 1)));
            }
            spawned.Add(go);
        }

        foreach (var unit in FindObjectsByType<Unit>())
        {
            if (!unit.IsWounded) continue;

            // Healing a unit is ATOMIC: its row costs its full WoundCount and
            // locks when the budget cannot cover it, rather than accepting a
            // point and leaving the unit down at 1 wound. That is the existing
            // picker idiom (cost <= remaining) and it stops the player spending
            // a point for no state change.
            int cost = unit.WoundCount;
            var go = Instantiate(entryButtonPrefab, entryContainer);
            bool pickable = cost <= _remaining;
            go.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{unit.unitSO.cardName} — {IconMarkup.Cost(IconConcept.Heal, cost)}";
            var button = go.GetComponent<Button>();
            button.interactable = pickable;
            UiLock.Apply(go.GetComponent<CanvasGroup>(), !pickable);
            if (pickable)
            {
                any = true;
                var captured = unit;
                button.onClick.AddListener(() => PickHeal(new HealTarget(captured, cost)));
            }
            spawned.Add(go);
        }

        if (!any) CloseInternal(); // unspent budget is lost — same as refresh
    }

    void PickHeal(HealTarget target)
    {
        _remaining -= target.Cost;
        if (target.Unit == null) _handWounds--;
        _onHealPick?.Invoke(target);
        RebuildHeal();
    }
```

- [ ] **Step 3: Clear the new state in `CloseInternal`**

In `CloseInternal()`, add before `Canvas.enabled = false;`:

```csharp
        _onHealPick = null;
        _handWounds = 0;
```

- [ ] **Step 4: Compile-check, then commit**

```bash
git add Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs
git commit -m "Add heal mode to UnitPickerPanel"
```

---

## Task 12: Route every heal through the picker, with snapshot undo

**Files:**
- Create: `Assets/Scripts/UnitPlay/HealAssignment.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs:229-246`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs:519-524,568-573,618-627`

**Interfaces:**
- Consumes: `UnitPickerPanel.OpenForHeal`, `HealTarget` (Task 11); `Player.HealUnit`, `Player.WoundUnit` (Task 6).
- Produces: `HealAssignment` with `Record(HealTarget)`, `HandWoundsHealed`, `UnitsHealed`.

- [ ] **Step 1: Create the snapshot type**

Create `Assets/Scripts/UnitPlay/HealAssignment.cs`:

```csharp
using System.Collections.Generic;

// What one heal activation actually did (spec 2026-08-12 §6.3).
//
// Heals used to undo by sign flip: RestoreHealedWound() N times. That breaks
// the moment the player chooses WHERE the healing went — the same problem
// conversion hit, and SkillToken already records the fix ("the sign-flip undo
// pattern can't reverse a conversion, so the applied amounts live here").
// Unity-free so it is mcs-CLI-testable; Unit is referenced only as object.
public class HealAssignment
{
    public int HandWoundsHealed { get; private set; }
    // (unit, wounds restored) pairs. Object rather than Unit so this class has
    // no UnityEngine dependency; the caller casts back.
    public readonly List<(object unit, int wounds)> UnitsHealed = new();

    public void RecordHand() => HandWoundsHealed++;

    public void RecordUnit(object unit, int wounds) => UnitsHealed.Add((unit, wounds));

    public bool IsEmpty => HandWoundsHealed == 0 && UnitsHealed.Count == 0;
}
```

- [ ] **Step 2: Give `PlayerHand` a picker-backed heal**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs`, add these two methods after `TownHeal` (line 246):

```csharp
    // How many Wound cards are in hand right now — the heal picker's "wounds in
    // hand" row needs this to render its count and to know whether to appear.
    public int HandWoundCount()
    {
        int n = 0;
        foreach (var c in cardsInPlay)
            if (c != null && c.cardSO != null && c.cardSO.cardType == StatType.Wound) n++;
        return n;
    }

    // The one heal funnel (spec 2026-08-12 §6). Opens the picker with `budget`
    // and records what the player spent it on into `into`, so the activation can
    // be undone exactly. Falls back to healing the hand directly when no picker
    // exists in the scene, so a heal is never silently dropped.
    public void OpenHeal(int budget, HealAssignment into)
    {
        var picker = FindAnyObjectByType<UnitPickerPanel>();
        if (picker == null)
        {
            for (int i = 0; i < budget; i++) { HealWound(); into.RecordHand(); }
            return;
        }

        var player = FindAnyObjectByType<Player>();
        picker.OpenForHeal(budget, HandWoundCount(), target =>
        {
            if (target.Unit == null) { HealWound(); into.RecordHand(); }
            else
            {
                player.HealUnit(target.Unit, target.Cost);
                into.RecordUnit(target.Unit, target.Cost);
            }
        });
    }

    // Reverses an OpenHeal exactly — hand wounds come back through the existing
    // restore path, unit wounds are re-applied to the same units.
    public void UndoHeal(HealAssignment done)
    {
        var player = FindAnyObjectByType<Player>();
        for (int i = 0; i < done.HandWoundsHealed; i++) RestoreHealedWound();
        foreach (var (unit, wounds) in done.UnitsHealed)
            if (unit is Unit u && u != null) player.WoundUnit(u, wounds);
    }
```

- [ ] **Step 3: Route the card-play heal**

In `PlayerHand.cs`, replace `Heal(Card card)` (lines 229-240):

```csharp
    // Per-card assignment record, so undoing a heal restores exactly what THAT
    // play cleared rather than N arbitrary hand wounds.
    readonly Dictionary<Card, HealAssignment> healAssignments = new();

    public void Heal(Card card)
    {
        var count = HealRules.HealCount(card.cardSO.cardType, card.IsEmpowered,
            card.cardSO.healAmount, card.cardSO.empowerHealAmount);
        if (count <= 0) return;

        if (card.IsPlayed)
        {
            var assignment = new HealAssignment();
            healAssignments[card] = assignment;
            OpenHeal(count, assignment);
        }
        else if (healAssignments.TryGetValue(card, out var done))
        {
            UndoHeal(done);
            healAssignments.Remove(card);
        }
    }
```

Add `using System.Collections.Generic;` at the top of `PlayerHand.cs` if it is not already there.

- [ ] **Step 4: Route the town heal**

In `PlayerHand.cs`, replace `TownHeal` (lines 242-246):

```csharp
    // A town visit is not undoable, so this needs no assignment record — but it
    // uses the same picker so the player chooses where the healing goes. The
    // town's healLevel is both the influence price and the budget.
    public void TownHeal(TownToken town)
    {
        OpenHeal(town.townSO.healLevel, new HealAssignment());
    }
```

- [ ] **Step 5: Route the unit-option heal**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`, add a field near the other unit state:

```csharp
    // Per-unit-option and per-skill heal assignments, for exact undo.
    readonly Dictionary<object, HealAssignment> healAssignments = new();
```

Replace the `UnitEffect.Heal` case in `ApplyUnitOption` (lines 519-524):

```csharp
            case UnitEffect.Heal:
            {
                var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
                var assignment = new HealAssignment();
                healAssignments[option] = assignment;
                hand.OpenHeal(option.amount, assignment);
                break;
            }
```

Replace the `UnitEffect.Heal` case in the revert method (lines 568-573):

```csharp
            case UnitEffect.Heal:
            {
                var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
                if (healAssignments.TryGetValue(option, out var done))
                {
                    hand.UndoHeal(done);
                    healAssignments.Remove(option);
                }
                break;
            }
```

- [ ] **Step 6: Route the skill heal**

Replace the `SkillEffect.HealWound` case in `ApplySkill` (lines 618-627):

```csharp
            case SkillEffect.HealWound:
            {
                var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
                if (sign > 0)
                {
                    var assignment = new HealAssignment();
                    healAssignments[skill] = assignment;
                    hand.OpenHeal(skill.magnitude, assignment);
                }
                else if (healAssignments.TryGetValue(skill, out var done))
                {
                    hand.UndoHeal(done);
                    healAssignments.Remove(skill);
                }
                break;
            }
```

- [ ] **Step 7: Compile-check**

Confirm the assembly builds with zero errors. `Player.cs` needs `using System.Collections.Generic;` — it almost certainly already has it.

- [ ] **Step 8: Human verification**

Ask the user to confirm in the editor:
1. Take a hand wound and wound a unit. Play a Heal 2 card → the picker lists both, the unit's row costs its wound count.
2. Heal the unit, then undo the card → the unit is wounded again and the card returns to hand.
3. Heal a hand wound, then undo → the wound card comes back.
4. A venom-struck unit (2 wounds) locks under a Heal 1.
5. Town heal opens the same picker with the town's `healLevel` as budget.

- [ ] **Step 9: Commit**

```bash
git add Assets/Scripts/UnitPlay/HealAssignment.cs Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs
git commit -m "Route every heal through the unit picker with snapshot undo"
```

---

## Task 13: Army HUD readout

Fully independent of every other task — it could be done first or last.

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/ArmyHud.cs`

**Interfaces:**
- Consumes: `Player.Units`, `Player.ArmyCap`, `ArmyRules.NeedsDisband`, `Unit.IsWounded` (Task 5).
- Produces: nothing.

- [ ] **Step 1: Create the component**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/ArmyHud.cs`:

```csharp
using TMPro;
using UnityEngine;

// Army size readout (spec 2026-08-12 §7): "army 1/2".
//
// Update-polled rather than event-driven on purpose. An IntEvent + listener
// means more hand-wiring in the scene and re-exposes the Static-vs-Dynamic
// dropdown footgun that silently pins listeners to 0. HealButton, CombatButtons
// and CombatController all already refresh per frame; polling two ints is
// in-idiom and costs nothing.
public class ArmyHud : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;
    // Optional. "2/2" is ambiguous between "full army" and "full army, both
    // down", so this renders a wounded count when there is one. Leave unassigned
    // to hide it entirely.
    [SerializeField] TextMeshProUGUI woundedLabel;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color atCapColor = new Color(0.95f, 0.80f, 0.30f);

    Player player;

    void Update()
    {
        if (label == null) return;
        if (player == null) player = FindAnyObjectByType<Player>();
        if (player == null) return;

        int count = player.Units.Count;
        int cap = player.ArmyCap;

        label.text = $"{IconMarkup.Tag(IconConcept.Army)} {count}/{cap}";
        // Same predicate the recruit flow uses to decide whether hiring opens the
        // disband picker, so the HUD and that flow can never disagree.
        label.color = ArmyRules.NeedsDisband(count, cap) ? atCapColor : normalColor;

        if (woundedLabel == null) return;
        int wounded = 0;
        foreach (var unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
            if (unit.IsWounded) wounded++;
        woundedLabel.gameObject.SetActive(wounded > 0);
        woundedLabel.text = $"{wounded} {IconMarkup.Tag(IconConcept.Wound)}";
    }
}
```

- [ ] **Step 2: Compile-check**

Confirm the assembly builds with zero errors. If `ArmyRules.NeedsDisband` has a different parameter order, check:

```powershell
Select-String -Path "Assets\Scripts\**\ArmyRules.cs" -Pattern "NeedsDisband"
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlayerScripts/ArmyHud.cs
git commit -m "Add army size HUD readout"
```

---

## Task 14: Editor wiring (human)

These are authored in the Unity editor, never by editing YAML. Hand the user this list.

- [ ] **Step 1: Unit prefab — wound decal**

1. Open the Unit prefab (the one `Player.unitPrefab` points at).
2. Add a child Image for the wound decal; leave it disabled.
3. Add a child TextMeshProUGUI for the wound count; leave it disabled.
4. On the `Unit` component, assign **Wound Decal** and **Wound Count Label**, and pick a **Wounded Red**.
5. Save the prefab.

- [ ] **Step 2: `UnitPickerPanel` canvas sort order**

Confirm the picker's Canvas sorts **above** the combat canvas — the wound picker opens mid-fight and must not render behind the enemy cards. Raise its `Sort Order` if needed.

- [ ] **Step 3: Army HUD**

1. Add an `ArmyHud` component to the HUD.
2. Add a TextMeshProUGUI for the readout and assign it to **Label**.
3. Optionally add a second one and assign it to **Wounded Label**; leave unassigned to hide it.

- [ ] **Step 4: Author `armorClass` on every unit**

Open each `UnitsSO` asset and set `armorClass` per the bands in the spec: cheap (2–3 Influence) → **1**, standard (3–4) → **2**, premium (5+) → **3**, support/caster → **0**.

- [ ] **Step 5: Full playthrough check**

Fight → shelter with a unit → confirm the wound lands on it, it turns red, and it refuses to open its inspector. Save, quit, reload → the unit is still wounded. Heal it at a town → it becomes usable again.

- [ ] **Step 6: Commit any asset changes**

```bash
git add Assets/
git commit -m "Wire unit wound decal, army HUD, and author unit armor classes"
```

---

## Task 15: Documentation

**Files:**
- Modify: `.claude/skills/archons-rise-design/mechanics.md`
- Modify: `.claude/skills/archons-rise-design/content-rules.md`
- Modify: `.claude/skills/archons-rise-design/balance.md`
- Modify: `.claude/skills/archons-rise-roadmap/decisions-log.md`

- [ ] **Step 1: `mechanics.md` — Units section**

Append to the `## Units` section:

```markdown
**Unit armor and wounds** (spec 2026-08-12): each unit carries an `armorClass`. During the Defend
phase, pressing the advance button opens a picker listing every unwounded unit (exhausted ones
included — taking a hit is not *using* the unit). Committing units sums their armor into the Defend
pool before the counterattack comparison runs — **armor is Defend, bought with a unit wound instead
of a card** — and each committed unit takes **1 wound** (2 if any unblocked enemy is Toxic). A
wounded unit is unusable until healed, exactly like exhaustion, but persists across rounds. **Unit
wounds do not count toward wound-out**, which is what makes units a wound sink that trades run-loss
pressure for army capability. Two traits are exceptions: **Leech** reads the pre-soak number and
always steals, and **Toxic** transfers — its discard wounds become 0 and each committed unit takes 2.
The army cap is the implicit stacking limit. Healing routes through the same picker: hand wounds and
wounded units compete for one budget, and healing a unit is atomic (its full wound count or nothing).
```

- [ ] **Step 2: `mechanics.md` — correct the stale Toxic line**

In the `## Lose — Wounds (tactical)` section, replace:

> A trait (Toxic, spec 2026-07-29) can instead place a wound into the **discard**

with:

> A trait (Toxic, spec 2026-07-29) **additionally** places wounds into the **discard** — it does not
> divert them from the hand; `EnemyTraitRules.HandWounds` is unchanged by Toxic, and the discard
> copies are extra (pinned by `EnemyTraitShelterTests`)

**Only do this step if `Toxic_AddsDiscardCopies_RatherThanDivertingFromHand` passed in Task 8.** If it failed, the code is the bug and this correction must not be made.

- [ ] **Step 3: `content-rules.md` — `UnitsSO` and `healLevel`**

Add to the `UnitsSO` field table:

```markdown
| `armorClass` | int | Defend soaked when committed as a shield (spec 2026-08-12). `0` = cannot shield. Clamped ≥ 0 by `OnValidate` |
```

Add to the `TownsSO` rules:

```markdown
**Note:** `healLevel` carries **two** meanings — it is both the Influence price of the heal service
*and* the heal budget it grants. Coherent as tuning ("1 Influence per wound"), but change one and
you change the other.
```

- [ ] **Step 4: `balance.md` — new section**

Append:

```markdown
## Unit Armor (spec 2026-08-12)
- `armorClass` scales with recruit price so armor is bought, not free:
  **cheap (2–3 Influence) → 1**, **standard (3–4) → 2**, **premium (5+) → 3**, **support/caster → 0**.
- **The army cap is already the stacking limit** — it starts at 1 and rises only on milestone
  level-ups, so unlimited stacking can never exceed a level-gated resource. No separate soak cap.
- Toxic makes a unit a **heal-2** problem, which a per-round Field Medic (heal 1) can never clear
  alone — that is the intended cost, not an oversight.
- _Starting values — tune in playtest._
```

- [ ] **Step 5: `decisions-log.md`**

Append:

```markdown
## 2026-08-12 — Unit wounds and armor
- **Unit armor is a flat soak added to Defend**, not a Toughness-style divisor. Reverses
  `2026-07-29-enemy-traits-and-wound-plumbing-design.md` §6.4. A divisor makes a unit's armor
  unreadable at a glance; soak means armor and Defend are the same thing, which is the whole design.
- **Toxic transfers rather than bypasses.** §6.4 planned "Toxic bypasses unit armor"; instead its
  discard wounds become 0 and each committed unit takes 2. More legible, and it makes Toxic a heal
  *size* threshold rather than just more of the same.
- **`WoundDestination.Unit = 2` retired unused.** Units never receive a wound card — the soak happens
  before the wound math — so the reserved destination was never needed.
- **Units stack freely**; the army cap is the implicit limit.
- **Leech reads pre-soak** and is the only trait a unit wall cannot blunt.
- **Shelter applies to the Defend-phase counterattack only** — not flee wounds, not Vengeful.
- **Town heal now refuses when nothing is wounded.** It previously spent both the Influence and the
  visit's action for no effect, unundoably.
```

- [ ] **Step 6: Commit**

```bash
git add .claude/skills/
git commit -m "Document unit wounds, armor, and the town heal guard"
```

---

## Self-Review

**Spec coverage:** §1 principle → Task 8. §3 armor + soak math → Tasks 4, 8. §4 picker → Tasks 9, 10. §5 unit state → Tasks 5, 6. §6.1–6.3 healing → Tasks 11, 12. §6.4 town guard → Tasks 1, 2, 3, 6. §6.5 disband shows wound state → **gap, see below.** §7 army HUD → Task 13. §8 persistence → Task 7. §10 balance → Tasks 14, 15. §11 tests → Tasks 1, 2, 6, 7, 8. §12 manual edits → Task 14. §13 docs → Task 15.

**One gap found and left deliberately open:** §6.5 asks for `DisbandPanel` to show wound state. It is a one-line label change in a file this plan does not otherwise touch, and it depends on nothing. It is folded into Task 14 Step 4's editor pass only if the panel renders unit names from a script — otherwise it needs its own small task. Flag it to the user rather than guessing at a file that was never read during design.

**Type consistency:** `RefreshRules.CanPick` gains `wounded` as parameter 2 in Task 6, and both callers are updated in the same task. `TownActionSnapshot` gains `anyWoundToHeal` after `anyUnitAffordable` in Task 2, and its only construction site is updated in Task 3. `Player.RebuildUnits` gains `int[] wounds` as an optional third parameter in Task 7, and its only caller is updated in the same task. `HealTarget` is defined in Task 11 and consumed in Task 12. `CounterattackOutcome` is defined in Task 8 and consumed in Tasks 9 and 10. `HealAssignment` is defined in Task 12 and used only there.
