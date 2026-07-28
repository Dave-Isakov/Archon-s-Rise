# Place UI Spine & Fan — Implementation Plan (Plan 1 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the full-screen town and dungeon canvases (and shrine entry) with a small arc of icon buttons over the player's head, on top of a reusable spine so a new place type is one subclass plus one rules method.

**Architecture:** A pure `PlaceActionRules` turns a per-place snapshot into an ordered `PlaceAction` list. A single `PlaceFan` renders that list through a shared `FanLayout` (extracted from `ShrinePanel`) and routes clicks back to `token.Dispatch(id)` — so the fan never learns about place types. `PlaceTokenBase` absorbs the entry sequence the three tokens currently duplicate.

**Tech Stack:** Unity 6000.5.1f1, C# (Mono/mcs for pure tests), NUnit, TextMeshPro, Unity UI (uGUI).

Spec: `docs/superpowers/specs/2026-07-28-minimal-place-ui-and-player-log-design.md`

## Global Constraints

- **Never hand-edit scene or prefab YAML.** All scene/prefab wiring is authored manually by the user in the Unity editor. Where a task needs wiring, it produces step-by-step editor instructions and stops.
- **Pure classes are tested from the CLI**, not batch-mode Unity: the editor lock makes `-runTests` unreliable while the editor is open. Use `tools/pure-tests/run.sh` (committed, verified working).
- **Pure = no `UnityEngine` reference.** Anything in `Assets/Scripts/Places/` must compile without Unity.
- **Adding an `IconConcept` member requires three things or the validation tests go red:** an `IconMarkup.TmpName` case, a TMP sprite asset at `Assets/TextMesh Pro/Resources/Sprite Assets/<name>`, and an entry in `Assets/Resources/IconRegistry.asset`. See `IconRegistryValidationTests`.
- **Icon + amount only, never words** on fan slots (the shipped Play/Convert convention).
- **Locked/unaffordable look is always `UiLock.Apply(canvasGroup, locked)`** (alpha 0.4), paired with `Button.interactable = false`.
- **Opening a place is a free peek.** The turn's action is committed by the service, never by opening the fan.
- Commit after every task. Do not skip hooks.

---

## File Structure

**Pure layer — `Assets/Scripts/Places/` (`ArchonsRise.Places`, no Unity dependency)**
- `PlaceActionId.cs` — the action enum.
- `PlaceAction.cs` — the descriptor struct + the three snapshot structs.
- `PlaceActionRules.cs` — `ForTown` / `ForDungeon` / `ForShrine`.
- `ArchonsRise.Places.asmdef` — gains an `ArchonsRise.UiLanguage` reference (for `IconConcept`).

**Icon language — `Assets/Scripts/UiLanguage/`**
- `IconConcept.cs`, `IconMarkup.cs` — two new concepts: `Card`, `Menu`.

**Scene layer — `Assets/Scripts/GameObjectScripts/PlaceUI/` (new folder, main assembly)**
- `FanLayout.cs` — pooling + `FanMath.Solve` + placement + parking. Shared by `PlaceFan` and `ShrinePanel`.
- `ClickOffCatcher.cs` — full-screen transparent catcher firing a `UnityEvent`.
- `PlaceFanSlot.cs` — one slot: icon, cost badge, button, lock group.
- `PlaceFan.cs` — binds an action list, dispatches ids, re-gates live.
- `FanPreviewTrigger.cs` — hover preview for Assault and Delve slots.

**Tokens — `Assets/Scripts/GameObjectScripts/GameBoardObjects/`**
- `PlaceTokenBase.cs` — new shared base.
- `TownToken.cs`, `DungeonToken.cs`, `ShrineToken.cs` — converted to subclasses.

**Tests — `Assets/Tests/EditMode/`**
- `PlaceActionRulesTests.cs` — new.
- `IconMarkupTests.cs` — extended for the two new concepts.

---

### Task 1: Add the `Card` and `Menu` icon concepts

The fan needs a glyph for the Cards service and one for the ledger (open-full-menu) slot. Every other action reuses an existing concept: Assault→`Attack`, Recruit→`Army`, Heal→`Heal`, Crystal→`Crystal`, Delve→`Dungeon`, Engage→`Crystal`.

**Files:**
- Modify: `Assets/Scripts/UiLanguage/IconConcept.cs`
- Modify: `Assets/Scripts/UiLanguage/IconMarkup.cs:18-40`
- Modify: `Assets/Tests/EditMode/IconMarkupTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `IconConcept.Card`, `IconConcept.Menu`; `IconMarkup.TmpName(IconConcept.Card) == "card"`, `IconMarkup.TmpName(IconConcept.Menu) == "menu"`.

- [ ] **Step 1: Write the failing test**

Append these two assertions inside the existing `Tag_NewConceptsGetNewNames` test in `Assets/Tests/EditMode/IconMarkupTests.cs`, after the `refresh` line:

```csharp
        Assert.AreEqual("<sprite=\"card\" index=0>", IconMarkup.Tag(IconConcept.Card));
        Assert.AreEqual("<sprite=\"menu\" index=0>", IconMarkup.Tag(IconConcept.Menu));
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
tools/pure-tests/run.sh \
  Assets/Scripts/Enums/Enums/EmpowerType.cs \
  Assets/Scripts/UiLanguage/IconConcept.cs \
  Assets/Scripts/UiLanguage/IconMarkup.cs \
  Assets/Tests/EditMode/IconMarkupTests.cs
```

Expected: compilation fails with `error CS0117: 'IconConcept' does not contain a definition for 'Card'`.

- [ ] **Step 3: Add the enum members**

In `Assets/Scripts/UiLanguage/IconConcept.cs`, add two members at the **end** of the enum (appending keeps existing serialized values stable — never reorder):

```csharp
    Refresh,
    Card,
    Menu,
}
```

- [ ] **Step 4: Add the TmpName cases**

In `Assets/Scripts/UiLanguage/IconMarkup.cs`, add these two cases immediately before `default:`:

```csharp
            case IconConcept.Card:       return "card";
            case IconConcept.Menu:       return "menu";
```

- [ ] **Step 5: Run the test to verify it passes**

Run the same command as Step 2.
Expected: `--- N passed, 0 failed ---` with every `IconMarkupTests` line PASS, including `TmpName_NonEmptyForEveryConcept`.

- [ ] **Step 6: Commit the code**

```bash
git add Assets/Scripts/UiLanguage/IconConcept.cs Assets/Scripts/UiLanguage/IconMarkup.cs Assets/Tests/EditMode/IconMarkupTests.cs
git commit -m "feat: add Card and Menu icon concepts for the place fan"
```

- [ ] **Step 7: Hand the user the editor authoring steps**

`IconRegistryValidationTests` runs inside Unity and will stay RED until these are authored. Give the user exactly this and stop:

> 1. Create two single-glyph TMP Sprite Assets named `card` and `menu` in `Assets/TextMesh Pro/Resources/Sprite Assets/`. (Copy an existing one such as `dungeon` and swap its texture — the name must match exactly and is case-sensitive.)
> 2. Open `Assets/Resources/IconRegistry.asset`. Add two entries: concept `Card` → the card sprite, concept `Menu` → the ledger/menu sprite.
> 3. Run the EditMode tests in Unity. `IconRegistryValidationTests.RegistryAssetIsComplete` and `EveryConceptTmpAssetResolves` must both be green before continuing to Task 2.

---

### Task 2: Pure descriptor types + `ForTown`

**Files:**
- Create: `Assets/Scripts/Places/PlaceActionId.cs`
- Create: `Assets/Scripts/Places/PlaceAction.cs`
- Create: `Assets/Scripts/Places/PlaceActionRules.cs`
- Create: `Assets/Tests/EditMode/PlaceActionRulesTests.cs`
- Modify: `Assets/Scripts/Places/ArchonsRise.Places.asmdef`

**Interfaces:**
- Consumes: `PlaceRules.AllowedServices(PlaceType)`, `PlaceService`, `IconConcept` (Task 1).
- Produces:
  - `enum PlaceActionId { Assault, Recruit, Heal, Cards, Crystal, Delve, Engage, OpenMenu }`
  - `struct PlaceAction { PlaceActionId Id; IconConcept Icon; IconConcept? CostIcon; int CostAmount; bool Enabled; }`
  - `struct TownActionSnapshot(PlaceType placeType, bool conquered, int guardiansRemaining, int influence, int healCost, bool anyUnitAffordable, bool visitCanAct, bool hasMenu)`
  - `PlaceActionRules.ForTown(TownActionSnapshot) -> IReadOnlyList<PlaceAction>`

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/PlaceActionRulesTests.cs`:

```csharp
using NUnit.Framework;

public class PlaceActionRulesTests
{
    // A conquered Town with everything affordable and the action unspent.
    static TownActionSnapshot Town(PlaceType type = PlaceType.Town, bool conquered = true,
        int guardiansRemaining = 0, int influence = 99, int healCost = 3,
        bool anyUnitAffordable = true, bool visitCanAct = true, bool hasMenu = true)
        => new TownActionSnapshot(type, conquered, guardiansRemaining, influence, healCost,
            anyUnitAffordable, visitCanAct, hasMenu);

    [Test]
    public void Unconquered_AssaultThenMenuOnly()
    {
        var actions = PlaceActionRules.ForTown(Town(conquered: false, guardiansRemaining: 2));
        Assert.AreEqual(2, actions.Count);
        Assert.AreEqual(PlaceActionId.Assault, actions[0].Id);
        Assert.AreEqual(2, actions[0].CostAmount, "assault badge shows guardians remaining");
        Assert.IsNull(actions[0].CostIcon, "the guardian count is a bare number, not a cost");
        Assert.AreEqual(PlaceActionId.OpenMenu, actions[1].Id);
    }

    [Test]
    public void ConqueredTown_RecruitHealCrystalThenMenu()
    {
        var actions = PlaceActionRules.ForTown(Town());
        Assert.AreEqual(4, actions.Count);
        Assert.AreEqual(PlaceActionId.Recruit, actions[0].Id);
        Assert.AreEqual(PlaceActionId.Heal, actions[1].Id);
        Assert.AreEqual(PlaceActionId.Crystal, actions[2].Id);
        Assert.AreEqual(PlaceActionId.OpenMenu, actions[3].Id);
    }

    [Test]
    public void ConqueredCastle_IncludesCardsButDisabled()
    {
        var actions = PlaceActionRules.ForTown(Town(PlaceType.Castle));
        var cards = actions.Find(a => a.Id == PlaceActionId.Cards);
        Assert.AreEqual(PlaceActionId.Cards, cards.Id, "Castle must offer the Cards slot");
        Assert.IsFalse(cards.Enabled, "Cards is an M2 stub and must render locked");
    }

    [Test]
    public void HealShowsItsInfluenceCostAndLocksWhenUnaffordable()
    {
        var actions = PlaceActionRules.ForTown(Town(influence: 2, healCost: 3));
        var heal = actions.Find(a => a.Id == PlaceActionId.Heal);
        Assert.AreEqual(3, heal.CostAmount);
        Assert.AreEqual(IconConcept.Influence, heal.CostIcon);
        Assert.IsFalse(heal.Enabled);
    }

    [Test]
    public void RecruitLocksWhenNoUnitAffordable()
    {
        var actions = PlaceActionRules.ForTown(Town(anyUnitAffordable: false));
        Assert.IsFalse(actions.Find(a => a.Id == PlaceActionId.Recruit).Enabled);
    }

    [Test]
    public void ActionSpent_ServicesLockButMenuStaysOpen()
    {
        var actions = PlaceActionRules.ForTown(Town(visitCanAct: false));
        foreach (var a in actions)
            if (a.Id != PlaceActionId.OpenMenu)
                Assert.IsFalse(a.Enabled, a.Id + " must lock once the action is spent");
        Assert.IsTrue(actions.Find(a => a.Id == PlaceActionId.OpenMenu).Enabled,
            "the ledger is a free peek and never locks");
    }
}
```

Note: `actions.Find(...)` requires the return type to be `List<PlaceAction>`; declare the method's return type as `List<PlaceAction>` so the tests can use `Find` and `Count` directly.

- [ ] **Step 2: Run the test to verify it fails**

```bash
tools/pure-tests/run.sh \
  Assets/Scripts/Enums/Enums/EmpowerType.cs \
  Assets/Scripts/UiLanguage/IconConcept.cs \
  Assets/Scripts/Places/PlaceType.cs \
  Assets/Scripts/Places/PlaceService.cs \
  Assets/Scripts/Places/PlaceRules.cs \
  Assets/Tests/EditMode/PlaceActionRulesTests.cs
```

Expected: compilation fails with `error CS0246: The type or namespace name 'TownActionSnapshot' could not be found`.

- [ ] **Step 3: Create the enum**

`Assets/Scripts/Places/PlaceActionId.cs`:

```csharp
// One member per thing a player can press on a place fan. OpenMenu is the
// ledger slot that opens the place's full detail menu, where one exists.
public enum PlaceActionId
{
    Assault,
    Recruit,
    Heal,
    Cards,
    Crystal,
    Delve,
    Engage,
    OpenMenu,
}
```

- [ ] **Step 4: Create the descriptor and snapshot structs**

`Assets/Scripts/Places/PlaceAction.cs`:

```csharp
// One slot on a place fan: what glyph it shows, what it costs, and whether it
// can be pressed right now. Pure — the fan renders this, the token dispatches it.
public readonly struct PlaceAction
{
    public readonly PlaceActionId Id;
    public readonly IconConcept Icon;
    // null renders the amount as a bare number (e.g. guardians remaining, which
    // is a count, not a price). Ignored entirely when CostAmount is 0.
    public readonly IconConcept? CostIcon;
    public readonly int CostAmount;   // 0 = no badge
    public readonly bool Enabled;     // false => UiLock dim + non-interactable

    public PlaceAction(PlaceActionId id, IconConcept icon, IconConcept? costIcon,
        int costAmount, bool enabled)
    {
        Id = id;
        Icon = icon;
        CostIcon = costIcon;
        CostAmount = costAmount;
        Enabled = enabled;
    }
}

public readonly struct TownActionSnapshot
{
    public readonly PlaceType PlaceType;
    public readonly bool Conquered;
    public readonly int GuardiansRemaining;
    public readonly int Influence;
    public readonly int HealCost;
    public readonly bool AnyUnitAffordable;
    public readonly bool VisitCanAct;
    public readonly bool HasMenu;

    public TownActionSnapshot(PlaceType placeType, bool conquered, int guardiansRemaining,
        int influence, int healCost, bool anyUnitAffordable, bool visitCanAct, bool hasMenu)
    {
        PlaceType = placeType;
        Conquered = conquered;
        GuardiansRemaining = guardiansRemaining;
        Influence = influence;
        HealCost = healCost;
        AnyUnitAffordable = anyUnitAffordable;
        VisitCanAct = visitCanAct;
        HasMenu = hasMenu;
    }
}
```

- [ ] **Step 5: Create the rules**

`Assets/Scripts/Places/PlaceActionRules.cs`:

```csharp
using System.Collections.Generic;

// Which actions a place offers right now, in canonical order. Pure: no scene,
// no Unity. Order is authored here so the fan never reshuffles between opens.
//
// The turn's action gates every SERVICE but never the ledger — opening a place
// is a free peek (spec 2026-07-22), so a player who already acted can still look.
public static class PlaceActionRules
{
    public static List<PlaceAction> ForTown(TownActionSnapshot s)
    {
        var list = new List<PlaceAction>();

        if (!s.Conquered)
        {
            // The badge is a guardian COUNT, so it carries no cost icon.
            list.Add(new PlaceAction(PlaceActionId.Assault, IconConcept.Attack,
                null, s.GuardiansRemaining, s.VisitCanAct));
        }
        else
        {
            var allowed = PlaceRules.AllowedServices(s.PlaceType);

            if ((allowed & PlaceService.Recruit) != 0)
                list.Add(new PlaceAction(PlaceActionId.Recruit, IconConcept.Army,
                    null, 0, s.AnyUnitAffordable && s.VisitCanAct));

            if ((allowed & PlaceService.Heal) != 0)
                list.Add(new PlaceAction(PlaceActionId.Heal, IconConcept.Heal,
                    IconConcept.Influence, s.HealCost,
                    s.Influence >= s.HealCost && s.VisitCanAct));

            // M2 stub: the slot is shown so the place reports itself honestly,
            // but buying is disabled until the purchase economics land.
            if ((allowed & PlaceService.Cards) != 0)
                list.Add(new PlaceAction(PlaceActionId.Cards, IconConcept.Card,
                    null, 0, false));

            // Per-color affordability is shown inside the picker itself, so the
            // slot only gates on the turn's action.
            if ((allowed & PlaceService.Crystal) != 0)
                list.Add(new PlaceAction(PlaceActionId.Crystal, IconConcept.Crystal,
                    null, 0, s.VisitCanAct));
        }

        AppendMenu(list, s.HasMenu);
        return list;
    }

    // The ledger slot: always last, always enabled, and only for places that
    // actually have a detail menu (shrines do not, so they get no dead button).
    static void AppendMenu(List<PlaceAction> list, bool hasMenu)
    {
        if (hasMenu)
            list.Add(new PlaceAction(PlaceActionId.OpenMenu, IconConcept.Menu, null, 0, true));
    }
}
```

- [ ] **Step 6: Add the asmdef reference**

`PlaceAction` uses `IconConcept`, which lives in `ArchonsRise.UiLanguage`. Edit `Assets/Scripts/Places/ArchonsRise.Places.asmdef` so its `references` array reads:

```json
    "references": ["ArchonsRise.SaveData", "ArchonsRise.UiLanguage"],
```

(`ArchonsRise.UiLanguage` references only `ArchonsRise.Enums`, so this introduces no cycle.)

- [ ] **Step 7: Run the tests to verify they pass**

```bash
tools/pure-tests/run.sh \
  Assets/Scripts/Enums/Enums/EmpowerType.cs \
  Assets/Scripts/UiLanguage/IconConcept.cs \
  Assets/Scripts/Places/PlaceType.cs \
  Assets/Scripts/Places/PlaceService.cs \
  Assets/Scripts/Places/PlaceRules.cs \
  Assets/Scripts/Places/PlaceActionId.cs \
  Assets/Scripts/Places/PlaceAction.cs \
  Assets/Scripts/Places/PlaceActionRules.cs \
  Assets/Tests/EditMode/PlaceActionRulesTests.cs
```

Expected: `--- 6 passed, 0 failed ---`.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Places/ Assets/Tests/EditMode/PlaceActionRulesTests.cs
git commit -m "feat: pure PlaceActionRules.ForTown + action descriptor types"
```

---

### Task 3: `ForDungeon` and `ForShrine`

**Files:**
- Modify: `Assets/Scripts/Places/PlaceAction.cs` (add two snapshot structs)
- Modify: `Assets/Scripts/Places/PlaceActionRules.cs`
- Modify: `Assets/Tests/EditMode/PlaceActionRulesTests.cs`

**Interfaces:**
- Consumes: `PlaceAction`, `PlaceActionId`, `AppendMenu` (Task 2).
- Produces:
  - `struct DungeonActionSnapshot(bool complete, int explore, int delveCost, bool visitCanAct, bool hasMenu)`
  - `struct ShrineActionSnapshot(bool isLive, int crystalCost, bool visitCanAct)`
  - `PlaceActionRules.ForDungeon(DungeonActionSnapshot) -> List<PlaceAction>`
  - `PlaceActionRules.ForShrine(ShrineActionSnapshot) -> List<PlaceAction>`

`ShrineActionSnapshot` deliberately takes a plain `bool isLive` rather than `ShrineVisualState`: that keeps `ArchonsRise.Places` free of a dependency on `ArchonsRise.Shrines`, and the rules genuinely only care whether the shrine is engageable.

- [ ] **Step 1: Write the failing tests**

Append to `Assets/Tests/EditMode/PlaceActionRulesTests.cs`, inside the class:

```csharp
    [Test]
    public void Dungeon_DelveShowsExploreCostThenMenu()
    {
        var actions = PlaceActionRules.ForDungeon(
            new DungeonActionSnapshot(false, 5, 2, true, true));
        Assert.AreEqual(2, actions.Count);
        Assert.AreEqual(PlaceActionId.Delve, actions[0].Id);
        Assert.AreEqual(IconConcept.Explore, actions[0].CostIcon);
        Assert.AreEqual(2, actions[0].CostAmount);
        Assert.IsTrue(actions[0].Enabled);
        Assert.AreEqual(PlaceActionId.OpenMenu, actions[1].Id);
    }

    [Test]
    public void Dungeon_DelveLocksBelowExploreCost()
    {
        var actions = PlaceActionRules.ForDungeon(
            new DungeonActionSnapshot(false, 1, 2, true, true));
        Assert.IsFalse(actions[0].Enabled);
    }

    [Test]
    public void Dungeon_CompleteLeavesMenuOnly()
    {
        var actions = PlaceActionRules.ForDungeon(
            new DungeonActionSnapshot(true, 9, 2, true, true));
        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual(PlaceActionId.OpenMenu, actions[0].Id);
    }

    [Test]
    public void LiveShrine_EngageOnlyWithNoLedgerSlot()
    {
        var actions = PlaceActionRules.ForShrine(new ShrineActionSnapshot(true, 4, true));
        Assert.AreEqual(1, actions.Count, "a shrine has no detail menu, so no ledger slot");
        Assert.AreEqual(PlaceActionId.Engage, actions[0].Id);
        Assert.AreEqual(IconConcept.Crystal, actions[0].CostIcon);
        Assert.AreEqual(4, actions[0].CostAmount);
        Assert.IsTrue(actions[0].Enabled);
    }

    [Test]
    public void SpentOrGuardedShrine_EngagePresentButDisabled()
    {
        var actions = PlaceActionRules.ForShrine(new ShrineActionSnapshot(false, 4, true));
        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual(PlaceActionId.Engage, actions[0].Id);
        Assert.IsFalse(actions[0].Enabled,
            "a spent or guarded shrine shows a locked slot, never a message");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the Step 7 command from Task 2.
Expected: compilation fails with `error CS0246: The type or namespace name 'DungeonActionSnapshot' could not be found`.

- [ ] **Step 3: Add the snapshot structs**

Append to `Assets/Scripts/Places/PlaceAction.cs`:

```csharp
public readonly struct DungeonActionSnapshot
{
    public readonly bool Complete;
    public readonly int Explore;
    public readonly int DelveCost;
    public readonly bool VisitCanAct;
    public readonly bool HasMenu;

    public DungeonActionSnapshot(bool complete, int explore, int delveCost,
        bool visitCanAct, bool hasMenu)
    {
        Complete = complete;
        Explore = explore;
        DelveCost = delveCost;
        VisitCanAct = visitCanAct;
        HasMenu = hasMenu;
    }
}

// isLive rather than ShrineVisualState on purpose: it keeps ArchonsRise.Places
// independent of ArchonsRise.Shrines, and the rule only cares whether the
// shrine can still be engaged.
public readonly struct ShrineActionSnapshot
{
    public readonly bool IsLive;
    public readonly int CrystalCost;
    public readonly bool VisitCanAct;

    public ShrineActionSnapshot(bool isLive, int crystalCost, bool visitCanAct)
    {
        IsLive = isLive;
        CrystalCost = crystalCost;
        VisitCanAct = visitCanAct;
    }
}
```

- [ ] **Step 4: Add the two rules methods**

Insert into `PlaceActionRules`, immediately before `AppendMenu`:

```csharp
    public static List<PlaceAction> ForDungeon(DungeonActionSnapshot s)
    {
        var list = new List<PlaceAction>();
        if (!s.Complete)
            list.Add(new PlaceAction(PlaceActionId.Delve, IconConcept.Dungeon,
                IconConcept.Explore, s.DelveCost,
                s.Explore >= s.DelveCost && s.VisitCanAct));

        AppendMenu(list, s.HasMenu);
        return list;
    }

    // A shrine always shows its Engage slot — a spent or guarded one renders
    // locked rather than firing a message, so the click is never a no-op.
    // Shrines have no detail menu, hence no ledger slot.
    public static List<PlaceAction> ForShrine(ShrineActionSnapshot s)
    {
        var list = new List<PlaceAction>
        {
            new PlaceAction(PlaceActionId.Engage, IconConcept.Crystal,
                IconConcept.Crystal, s.CrystalCost, s.IsLive && s.VisitCanAct),
        };
        AppendMenu(list, false);
        return list;
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run the Step 7 command from Task 2.
Expected: `--- 11 passed, 0 failed ---`.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Places/ Assets/Tests/EditMode/PlaceActionRulesTests.cs
git commit -m "feat: PlaceActionRules.ForDungeon and ForShrine"
```

---

### Task 4: `ClickOffCatcher`

A tiny standalone component the fan needs; the wider sweep across other menus is Plan 2.

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/PlaceUI/ClickOffCatcher.cs`

**Interfaces:**
- Produces: `ClickOffCatcher` MonoBehaviour with a public `UnityEvent onClickOff` and `SetActive(bool)`.

- [ ] **Step 1: Write the component**

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

// The one dismiss gesture (spec 2026-07-28): a full-screen transparent Image
// behind a surface's content. Clicking anywhere that is not content closes it.
//
// MUST sit at sibling index 0 within its surface so it renders BEHIND the
// content — at any other index it swallows the clicks it is meant to sit under.
//
// The GameObject stays active while disarmed (only raycastTarget is toggled) so
// any event listeners on it keep receiving events, matching the pattern the
// retired CrystalDismissCatcher used.
[RequireComponent(typeof(Image))]
public class ClickOffCatcher : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Invoked when the player clicks off the surface. Wire the surface's close method here.")]
    public UnityEvent onClickOff;

    Image image;

    void Awake()
    {
        image = GetComponent<Image>();
        // A fully transparent Image still receives raycasts, which is exactly
        // what we want: invisible, but clickable.
        var c = image.color;
        c.a = 0f;
        image.color = c;
    }

    // Arm when the surface opens, disarm when it closes.
    public void SetArmed(bool armed)
    {
        if (image == null) image = GetComponent<Image>();
        image.raycastTarget = armed;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (image != null && !image.raycastTarget) return;
        onClickOff?.Invoke();
    }
}
```

- [ ] **Step 2: Verify it compiles**

Ask the user to let Unity recompile and confirm the Console shows no errors. There is no pure test for this — it is entirely scene behaviour, verified in Task 9's play acceptance.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlaceUI/ClickOffCatcher.cs
git commit -m "feat: shared ClickOffCatcher component"
```

---

### Task 5: Extract `FanLayout` from `ShrinePanel`

`ShrinePanel` currently owns slot pooling, `FanMath.Solve`, and placement. `PlaceFan` needs the same. Extract it, and re-point `ShrinePanel` at it so the extraction is proven by working code rather than a second copy.

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/PlaceUI/FanLayout.cs`
- Modify: `Assets/Scripts/GameObjectScripts/ShrineMenuScripts/ShrinePanel.cs:131-162`

**Interfaces:**
- Consumes: `FanMath.Solve(int, FanSettings)`, `FanSlot` (from `ArchonsRise.Hand`).
- Produces: `FanLayout.Place(RectTransform[] items, FanSettings fan)` — solves for `items.Length` seats and applies position + tilt to each, in order.

- [ ] **Step 1: Write `FanLayout`**

```csharp
using System.Collections.Generic;
using UnityEngine;

// The shared arc renderer (spec 2026-07-28). Owns ONLY geometry: given some
// RectTransforms and the fan settings, seat them along the arc in order.
//
// Deliberately not a MonoBehaviour and deliberately ignorant of what the seats
// mean — ShrinePanel's seats are cycling payment slots, PlaceFan's are actions.
// Sharing geometry is the whole win; sharing semantics would force a mode flag
// into both.
//
// No per-hex projection is needed by either caller: a place is entered by
// STANDING on it and the camera rides PlayerPosition, so the place is always
// screen centre. Park the container at its authored offset and the arc lands.
public static class FanLayout
{
    public static void Place(IReadOnlyList<RectTransform> items, FanSettings fan)
    {
        if (items == null || items.Count == 0) return;
        var solved = FanMath.Solve(items.Count, fan);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;
            items[i].anchoredPosition = solved[i].AnchoredPosition;
            items[i].localRotation = Quaternion.Euler(0f, 0f, solved[i].TiltZ);
        }
    }
}
```

- [ ] **Step 2: Re-point `ShrinePanel` at it**

In `Assets/Scripts/GameObjectScripts/ShrineMenuScripts/ShrinePanel.cs`, replace the whole `Refresh` method and delete the now-unused private `Place` helper:

```csharp
    // Lays the fan out and paints each slot. The checkmark rides an APPENDED fan
    // position, so solving for one extra seat re-centres the arc as it appears —
    // and re-centres back if a slot is cycled to empty again.
    private readonly List<RectTransform> seats = new();

    private void Refresh()
    {
        if (current == null || picks == null) return;

        bool complete = ShrinePaymentRules.IsComplete(picks);

        seats.Clear();
        for (int i = 0; i < picks.Length && i < slots.Count; i++)
        {
            seats.Add((RectTransform)slots[i].transform);
            slots[i].Show(SpriteFor(picks[i]));
        }

        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(complete);
            if (complete) seats.Add((RectTransform)confirmButton.transform);
        }

        FanLayout.Place(seats, fan);

        if (confirmButton == null || !complete) return;

        // Opening a shrine is a free peek, so a player who already acted this turn
        // can still fill the slots — but the confirm shows locked (UiLock dim +
        // non-interactable), matching how DungeonPanel gates its Delve button.
        bool canAct = TurnPhaseController.Instance == null || TurnPhaseController.Instance.VisitCanAct;
        confirmButton.interactable = canAct;
        UiLock.Apply(confirmGroup, !canAct);
    }
```

Delete this method entirely — `FanLayout.Place` replaces it:

```csharp
    private static void Place(RectTransform rt, FanSlot slot)
    {
        rt.anchoredPosition = slot.AnchoredPosition;
        rt.localRotation = Quaternion.Euler(0f, 0f, slot.TiltZ);
    }
```

- [ ] **Step 3: Verify no behaviour change in the editor**

Ask the user to play, stand on a live shrine, and confirm: slots fan exactly as before; cycling a slot repaints; the checkmark appears when all slots are full and the arc re-centres; cycling back to empty removes it and re-centres again. **This must look identical to before — it is a pure refactor.** Stop until confirmed.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlaceUI/FanLayout.cs Assets/Scripts/GameObjectScripts/ShrineMenuScripts/ShrinePanel.cs
git commit -m "refactor: extract FanLayout from ShrinePanel"
```

---

### Task 6: `PlaceFanSlot` and `PlaceFan`

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/PlaceUI/PlaceFanSlot.cs`
- Create: `Assets/Scripts/GameObjectScripts/PlaceUI/PlaceFan.cs`

**Interfaces:**
- Consumes: `PlaceAction` (Task 2), `FanLayout` (Task 5), `ClickOffCatcher` (Task 4), `IconRegistrySO.Instance.SpriteFor(IconConcept)`, `UiLock.Apply`.
- Produces:
  - `PlaceFanSlot.Bind(PlaceAction action, Action<PlaceActionId> onClick)`
  - `PlaceFan.Instance` (lazy scene singleton), `PlaceFan.Open(IPlaceFanHost host)`, `PlaceFan.Dismiss()`, `PlaceFan.IsOpen`
  - `interface IPlaceFanHost { List<PlaceAction> BuildActions(); void Dispatch(PlaceActionId id); }`

- [ ] **Step 1: Write `PlaceFanSlot`**

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One slot on a place fan (spec 2026-07-28). Owns its own visuals; PlaceFan owns
// what the slot means. ONE prefab serves every action — the glyph is swapped at
// runtime from IconRegistry, so a new action needs a registry entry, not a prefab.
//
// Icon + amount only, never words (the shipped Play/Convert convention).
public class PlaceFanSlot : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI costLabel;  // hidden when the action is free
    [SerializeField] Button button;
    [SerializeField] CanvasGroup lockGroup;      // gets the UiLock dim

    // The action this slot currently shows, so hover previews can ask.
    public PlaceActionId Action { get; private set; }

    void Reset()
    {
        button = GetComponent<Button>();
        lockGroup = GetComponent<CanvasGroup>();
    }

    // Re-pointed on every Open so a pooled slot never reports a stale action.
    public void Bind(PlaceAction action, Action<PlaceActionId> onClick)
    {
        Action = action.Id;

        if (iconImage != null && IconRegistrySO.Instance != null)
            iconImage.sprite = IconRegistrySO.Instance.SpriteFor(action.Icon);

        if (costLabel != null)
        {
            bool showBadge = action.CostAmount != 0;
            costLabel.gameObject.SetActive(showBadge);
            if (showBadge)
                costLabel.text = action.CostIcon.HasValue
                    ? IconMarkup.Cost(action.CostIcon.Value, action.CostAmount)
                    : action.CostAmount.ToString();
        }

        if (button != null)
        {
            button.interactable = action.Enabled;
            button.onClick.RemoveAllListeners();
            var id = action.Id;
            button.onClick.AddListener(() => onClick(id));
        }

        UiLock.Apply(lockGroup, !action.Enabled);
    }
}
```

- [ ] **Step 2: Write `PlaceFan`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

// What a place must provide to be shown on the fan. Implemented by
// PlaceTokenBase subclasses; PlaceFan never learns about place types, which is
// what lets a new place type land without touching this file.
public interface IPlaceFanHost
{
    List<PlaceAction> BuildActions();
    void Dispatch(PlaceActionId id);
}

// The arc of action icons over the player's head (spec 2026-07-28). Replaces
// the full-screen town/dungeon canvases as the default way to interact with a
// place; the full menus live behind the ledger slot.
//
// The container is parked at an authored offset above screen centre with no
// per-hex projection: a place is entered by STANDING on it and the camera rides
// PlayerPosition, so the place is always screen centre (same reasoning as
// ShrinePanel's fan).
public class PlaceFan : MonoBehaviour
{
    [SerializeField] GameObject root;              // fan + click-off catcher
    [SerializeField] RectTransform fanContainer;   // parked above screen centre
    [SerializeField] PlaceFanSlot slotPrefab;      // ONE prefab; glyph swapped per action
    [SerializeField] ClickOffCatcher catcher;
    [SerializeField] FanSettings fan = new FanSettings
    {
        SpreadDegrees = 0f,   // 0 keeps the action icons upright
        CardSpacing = 70f,    // buttons are smaller than cards
        ArcDrop = 22f,        // edges sit this far below the centre slots
    };

    readonly List<PlaceFanSlot> slots = new();
    readonly List<RectTransform> seats = new();
    IPlaceFanHost current;
    List<PlaceAction> shown = new();

    static PlaceFan instance;
    public static PlaceFan Instance
        => instance != null
            ? instance
            : (instance = FindAnyObjectByType<PlaceFan>(FindObjectsInactive.Include));

    public bool IsOpen => current != null;

    public void Open(IPlaceFanHost host)
    {
        current = host;
        if (root != null) root.SetActive(true);
        if (catcher != null) catcher.SetArmed(true);
        shown = null;      // force the first Render
        Refresh();
    }

    // Wired to the click-off catcher. Nothing was spent, so this is a plain
    // close — opening a place is a free peek.
    public void Dismiss()
    {
        current = null;
        shown = null;
        if (catcher != null) catcher.SetArmed(false);
        if (root != null) root.SetActive(false);
    }

    void Update()
    {
        if (current != null) Refresh();
    }

    // Rebuild the action list every frame but re-render ONLY when it changed, so
    // Delve unlocks the instant an Explore card is played and Recruit locks when
    // influence drops — with no per-frame layout cost and no event wiring. This
    // replaces the five per-button Update() loops the TownButtons used.
    void Refresh()
    {
        var next = current.BuildActions();
        if (Same(shown, next)) return;
        shown = next;
        Render(next);
    }

    static bool Same(List<PlaceAction> a, List<PlaceAction> b)
    {
        if (a == null || b == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Id != b[i].Id) return false;
            if (a[i].Enabled != b[i].Enabled) return false;
            if (a[i].CostAmount != b[i].CostAmount) return false;
        }
        return true;
    }

    void Render(List<PlaceAction> actions)
    {
        EnsureSlots(actions.Count);
        seats.Clear();
        for (int i = 0; i < actions.Count; i++)
        {
            slots[i].Bind(actions[i], OnSlotClicked);
            seats.Add((RectTransform)slots[i].transform);
        }
        FanLayout.Place(seats, fan);
    }

    // Grow the pool to `count` and hide the rest. Slots are reused across visits.
    void EnsureSlots(int count)
    {
        if (slotPrefab == null || fanContainer == null) return;
        while (slots.Count < count)
            slots.Add(Instantiate(slotPrefab, fanContainer));
        for (int i = 0; i < slots.Count; i++)
            slots[i].gameObject.SetActive(i < count);
    }

    void OnSlotClicked(PlaceActionId id)
    {
        if (current == null) return;
        var host = current;
        // Close first: a dispatch can open a panel or a modal, and the fan must
        // not sit behind it. The ledger slot re-opens its own menu.
        Dismiss();
        host.Dispatch(id);
    }
}
```

- [ ] **Step 3: Verify it compiles**

Ask the user to let Unity recompile and confirm the Console is clean. Nothing is wired yet, so there is nothing to play-test.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlaceUI/PlaceFanSlot.cs Assets/Scripts/GameObjectScripts/PlaceUI/PlaceFan.cs
git commit -m "feat: PlaceFan + PlaceFanSlot renderer"
```

---

### Task 7: `PlaceTokenBase` + convert `DungeonToken`

Convert the simplest token first and verify it in the editor before touching the other two.

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/GameBoardObjects/PlaceTokenBase.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonToken.cs`
- Modify: `Assets/Scripts/GameObjectScripts/DungeonMenuScripts/DungeonPanel.cs:60-85`

**Interfaces:**
- Consumes: `IPlaceFanHost`, `PlaceFan.Instance` (Task 6), `PlaceActionRules.ForDungeon` (Task 3).
- Produces:
  - `abstract class PlaceTokenBase : MonoBehaviour, IPointerClickHandler, IHexOccupant, IPlaceFanHost` with abstract `PlaceName`, `Describe()`, `BuildActions()`, `Dispatch(PlaceActionId)`.
  - `DungeonPanel.PerformDelve(DungeonToken token)` — the delve body, callable from both the panel and the fan.

- [ ] **Step 1: Write `PlaceTokenBase`**

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using ArchonsRise.HexTooltipInfo;

// The one place-entry path (spec 2026-07-28). Towns, dungeons and shrines each
// carried a near-identical OnPointerClick; this is that sequence, once.
//
// A new place type implements PlaceName / Describe / BuildActions / Dispatch and
// nothing else. Dispatch lives on the TOKEN rather than in PlaceFan so the fan
// never learns about place types — that is what keeps this extensible.
public abstract class PlaceTokenBase : MonoBehaviour, IPointerClickHandler, IHexOccupant, IPlaceFanHost
{
    // Stable identity over the seeded map; assigned by GridGeneration at spawn.
    public Vector3Int gridPos;

    protected PlayerPosition player;
    protected Grid gameboard;

    // IHexOccupant: places are entered by standing on the cell, so an adjacent
    // click dispatches a move rather than walking through them.
    public Vector3Int Cell => gridPos;
    public virtual bool BlocksMove => true;

    protected abstract string PlaceName { get; }
    public abstract HexDescriptor Describe();

    // IPlaceFanHost
    public abstract System.Collections.Generic.List<PlaceAction> BuildActions();
    public abstract void Dispatch(PlaceActionId id);

    // Subclasses that need their own Start work override this; the base still
    // runs first so player/gameboard/registry are always set up.
    protected virtual void OnStart() { }

    // Hook for one-shots that used to key off a PANEL opening. The fan is the
    // normal path now, so anything that fired on panel-open must fire here or it
    // never fires again once players stop opening panels.
    protected virtual void OnFanOpening() { }

    void Start()
    {
        player = FindAnyObjectByType<PlayerPosition>();
        gameboard = FindAnyObjectByType<Grid>();
        HexOccupantRegistry.Instance.Register(this);
        OnStart();
    }

    void OnDestroy()
    {
        if (HexOccupantRegistry.Existing != null) HexOccupantRegistry.Existing.Unregister(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (MapFog.IsHidden(gridPos)) return; // hidden by fog → not interactable

        // During teleport targeting the interactor owns all clicks (you can
        // teleport onto a place cell); let it handle this one.
        if (HexInteractor.Instance != null && HexInteractor.Instance.IsTeleporting) return;

        // Places are entered by standing on the cell. If the player is adjacent
        // instead, treat the click as a move request onto this cell.
        if (gameboard.LocalToCell(player.transform.position) != gridPos)
        {
            if (ExplorationController.Instance != null && ExplorationController.Instance.IsAdjacent(gridPos))
                ExplorationController.Instance.Move(gridPos);
            else
                GameManager.Instance.ValidationMessage(
                    $"You must be standing at {PlaceName} to enter it.");
            return;
        }

        // Opening a place is a free peek (spec 2026-07-22): the turn's one action
        // is spent by the service committed inside, not by opening the fan.
        if (TurnPhaseController.Instance != null)
            TurnPhaseController.Instance.BeginVisit();

        OnFanOpening();
        if (PlaceFan.Instance != null) PlaceFan.Instance.Open(this);
    }
}
```

- [ ] **Step 2: Lift the delve body out of `DungeonPanel`**

In `Assets/Scripts/GameObjectScripts/DungeonMenuScripts/DungeonPanel.cs`, replace the `Delve()` method with these two, so the fan and the panel share one path:

```csharp
    // Wired to the panel's Delve button's OnClick.
    public void Delve()
    {
        if (current == null) return;
        var token = current;
        Close();
        PerformDelve(token);
    }

    // The delve itself, shared by the panel button and the place fan's Delve slot.
    public static void PerformDelve(DungeonToken token)
    {
        if (token == null) return;
        var player = FindAnyObjectByType<Player>();
        int cost = token.dungeonSO.exploreCost;
        if (player.PlayerExplore < cost)
        {
            GameManager.Instance.ValidationMessage(
                $"You need {cost} Explore to delve into {token.dungeonSO.cardName}.");
            return;
        }
        player.PlayerExplore -= cost;
        player.GetCurrentExplore();
        // Delving is the visit's committed action (spec 2026-07-22): spend the
        // turn's action now. This also commits the movement stack.
        if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
        // Delving is a firm decision: commit all pending plays so the explore
        // that paid for it can't be undone into a negative total.
        GameManager.Instance.commands.ClearStack();

        DungeonDelve.Instance.Begin(token);
    }
```

- [ ] **Step 3: Convert `DungeonToken`**

Replace the whole of `Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonToken.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using ArchonsRise.HexTooltipInfo;

// Map-side dungeon identity (M2.9). Entry, registry and fan handling all live in
// PlaceTokenBase (spec 2026-07-28); this class carries only what is dungeon-specific:
// its SO, its visual state markers, and what its fan offers.
public class DungeonToken : PlaceTokenBase
{
    public DungeonsSO dungeonSO;
    [SerializeField] GameObject flagMarker;    // active while flagged, until cleared
    [SerializeField] GameObject clearedMarker; // active once complete
    [SerializeField] VoidEvent onDungeonOpenTutorial; // M2.12 one-shot, raised on fan open

    protected override string PlaceName => dungeonSO.cardName;

    protected override void OnStart()
    {
        DungeonTracker.Instance.Register(gridPos, dungeonSO.id);
        RefreshVisual();
    }

    public override HexDescriptor Describe()
        => new HexDescriptor(
            TileDescriptor.Dungeon(dungeonSO.cardName,
                DungeonTracker.Instance.DefeatedCount(gridPos), DungeonRules.DelveCount),
            TileDescriptor.PlacePriority);

    public override List<PlaceAction> BuildActions()
    {
        var player = FindAnyObjectByType<Player>();
        bool canAct = TurnPhaseController.Instance == null || TurnPhaseController.Instance.VisitCanAct;
        return PlaceActionRules.ForDungeon(new DungeonActionSnapshot(
            complete: DungeonTracker.Instance.IsComplete(gridPos),
            explore: player != null ? player.PlayerExplore : 0,
            delveCost: dungeonSO.exploreCost,
            visitCanAct: canAct,
            hasMenu: true));
    }

    public override void Dispatch(PlaceActionId id)
    {
        switch (id)
        {
            case PlaceActionId.Delve:
                DungeonPanel.PerformDelve(this);
                break;
            case PlaceActionId.OpenMenu:
                FindAnyObjectByType<DungeonPanel>(FindObjectsInactive.Include).Open(this);
                break;
        }
    }

    // The M2.12 one-shot used to key off DungeonPanel.Open. The fan is the normal
    // path now, so it fires here instead — otherwise it would never fire again
    // once players stop opening the panel.
    protected override void OnFanOpening()
    {
        if (onDungeonOpenTutorial != null) onDungeonOpenTutorial.Raise();
    }

    public void RefreshVisual()
    {
        bool complete = DungeonTracker.Instance.IsComplete(gridPos);
        if (clearedMarker != null) clearedMarker.SetActive(complete);
        if (flagMarker != null) flagMarker.SetActive(!complete && DungeonTracker.Instance.IsFlagged(gridPos));
    }
}
```

- [ ] **Step 4: Stop `DungeonPanel` raising the tutorial one-shot**

`DungeonToken.OnFanOpening` now owns it, so the panel must not double-raise. In `DungeonPanel.cs` delete this line from `Open`:

```csharp
        if (onDungeonOpenTutorial != null) onDungeonOpenTutorial.Raise();
```

and its now-unused field:

```csharp
    [SerializeField] VoidEvent onDungeonOpenTutorial; // M2.12 one-shot trigger
```

- [ ] **Step 5: Hand the user the editor authoring steps**

Give the user exactly this and stop:

> 1. Create a `PlaceFanSlot` prefab: a Button with a child `Image` (the glyph), a child TMP text (the cost badge), and a `CanvasGroup`. Add the `PlaceFanSlot` component and wire `iconImage`, `costLabel`, `button`, `lockGroup`.
> 2. On the gameplay canvas create an empty GameObject `PlaceFan`. Under it: a full-screen `Image` with the `ClickOffCatcher` component at **sibling index 0**, and a `RectTransform` named `FanContainer` positioned above screen centre (copy the offset from the shrine's `fanContainer`).
> 3. Add the `PlaceFan` component to `PlaceFan`. Wire `root` = the PlaceFan GameObject, `fanContainer`, `slotPrefab` = the prefab from step 1, `catcher` = the ClickOffCatcher.
> 4. On the ClickOffCatcher's `onClickOff` event, add `PlaceFan.Dismiss`.
> 5. On the `DungeonToken` prefab, wire the new `onDungeonOpenTutorial` field to the same VoidEvent asset `DungeonPanel` used to reference, then clear that field on `DungeonPanel`.
> 6. Play: stand on a dungeon. A Delve icon with its Explore cost plus a ledger icon should fan over your head. Delve should grey out below the cost and ungrey the moment you play an Explore card, **without the fan closing**. The ledger icon should open the old dungeon panel unchanged. Clicking off should close the fan with nothing spent.

- [ ] **Step 6: Commit after the user confirms**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/PlaceTokenBase.cs Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonToken.cs Assets/Scripts/GameObjectScripts/DungeonMenuScripts/DungeonPanel.cs
git commit -m "feat: PlaceTokenBase + dungeon fan entry"
```

---

### Task 8: Convert `TownToken`

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs`

**Interfaces:**
- Consumes: `PlaceTokenBase` (Task 7), `PlaceActionRules.ForTown` (Task 2).
- Produces: `TownToken : PlaceTokenBase` with the town dispatch table.

- [ ] **Step 1: Replace `TownToken`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using ArchonsRise.HexTooltipInfo;

// Map-side town/keep/castle identity. Entry, registry and fan handling live in
// PlaceTokenBase (spec 2026-07-28); this class carries only what is town-specific.
public class TownToken : PlaceTokenBase
{
    public TownsSO townSO;
    [SerializeField] TownDeck deck;
    [SerializeField] TownEvent onClick_OpenTownMenu;
    [SerializeField] TownEvent onClick_GetTownData;
    [SerializeField] RecruitPanel recruitPanel;
    [SerializeField] IntEvent healInfluenceCostEvent; // the SPEND event, not GetCurrentInfluence
    [SerializeField] TownEvent healTownEvent;
    [SerializeField] VoidEvent onCrystalButtonClick;  // reveals the crystal pop-out

    protected override string PlaceName => townSO.cardName;

    protected override void OnStart()
    {
        ConquestTracker.Instance.Register(gridPos, townSO.placeType, townSO.guardians.Count);
    }

    public override HexDescriptor Describe()
        => new HexDescriptor(
            TileDescriptor.Town(townSO.cardName, PlaceTypeIcon(townSO.placeType),
                ConquestTracker.Instance.IsConquered(gridPos)),
            TileDescriptor.PlacePriority);

    private static IconConcept PlaceTypeIcon(PlaceType type)
    {
        switch (type)
        {
            case PlaceType.Keep:   return IconConcept.Keep;
            case PlaceType.Castle: return IconConcept.Castle;
            default:               return IconConcept.Town;
        }
    }

    public override List<PlaceAction> BuildActions()
    {
        var player = FindAnyObjectByType<Player>();
        int influence = player != null ? player.playerInfluence : 0;
        bool canAct = TurnPhaseController.Instance == null || TurnPhaseController.Instance.VisitCanAct;

        bool anyUnitAffordable = townSO.recruitableUnits.Exists(
            u => u != null && u.influenceCost <= influence);

        return PlaceActionRules.ForTown(new TownActionSnapshot(
            placeType: townSO.placeType,
            conquered: ConquestTracker.Instance.IsConquered(gridPos),
            guardiansRemaining: townSO.guardians.Count
                                - ConquestTracker.Instance.DefeatedCount(gridPos),
            influence: influence,
            healCost: townSO.healLevel,
            anyUnitAffordable: anyUnitAffordable,
            visitCanAct: canAct,
            hasMenu: true));
    }

    public override void Dispatch(PlaceActionId id)
    {
        switch (id)
        {
            case PlaceActionId.Assault:
                GuardianAssault.Instance.Begin(this);
                break;

            case PlaceActionId.Heal:
                // Same three effects the old HealButton wired, in the same order.
                if (healTownEvent != null) healTownEvent.Raise(this);
                if (healInfluenceCostEvent != null) healInfluenceCostEvent.Raise(townSO.healLevel);
                if (TurnPhaseController.Instance != null)
                    TurnPhaseController.Instance.CommitVisitAction();
                break;

            case PlaceActionId.Recruit:
                if (recruitPanel != null) recruitPanel.Open(this);
                break;

            case PlaceActionId.Crystal:
                if (onCrystalButtonClick != null) onCrystalButtonClick.Raise();
                break;

            case PlaceActionId.Cards:
                break; // M2 stub: the slot renders locked and does nothing

            case PlaceActionId.OpenMenu:
                GameManager.Instance.townCanvas.enabled = true;
                deck.CreateTown(this);
                // Revive any button that hid itself on a previous open so its
                // listener re-registers before the events below drive
                // UpdateButtonText.
                TownMenu.Instance.PrepareButtons();
                onClick_GetTownData.Raise(this);
                onClick_OpenTownMenu.Raise(this);
                break;
        }
    }
}
```

- [ ] **Step 2: Hand the user the editor authoring steps**

Give the user exactly this and stop:

> On the `TownToken` prefab, four new fields need wiring. Copy each value from the town-menu button that used to own it:
> 1. `recruitPanel` — the same `RecruitPanel` object the old Recruit button referenced.
> 2. `healTownEvent` — the `townEvent` from the old Heal button.
> 3. `healInfluenceCostEvent` — the `influenceCostEvent` from the old Heal button. **This must be `AdjustPlayerInfluence` (the event that spends), not `GetCurrentInfluence` (which only rebroadcasts).** Wiring the wrong one silently skips the deduction.
> 4. `onCrystalButtonClick` — the VoidEvent the old Crystal button raised to reveal the crystal pop-out.
>
> Then play and check: an unconquered Keep fans a single Assault icon with the guardian count, plus the ledger. Conquer it and reopen — Recruit and Crystal appear. Recruit greys when influence is short and ungreys when it rises, without the fan closing. Heal shows its influence cost and actually deducts it. The ledger opens the old town menu unchanged.

- [ ] **Step 3: Commit after the user confirms**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs
git commit -m "feat: town fan entry"
```

---

### Task 9: Convert `ShrineToken` + Engage dispatch

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/ShrineToken.cs`

**Interfaces:**
- Consumes: `PlaceTokenBase` (Task 7), `PlaceActionRules.ForShrine` (Task 3), `ShrinePanel.Open(ShrineToken)`.
- Produces: `ShrineToken : PlaceTokenBase`.

- [ ] **Step 1: Replace `ShrineToken`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using ArchonsRise.HexTooltipInfo;
using ArchonsRise.Shrines;

// Map-side shrine identity (spec 2026-07-24). Entry lives in PlaceTokenBase
// (spec 2026-07-28). A spent or guarded shrine now shows a LOCKED Engage slot
// rather than firing a message — the same way towns and dungeons show state.
public class ShrineToken : PlaceTokenBase
{
    public ShrineSO shrineSO;
    [SerializeField] GameObject liveMarker;
    [SerializeField] GameObject dormantMarker;
    [SerializeField] GameObject guardingMarker;

    protected override string PlaceName => shrineSO.cardName;

    protected override void OnStart()
    {
        ShrineTracker.Instance.Register(gridPos, shrineSO.id);
        RefreshVisual();
    }

    public override HexDescriptor Describe()
        => new HexDescriptor(
            TileDescriptor.Shrine(ShrineTracker.Instance.State(gridPos), shrineSO.crystalCost),
            TileDescriptor.PlacePriority);

    public override List<PlaceAction> BuildActions()
    {
        bool canAct = TurnPhaseController.Instance == null || TurnPhaseController.Instance.VisitCanAct;
        return PlaceActionRules.ForShrine(new ShrineActionSnapshot(
            isLive: ShrineTracker.Instance.State(gridPos) == ShrineVisualState.Live,
            crystalCost: shrineSO.crystalCost,
            visitCanAct: canAct));
    }

    public override void Dispatch(PlaceActionId id)
    {
        if (id != PlaceActionId.Engage) return;
        FindAnyObjectByType<ShrinePanel>(FindObjectsInactive.Include).Open(this);
    }

    public void RefreshVisual()
    {
        var s = ShrineTracker.Instance.State(gridPos);
        if (liveMarker != null) liveMarker.SetActive(s == ShrineVisualState.Live);
        if (dormantMarker != null) dormantMarker.SetActive(s == ShrineVisualState.ConsumedDormant);
        if (guardingMarker != null) guardingMarker.SetActive(s == ShrineVisualState.Guarding);
    }
}
```

- [ ] **Step 2: Ask the user to play-verify**

> Stand on a live shrine: an Engage icon showing the crystal cost fans over your head. Clicking it opens the payment widget exactly as before. Stand on a spent or guarded shrine: the Engage icon appears **dimmed and unclickable** instead of a popup message. Clicking off closes the fan with nothing spent.

- [ ] **Step 3: Commit after the user confirms**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/ShrineToken.cs
git commit -m "feat: shrine fan entry"
```

---

### Task 10: Hover previews on the Assault and Delve slots

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/PlaceUI/FanPreviewTrigger.cs`

**Interfaces:**
- Consumes: `PreviewTrigger` (abstract base), `EnemyPreviewData`, `PreviewRules.RemainingGuardians`, `PreviewRules.CanPreview()`, `PlaceFanSlot.Action` (Task 6).
- Produces: `FanPreviewTrigger : PreviewTrigger` on the slot prefab.

- [ ] **Step 1: Expose the current host on `PlaceFan`**

The trigger needs to know which place is open. Add this property to `PlaceFan`, next to `IsOpen`:

```csharp
    // Read-only access for hover triggers that need to know which place is open.
    public IPlaceFanHost CurrentHost => current;
```

- [ ] **Step 2: Write the trigger**

```csharp
using System.Collections.Generic;
using UnityEngine;

// Hover preview for fan slots (spec 2026-07-28). Extends the shipped
// PreviewTrigger, so the gamepad focus path added at the controller milestone
// drives this with no change here.
//
// Only Assault and Delve preview anything; every other slot returns empty, and
// PreviewTrigger.Focus already no-ops on an empty list.
[RequireComponent(typeof(PlaceFanSlot))]
public class FanPreviewTrigger : PreviewTrigger
{
    PlaceFanSlot slot;
    Camera uiCam;   // the slot's canvas render camera (null under Overlay)

    void Awake()
    {
        slot = GetComponent<PlaceFanSlot>();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) uiCam = canvas.rootCanvas.worldCamera;
    }

    protected override IReadOnlyList<EnemyPreviewData> ResolveEntries()
    {
        var empty = new List<EnemyPreviewData>();
        var host = PlaceFan.Instance != null ? PlaceFan.Instance.CurrentHost : null;
        if (host == null) return empty;

        if (slot.Action == PlaceActionId.Assault && host is TownToken town)
        {
            if (town.townSO == null) return empty;
            int defeated = ConquestTracker.Instance.DefeatedCount(town.gridPos);
            var remaining = PreviewRules.RemainingGuardians(town.townSO.guardians, defeated);
            var entries = new List<EnemyPreviewData>(remaining.Count);
            foreach (var g in remaining)
                entries.Add(new EnemyPreviewData(g, 0, 0)); // guardians never doom-scale
            return entries;
        }

        if (slot.Action == PlaceActionId.Delve && host is DungeonToken dungeon)
        {
            if (dungeon.dungeonSO == null || !PreviewRules.CanPreview()) return empty;
            int cleared = DungeonTracker.Instance.DefeatedCount(dungeon.gridPos);
            if (cleared >= dungeon.dungeonSO.enemies.Count) return empty;
            return new List<EnemyPreviewData>
            {
                new EnemyPreviewData(dungeon.dungeonSO.enemies[cleared], 0, 0),
            };
        }

        return empty;
    }

    // The slot is UI on a Screen Space - Camera canvas, so its transform.position
    // is world space; convert it to the screen pixels the panel expects.
    protected override Vector3 ScreenPosition()
        => RectTransformUtility.WorldToScreenPoint(uiCam, transform.position);
}
```

- [ ] **Step 3: Hand the user the editor authoring step**

> Add the `FanPreviewTrigger` component to the `PlaceFanSlot` prefab. Then play and hover the Assault icon on an unconquered Keep — the remaining guardians should appear in the usual preview panel, anchored to the icon. Hover Delve on a dungeon — the next enemy should appear. Hovering Recruit, Heal, Crystal or the ledger should show nothing.

- [ ] **Step 4: Commit after the user confirms**

```bash
git add Assets/Scripts/GameObjectScripts/PlaceUI/FanPreviewTrigger.cs Assets/Scripts/GameObjectScripts/PlaceUI/PlaceFan.cs
git commit -m "feat: hover enemy previews on Assault and Delve fan slots"
```

---

### Task 11: Fix the three gates the fan invalidates

Three existing checks assume the town/dungeon canvas is the only way in. Each fails silently once the fan is the normal path.

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/CreateCrystalButtons.cs:35-40`
- Modify: `Assets/Scripts/Managers/DataManager.cs:455-459`

**Interfaces:**
- Consumes: `PlaceFan.Instance.IsOpen` (Task 6).

- [ ] **Step 1: Fix the crystal pop-out gate**

`CreateCrystalButtons.Update` force-disables every crystal button whenever `townCanvas.enabled` is false. Opening the picker from the fan would leave the pop-out permanently dead. Replace the `Update` method:

```csharp
    // The pop-out used to be reachable only from the town canvas, so this gate
    // keyed off that canvas. The place fan can now open it with the canvas shut
    // (spec 2026-07-28), so the buttons stay live while the pop-out is armed and
    // are disarmed by HideAll on purchase or click-off.
    private void Update()
    {
        if (GameManager.Instance.townCanvas.enabled) return;
        if (PlaceFan.Instance != null && PlaceFan.Instance.IsOpen) return;
        // Neither route is open: make sure a stale pop-out cannot be clicked.
        if (!thisButton.interactable) return;
        var anyCatcherArmed = FindAnyObjectByType<CrystalDismissCatcher>(FindObjectsInactive.Include);
        if (anyCatcherArmed == null) thisButton.interactable = false;
    }
```

- [ ] **Step 2: Fix the save gate**

`DataManager.CanSave` refuses to save while a place menu is open. The fan replaces those menus as the normal path, so it must join the guard — otherwise a mid-visit save becomes possible. Add this line alongside the existing canvas checks around `DataManager.cs:455`:

```csharp
        if (PlaceFan.Instance != null && PlaceFan.Instance.IsOpen) return false;
```

- [ ] **Step 3: Ask the user to play-verify**

> 1. Open a town's fan, click Crystal — the colour pop-out appears and buying works, with the town menu never opened. Buy one, then reopen and buy another.
> 2. With a fan open, try to save. It should be refused exactly as it is with the old town menu open.

- [ ] **Step 4: Commit after the user confirms**

```bash
git add Assets/Scripts/GameObjectScripts/TownMenuScripts/CreateCrystalButtons.cs Assets/Scripts/Managers/DataManager.cs
git commit -m "fix: crystal pop-out and save gates account for the place fan"
```

---

## Plan 1 Acceptance

Run through these in one session before moving to Plan 2:

1. Unconquered Keep → Assault (with guardian count) + ledger; hovering Assault previews the guardians.
2. Conquered Keep → Recruit + Crystal + ledger; Recruit re-gates live as influence changes, fan stays open.
3. Castle → Cards slot present but dimmed.
4. Heal deducts its influence cost and spends the turn's action.
5. Dungeon → Delve with Explore cost, re-gates live, hover previews the next enemy.
6. Live shrine → Engage opens the unchanged payment widget; spent/guarded shrine → dimmed Engage, no popup.
7. Ledger opens the old town and dungeon menus unchanged, `?` icons included.
8. Clicking off any fan closes it with nothing spent.
9. Adjacency click-to-move and teleport targeting onto all three place types behave exactly as before.
10. Save is refused while a fan is open.
