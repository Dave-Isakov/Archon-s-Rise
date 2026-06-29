# Card-Play Rework — Phase 1: Selection Model + Inspector (Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the toggle-driven card menu with a single-state `CardPlaySelection` model owned by a `CardInspector`, fixing the choice/improvise selection-wipe bug by construction, while reusing the existing `PlayCommand`/undo and `Player` stat math unchanged.

**Architecture:** Pure-logic `CardPlaySelection` (in its own asmdef, unit-tested in EditMode) computes the play preview and enforces the play-mode rules. A `CardInspector` MonoBehaviour (in `Assembly-CSharp`) owns the selection, builds a `CardSnapshot` from `card.cardSO`, drives the pop-out, and on Play routes to one of the nine existing `CardEvent` assets via the existing `PlayCommand`. Four small section-view scripts render from the selection and call back into the inspector — they never talk to each other, which is what eliminates the bug.

**Tech Stack:** Unity 6 (URP), C#, Unity Test Framework (EditMode), existing ScriptableObject event system, existing `PlayManager` command stack. DOTween is available but **not used in Phase 1** (visuals/juice are Phases 2–3).

## Global Constraints

- **Do not modify** `PlayCommand`, `ICommands`, `PlayManager` (`commands`), or any `Player` stat method (`PlayCard`, `AttackChoice`/`DefendChoice`/`InfluenceChoice`/`ExploreChoice`, `ImprovAttack`/`ImprovDefend`/`ImprovInfluence`/`ImprovExplore`, `EmpowerCrystalCheck`, `UndoEmpower`). Phase 1 only *routes to* them.
- **Do not modify** the crystal consume/restore path (`CrystalInventory.EmpowerCrystal`/`RegenCrystal`). Phase 1 only adds a display-only `Crystal.SetReserved`.
- **Stat array order is `[attack, defend, influence, explore]`** everywhere (matches `Player.AssignPlayerStats`).
- **Improvise flat value is `1`** per stat (matches `Player.improvAttackValue` etc.).
- **Empower rule (Phase 1):** `CanEmpower = empowerType != EmpowerType.None && mode != PlayMode.Improvise`. Keep it a single method so the future "empowered improvise" mechanic is a one-line change.
- **Play-mode exclusivity preserves state:** switching modes must NOT clear the other mode's stored stat or the empower flag. The regression test in Task 3 guards this.
- New runtime logic lives under `Assets/Scripts/CardPlay/`; tests under `Assets/Tests/EditMode/`.

---

## File Structure

- `Assets/Scripts/Enums/ArchonsRise.Enums.asmdef` — **new** asmdef wrapping the existing enums folder so the pure-logic assembly can reference `StatType`/`EmpowerType`. `Assembly-CSharp` auto-references it, so existing code keeps compiling.
- `Assets/Scripts/CardPlay/ArchonsRise.CardPlay.asmdef` — **new** asmdef for the pure-logic types. References `ArchonsRise.Enums` only (no Unity engine types).
- `Assets/Scripts/CardPlay/PlayMode.cs` — **new** enum `{ Normal, Choice, Improvise }`.
- `Assets/Scripts/CardPlay/CardSnapshot.cs` — **new** immutable struct: the card data the logic needs, decoupled from `CardsSO`.
- `Assets/Scripts/CardPlay/CardPlaySelection.cs` — **new** the single-state model + rules + preview.
- `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` — **new** test assembly.
- `Assets/Tests/EditMode/CardPlaySelectionTests.cs` — **new** unit tests incl. the bug regression.
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs` — **new** controller MonoBehaviour (Assembly-CSharp).
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ChoiceBanner.cs` — **new** view.
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ImprovisePanel.cs` — **new** view.
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/EmpowerPanel.cs` — **new** view.
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/PlayBar.cs` — **new** view.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/Crystal.cs` — **modify**: add `SetReserved(bool)`.
- **Delete** (Task 7): `Empower.cs`, `ImprovToggle.cs`, `ImprovButtons.cs`, `ImprovAttack/Defend/Explore/Influence.cs`, `Improv.cs`, `ChoiceToggles.cs`, `AttackToggle/DefendToggle/InfluenceToggle/ExploreToggle.cs`, `PlayButton.cs`, `CardMenuInterface.cs` (if unreferenced).

---

### Task 1: Assembly definitions + EditMode test harness

**Files:**
- Create: `Assets/Scripts/Enums/ArchonsRise.Enums.asmdef`
- Create: `Assets/Scripts/CardPlay/ArchonsRise.CardPlay.asmdef`
- Create: `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`
- Create: `Assets/Tests/EditMode/HarnessSmokeTest.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: assemblies `ArchonsRise.Enums`, `ArchonsRise.CardPlay`, `ArchonsRise.Tests.EditMode`. Later tasks add types into `ArchonsRise.CardPlay` and tests into `ArchonsRise.Tests.EditMode`.

- [ ] **Step 1: Create the enums asmdef**

Create `Assets/Scripts/Enums/ArchonsRise.Enums.asmdef`:

```json
{
    "name": "ArchonsRise.Enums",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": true
}
```

- [ ] **Step 2: Create the CardPlay asmdef**

Create `Assets/Scripts/CardPlay/ArchonsRise.CardPlay.asmdef`:

```json
{
    "name": "ArchonsRise.CardPlay",
    "rootNamespace": "",
    "references": ["ArchonsRise.Enums"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": true
}
```

- [ ] **Step 3: Create the EditMode test asmdef**

Create `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`. The `precompiledReferences` + `defineConstraints` + test-framework reference make it an EditMode test assembly:

```json
{
    "name": "ArchonsRise.Tests.EditMode",
    "rootNamespace": "",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "ArchonsRise.CardPlay",
        "ArchonsRise.Enums"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "precompiledReferences": ["nunit.framework.dll"],
    "overrideReferences": true
}
```

> If Unity reports the `UnityEngine.TestRunner`/`UnityEditor.TestRunner` references as unresolved, ensure `com.unity.test-framework` is installed (it is, via the package cache) and that **Edit ▸ Project Settings ▸ Editor ▸ Enter Play Mode Options** is not blocking domain reload. The references above match what the Test Runner generates for a hand-authored EditMode assembly.

- [ ] **Step 4: Write a smoke test**

Create `Assets/Tests/EditMode/HarnessSmokeTest.cs`:

```csharp
using NUnit.Framework;

public class HarnessSmokeTest
{
    [Test]
    public void Harness_Compiles_And_Runs()
    {
        Assert.AreEqual(2, 1 + 1);
    }
}
```

- [ ] **Step 5: Run the EditMode tests**

In Unity: **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**.
Expected: `HarnessSmokeTest.Harness_Compiles_And_Runs` PASSES, and the project still compiles with no errors in the Console (confirms moving the enums into an asmdef did not break `Assembly-CSharp`).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Enums/ArchonsRise.Enums.asmdef* Assets/Scripts/CardPlay/ArchonsRise.CardPlay.asmdef* Assets/Tests/EditMode/
git commit -m "chore: add CardPlay + enums asmdefs and EditMode test harness"
```

---

### Task 2: PlayMode enum + CardSnapshot

**Files:**
- Create: `Assets/Scripts/CardPlay/PlayMode.cs`
- Create: `Assets/Scripts/CardPlay/CardSnapshot.cs`
- Test: `Assets/Tests/EditMode/CardSnapshotTests.cs`

**Interfaces:**
- Consumes: `StatType`, `EmpowerType` (from `ArchonsRise.Enums`).
- Produces:
  - `enum PlayMode { Normal, Choice, Improvise }`
  - `readonly struct CardSnapshot` with fields `StatType CardType`, `EmpowerType EmpowerType`, `bool IsChoice`, and base/empower stats `int Attack, Defend, Influence, Explore, EmpowerAttack, EmpowerDefend, EmpowerInfluence, EmpowerExplore`; constructor taking all of them in that order; method `int BaseOf(StatType single)` and `int EmpowerOf(StatType single)` returning the value for a single stat flag (0 if not that one of the four).

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/CardSnapshotTests.cs`:

```csharp
using NUnit.Framework;

public class CardSnapshotTests
{
    static CardSnapshot Rally() =>
        // Attack|Influence choice card, yellow-empowerable. base 2/2, empower 4/4.
        new CardSnapshot(StatType.Attack | StatType.Influence, EmpowerType.Yellow, true,
            attack: 2, defend: 0, influence: 2, explore: 0,
            empowerAttack: 4, empowerDefend: 0, empowerInfluence: 4, empowerExplore: 0);

    [Test]
    public void BaseOf_ReturnsSingleStatValue()
    {
        var c = Rally();
        Assert.AreEqual(2, c.BaseOf(StatType.Attack));
        Assert.AreEqual(2, c.BaseOf(StatType.Influence));
        Assert.AreEqual(0, c.BaseOf(StatType.Defend));
    }

    [Test]
    public void EmpowerOf_ReturnsSingleStatValue()
    {
        var c = Rally();
        Assert.AreEqual(4, c.EmpowerOf(StatType.Attack));
        Assert.AreEqual(0, c.EmpowerOf(StatType.Explore));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Test Runner ▸ EditMode ▸ Run All.
Expected: FAIL — `CardSnapshot` / `StatType` symbols not found / does not compile.

- [ ] **Step 3: Write PlayMode**

Create `Assets/Scripts/CardPlay/PlayMode.cs`:

```csharp
public enum PlayMode
{
    Normal,
    Choice,
    Improvise
}
```

- [ ] **Step 4: Write CardSnapshot**

Create `Assets/Scripts/CardPlay/CardSnapshot.cs`:

```csharp
public readonly struct CardSnapshot
{
    public readonly StatType CardType;
    public readonly EmpowerType EmpowerType;
    public readonly bool IsChoice;
    public readonly int Attack, Defend, Influence, Explore;
    public readonly int EmpowerAttack, EmpowerDefend, EmpowerInfluence, EmpowerExplore;

    public CardSnapshot(StatType cardType, EmpowerType empowerType, bool isChoice,
        int attack, int defend, int influence, int explore,
        int empowerAttack, int empowerDefend, int empowerInfluence, int empowerExplore)
    {
        CardType = cardType;
        EmpowerType = empowerType;
        IsChoice = isChoice;
        Attack = attack; Defend = defend; Influence = influence; Explore = explore;
        EmpowerAttack = empowerAttack; EmpowerDefend = empowerDefend;
        EmpowerInfluence = empowerInfluence; EmpowerExplore = empowerExplore;
    }

    public int BaseOf(StatType single)
    {
        if (single == StatType.Attack) return Attack;
        if (single == StatType.Defend) return Defend;
        if (single == StatType.Influence) return Influence;
        if (single == StatType.Explore) return Explore;
        return 0;
    }

    public int EmpowerOf(StatType single)
    {
        if (single == StatType.Attack) return EmpowerAttack;
        if (single == StatType.Defend) return EmpowerDefend;
        if (single == StatType.Influence) return EmpowerInfluence;
        if (single == StatType.Explore) return EmpowerExplore;
        return 0;
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Test Runner ▸ EditMode ▸ Run All. Expected: `CardSnapshotTests` PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/CardPlay/PlayMode.cs* Assets/Scripts/CardPlay/CardSnapshot.cs* Assets/Tests/EditMode/CardSnapshotTests.cs*
git commit -m "feat: add PlayMode enum and pure CardSnapshot data"
```

---

### Task 3: CardPlaySelection — rules, preview, and the bug regression

**Files:**
- Create: `Assets/Scripts/CardPlay/CardPlaySelection.cs`
- Test: `Assets/Tests/EditMode/CardPlaySelectionTests.cs`

**Interfaces:**
- Consumes: `CardSnapshot`, `PlayMode`, `StatType`, `EmpowerType`.
- Produces: `class CardPlaySelection` with:
  - ctor `CardPlaySelection(CardSnapshot card)` — defaults `Mode = Normal`, `ChoiceStat` = first set flag of `CardType`, `ImproviseStat = StatType.Attack`, `Empowered = false`.
  - props `PlayMode Mode { get; }`, `StatType ChoiceStat { get; }`, `StatType ImproviseStat { get; }`, `bool Empowered { get; }`
  - `void SetMode(PlayMode mode)`
  - `void SetChoiceStat(StatType stat)` (also sets `Mode = Choice`)
  - `void SetImproviseStat(StatType stat)` (also sets `Mode = Improvise`)
  - `void SetEmpowered(bool value)`
  - `bool CanEmpower()`
  - `bool EffectiveEmpowered()` → `Empowered && CanEmpower()`
  - `bool IsPlayable()` → card has at least one playable stat (CardType intersects the four action stats) — Wounds (`StatType.Wound`/`None`) are not playable.
  - `int[] ResolveStats()` → `[atk, def, inf, exp]` preview for the current mode/stat/empower.
  - `StatType ResolvedStat()` → the single stat a Choice/Improvise play targets (for routing in Task 4).
  - `string Describe()` → e.g. `"+2 Attack"`, `"+1 Defend (improvised)"`, or for multi-stat Normal `"+2 Attack, +1 Influence"`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/CardPlaySelectionTests.cs`:

```csharp
using NUnit.Framework;

public class CardPlaySelectionTests
{
    static CardSnapshot Rally() =>
        new CardSnapshot(StatType.Attack | StatType.Influence, EmpowerType.Yellow, true,
            2, 0, 2, 0, 4, 0, 4, 0);

    static CardSnapshot Strike() => // single-stat normal, red-empowerable
        new CardSnapshot(StatType.Attack, EmpowerType.Red, false,
            2, 0, 0, 0, 3, 0, 0, 0);

    static CardSnapshot Wound() =>
        new CardSnapshot(StatType.Wound, EmpowerType.None, false,
            0, 0, 0, 0, 0, 0, 0, 0);

    [Test]
    public void Normal_SingleStat_ResolvesPrintedValue()
    {
        var s = new CardPlaySelection(Strike());
        Assert.AreEqual(new[] { 2, 0, 0, 0 }, s.ResolveStats());
        Assert.AreEqual("+2 Attack", s.Describe());
    }

    [Test]
    public void Empowered_Normal_UsesEmpowerValue()
    {
        var s = new CardPlaySelection(Strike());
        s.SetEmpowered(true);
        Assert.IsTrue(s.EffectiveEmpowered());
        Assert.AreEqual(new[] { 3, 0, 0, 0 }, s.ResolveStats());
    }

    [Test]
    public void Choice_AppliesOnlyChosenStat()
    {
        var s = new CardPlaySelection(Rally());
        s.SetChoiceStat(StatType.Attack);
        Assert.AreEqual(new[] { 2, 0, 0, 0 }, s.ResolveStats());
        s.SetChoiceStat(StatType.Influence);
        Assert.AreEqual(new[] { 0, 0, 2, 0 }, s.ResolveStats());
    }

    [Test]
    public void Improvise_GivesFlatOneToChosenStat()
    {
        var s = new CardPlaySelection(Rally());
        s.SetImproviseStat(StatType.Defend);
        Assert.AreEqual(PlayMode.Improvise, s.Mode);
        Assert.AreEqual(new[] { 0, 1, 0, 0 }, s.ResolveStats());
        Assert.AreEqual("+1 Defend (improvised)", s.Describe());
    }

    [Test]
    public void CanEmpower_FalseForImproviseAndForWound()
    {
        var imp = new CardPlaySelection(Rally());
        imp.SetImproviseStat(StatType.Attack);
        Assert.IsFalse(imp.CanEmpower());

        var wound = new CardPlaySelection(Wound());
        Assert.IsFalse(wound.CanEmpower());
        Assert.IsFalse(wound.IsPlayable());
    }

    // The bug: choosing Improvise must not destroy the choice selection.
    [Test]
    public void SwitchingToImproviseAndBack_PreservesChoice()
    {
        var s = new CardPlaySelection(Rally());
        s.SetChoiceStat(StatType.Influence);          // choice = Influence
        s.SetImproviseStat(StatType.Attack);          // mode -> Improvise
        s.SetMode(PlayMode.Choice);                   // back to choice
        Assert.AreEqual(StatType.Influence, s.ChoiceStat);
        Assert.AreEqual(new[] { 0, 0, 2, 0 }, s.ResolveStats());
    }

    // Empower flag survives a trip through Improvise (future-proofing the rule).
    [Test]
    public void EmpowerFlag_SurvivesImproviseRoundTrip()
    {
        var s = new CardPlaySelection(Strike());
        s.SetEmpowered(true);
        s.SetImproviseStat(StatType.Attack);          // CanEmpower now false
        Assert.IsFalse(s.EffectiveEmpowered());
        s.SetMode(PlayMode.Normal);                   // empower applies again
        Assert.IsTrue(s.EffectiveEmpowered());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Test Runner ▸ EditMode ▸ Run All. Expected: FAIL — `CardPlaySelection` not found.

- [ ] **Step 3: Write CardPlaySelection**

Create `Assets/Scripts/CardPlay/CardPlaySelection.cs`:

```csharp
using System;
using System.Collections.Generic;

public class CardPlaySelection
{
    static readonly StatType[] ActionStats =
        { StatType.Attack, StatType.Defend, StatType.Influence, StatType.Explore };
    const int ImproviseValue = 1;

    readonly CardSnapshot _card;

    public PlayMode Mode { get; private set; }
    public StatType ChoiceStat { get; private set; }
    public StatType ImproviseStat { get; private set; }
    public bool Empowered { get; private set; }

    public CardPlaySelection(CardSnapshot card)
    {
        _card = card;
        Mode = PlayMode.Normal;
        ChoiceStat = FirstFlag(card.CardType);
        ImproviseStat = StatType.Attack;
        Empowered = false;
    }

    public void SetMode(PlayMode mode) => Mode = mode;

    public void SetChoiceStat(StatType stat)
    {
        ChoiceStat = stat;
        Mode = PlayMode.Choice;
    }

    public void SetImproviseStat(StatType stat)
    {
        ImproviseStat = stat;
        Mode = PlayMode.Improvise;
    }

    public void SetEmpowered(bool value) => Empowered = value;

    public bool CanEmpower() =>
        _card.EmpowerType != EmpowerType.None && Mode != PlayMode.Improvise;

    public bool EffectiveEmpowered() => Empowered && CanEmpower();

    public bool IsPlayable()
    {
        foreach (var s in ActionStats)
            if (_card.CardType.HasFlag(s)) return true;
        return false;
    }

    public StatType ResolvedStat() =>
        Mode == PlayMode.Improvise ? ImproviseStat : ChoiceStat;

    public int[] ResolveStats()
    {
        var result = new int[4]; // attack, defend, influence, explore
        bool emp = EffectiveEmpowered();

        switch (Mode)
        {
            case PlayMode.Improvise:
                AddStat(result, ImproviseStat, ImproviseValue);
                break;

            case PlayMode.Choice:
                AddStat(result, ChoiceStat, emp ? _card.EmpowerOf(ChoiceStat) : _card.BaseOf(ChoiceStat));
                break;

            default: // Normal — every set action flag contributes
                foreach (var s in ActionStats)
                    if (_card.CardType.HasFlag(s))
                        AddStat(result, s, emp ? _card.EmpowerOf(s) : _card.BaseOf(s));
                break;
        }
        return result;
    }

    public string Describe()
    {
        var parts = new List<string>();
        var stats = ResolveStats();
        string[] names = { "Attack", "Defend", "Influence", "Explore" };
        for (int i = 0; i < 4; i++)
            if (stats[i] != 0) parts.Add($"+{stats[i]} {names[i]}");

        if (parts.Count == 0) return "—";
        string body = string.Join(", ", parts);
        return Mode == PlayMode.Improvise ? body + " (improvised)" : body;
    }

    static void AddStat(int[] result, StatType single, int value)
    {
        if (single == StatType.Attack) result[0] += value;
        else if (single == StatType.Defend) result[1] += value;
        else if (single == StatType.Influence) result[2] += value;
        else if (single == StatType.Explore) result[3] += value;
    }

    static StatType FirstFlag(StatType type)
    {
        foreach (var s in ActionStats)
            if (type.HasFlag(s)) return s;
        return StatType.Attack;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Test Runner ▸ EditMode ▸ Run All. Expected: all `CardPlaySelectionTests` PASS — including `SwitchingToImproviseAndBack_PreservesChoice` (the bug regression).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CardPlay/CardPlaySelection.cs* Assets/Tests/EditMode/CardPlaySelectionTests.cs*
git commit -m "feat: CardPlaySelection model with play-mode rules and bug regression test"
```

---

### Task 4: CardInspector controller + commit routing

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs`
- Test: manual Play-mode verification (MonoBehaviour; no EditMode test).

**Interfaces:**
- Consumes: `CardPlaySelection`, `CardSnapshot`, `PlayMode`, `StatType` (logic); `Card`, `CardsSO`, `PlayCommand`, `CardEvent`, `GameManager`, `GameManager.Instance.commands` (Assembly-CSharp).
- Produces (the API the Task 5 views call):
  - `void Open(Card card)` / `void Close()`
  - `CardPlaySelection Selection { get; }`
  - `event Action Changed;` — raised after any mutation so views re-render
  - `void SetMode(PlayMode mode)`
  - `void ChooseStat(StatType stat)` (Choice)
  - `void ImproviseStat(StatType stat)`
  - `void SetEmpowered(bool value)` (reservation hookup added in Task 6)
  - `void Play()` — routes to the matching `CardEvent` and pushes a `PlayCommand`

- [ ] **Step 1: Write CardInspector**

Create `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs`:

```csharp
using System;
using UnityEngine;

// Owns the in-progress play for the focused card. Single source of truth that the
// section views render from; replaces the old toggle-event web. Routes Play to the
// existing CardEvent assets so PlayCommand/undo and Player stat math stay unchanged.
public class CardInspector : MonoBehaviour
{
    [Header("Normal / Choice routing (existing assets)")]
    [SerializeField] CardEvent onPlay_Normal;     // OnPlay_SetCardDataToPlayer
    [SerializeField] CardEvent onPlay_Attack;      // OnPlay_SetAttackDataToPlayer
    [SerializeField] CardEvent onPlay_Defend;      // OnPlay_SetDefendDataToPlayer
    [SerializeField] CardEvent onPlay_Influence;   // OnPlay_SetInfluenceDataToPlayer
    [SerializeField] CardEvent onPlay_Explore;     // OnPlay_SetExploreDataToPlayer

    [Header("Improvise routing (existing assets)")]
    [SerializeField] CardEvent onImprov_Attack;    // OnImprovAttack_SetPlayerStats
    [SerializeField] CardEvent onImprov_Defend;    // OnImprovDefend_SetPlayerStats
    [SerializeField] CardEvent onImprov_Influence; // OnImprovInfluence_SetPlayerStats
    [SerializeField] CardEvent onImprov_Explore;   // OnImprovExplore_SetPlayerStats

    public CardPlaySelection Selection { get; private set; }
    public Card Card { get; private set; }
    public event Action Changed;

    public void Open(Card card)
    {
        Card = card;
        Selection = new CardPlaySelection(Snapshot(card.cardSO));
        GameManager.Instance.cardCanvas.enabled = true;
        Raise();
    }

    public void Close()
    {
        GameManager.Instance.cardCanvas.enabled = false;
        Card = null;
        Selection = null;
    }

    public void SetMode(PlayMode mode)      { Selection?.SetMode(mode); Raise(); }
    public void ChooseStat(StatType stat)   { Selection?.SetChoiceStat(stat); Raise(); }
    public void ImproviseStat(StatType s)   { Selection?.SetImproviseStat(s); Raise(); }
    public void SetEmpowered(bool value)    { Selection?.SetEmpowered(value); Raise(); }

    public void Play()
    {
        if (Selection == null || !Selection.IsPlayable()) return;
        Card.IsEmpowered = Selection.EffectiveEmpowered();
        var evt = EventFor(Selection);
        if (evt == null) return;
        GameManager.Instance.commands.AddCommand(new PlayCommand(evt, Card));
        Raise();
    }

    CardEvent EventFor(CardPlaySelection s)
    {
        switch (s.Mode)
        {
            case PlayMode.Improvise: return ImprovEventFor(s.ImproviseStat);
            case PlayMode.Choice:    return ChoiceEventFor(s.ChoiceStat);
            default:                 return onPlay_Normal;
        }
    }

    CardEvent ChoiceEventFor(StatType stat)
    {
        if (stat == StatType.Attack)    return onPlay_Attack;
        if (stat == StatType.Defend)    return onPlay_Defend;
        if (stat == StatType.Influence) return onPlay_Influence;
        if (stat == StatType.Explore)   return onPlay_Explore;
        return onPlay_Normal;
    }

    CardEvent ImprovEventFor(StatType stat)
    {
        if (stat == StatType.Attack)    return onImprov_Attack;
        if (stat == StatType.Defend)    return onImprov_Defend;
        if (stat == StatType.Influence) return onImprov_Influence;
        if (stat == StatType.Explore)   return onImprov_Explore;
        return onImprov_Attack;
    }

    static CardSnapshot Snapshot(CardsSO so) =>
        new CardSnapshot(so.cardType, so.empowerType, so.isChoice,
            so.attack, so.defend, so.influence, so.explore,
            so.empowerAttack, so.empowerDefend, so.empowerInfluence, so.empowerExplore);

    void Raise() => Changed?.Invoke();
}
```

- [ ] **Step 2: Create the inspector GameObject and assign events**

In the GameBoard scene, on the card menu canvas (the object referenced by `GameManager.cardCanvas`), add an empty child `CardInspector` and attach the `CardInspector` component. Assign the nine `CardEvent` fields from `Assets/Scripts/GameEvents/Events/Card Events/`:
`onPlay_Normal` = `OnPlay_SetCardDataToPlayer`; `onPlay_Attack/Defend/Influence/Explore` = the matching `OnPlay_Set*DataToPlayer`; `onImprov_Attack/Defend/Influence/Explore` = the matching `OnImprov*_SetPlayerStats`.

- [ ] **Step 3: Temporary smoke wiring**

Temporarily, in `Card.OnPointerClick`, where it currently raises `onClick_OpenCardMenu`/`onOpenCardMenu_MaximizeCard`, also call `FindAnyObjectByType<CardInspector>().Open(this);` (a throwaway line — Task 7 replaces the open path properly). This lets you verify routing before the views exist.

- [ ] **Step 4: Play-mode verify routing**

Enter Play mode. Click a single-stat card (e.g. a Strike). In the Console, confirm no errors. Then temporarily call `Play()` from a debug key or button: confirm the matching `Player` stat increases by the printed value and `Undo` reverses it. This proves the inspector routes through the existing command/undo path.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs* Assets/Scenes/GameBoard.unity
git commit -m "feat: CardInspector owns play selection and routes to existing play events"
```

---

### Task 5: Section views (Choice / Improvise / Empower / Play)

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ChoiceBanner.cs`
- Create: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ImprovisePanel.cs`
- Create: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/EmpowerPanel.cs`
- Create: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/PlayBar.cs`
- Test: manual Play-mode verification.

**Interfaces:**
- Consumes: `CardInspector` (`Selection`, `Changed`, `SetMode`, `ChooseStat`, `ImproviseStat`, `SetEmpowered`, `Play`, `Close`), `CardPlaySelection`, `PlayMode`, `StatType`.
- Produces: four MonoBehaviours that subscribe to `inspector.Changed` and render. Each holds a `[SerializeField] CardInspector inspector`. Phase 1 keeps the visuals plain (existing buttons/labels); fan/animation styling is Phases 2–3.

For Phase 1, each view is wired to existing UI Buttons/labels in the card menu. The point of Task 5 is the **logic + state rendering**, not the final look. Each view's `Render()` reads `inspector.Selection` and updates interactability/highlight; click handlers call inspector mutators.

- [ ] **Step 1: Write ChoiceBanner**

Create `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ChoiceBanner.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

// Shows one button per set action-flag on a choice card. Selecting a segment sets
// the choice stat. Hidden for non-choice cards and when Improvise is active.
public class ChoiceBanner : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;          // the banner container to show/hide
    [SerializeField] Button attackButton;
    [SerializeField] Button defendButton;
    [SerializeField] Button influenceButton;
    [SerializeField] Button exploreButton;

    void OnEnable()  { inspector.Changed += Render; }
    void OnDisable() { inspector.Changed -= Render; }

    void Start()
    {
        attackButton.onClick.AddListener(() => inspector.ChooseStat(StatType.Attack));
        defendButton.onClick.AddListener(() => inspector.ChooseStat(StatType.Defend));
        influenceButton.onClick.AddListener(() => inspector.ChooseStat(StatType.Influence));
        exploreButton.onClick.AddListener(() => inspector.ChooseStat(StatType.Explore));
    }

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool show = sel != null && card != null && card.cardSO.isChoice
                    && sel.Mode != PlayMode.Improvise;
        root.SetActive(show);
        if (!show) return;

        Bind(attackButton, StatType.Attack, card.cardSO.cardType, sel);
        Bind(defendButton, StatType.Defend, card.cardSO.cardType, sel);
        Bind(influenceButton, StatType.Influence, card.cardSO.cardType, sel);
        Bind(exploreButton, StatType.Explore, card.cardSO.cardType, sel);
    }

    static void Bind(Button b, StatType stat, StatType cardType, CardPlaySelection sel)
    {
        bool available = cardType.HasFlag(stat);
        b.gameObject.SetActive(available);
        if (!available) return;
        // selected highlight: interactable=false marks the chosen one
        b.interactable = !(sel.Mode == PlayMode.Choice && sel.ChoiceStat == stat);
    }
}
```

- [ ] **Step 2: Write ImprovisePanel**

Create `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ImprovisePanel.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

// Four +1 stat options. Selecting one puts the selection into Improvise mode.
// Disabled for non-empowerable cards (Wounds / EmpowerType.None).
public class ImprovisePanel : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;
    [SerializeField] Button attackButton;
    [SerializeField] Button defendButton;
    [SerializeField] Button influenceButton;
    [SerializeField] Button exploreButton;

    void OnEnable()  { inspector.Changed += Render; }
    void OnDisable() { inspector.Changed -= Render; }

    void Start()
    {
        attackButton.onClick.AddListener(() => inspector.ImproviseStat(StatType.Attack));
        defendButton.onClick.AddListener(() => inspector.ImproviseStat(StatType.Defend));
        influenceButton.onClick.AddListener(() => inspector.ImproviseStat(StatType.Influence));
        exploreButton.onClick.AddListener(() => inspector.ImproviseStat(StatType.Explore));
    }

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool canImprovise = sel != null && card != null
                            && card.cardSO.empowerType != EmpowerType.None;
        root.SetActive(canImprovise);
        if (!canImprovise) return;

        Mark(attackButton, StatType.Attack, sel);
        Mark(defendButton, StatType.Defend, sel);
        Mark(influenceButton, StatType.Influence, sel);
        Mark(exploreButton, StatType.Explore, sel);
    }

    static void Mark(Button b, StatType stat, CardPlaySelection sel)
    {
        b.interactable = !(sel.Mode == PlayMode.Improvise && sel.ImproviseStat == stat);
    }
}
```

- [ ] **Step 3: Write EmpowerPanel**

Create `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/EmpowerPanel.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

// Toggles the empower flag. Locked (non-interactable) when CanEmpower() is false
// (Improvise active, or a non-empowerable card). Crystal reservation is added in Task 6.
public class EmpowerPanel : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;
    [SerializeField] Toggle empowerToggle;

    void OnEnable()  { inspector.Changed += Render; }
    void OnDisable() { inspector.Changed -= Render; }

    void Start()
    {
        empowerToggle.onValueChanged.AddListener(OnToggle);
    }

    void OnToggle(bool value)
    {
        // Ignore programmatic changes during Render (guarded by _suppress).
        if (_suppress) return;
        inspector.SetEmpowered(value);
    }

    bool _suppress;

    void Render()
    {
        var sel = inspector.Selection;
        bool show = sel != null;
        root.SetActive(show);
        if (!show) return;

        empowerToggle.interactable = sel.CanEmpower();

        _suppress = true;
        empowerToggle.isOn = sel.EffectiveEmpowered();
        _suppress = false;
    }
}
```

- [ ] **Step 4: Write PlayBar**

Create `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/PlayBar.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The Play button (label = live preview) plus Back. Play is disabled for unplayable
// cards (Wounds). Back closes the inspector.
public class PlayBar : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] Button playButton;
    [SerializeField] TextMeshProUGUI playLabel;
    [SerializeField] Button backButton;

    void OnEnable()  { inspector.Changed += Render; }
    void OnDisable() { inspector.Changed -= Render; }

    void Start()
    {
        playButton.onClick.AddListener(() => inspector.Play());
        backButton.onClick.AddListener(() => inspector.Close());
    }

    void Render()
    {
        var sel = inspector.Selection;
        if (sel == null) return;
        playButton.interactable = sel.IsPlayable();
        playLabel.text = sel.IsPlayable() ? $"PLAY · {sel.Describe()}" : "Cannot play";
    }
}
```

- [ ] **Step 5: Wire the views in the scene**

On the card menu canvas, attach `ChoiceBanner`, `ImprovisePanel`, `EmpowerPanel`, `PlayBar` to their respective section GameObjects. Assign each view's `inspector` field to the `CardInspector` from Task 4, and assign the existing buttons/labels/toggle (reuse the current `PlayButton`, `EmpowerToggle`, `ImproviseToggle`, `StatChoiceToggles` UI objects as the button references — only the scripts change, not the UI widgets).

- [ ] **Step 6: Play-mode verify the bug is fixed**

Enter Play mode. Open a choice card (Attack|Influence). Pick **Influence** in the Choice banner → Play label reads `PLAY · +2 Influence`. Now click an **Improvise** option → Choice banner hides, Empower locks, label reads `+1 … (improvised)`. Switch back to a Choice segment → **the previous Influence choice is still selected** and the label returns to `+2 Influence`. Confirm no selection was wiped. Open a Wound → Improvise/Empower hidden, Play disabled (`Cannot play`).

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ Assets/Scenes/GameBoard.unity
git commit -m "feat: card-menu section views render from CardInspector (bug fixed)"
```

---

### Task 6: Empower crystal reservation (display only)

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/Crystal.cs`
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs:SetEmpowered`
- Test: manual Play-mode verification.

**Interfaces:**
- Consumes: `CrystalInventory.SelectEmpowerCrystal()`, `CrystalInventory.crystalsInInventory`.
- Produces: `Crystal.SetReserved(bool)`; `CardInspector.SetEmpowered` now reserves/releases a crystal for display.

- [ ] **Step 1: Add SetReserved to Crystal**

In `Crystal.cs`, add (do not touch `RemoveCrystal`):

```csharp
    [SerializeField] private CanvasGroup canvasGroup; // optional; for dim visual

    // Display-only reservation. The crystal stays in inventory; this only signals
    // "this crystal will be spent when the empowered card is played." Real consume
    // is still CrystalInventory.EmpowerCrystal() at play time.
    public void SetReserved(bool reserved)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = reserved ? 0.4f : 1f;
    }
```

- [ ] **Step 2: Reserve/release from the inspector**

In `CardInspector.cs`, replace `SetEmpowered` with reservation handling:

```csharp
    Crystal _reserved;

    public void SetEmpowered(bool value)
    {
        if (Selection == null) return;

        if (value && Selection.CanEmpower())
        {
            var inv = FindAnyObjectByType<CrystalInventory>();
            var crystal = inv != null ? inv.SelectEmpowerCrystal() : null;
            if (crystal == null)
            {
                GameManager.Instance.ValidationMessage(
                    $"You cannot empower without {Card.cardSO.empowerType} crystals or an Allcrystal!");
                Raise();
                return;
            }
            _reserved = crystal;
            _reserved.SetReserved(true);
            Selection.SetEmpowered(true);
        }
        else
        {
            ReleaseReservation();
            Selection.SetEmpowered(false);
        }
        Raise();
    }

    void ReleaseReservation()
    {
        if (_reserved != null) { _reserved.SetReserved(false); _reserved = null; }
    }
```

Also call `ReleaseReservation();` at the start of `Close()` and inside `Play()` right after building the command (the real consume takes over), so a reserved-but-unplayed crystal is always returned:

```csharp
    public void Close()
    {
        ReleaseReservation();
        GameManager.Instance.cardCanvas.enabled = false;
        Card = null;
        Selection = null;
    }
```

In `Play()`, after `AddCommand(...)`:

```csharp
        _reserved = null; // ownership passes to the real consume/undo path
```

- [ ] **Step 3: Add a CanvasGroup to crystal prefabs**

On each crystal prefab in `Assets/Prefabs/Crystal Prefabs/` (Green/Purple/Red/Yellow/Wild), add a `CanvasGroup` component and assign it to the new `canvasGroup` field. (If a crystal is a sprite rather than UI, instead dim its `SpriteRenderer` color in `SetReserved`; pick whichever matches the crystal's actual renderer.)

- [ ] **Step 4: Play-mode verify reservation lifecycle**

Enter Play mode with at least one matching crystal. Open an empowerable card, toggle Empower on → a matching crystal dims and the Play label shows the empower value (e.g. `+3`). Toggle Empower off → the crystal returns to full opacity and the label reverts. Toggle on, then **Back** → crystal returns. Toggle on, then **Play** → the crystal is actually consumed (existing path); **Undo** → it returns (existing path). Confirm no crystal is double-spent or lost.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/Crystal.cs* Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs "Assets/Prefabs/Crystal Prefabs/"
git commit -m "feat: display-only crystal reservation on empower toggle"
```

---

### Task 7: Open path + delete old toggle scripts + final verification

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs` (open via inspector; remove temp smoke line)
- Delete: the old menu scripts (see list below)
- Modify: card-menu prefabs/scene to drop deleted components
- Test: full manual Play-mode verification.

**Interfaces:**
- Consumes: `CardInspector.Open/Close`.
- Produces: the card click opens the inspector; old scripts and their `ToggleEvent` wiring are gone.

- [ ] **Step 1: Route card open/close through the inspector**

In `Card.cs`, replace the body of `OnPointerClick` so the maximize/minimize still fire (Phase 2 restyles them) but the menu state goes through the inspector. Remove the throwaway line from Task 4 Step 3:

```csharp
    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameManager.Instance.cardListCanvas.enabled) return;

        var inspector = FindAnyObjectByType<CardInspector>();
        if (isMaximized)
        {
            onCloseCardMenu_MinimizeCard.Raise(this);
            inspector.Close();
            isMaximized = false;
        }
        else if (!isPlayed)
        {
            onOpenCardMenu_MaximizeCard.Raise(this);
            inspector.Open(this);
            isMaximized = true;
        }
        else
        {
            GameManager.Instance.ValidationMessage(
                $"{cardSO.name} has already been played. Click Undo on the Gameboard to undo previous plays.");
        }
    }
```

(`onClick_OpenCardMenu`/`onClick_CloseCardMenu` `CardEvent`s previously toggled `cardCanvas`; the inspector now owns `cardCanvas.enabled`, so those two raises are removed here. Leave the serialized fields in place for Phase 2 or delete if unused — your call, but do not break other listeners on those assets.)

- [ ] **Step 2: Verify no remaining references to the old scripts**

Search the project for references before deleting:

```bash
grep -rln "ImprovButtons\|ImprovToggle\|ChoiceToggles\|class Empower\|PlayButton\|CardMenuInterface" \
  "Assets" --include=*.cs
```

Expected: only the files about to be deleted appear. If a scene/prefab still references a component, open it and remove the missing-script slot in Step 4.

- [ ] **Step 3: Delete the old scripts**

```bash
cd "Assets/Scripts/GameObjectScripts/CardMenuScripts"
rm -f Empower.cs Empower.cs.meta PlayButton.cs PlayButton.cs.meta
rm -f ImprovScripts/Improv.cs ImprovScripts/Improv.cs.meta \
      ImprovScripts/ImprovButtons.cs ImprovScripts/ImprovButtons.cs.meta \
      ImprovScripts/ImprovToggle.cs ImprovScripts/ImprovToggle.cs.meta \
      ImprovScripts/ImprovAttack.cs ImprovScripts/ImprovAttack.cs.meta \
      ImprovScripts/ImprovDefend.cs ImprovScripts/ImprovDefend.cs.meta \
      ImprovScripts/ImprovExplore.cs ImprovScripts/ImprovExplore.cs.meta \
      ImprovScripts/ImprovInfluence.cs ImprovScripts/ImprovInfluence.cs.meta \
      ChoiceToggles/ChoiceToggles.cs ChoiceToggles/ChoiceToggles.cs.meta \
      ChoiceToggles/AttackToggle.cs ChoiceToggles/AttackToggle.cs.meta \
      ChoiceToggles/DefendToggle.cs ChoiceToggles/DefendToggle.cs.meta \
      ChoiceToggles/InfluenceToggle.cs ChoiceToggles/InfluenceToggle.cs.meta \
      ChoiceToggles/ExploreToggle.cs ChoiceToggles/ExploreToggle.cs.meta
```

Delete `CardMenuInterface.cs` only if Step 2 showed no remaining references.

- [ ] **Step 4: Clear missing-script slots in the scene/prefabs**

Back in Unity, open the GameBoard scene and the `PlayButton`, `EmpowerToggle`, `ImproviseToggle`, `StatChoiceToggles` prefabs. Remove any "Missing (Mono Script)" component slots left by the deletions, keeping the UI widgets (Buttons/Toggle/Text) that the Task 5 views now reference. Confirm the Console shows no compile errors.

- [ ] **Step 5: Full Play-mode verification**

Enter Play mode and confirm end to end:
1. Click a card → inspector opens centered menu; the rest of the hand is still visible.
2. Single-stat card → Play applies the printed stat; Undo reverses it.
3. Choice card → pick a stat → Play applies only that stat; the **choice survives** switching to Improvise and back (the bug).
4. Improvise → Empower and Choice lock; Play applies +1; Undo reverses.
5. Empower → crystal reserves on toggle, returns on un-toggle/Back, consumes on Play, restores on Undo.
6. Wound → not playable, cannot be improvised or empowered.
7. Commit point (explore/influence/turn end) clears the stack and played cards behave as before.

- [ ] **Step 6: Run EditMode tests once more**

Test Runner ▸ EditMode ▸ Run All. Expected: all `CardSnapshotTests` and `CardPlaySelectionTests` PASS.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: route card menu through CardInspector and remove old toggle scripts"
```

---

## Self-Review notes (coverage)

- **Bug fix** — Task 3 regression test + Task 5 Step 6 manual check.
- **Single-state model** — Tasks 2–4.
- **Reuse PlayCommand/undo + Player math** — Task 4 routing; Global Constraints forbid touching them.
- **Empower reservation (display only)** — Task 6.
- **Enum-as-choice-list** — `CardSnapshot.CardType` flags drive ChoiceBanner (Task 5).
- **Delete old scripts / rewire** — Task 7.
- **Out of scope for Phase 1 (later plans):** fan layout, center-float animation, echo-flight/juice, new Input System/gamepad. These are Phases 2–4 and get their own plans.
