# Enemy Traits, Per-Enemy Blocking & Wound Plumbing — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 13 authored enemy traits that bend the combat math, make the Defend phase a per-enemy blocking decision, and rebuild wound placement so wounds can be routed to hand or discard (and later to units).

**Architecture:** All combat math lands in pure, Unity-free static classes in the existing `ArchonsRise.CardPlay` assembly, tested from the CLI without opening Unity. `CombatController` builds a plain `EnemyCombatant[]` from its `EnemyCard`s, hands it to `EnemyTraitRules`, and consumes a `CounterattackPreview` struct plus a `List<WoundDestination>`. MonoBehaviours only render and dispatch.

**Tech Stack:** Unity (C#), NUnit, the repo's CLI pure-test harness (`tools/pure-tests/run.sh`, Unity's MonoBleedingEdge `mcs`).

**Spec:** `docs/superpowers/specs/2026-07-29-enemy-traits-and-wound-plumbing-design.md`. Section references below (§4.3, §7.1, …) point into it.

## Global Constraints

- **No new asmdefs.** `ArchonsRise.CardPlay` (references `ArchonsRise.Enums`, `ArchonsRise.UiLanguage`) already exists and `ArchonsRise.Tests.EditMode` already references it. New pure classes go into existing folders.
- **Dependency direction:** `CardPlay → UiLanguage → Enums`. `UiLanguage` **cannot** see `CardPlay`. Anything needing `EnemyTraitTuning` must live in `CardPlay`.
- **Every pure class must be Unity-free** (no `using UnityEngine;`) or the CLI harness cannot compile it. `IconMarkup` and `CombatRules` are already Unity-free — keep them that way.
- **Test command, always from the repo root:** `tools/pure-tests/run.sh <source files...> <test files...>`. Paths must be **relative** (the repo path contains spaces and `mcs.bat` mangles quoted absolute paths).
- **`CombatRules.cs` is not modified.** `Assets/Tests/EditMode/CombatRulesTests.cs` must pass **unmodified** at every point in this plan. If a task appears to require changing it, the pipeline has drifted — stop.
- **Never hand-edit Unity scene or prefab YAML.** Prefab/scene wiring is done by the user in the editor from step-by-step instructions (Tasks 8, 12, 15).
- **`[Flags]` enums and `WoundDestination` are append-only.** New members go at the end.
- **All glyph strings route through `IconMarkup`.** Never hand-roll a `<sprite=…>` literal.
- **Trait rule text is generated from tuning values, never authored as a string** (§8.2).
- **Commit after every task.** The interface is `ICommands { void Execute(); void Undo(); }`.

---

## File Structure

**Create:**
- `Assets/Scripts/Enums/Enums/EnemyTrait.cs` — the `[Flags]` trait enum.
- `Assets/Scripts/Enums/Enums/WoundDestination.cs` — where a wound lands.
- `Assets/Scripts/CardPlay/EnemyCombatant.cs` — the pure per-enemy input record + `EnemyTraitTuning`.
- `Assets/Scripts/CardPlay/EnemyTraitRules.cs` — the whole §4 pipeline.
- `Assets/Scripts/CardPlay/EnemyTraitCopy.cs` — generated rule text (needs tuning, so not in `UiLanguage`).
- `Assets/Scripts/CardPlay/WoundPlacementRules.cs` — wound count → destination list.
- `Assets/Scripts/GameScriptableObjectTypes/EnemyTraitTuningSO.cs` — Unity wrapper around `EnemyTraitTuning`.
- `Assets/Scripts/Managers/Commands/BlockCommand.cs` — undoable block.
- Test files under `Assets/Tests/EditMode/`.

**Modify:**
- `Assets/Scripts/GameScriptableObjectTypes/EnemiesSO.cs` — add `traits`.
- `Assets/Scripts/UiLanguage/IconMarkup.cs` — add badge/name/aura helpers.
- `Assets/Scripts/CardPlay/CombatPhaseRules.cs` — add `CanBlock`, rewire `Advance`.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs` — blocked state, badges, Defend button.
- `Assets/Scripts/Managers/CombatController.cs` — pipeline + placement wiring.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs` — `AddWound(WoundDestination)`.
- `Assets/Tests/EditMode/CombatAdvanceStateTests.cs` — `Advance` signature changed.

---

## Task 1: Trait enum and aura resolution

**Files:**
- Create: `Assets/Scripts/Enums/Enums/EnemyTrait.cs`
- Create: `Assets/Scripts/CardPlay/EnemyCombatant.cs`
- Create: `Assets/Scripts/CardPlay/EnemyTraitRules.cs`
- Test: `Assets/Tests/EditMode/EnemyTraitAuraTests.cs`

**Interfaces:**
- Produces: `EnemyTrait` (flags enum); `struct EnemyCombatant { int Attack; int HP; EnemyTrait Traits; bool Blocked; }`; `EnemyTraitRules.GrantedByAuras(IReadOnlyList<EnemyCombatant>) → EnemyTrait`; `EnemyTraitRules.EffectiveTraits(EnemyCombatant, IReadOnlyList<EnemyCombatant>) → EnemyTrait`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/EnemyTraitAuraTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitAuraTests
{
    static EnemyCombatant E(int atk, EnemyTrait traits = EnemyTrait.None) =>
        new EnemyCombatant { Attack = atk, HP = atk, Traits = traits, Blocked = false };

    [Test]
    public void NoAuras_GrantsNothing()
    {
        var roster = new List<EnemyCombatant> { E(3), E(4, EnemyTrait.Armored) };
        Assert.AreEqual(EnemyTrait.None, EnemyTraitRules.GrantedByAuras(roster));
    }

    [Test]
    public void Miasma_GrantsToxicToEveryone()
    {
        var roster = new List<EnemyCombatant> { E(1, EnemyTrait.Miasma), E(4) };
        var ogre = roster[1];
        Assert.IsTrue(EnemyTraitRules.EffectiveTraits(ogre, roster).HasFlag(EnemyTrait.Toxic));
    }

    [Test]
    public void Ironclad_GrantsArmored_Outrider_GrantsSwift()
    {
        var roster = new List<EnemyCombatant> { E(1, EnemyTrait.Ironclad), E(1, EnemyTrait.Outrider), E(4) };
        var t = EnemyTraitRules.EffectiveTraits(roster[2], roster);
        Assert.IsTrue(t.HasFlag(EnemyTrait.Armored));
        Assert.IsTrue(t.HasFlag(EnemyTrait.Swift));
    }

    [Test]
    public void GrantingIsIdempotent_TwoMiasmaSameAsOne()
    {
        var one = new List<EnemyCombatant> { E(1, EnemyTrait.Miasma), E(4) };
        var two = new List<EnemyCombatant> { E(1, EnemyTrait.Miasma), E(1, EnemyTrait.Miasma), E(4) };
        Assert.AreEqual(EnemyTraitRules.GrantedByAuras(one), EnemyTraitRules.GrantedByAuras(two));
    }

    [Test]
    public void EffectiveTraits_PreservesOwnTraits()
    {
        var roster = new List<EnemyCombatant> { E(1, EnemyTrait.Miasma), E(4, EnemyTrait.Brutal) };
        var t = EnemyTraitRules.EffectiveTraits(roster[1], roster);
        Assert.IsTrue(t.HasFlag(EnemyTrait.Brutal));
        Assert.IsTrue(t.HasFlag(EnemyTrait.Toxic));
    }

    [Test]
    public void BlockedAuraEnemy_StillGrants()
    {
        // A blocked enemy is ALIVE, so its aura persists (spec 7.4).
        var herald = E(1, EnemyTrait.Miasma); herald.Blocked = true;
        var roster = new List<EnemyCombatant> { herald, E(4) };
        Assert.IsTrue(EnemyTraitRules.EffectiveTraits(roster[1], roster).HasFlag(EnemyTrait.Toxic));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Tests/EditMode/EnemyTraitAuraTests.cs`

Expected: compile FAILS — the source files do not exist yet.

- [ ] **Step 3: Create the enum**

Create `Assets/Scripts/Enums/Enums/EnemyTrait.cs`:

```csharp
// Authored enemy traits (spec 2026-07-29). APPEND-ONLY: new members go at the
// end. Self traits make one enemy harder to remove; aura traits grant a self
// trait to the whole roster, which is why granting is a bitwise OR and can
// never double-stack.
[System.Flags]
public enum EnemyTrait
{
    None = 0,
    // self
    Armored  = 1,    Elusive = 2,     Hulking  = 4,     Swift    = 8,
    Brutal   = 16,   Toxic   = 32,    Leech    = 64,    Harrying = 128,
    Vengeful = 256,
    // aura
    Warlord  = 512,  Miasma  = 1024,  Ironclad = 2048,  Outrider = 4096,
}
```

- [ ] **Step 4: Create the combatant record and tuning**

Create `Assets/Scripts/CardPlay/EnemyCombatant.cs`:

```csharp
// The pure, Unity-free view of one enemy in a fight. CombatController builds
// these from EnemyCard so every rule below stays testable from the CLI.
// Attack/HP are the EFFECTIVE values (SO value + doom-scaling bonus).
public struct EnemyCombatant
{
    public int Attack;
    public int HP;
    public EnemyTrait Traits;
    public bool Blocked;
}

// Every trait magnitude. Mirrors EnemyTraitTuningSO's serialized instance, the
// same split RewardTuning/RewardTuningSO already uses: pure math reads this,
// Unity only stores it.
[System.Serializable]
public class EnemyTraitTuning
{
    public int armorSiegeMult = 2;
    public int hulkAttackMult = 2;
    public int swiftThreatMult = 2;
    public int brutalSurchargeMult = 1;
    public int warlordBonus = 1;
    public int toxicCopies = 1;
    public int leechCrystals = 1;
    public int vengefulWounds = 1;
    public int harryHandPenalty = 1;
}
```

- [ ] **Step 5: Implement aura resolution**

Create `Assets/Scripts/CardPlay/EnemyTraitRules.cs`:

```csharp
using System.Collections.Generic;

// The trait pipeline (spec 2026-07-29 §4). Pure and Unity-free so it runs in
// the CLI harness. CombatRules is NOT modified — this calls into it for the
// one and only Toughness bite loop.
public static class EnemyTraitRules
{
    // §4.1 Auras resolve to a granted mask BEFORE anything else runs, so every
    // downstream step reads effective traits and needs no aura awareness.
    // Blocked enemies still grant: blocking is not killing (§7.4).
    public static EnemyTrait GrantedByAuras(IReadOnlyList<EnemyCombatant> roster)
    {
        EnemyTrait granted = EnemyTrait.None;
        for (int i = 0; i < roster.Count; i++)
        {
            var t = roster[i].Traits;
            if (t.HasFlag(EnemyTrait.Miasma))   granted |= EnemyTrait.Toxic;
            if (t.HasFlag(EnemyTrait.Ironclad)) granted |= EnemyTrait.Armored;
            if (t.HasFlag(EnemyTrait.Outrider)) granted |= EnemyTrait.Swift;
        }
        return granted;
    }

    public static EnemyTrait EffectiveTraits(EnemyCombatant e, IReadOnlyList<EnemyCombatant> roster)
        => e.Traits | GrantedByAuras(roster);
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Tests/EditMode/EnemyTraitAuraTests.cs`

Expected: `--- 6 passed, 0 failed ---`

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Tests/EditMode/EnemyTraitAuraTests.cs
git commit -m "feat: enemy trait enum and aura resolution"
```

---

## Task 2: Per-enemy derived values

**Files:**
- Modify: `Assets/Scripts/CardPlay/EnemyTraitRules.cs`
- Test: `Assets/Tests/EditMode/EnemyTraitValueTests.cs`

**Interfaces:**
- Consumes: `EnemyTrait`, `EnemyCombatant`, `EnemyTraitTuning`, `EnemyTraitRules.EffectiveTraits` (Task 1).
- Produces: `WarlordAura(int i, roster, tuning) → int`; `BaseAttack(i, roster, tuning) → int`; `Threat(i, roster, tuning) → int`; `Basis(i, roster, tuning) → int`; `SiegeCost(i, roster, tuning) → int`; `AttackCost(i, roster, tuning) → int`. All take the enemy's **index** in the roster because Warlord must exclude the enemy itself.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/EnemyTraitValueTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitValueTests
{
    static EnemyTraitTuning T() => new EnemyTraitTuning();
    static EnemyCombatant E(int atk, int hp, EnemyTrait traits = EnemyTrait.None) =>
        new EnemyCombatant { Attack = atk, HP = hp, Traits = traits, Blocked = false };

    [Test]
    public void Swift_DoublesThreat_ButNotBasis()
    {
        var r = new List<EnemyCombatant> { E(3, 3, EnemyTrait.Swift) };
        Assert.AreEqual(6, EnemyTraitRules.Threat(0, r, T()));
        Assert.AreEqual(3, EnemyTraitRules.Basis(0, r, T()));
    }

    [Test]
    public void Plain_ThreatEqualsBasisEqualsAttack()
    {
        var r = new List<EnemyCombatant> { E(4, 6) };
        Assert.AreEqual(4, EnemyTraitRules.Threat(0, r, T()));
        Assert.AreEqual(4, EnemyTraitRules.Basis(0, r, T()));
    }

    [Test]
    public void Armored_DoublesSiegeCost_LeavesAttackCost()
    {
        var r = new List<EnemyCombatant> { E(3, 6, EnemyTrait.Armored) };
        Assert.AreEqual(12, EnemyTraitRules.SiegeCost(0, r, T()));
        Assert.AreEqual(6, EnemyTraitRules.AttackCost(0, r, T()));
    }

    [Test]
    public void Hulking_DoublesAttackCost_LeavesSiegeCost()
    {
        var r = new List<EnemyCombatant> { E(3, 6, EnemyTrait.Hulking) };
        Assert.AreEqual(6, EnemyTraitRules.SiegeCost(0, r, T()));
        Assert.AreEqual(12, EnemyTraitRules.AttackCost(0, r, T()));
    }

    [Test]
    public void Elusive_SiegeCostIsUnreachable()
    {
        var r = new List<EnemyCombatant> { E(3, 6, EnemyTrait.Elusive) };
        Assert.AreEqual(int.MaxValue, EnemyTraitRules.SiegeCost(0, r, T()));
    }

    [Test]
    public void Warlord_BuffsOthersNotItself()
    {
        var r = new List<EnemyCombatant> { E(2, 2, EnemyTrait.Warlord), E(4, 6) };
        Assert.AreEqual(2, EnemyTraitRules.BaseAttack(0, r, T()), "warlord must not buff itself");
        Assert.AreEqual(5, EnemyTraitRules.BaseAttack(1, r, T()));
    }

    [Test]
    public void Warlord_StacksAdditively()
    {
        var r = new List<EnemyCombatant>
            { E(2, 2, EnemyTrait.Warlord), E(2, 2, EnemyTrait.Warlord), E(4, 6) };
        Assert.AreEqual(6, EnemyTraitRules.BaseAttack(2, r, T()));
    }

    [Test]
    public void Ironclad_GrantsArmored_SoSiegeCostDoubles()
    {
        var r = new List<EnemyCombatant> { E(1, 1, EnemyTrait.Ironclad), E(3, 6) };
        Assert.AreEqual(12, EnemyTraitRules.SiegeCost(1, r, T()));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Tests/EditMode/EnemyTraitValueTests.cs`

Expected: compile FAILS with "does not contain a definition for `Threat`".

- [ ] **Step 3: Implement the derived values**

Append inside the `EnemyTraitRules` class in `Assets/Scripts/CardPlay/EnemyTraitRules.cs`:

```csharp
    // §4.2 Warlord grants real Attack to every OTHER survivor. Additive, so two
    // Warlords stack — unlike granting auras, which are idempotent by OR.
    public static int WarlordAura(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        int count = 0;
        for (int i = 0; i < roster.Count; i++)
            if (i != index && roster[i].Traits.HasFlag(EnemyTrait.Warlord)) count++;
        return count * t.warlordBonus;
    }

    public static int BaseAttack(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
        => roster[index].Attack + WarlordAura(index, roster, t);

    // The bar Defend must clear. Swift raises it WITHOUT raising the punishment.
    public static int Threat(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        int b = BaseAttack(index, roster, t);
        return EffectiveTraits(roster[index], roster).HasFlag(EnemyTrait.Swift) ? b * t.swiftThreatMult : b;
    }

    // What can become wounds. Never scaled by Swift — that is the cap's job.
    public static int Basis(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
        => BaseAttack(index, roster, t);

    // Elusive returns int.MaxValue rather than needing a separate bool, so the
    // Siege phase keeps exactly one comparison.
    public static int SiegeCost(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        var traits = EffectiveTraits(roster[index], roster);
        if (traits.HasFlag(EnemyTrait.Elusive)) return int.MaxValue;
        int hp = roster[index].HP;
        return traits.HasFlag(EnemyTrait.Armored) ? hp * t.armorSiegeMult : hp;
    }

    public static int AttackCost(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        int hp = roster[index].HP;
        return EffectiveTraits(roster[index], roster).HasFlag(EnemyTrait.Hulking) ? hp * t.hulkAttackMult : hp;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Tests/EditMode/EnemyTraitAuraTests.cs Assets/Tests/EditMode/EnemyTraitValueTests.cs`

Expected: `--- 14 passed, 0 failed ---`

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Tests/EditMode/EnemyTraitValueTests.cs
git commit -m "feat: per-enemy trait-derived threat, basis, siege and attack costs"
```

---

## Task 3: Group counterattack — filter, cap, surcharge

**Files:**
- Modify: `Assets/Scripts/CardPlay/EnemyTraitRules.cs`
- Test: `Assets/Tests/EditMode/EnemyTraitCounterattackTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–2, plus `CombatRules.WoundCount(AttackKind, int, int, int)` (existing, unmodified).
- Produces: `struct CounterattackPreview { int UnblockedThreat; int UnblockedBasis; int BrutalSurcharge; int TotalContribution; int ToxicContribution; int LeechContribution; }`; `EnemyTraitRules.BuildPreview(roster, tuning) → CounterattackPreview`; `EnemyTraitRules.Effective(preview, defendLeft) → int`; `EnemyTraitRules.HandWounds(preview, defendLeft, toughness) → int`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/EnemyTraitCounterattackTests.cs`. Every case is a row of spec §4.5:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitCounterattackTests
{
    static EnemyTraitTuning T() => new EnemyTraitTuning();
    static EnemyCombatant E(int atk, EnemyTrait traits = EnemyTrait.None, bool blocked = false) =>
        new EnemyCombatant { Attack = atk, HP = atk, Traits = traits, Blocked = blocked };

    static int Wounds(List<EnemyCombatant> r, int defend, int toughness) =>
        EnemyTraitRules.HandWounds(EnemyTraitRules.BuildPreview(r, T()), defend, toughness);

    // --- spec 4.5 rows 1-2: Swift punishes the near-miss, never the whiff ---
    [Test]
    public void Row1_Swift_Defend5_OneWound()
    {
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Swift) };
        Assert.AreEqual(1, Wounds(r, 5, 2));
    }

    [Test]
    public void Row2_Swift_Defend0_NeverWorseThanPlain()
    {
        var swift = new List<EnemyCombatant> { E(3, EnemyTrait.Swift) };
        var plain = new List<EnemyCombatant> { E(3) };
        Assert.AreEqual(2, Wounds(swift, 0, 2));
        Assert.AreEqual(Wounds(plain, 0, 2), Wounds(swift, 0, 2));
    }

    // --- spec 4.5 rows 3-5: Brutal's surcharge bypasses the cap ---
    [Test]
    public void Row3_Brutal_FullyBlocked_ZeroWounds()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal) };
        Assert.AreEqual(0, Wounds(r, 4, 2));
    }

    [Test]
    public void Row4_Brutal_Defend2_ThreeWounds()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal) };
        Assert.AreEqual(3, Wounds(r, 2, 2)); // (8-2)/2
    }

    [Test]
    public void Row5_Brutal_Defend0_FourWounds()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal) };
        Assert.AreEqual(4, Wounds(r, 0, 2)); // (8-0)/2
    }

    [Test]
    public void Row9_Warlord_AuraIsRealAttack()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Warlord), E(4) };
        Assert.AreEqual(4, Wounds(r, 0, 2)); // threat 2+5=7 -> bite(7,2)=4
    }

    [Test]
    public void Row10_SwiftPlusBrutal_WallAndCliffCompose()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Swift | EnemyTrait.Brutal) };
        Assert.AreEqual(4, Wounds(r, 5, 2)); // threat 8, short 3, cap 4->3, +4 = 7 -> 4
    }

    // --- the cap and surcharge in isolation ---
    [Test]
    public void Cap_NeverExceedsTotalBasis()
    {
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Swift) };
        var p = EnemyTraitRules.BuildPreview(r, T());
        Assert.AreEqual(3, EnemyTraitRules.Effective(p, 0), "capped at basis, not threat");
    }

    [Test]
    public void FullyBlockedGroup_ShortCircuits_NoSurcharge()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal) };
        var p = EnemyTraitRules.BuildPreview(r, T());
        Assert.AreEqual(0, EnemyTraitRules.Effective(p, 99));
    }

    // --- blocking (spec 7) ---
    [Test]
    public void BlockedEnemy_ContributesNothing()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal, blocked: true), E(3) };
        var p = EnemyTraitRules.BuildPreview(r, T());
        Assert.AreEqual(3, p.UnblockedThreat);
        Assert.AreEqual(3, p.UnblockedBasis);
        Assert.AreEqual(0, p.BrutalSurcharge, "a blocked Brutal must not surcharge");
    }

    [Test]
    public void BlockingEveryone_YieldsZeroWounds()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal, true), E(3, EnemyTrait.Toxic, true) };
        Assert.AreEqual(0, Wounds(r, 0, 2));
    }

    // --- spec 7.3: traits decide what is worth blocking ---
    [Test]
    public void BlockingBrutal_PreventsMoreThanItCosts()
    {
        var unblocked = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal) };
        var blocked   = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal, blocked: true) };
        int cost = EnemyTraitRules.Threat(0, unblocked, T());       // 4
        Assert.AreEqual(4, cost);
        // Spending 4 Defend to block removes 8 of punishment.
        Assert.AreEqual(4, Wounds(unblocked, 0, 2));
        Assert.AreEqual(0, Wounds(blocked, 0, 2));
        // Soaking with the same 4 Defend instead leaves more damage standing.
        Assert.Greater(Wounds(unblocked, cost, 2), Wounds(blocked, 0, 2));
    }

    [Test]
    public void BlockingSwift_IsATrap_SoakingIsBetter()
    {
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Swift) };
        int blockCost = EnemyTraitRules.Threat(0, r, T());          // 6
        Assert.AreEqual(6, blockCost);
        // Spending the 6 it costs to block, but soaking with it instead,
        // already yields zero wounds — so the block bought nothing.
        Assert.AreEqual(0, Wounds(r, blockCost, 2));
    }

    // --- THE compatibility guarantee: no traits, no blocks == today's math ---
    [Test]
    public void NoTraitsNoBlocks_MatchesLegacyGroupWoundCount()
    {
        for (int atkA = 0; atkA <= 6; atkA++)
        for (int atkB = 0; atkB <= 6; atkB++)
        for (int defend = 0; defend <= 12; defend++)
        for (int tough = 1; tough <= 3; tough++)
        {
            var r = new List<EnemyCombatant> { E(atkA), E(atkB) };
            int legacy = CombatRules.GroupWoundCount(defend, atkA + atkB, tough);
            Assert.AreEqual(legacy, Wounds(r, defend, tough),
                $"drift at atk {atkA}+{atkB}, defend {defend}, toughness {tough}");
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Scripts/CardPlay/CombatRules.cs Assets/Tests/EditMode/EnemyTraitCounterattackTests.cs`

Expected: compile FAILS with "does not contain a definition for `BuildPreview`".

- [ ] **Step 3: Implement the preview and wound math**

Add at the bottom of `Assets/Scripts/CardPlay/EnemyTraitRules.cs`, **outside** the class:

```csharp
// One pass over the unblocked survivors, so CombatPhaseRules.Advance takes a
// single argument instead of a growing parameter list. Contribution is the
// attribution weight for share traits (§4.4).
public struct CounterattackPreview
{
    public int UnblockedThreat;
    public int UnblockedBasis;
    public int BrutalSurcharge;
    public int TotalContribution;
    public int ToxicContribution;
    public int LeechContribution;
}
```

And append inside the `EnemyTraitRules` class:

```csharp
    // §4.3 step 0-2 + 6, plus the §4.4 attribution weights, in one pass.
    // BLOCKED ENEMIES ARE SKIPPED — but they were already counted for auras,
    // because blocking is not killing (§7.4).
    public static CounterattackPreview BuildPreview(IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        var p = new CounterattackPreview();
        for (int i = 0; i < roster.Count; i++)
        {
            if (roster[i].Blocked) continue;

            var traits = EffectiveTraits(roster[i], roster);
            int basis = Basis(i, roster, t);
            int surcharge = traits.HasFlag(EnemyTrait.Brutal)
                ? BaseAttack(i, roster, t) * t.brutalSurchargeMult : 0;

            p.UnblockedThreat += Threat(i, roster, t);
            p.UnblockedBasis  += basis;
            p.BrutalSurcharge += surcharge;

            int contribution = basis + surcharge;
            p.TotalContribution += contribution;
            if (traits.HasFlag(EnemyTrait.Toxic)) p.ToxicContribution += contribution;
            if (traits.HasFlag(EnemyTrait.Leech)) p.LeechContribution += contribution;
        }
        return p;
    }

    // §4.3 steps 3-7. The CAP (Swift's guarantee) must be applied BEFORE the
    // SURCHARGE (Brutal's punishment), or the cap swallows the surcharge and
    // Brutal does nothing.
    public static int Effective(CounterattackPreview p, int defendLeft)
    {
        int shortfall = p.UnblockedThreat - defendLeft;
        if (shortfall <= 0) return 0;
        int capped = shortfall < p.UnblockedBasis ? shortfall : p.UnblockedBasis;
        return capped + p.BrutalSurcharge;
    }

    // §4.3 step 8. Reuses CombatRules' bite loop so there is exactly one
    // implementation of "divide a shortfall into Toughness-sized bites".
    public static int HandWounds(CounterattackPreview p, int defendLeft, int toughness)
        => Bite(Effective(p, defendLeft), toughness);

    static int Bite(int amount, int toughness)
        => amount <= 0 ? 0 : CombatRules.WoundCount(AttackKind.Normal, 0, amount, toughness);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Scripts/CardPlay/CombatRules.cs Assets/Tests/EditMode/EnemyTraitCounterattackTests.cs`

Expected: `--- 12 passed, 0 failed ---`

- [ ] **Step 5: Confirm CombatRules is untouched**

Run: `git diff --stat Assets/Scripts/CardPlay/CombatRules.cs`

Expected: **no output** (the file is unmodified). If it shows changes, revert them — the pipeline has drifted.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Tests/EditMode/EnemyTraitCounterattackTests.cs
git commit -m "feat: group counterattack with Swift cap and Brutal surcharge"
```

---

## Task 4: Share attribution — Toxic and Leech

**Files:**
- Modify: `Assets/Scripts/CardPlay/EnemyTraitRules.cs`
- Test: `Assets/Tests/EditMode/EnemyTraitShareTests.cs`

**Interfaces:**
- Consumes: `CounterattackPreview`, `Effective`, `HandWounds` (Task 3).
- Produces: `EnemyTraitRules.DiscardWounds(preview, defendLeft, toughness, tuning) → int`; `EnemyTraitRules.CrystalsStolen(preview, defendLeft, toughness, tuning) → int`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/EnemyTraitShareTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitShareTests
{
    static EnemyTraitTuning T() => new EnemyTraitTuning();
    static EnemyCombatant E(int atk, EnemyTrait traits = EnemyTrait.None) =>
        new EnemyCombatant { Attack = atk, HP = atk, Traits = traits, Blocked = false };

    static int Hand(List<EnemyCombatant> r, int d, int t) =>
        EnemyTraitRules.HandWounds(EnemyTraitRules.BuildPreview(r, T()), d, t);
    static int Discard(List<EnemyCombatant> r, int d, int t) =>
        EnemyTraitRules.DiscardWounds(EnemyTraitRules.BuildPreview(r, T()), d, t, T());

    [Test]
    public void Row6_SoloToxic_ExactDoubling()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Toxic) };
        Assert.AreEqual(1, Hand(r, 0, 2));
        Assert.AreEqual(1, Discard(r, 0, 2));
    }

    [Test]
    public void Row7_ToxicSpiderAndOgre_OgresClubIsNotPoisoned()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Toxic), E(4) };
        Assert.AreEqual(3, Hand(r, 0, 2));
        Assert.AreEqual(1, Discard(r, 0, 2)); // share 6*2/6 = 2 -> bite(2,2) = 1
    }

    [Test]
    public void Row8_MiasmaMakesEveryoneToxic_FullDoubling()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Toxic), E(4), E(1, EnemyTrait.Miasma) };
        Assert.AreEqual(4, Hand(r, 0, 2));
        Assert.AreEqual(4, Discard(r, 0, 2));
    }

    [Test]
    public void NoToxic_NoDiscardWounds()
    {
        var r = new List<EnemyCombatant> { E(4) };
        Assert.AreEqual(0, Discard(r, 0, 2));
    }

    [Test]
    public void FullyBlocked_NoDiscardWounds()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Toxic) };
        Assert.AreEqual(0, Discard(r, 99, 2));
    }

    [Test]
    public void EmptyRoster_GuardsDivideByZero()
    {
        var r = new List<EnemyCombatant>();
        Assert.AreEqual(0, Discard(r, 0, 2));
        Assert.AreEqual(0, Hand(r, 0, 2));
    }

    [Test]
    public void Leech_StealsOnItsShare()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Leech) };
        var p = EnemyTraitRules.BuildPreview(r, T());
        Assert.AreEqual(1, EnemyTraitRules.CrystalsStolen(p, 0, 2, T()));
    }

    [Test]
    public void Leech_StealsNothingWhenBlocked()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Leech) };
        var p = EnemyTraitRules.BuildPreview(r, T());
        Assert.AreEqual(0, EnemyTraitRules.CrystalsStolen(p, 99, 2, T()));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Scripts/CardPlay/CombatRules.cs Assets/Tests/EditMode/EnemyTraitShareTests.cs`

Expected: compile FAILS with "does not contain a definition for `DiscardWounds`".

- [ ] **Step 3: Implement share attribution**

Append inside the `EnemyTraitRules` class:

```csharp
    // §4.4 The summed counterattack has no notion of whose wound is whose, so
    // payout traits claim a share of the total contribution. One function
    // serves both Toxic and Leech.
    static int Share(CounterattackPreview p, int defendLeft, int traitContribution)
    {
        if (p.TotalContribution <= 0 || traitContribution <= 0) return 0;
        return Effective(p, defendLeft) * traitContribution / p.TotalContribution; // floor
    }

    public static int DiscardWounds(CounterattackPreview p, int defendLeft, int toughness, EnemyTraitTuning t)
        => Bite(Share(p, defendLeft, p.ToxicContribution), toughness) * t.toxicCopies;

    public static int CrystalsStolen(CounterattackPreview p, int defendLeft, int toughness, EnemyTraitTuning t)
        => Bite(Share(p, defendLeft, p.LeechContribution), toughness) * t.leechCrystals;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Scripts/CardPlay/CombatRules.cs Assets/Tests/EditMode/EnemyTraitShareTests.cs`

Expected: `--- 8 passed, 0 failed ---`

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Tests/EditMode/EnemyTraitShareTests.cs
git commit -m "feat: Toxic and Leech share attribution"
```

---

## Task 5: Wound destinations and placement

**Files:**
- Create: `Assets/Scripts/Enums/Enums/WoundDestination.cs`
- Create: `Assets/Scripts/CardPlay/WoundPlacementRules.cs`
- Test: `Assets/Tests/EditMode/WoundPlacementRulesTests.cs`

**Interfaces:**
- Produces: `enum WoundDestination { Hand = 0, Discard = 1 }`; `WoundPlacementRules.Place(int handWounds, int discardWounds) → List<WoundDestination>`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/WoundPlacementRulesTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class WoundPlacementRulesTests
{
    [Test]
    public void HandOnly_AllHand()
    {
        var p = WoundPlacementRules.Place(3, 0);
        Assert.AreEqual(3, p.Count);
        foreach (var d in p) Assert.AreEqual(WoundDestination.Hand, d);
    }

    [Test]
    public void HandAndDiscard_CountsMatch()
    {
        var p = WoundPlacementRules.Place(3, 1);
        Assert.AreEqual(4, p.Count);
        Assert.AreEqual(3, p.FindAll(d => d == WoundDestination.Hand).Count);
        Assert.AreEqual(1, p.FindAll(d => d == WoundDestination.Discard).Count);
    }

    [Test]
    public void HandWoundsComeFirst()
    {
        var p = WoundPlacementRules.Place(2, 2);
        Assert.AreEqual(WoundDestination.Hand, p[0]);
        Assert.AreEqual(WoundDestination.Hand, p[1]);
        Assert.AreEqual(WoundDestination.Discard, p[2]);
    }

    [Test]
    public void Zero_IsEmptyNotNull()
    {
        var p = WoundPlacementRules.Place(0, 0);
        Assert.IsNotNull(p);
        Assert.AreEqual(0, p.Count);
    }

    [Test]
    public void NegativeCounts_ClampToZero()
    {
        Assert.AreEqual(0, WoundPlacementRules.Place(-2, -1).Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/WoundDestination.cs Assets/Scripts/CardPlay/WoundPlacementRules.cs Assets/Tests/EditMode/WoundPlacementRulesTests.cs`

Expected: compile FAILS — the source files do not exist.

- [ ] **Step 3: Create the destination enum**

Create `Assets/Scripts/Enums/Enums/WoundDestination.cs`:

```csharp
// Where a wound card lands (spec 2026-07-29 §6.1). APPEND-ONLY — Unit = 2 is
// reserved for the unit-wounds phase.
//
// Destinations are COUNTING or NON-COUNTING for the wound-out loss. Hand and
// Discard both count (PlayerHand.TotalWoundCount enumerates them); Unit will
// NOT. Adding a zone to the loss axis must always be a deliberate edit in
// TotalWoundCount, never a side effect of adding a destination here.
public enum WoundDestination
{
    Hand = 0,
    Discard = 1,
}
```

- [ ] **Step 4: Implement placement**

Create `Assets/Scripts/CardPlay/WoundPlacementRules.cs`:

```csharp
using System.Collections.Generic;

// Produces the placement list the combat controller consumes. Today this is a
// pure rule; the unit-wounds phase swaps in a producer that can open the unit
// picker. The CONSUMER depends only on IReadOnlyList<WoundDestination>, which
// is why that phase costs no rework (spec §6.2, §6.3 rule 3).
public static class WoundPlacementRules
{
    public static List<WoundDestination> Place(int handWounds, int discardWounds)
    {
        var list = new List<WoundDestination>();
        for (int i = 0; i < handWounds; i++) list.Add(WoundDestination.Hand);
        for (int i = 0; i < discardWounds; i++) list.Add(WoundDestination.Discard);
        return list;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/WoundDestination.cs Assets/Scripts/CardPlay/WoundPlacementRules.cs Assets/Tests/EditMode/WoundPlacementRulesTests.cs`

Expected: `--- 5 passed, 0 failed ---`

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Enums/Enums/WoundDestination.cs Assets/Scripts/CardPlay/WoundPlacementRules.cs Assets/Tests/EditMode/WoundPlacementRulesTests.cs
git commit -m "feat: wound destination enum and placement rules"
```

---

## Task 6: Trait badges and generated rule text

**Files:**
- Modify: `Assets/Scripts/UiLanguage/IconMarkup.cs`
- Create: `Assets/Scripts/CardPlay/EnemyTraitCopy.cs`
- Test: `Assets/Tests/EditMode/EnemyTraitCopyTests.cs`

**Interfaces:**
- Produces: `IconMarkup.TraitBadge(EnemyTrait) → string`; `IconMarkup.TraitName(EnemyTrait) → string`; `IconMarkup.IsAuraTrait(EnemyTrait) → bool`; `IconMarkup.AllTraits → EnemyTrait[]`; `EnemyTraitCopy.Rule(EnemyTrait, EnemyTraitTuning) → string`; `EnemyTraitCopy.Split(EnemyTrait) → List<EnemyTrait>`.
- **Note:** `IconMarkup` lives in `ArchonsRise.UiLanguage`, which **cannot reference `CardPlay`**. That is why `Rule` (which needs `EnemyTraitTuning`) lives in `CardPlay` while the badge/name/aura helpers live in `IconMarkup`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/EnemyTraitCopyTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitCopyTests
{
    [Test]
    public void EveryTraitHasANonEmptyBadge()
    {
        foreach (var t in IconMarkup.AllTraits)
            Assert.IsFalse(string.IsNullOrEmpty(IconMarkup.TraitBadge(t)), "no badge for " + t);
    }

    [Test]
    public void AllBadgesAreUnique()
    {
        var seen = new HashSet<string>();
        foreach (var t in IconMarkup.AllTraits)
            Assert.IsTrue(seen.Add(IconMarkup.TraitBadge(t)), "duplicate badge for " + t);
    }

    [Test]
    public void EveryTraitHasANonEmptyName()
    {
        foreach (var t in IconMarkup.AllTraits)
            Assert.IsFalse(string.IsNullOrEmpty(IconMarkup.TraitName(t)), "no name for " + t);
    }

    [Test]
    public void AllThirteenTraitsAreListed()
    {
        Assert.AreEqual(13, IconMarkup.AllTraits.Length);
    }

    [Test]
    public void AurasAreFlaggedAsAuras()
    {
        Assert.IsTrue(IconMarkup.IsAuraTrait(EnemyTrait.Warlord));
        Assert.IsTrue(IconMarkup.IsAuraTrait(EnemyTrait.Miasma));
        Assert.IsTrue(IconMarkup.IsAuraTrait(EnemyTrait.Ironclad));
        Assert.IsTrue(IconMarkup.IsAuraTrait(EnemyTrait.Outrider));
        Assert.IsFalse(IconMarkup.IsAuraTrait(EnemyTrait.Armored));
        Assert.IsFalse(IconMarkup.IsAuraTrait(EnemyTrait.Toxic));
    }

    [Test]
    public void HulkingIsK_BecauseHarryingTookH()
    {
        Assert.AreEqual("K", IconMarkup.TraitBadge(EnemyTrait.Hulking));
        Assert.AreEqual("H", IconMarkup.TraitBadge(EnemyTrait.Harrying));
    }

    [Test]
    public void EveryTraitHasANonEmptyRuleLine()
    {
        var tuning = new EnemyTraitTuning();
        foreach (var t in IconMarkup.AllTraits)
            Assert.IsFalse(string.IsNullOrEmpty(EnemyTraitCopy.Rule(t, tuning)), "no rule for " + t);
    }

    [Test]
    public void RuleTextTracksTuning_NotHardcoded()
    {
        var a = new EnemyTraitTuning { armorSiegeMult = 2 };
        var b = new EnemyTraitTuning { armorSiegeMult = 3 };
        Assert.AreNotEqual(EnemyTraitCopy.Rule(EnemyTrait.Armored, a),
                           EnemyTraitCopy.Rule(EnemyTrait.Armored, b));
    }

    [Test]
    public void Split_ReturnsEachSetTrait()
    {
        var list = EnemyTraitCopy.Split(EnemyTrait.Armored | EnemyTrait.Toxic);
        Assert.AreEqual(2, list.Count);
        Assert.Contains(EnemyTrait.Armored, list);
        Assert.Contains(EnemyTrait.Toxic, list);
    }

    [Test]
    public void Split_NoneIsEmpty()
    {
        Assert.AreEqual(0, EnemyTraitCopy.Split(EnemyTrait.None).Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/UiLanguage/IconConcept.cs Assets/Scripts/UiLanguage/IconMarkup.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitCopy.cs Assets/Tests/EditMode/EnemyTraitCopyTests.cs`

Expected: compile FAILS with "does not contain a definition for `AllTraits`".

- [ ] **Step 3: Add badge helpers to IconMarkup**

Append inside the `IconMarkup` class in `Assets/Scripts/UiLanguage/IconMarkup.cs`:

```csharp
    // --- Enemy trait badges (spec 2026-07-29 §8.1) ---
    // TraitBadge returns a LETTER today and a <sprite=…> tag once trait art
    // exists. Call sites never change: that swap is this one method's body.
    // This is why badges route through IconMarkup like every other glyph.

    public static readonly EnemyTrait[] AllTraits =
    {
        EnemyTrait.Armored, EnemyTrait.Elusive, EnemyTrait.Hulking, EnemyTrait.Swift,
        EnemyTrait.Brutal,  EnemyTrait.Toxic,   EnemyTrait.Leech,   EnemyTrait.Harrying,
        EnemyTrait.Vengeful,
        EnemyTrait.Warlord, EnemyTrait.Miasma,  EnemyTrait.Ironclad, EnemyTrait.Outrider,
    };

    // First letter throughout, except Hulking = K (hulK) which yields to Harrying.
    public static string TraitBadge(EnemyTrait t)
    {
        switch (t)
        {
            case EnemyTrait.Armored:  return "A";
            case EnemyTrait.Brutal:   return "B";
            case EnemyTrait.Elusive:  return "E";
            case EnemyTrait.Harrying: return "H";
            case EnemyTrait.Hulking:  return "K";
            case EnemyTrait.Ironclad: return "I";
            case EnemyTrait.Leech:    return "L";
            case EnemyTrait.Miasma:   return "M";
            case EnemyTrait.Outrider: return "O";
            case EnemyTrait.Swift:    return "S";
            case EnemyTrait.Toxic:    return "T";
            case EnemyTrait.Vengeful: return "V";
            case EnemyTrait.Warlord:  return "W";
            default: return "";
        }
    }

    public static string TraitName(EnemyTrait t)
    {
        switch (t)
        {
            case EnemyTrait.Armored:  return "Armored";
            case EnemyTrait.Brutal:   return "Brutal";
            case EnemyTrait.Elusive:  return "Elusive";
            case EnemyTrait.Harrying: return "Harrying";
            case EnemyTrait.Hulking:  return "Hulking";
            case EnemyTrait.Ironclad: return "Ironclad";
            case EnemyTrait.Leech:    return "Leech";
            case EnemyTrait.Miasma:   return "Miasma";
            case EnemyTrait.Outrider: return "Outrider";
            case EnemyTrait.Swift:    return "Swift";
            case EnemyTrait.Toxic:    return "Toxic";
            case EnemyTrait.Vengeful: return "Vengeful";
            case EnemyTrait.Warlord:  return "Warlord";
            default: return "";
        }
    }

    // Auras render tinted so "which of these is buffing the others" — the read
    // the Siege targeting puzzle depends on — needs no hover.
    public static bool IsAuraTrait(EnemyTrait t)
        => t == EnemyTrait.Warlord || t == EnemyTrait.Miasma
        || t == EnemyTrait.Ironclad || t == EnemyTrait.Outrider;

    public const string AuraTint = "#F5D90A";

    // A badge ready to render: tinted for auras, plain for self traits.
    public static string TraitBadgeTinted(EnemyTrait t)
        => IsAuraTrait(t) ? "<color=" + AuraTint + ">" + TraitBadge(t) + "</color>" : TraitBadge(t);
```

- [ ] **Step 4: Create the generated rule text**

Create `Assets/Scripts/CardPlay/EnemyTraitCopy.cs`:

```csharp
using System.Collections.Generic;

// Trait rule lines, GENERATED from the tuning values (spec §8.2). Authored
// strings would go stale the first time a number moved, and EnemyTraitTuning
// exists precisely so numbers can move freely in playtest.
//
// Lives in CardPlay, not UiLanguage, because UiLanguage cannot reference
// CardPlay and this needs EnemyTraitTuning.
public static class EnemyTraitCopy
{
    public static List<EnemyTrait> Split(EnemyTrait mask)
    {
        var list = new List<EnemyTrait>();
        foreach (var t in IconMarkup.AllTraits)
            if (mask.HasFlag(t)) list.Add(t);
        return list;
    }

    public static string Rule(EnemyTrait t, EnemyTraitTuning tuning)
    {
        string hp     = IconMarkup.Tag(IconConcept.Hp);
        string siege  = IconMarkup.Tag(IconConcept.Siege);
        string attack = IconMarkup.Tag(IconConcept.Attack);
        string defend = IconMarkup.Tag(IconConcept.Defend);
        string wound  = IconMarkup.Tag(IconConcept.Wound);
        string cryst  = IconMarkup.Tag(IconConcept.Crystal);

        switch (t)
        {
            case EnemyTrait.Armored:
                return siege + " must cover " + tuning.armorSiegeMult + "x its " + hp;
            case EnemyTrait.Hulking:
                return attack + " must cover " + tuning.hulkAttackMult + "x its " + hp;
            case EnemyTrait.Elusive:
                return siege + " cannot remove it";
            case EnemyTrait.Swift:
                return "Needs " + tuning.swiftThreatMult + "x " + defend + " to block";
            case EnemyTrait.Brutal:
                return "Unblocked, it strikes for " + (1 + tuning.brutalSurchargeMult) + "x";
            case EnemyTrait.Toxic:
                return "Its " + wound + " are doubled into your discard";
            case EnemyTrait.Leech:
                return "Steals " + tuning.leechCrystals + " " + cryst + " per " + wound;
            case EnemyTrait.Vengeful:
                return "Killing it with " + attack + " costs " + tuning.vengefulWounds + " " + wound;
            case EnemyTrait.Harrying:
                return "Fleeing costs " + tuning.harryHandPenalty + " hand size next turn";
            case EnemyTrait.Warlord:
                return "Every other enemy gains +" + tuning.warlordBonus + " " + attack;
            case EnemyTrait.Miasma:
                return "Every enemy becomes Toxic";
            case EnemyTrait.Ironclad:
                return "Every enemy becomes Armored";
            case EnemyTrait.Outrider:
                return "Every enemy becomes Swift";
            default: return "";
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/UiLanguage/IconConcept.cs Assets/Scripts/UiLanguage/IconMarkup.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitCopy.cs Assets/Tests/EditMode/EnemyTraitCopyTests.cs`

Expected: `--- 10 passed, 0 failed ---`

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UiLanguage/IconMarkup.cs Assets/Scripts/CardPlay/EnemyTraitCopy.cs Assets/Tests/EditMode/EnemyTraitCopyTests.cs
git commit -m "feat: trait badges with icon upgrade seam and generated rule text"
```

---

## Task 7: Phase gating and the Advance rewire

**Files:**
- Modify: `Assets/Scripts/CardPlay/CombatPhaseRules.cs`
- Modify: `Assets/Tests/EditMode/CombatAdvanceStateTests.cs`
- Test: `Assets/Tests/EditMode/CombatPhaseRulesTests.cs` (append)

**Interfaces:**
- Consumes: `CounterattackPreview`, `EnemyTraitRules.HandWounds` (Task 3).
- Produces: `CombatPhaseRules.CanBlock(CombatPhase) → bool`; `CombatPhaseRules.Advance(CombatPhase, int playerSiege, bool anySiegeKillable, int defendLeft, CounterattackPreview preview, int toughness) → AdvanceState`.
- **Breaking change:** `Advance`'s `int enemyAttackTotal` parameter becomes `CounterattackPreview preview`, and `playerDefend` is renamed `defendLeft`. `CombatAdvanceStateTests` must be updated in this task. **`CombatRulesTests` must NOT change.**

- [ ] **Step 1: Update the existing Advance tests to the new signature**

Replace the whole body of `Assets/Tests/EditMode/CombatAdvanceStateTests.cs`. Every expectation is **unchanged** — only the call shape moves, which is itself proof of the compatibility guarantee:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class CombatAdvanceStateTests
{
    // A trait-free, unblocked roster of one enemy with the given Attack.
    // Threat == Basis == Attack, so this reproduces the old enemyAttackTotal.
    static CounterattackPreview P(int enemyAttackTotal)
    {
        var roster = new List<EnemyCombatant>
        {
            new EnemyCombatant { Attack = enemyAttackTotal, HP = enemyAttackTotal,
                                 Traits = EnemyTrait.None, Blocked = false }
        };
        return EnemyTraitRules.BuildPreview(roster, new EnemyTraitTuning());
    }

    [Test]
    public void Siege_NothingStaged_ShowsEngage()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Siege, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 0, preview: P(5), toughness: 1);
        Assert.AreEqual(AdvanceKind.Engage, s.Kind);
    }

    [Test]
    public void Siege_StagedAndKillable_Hides()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Siege, playerSiege: 3, anySiegeKillable: true,
            defendLeft: 0, preview: P(5), toughness: 1);
        Assert.AreEqual(AdvanceKind.Hidden, s.Kind);
    }

    [Test]
    public void Siege_StagedButNothingKillable_KeepsEngage()
    {
        // Leftover siege that can't kill anything must not trap the player.
        var s = CombatPhaseRules.Advance(CombatPhase.Siege, playerSiege: 2, anySiegeKillable: false,
            defendLeft: 0, preview: P(5), toughness: 1);
        Assert.AreEqual(AdvanceKind.Engage, s.Kind);
    }

    [Test]
    public void Defend_ShortfallShowsTakeHit_WithWoundCount()
    {
        // defend 2 vs 6 attack, toughness 2 -> shortfall 4 -> 2 wounds.
        var s = CombatPhaseRules.Advance(CombatPhase.Defend, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 2, preview: P(6), toughness: 2);
        Assert.AreEqual(AdvanceKind.TakeHit, s.Kind);
        Assert.AreEqual(2, s.Wounds);
    }

    [Test]
    public void Defend_MeetsOrBeatsAttack_FlipsToCounterattack()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Defend, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 6, preview: P(6), toughness: 2);
        Assert.AreEqual(AdvanceKind.Counterattack, s.Kind);
        Assert.AreEqual(0, s.Wounds);
    }

    [Test]
    public void Attack_Hides()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Attack, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 0, preview: P(6), toughness: 1);
        Assert.AreEqual(AdvanceKind.Hidden, s.Kind);
    }

    [Test]
    public void Resolved_Hides()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Resolved, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 0, preview: P(0), toughness: 1);
        Assert.AreEqual(AdvanceKind.Hidden, s.Kind);
    }

    [Test]
    public void Defend_BlockingEveryone_FlipsToCounterattack()
    {
        var roster = new List<EnemyCombatant>
        {
            new EnemyCombatant { Attack = 6, HP = 6, Traits = EnemyTrait.None, Blocked = true }
        };
        var preview = EnemyTraitRules.BuildPreview(roster, new EnemyTraitTuning());
        var s = CombatPhaseRules.Advance(CombatPhase.Defend, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 0, preview: preview, toughness: 2);
        Assert.AreEqual(AdvanceKind.Counterattack, s.Kind);
    }
}
```

- [ ] **Step 2: Add the CanBlock test**

Append to `Assets/Tests/EditMode/CombatPhaseRulesTests.cs` (inside the existing class):

```csharp
    [Test]
    public void CanBlock_OnlyInDefendPhase()
    {
        Assert.IsFalse(CombatPhaseRules.CanBlock(CombatPhase.Siege));
        Assert.IsTrue(CombatPhaseRules.CanBlock(CombatPhase.Defend));
        Assert.IsFalse(CombatPhaseRules.CanBlock(CombatPhase.Attack));
        Assert.IsFalse(CombatPhaseRules.CanBlock(CombatPhase.Resolved));
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/CombatPhase.cs Assets/Scripts/CardPlay/CombatRules.cs Assets/Scripts/CardPlay/CombatPhaseRules.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Tests/EditMode/CombatAdvanceStateTests.cs Assets/Tests/EditMode/CombatPhaseRulesTests.cs`

Expected: compile FAILS with "no overload for `Advance` takes 6 arguments" / "does not contain a definition for `CanBlock`".

- [ ] **Step 4: Rewire CombatPhaseRules**

In `Assets/Scripts/CardPlay/CombatPhaseRules.cs`, add `CanBlock` after `CanNormalAttack` and replace the `Advance` method:

```csharp
    // Blocking is the Defend phase's per-enemy action (spec §7.5), the same
    // shape as CanSiege/CanInfluence/CanNormalAttack.
    public static bool CanBlock(CombatPhase phase) => phase == CombatPhase.Defend;
```

Replace the whole `Advance` method with:

```csharp
    // The advance button's display state, as pure data so the MonoBehaviour only
    // renders (spec 2026-07-24 phase controls). Siege hides the button while the
    // player has staged siege that can actually kill something (they should spend
    // it on a target); Defend previews the incoming wounds and flips to a
    // strike-back prompt once the unblocked enemies are covered; Attack/Resolved
    // hide it (the win comes from clearing enemies, and Withdraw is its own
    // control).
    //
    // Takes a CounterattackPreview rather than a raw attack total (spec §7.5):
    // the parameter list was already six long, and blocking adds three more
    // numbers that always travel together.
    public static AdvanceState Advance(CombatPhase phase, int playerSiege, bool anySiegeKillable,
        int defendLeft, CounterattackPreview preview, int toughness)
    {
        if (phase == CombatPhase.Siege)
        {
            bool hide = playerSiege > 0 && anySiegeKillable;
            return new AdvanceState(hide ? AdvanceKind.Hidden : AdvanceKind.Engage, 0);
        }
        if (phase == CombatPhase.Defend)
        {
            int wounds = EnemyTraitRules.HandWounds(preview, defendLeft, toughness);
            return wounds == 0
                ? new AdvanceState(AdvanceKind.Counterattack, 0)
                : new AdvanceState(AdvanceKind.TakeHit, wounds);
        }
        return new AdvanceState(AdvanceKind.Hidden, 0);
    }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/CombatPhase.cs Assets/Scripts/CardPlay/CombatRules.cs Assets/Scripts/CardPlay/CombatPhaseRules.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Tests/EditMode/CombatAdvanceStateTests.cs Assets/Tests/EditMode/CombatPhaseRulesTests.cs`

Expected: all pass, including the 7 pre-existing Advance expectations with unchanged values.

- [ ] **Step 6: Confirm CombatRulesTests is untouched**

Run: `git diff --stat Assets/Tests/EditMode/CombatRulesTests.cs Assets/Scripts/CardPlay/CombatRules.cs`

Expected: **no output**.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/CardPlay/CombatPhaseRules.cs Assets/Tests/EditMode/CombatAdvanceStateTests.cs Assets/Tests/EditMode/CombatPhaseRulesTests.cs
git commit -m "feat: CanBlock gating and Advance driven by CounterattackPreview"
```

---

## Task 8: Tuning asset and the EnemiesSO field

**Files:**
- Create: `Assets/Scripts/GameScriptableObjectTypes/EnemyTraitTuningSO.cs`
- Modify: `Assets/Scripts/GameScriptableObjectTypes/EnemiesSO.cs`

**Interfaces:**
- Consumes: `EnemyTraitTuning` (Task 1), `EnemyTrait` (Task 1).
- Produces: `EnemyTraitTuningSO.tuning` (an `EnemyTraitTuning`); `EnemiesSO.traits` (an `EnemyTrait`).
- **No automated tests.** These are Unity serialization types; correctness is a compile check plus the manual asset step.

- [ ] **Step 1: Create the tuning ScriptableObject**

Create `Assets/Scripts/GameScriptableObjectTypes/EnemyTraitTuningSO.cs`:

```csharp
using UnityEngine;

// One shared asset holding every trait magnitude (spec §3.2), wired onto
// CombatController. Same split as RewardTuningSO: the pure EnemyTraitTuning
// holds the numbers, this only makes them an inspector-editable asset.
//
// Enemies tick trait boxes; this owns the magnitudes. That is deliberate —
// "Armored" then means one fixed thing game-wide, so the keyword is learnable
// after a single fight and retuning all armor is one field.
[CreateAssetMenu(fileName = "EnemyTraitTuning", menuName = "ScriptableObjects/EnemyTraitTuning")]
public class EnemyTraitTuningSO : ScriptableObject
{
    public EnemyTraitTuning tuning = new EnemyTraitTuning();
}
```

- [ ] **Step 2: Add the traits field to EnemiesSO**

In `Assets/Scripts/GameScriptableObjectTypes/EnemiesSO.cs`, add after the `tier` field:

```csharp
    // Authored traits (spec 2026-07-29). NOT saved — read from this SO via the
    // stable AllCards.id exactly like enemyHP, so no save schema bump.
    // Authoring rules (spec §5.3): field enemies use self traits only; granting
    // auras are reserved for guardian rosters; an Elusive enemy must also have
    // canInfluence = true, or it removes a choice instead of redirecting one.
    public EnemyTrait traits = EnemyTrait.None;
```

- [ ] **Step 3: Verify it compiles**

Have the user open Unity and confirm the console shows no compile errors, or run:

`tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Tests/EditMode/EnemyTraitAuraTests.cs`

Expected: still `--- 6 passed, 0 failed ---` (proves the enum did not regress; the SO itself needs Unity to compile).

- [ ] **Step 4: MANUAL — create the tuning asset**

**This step is done by the user in the Unity editor. Do not edit asset YAML by hand.**

Give the user these instructions:

1. In the Project window, navigate to `Assets/Scripts/ScriptableObjectData/` (or wherever `RewardTuning.asset` lives — match its folder).
2. Right-click → **Create → ScriptableObjects → EnemyTraitTuning**.
3. Name it exactly `EnemyTraitTuning`.
4. Leave every field at its default (2, 2, 2, 1, 1, 1, 1, 1, 1) — these are the spec's starting values.
5. Do **not** wire it to anything yet; Task 11 adds the `CombatController` field it plugs into.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/GameScriptableObjectTypes/EnemyTraitTuningSO.cs Assets/Scripts/GameScriptableObjectTypes/EnemiesSO.cs
git commit -m "feat: EnemyTraitTuningSO and the EnemiesSO traits field"
```

Then, after the user creates the asset:

```bash
git add Assets/Scripts/ScriptableObjectData/EnemyTraitTuning.asset Assets/Scripts/ScriptableObjectData/EnemyTraitTuning.asset.meta
git commit -m "chore: add the EnemyTraitTuning asset with starting values"
```

---

## Task 9: Wound destination routing in PlayerHand

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs`

**Interfaces:**
- Consumes: `WoundDestination` (Task 5).
- Produces: `PlayerHand.AddWound(WoundDestination dest = WoundDestination.Hand)`. The existing no-argument `AddWound()` call sites keep working via the default.
- **No automated test.** `PlayerHand` is a MonoBehaviour that instantiates prefabs; verified in-editor (Task 16).

- [ ] **Step 1: Route AddWound through a destination**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs`, replace the `AddWound` method (currently at line 105) with:

```csharp
    // Wounds are this game's health system, so placement is explicit (spec §6).
    // Hand is the historical behaviour and stays the default, which keeps every
    // existing call site correct. Discard is what Toxic needs.
    //
    // Contract (spec §6.3): every wound has exactly one destination; every add
    // re-runs the wound-out check at the moment of add (correct because wound
    // adds are not undoable, so a crossed threshold can never be un-crossed).
    public void AddWound(WoundDestination dest = WoundDestination.Hand)
    {
        // Parent to the fan container (like drawn/rebuilt cards) — HandFanLayout
        // only lays out cards whose parent is the container, so a wound parented
        // elsewhere stays stacked at the origin until something reparents it.
        playerCard = Instantiate(card, handLayout.Container);
        var woundCard = playerCard.GetComponent<Card>();
        woundCard.cardSO = wound;
        playerCard.name = woundCard.name;

        if (dest == WoundDestination.Discard)
        {
            woundCard.InHand = false;
            woundCard.InDeck = false;
            var discardPile = FindAnyObjectByType<DiscardPile>();
            if (discardPile != null) discardPile.AddCardToDiscard(woundCard);
            else                     AddWoundToHandZone(woundCard);  // contract rule 7: never drop a wound
        }
        else
        {
            AddWoundToHandZone(woundCard);
        }

        Relayout();
        if (onWoundTutorial != null) onWoundTutorial.Raise();
        // Wound adds are not undoable commands, so a threshold crossed here
        // can never be un-done back under it — check at the moment of add.
        if (RunEndRules.IsWoundOut(TotalWoundCount()))
            RunEndController.RequestEnd(RunOutcome.WoundOutLoss);
    }

    void AddWoundToHandZone(Card woundCard)
    {
        woundCard.InHand = true;
        woundCard.InDeck = false;
        cardsInPlay.Add(woundCard);
    }
```

- [ ] **Step 2: Verify the wound-out count already covers discard**

Read `TotalWoundCount()` in the same file and confirm it enumerates `cardsInPlay`, `deck.CardsInDeck`, **and** `FindAnyObjectByType<DiscardPile>().Cards`.

Expected: it already does — no change needed. Discard wounds count toward the wound-out loss, which is what makes Toxic dangerous rather than merely annoying.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs
git commit -m "feat: route AddWound through a WoundDestination"
```

---

## Task 10: Blocked state on EnemyCard and the undoable BlockCommand

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs`
- Create: `Assets/Scripts/Managers/Commands/BlockCommand.cs`

**Interfaces:**
- Consumes: `EnemyTrait` (Task 1), `ICommands` (existing).
- Produces: `EnemyCard.Blocked` (bool property with a setter); `EnemyCard.Traits` (reads `enemySO.traits`); `EnemyCard.ToCombatant() → EnemyCombatant`; `BlockCommand(EnemyCard card, Player player, int cost)`.

- [ ] **Step 1: Add blocked state and the combatant projection to EnemyCard**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs`, add after the `EffectiveAttack` property (line 17):

```csharp
    // Blocked for this Defend phase. A blocked enemy is ALIVE — its auras still
    // apply (spec §7.4), which is what keeps Siege strictly better than blocking.
    public bool Blocked { get; set; }

    public EnemyTrait Traits => enemySO != null ? enemySO.traits : EnemyTrait.None;

    // The pure projection every rule consumes, so EnemyTraitRules never sees a
    // MonoBehaviour and stays CLI-testable.
    public EnemyCombatant ToCombatant() => new EnemyCombatant
    {
        Attack = EffectiveAttack,
        HP = EffectiveHP,
        Traits = Traits,
        Blocked = Blocked,
    };
```

- [ ] **Step 2: Create the BlockCommand**

Create `Assets/Scripts/Managers/Commands/BlockCommand.cs`:

```csharp
// Blocking one enemy in the Defend phase (spec §7.6). Undoable like a card play
// or a unit use: undo refunds the Defend and returns the enemy to the unblocked
// set, so the advance button's wound preview reverts with it.
//
// Blocks stop being undoable when the advance button commits the counterattack
// and clears the stack — the same commit rule Engage already uses.
public class BlockCommand : ICommands
{
    readonly EnemyCard _card;
    readonly Player _player;
    readonly int _cost;

    public BlockCommand(EnemyCard card, Player player, int cost)
    {
        _card = card;
        _player = player;
        _cost = cost;
    }

    public void Execute()
    {
        _player.PlayerDefend -= _cost;
        _card.Blocked = true;
    }

    public void Undo()
    {
        _player.PlayerDefend += _cost;
        _card.Blocked = false;
    }
}
```

- [ ] **Step 3: Verify it compiles**

Have the user check the Unity console for compile errors. `Player.PlayerDefend` must be a settable property — confirm with:

Run: `grep -n "PlayerDefend" "Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs" | head -5`

Expected: a property with a setter. If it is read-only, add a setter in the same style as the other stat pools.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs Assets/Scripts/Managers/Commands/BlockCommand.cs
git commit -m "feat: blocked state on EnemyCard and the undoable BlockCommand"
```

---

## Task 11: CombatController pipeline wiring

**Files:**
- Modify: `Assets/Scripts/Managers/CombatController.cs`

**Interfaces:**
- Consumes: `EnemyTraitRules.BuildPreview/HandWounds/DiscardWounds/CrystalsStolen` (Tasks 3–4), `WoundPlacementRules.Place` (Task 5), `PlayerHand.AddWound(WoundDestination)` (Task 9), `EnemyCard.ToCombatant()` (Task 10), `EnemyTraitTuningSO` (Task 8).
- Produces: `CombatController.Preview()` → the current `CounterattackPreview`; `CombatController.ClearBlocks()`.

- [ ] **Step 1: Add the tuning field and the roster projection**

In `Assets/Scripts/Managers/CombatController.cs`, add a serialized field near the other inspector fields:

```csharp
    [SerializeField] EnemyTraitTuningSO traitTuning;   // wired in Task 11 step 5
```

And add these helpers to the class:

```csharp
    // The pure roster every rule consumes. Rebuilt on demand so it always
    // reflects current kills and blocks.
    // Public: the preview panel (Task 14) needs both to show roster-aware values.
    public System.Collections.Generic.List<EnemyCombatant> Roster()
    {
        var list = new System.Collections.Generic.List<EnemyCombatant>();
        foreach (var card in LiveEnemyCards()) list.Add(card.ToCombatant());
        return list;
    }

    public EnemyTraitTuning Tuning =>
        traitTuning != null ? traitTuning.tuning : new EnemyTraitTuning();

    public CounterattackPreview Preview() => EnemyTraitRules.BuildPreview(Roster(), Tuning);

    // Blocks last one Defend phase only.
    public void ClearBlocks()
    {
        foreach (var card in LiveEnemyCards()) card.Blocked = false;
    }
```

**Note for the implementer:** `LiveEnemyCards()` is this plan's name for however `CombatController` already enumerates its logical live enemy set. Find the existing accessor (search the file for the list the phase machine iterates) and use that instead of introducing a duplicate. Do **not** add a second source of truth for who is alive.

- [ ] **Step 2: Replace the counterattack resolution**

Find the counterattack block (around line 343, currently calling `CombatRules.GroupWoundCount` then looping `hand.AddWound()`), and replace the wound resolution with:

```csharp
        var preview = Preview();
        int defendLeft = player.PlayerDefend;
        int toughness = player.PlayerToughness;

        int handWounds    = EnemyTraitRules.HandWounds(preview, defendLeft, toughness);
        int discardWounds = EnemyTraitRules.DiscardWounds(preview, defendLeft, toughness, Tuning);
        int stolen        = EnemyTraitRules.CrystalsStolen(preview, defendLeft, toughness, Tuning);

        // The placement list is the seam (spec §6.2): today a pure rule produces
        // it, next phase an interactive picker does. This consumer depends only
        // on IReadOnlyList<WoundDestination> and never learns what a unit is.
        var placements = WoundPlacementRules.Place(handWounds, discardWounds);
        foreach (var dest in placements) hand.AddWound(dest);

        int wounds = placements.Count;
```

Leave the existing avatar-reaction and `GameLog` lines below it in place, then extend the log per Task 14.

Immediately after the counterattack resolves and before the phase advances to Attack, add:

```csharp
        if (stolen > 0) player.SpendCrystals(stolen);   // clamped to crystals held
        ClearBlocks();
```

**Note for the implementer:** `SpendCrystals(int)` is this plan's name for the existing crystal-removal path. Find how crystal spending is already done (search for where empower consumes a crystal) and reuse it, clamping to the number held. Do not add a second crystal-mutation path.

- [ ] **Step 3: Feed Advance the preview**

Find every `CombatPhaseRules.Advance(...)` call in this file and update it to the Task 7 signature:

```csharp
        var state = CombatPhaseRules.Advance(Phase, player.PlayerSiege, AnySiegeKillable(),
            player.PlayerDefend, Preview(), player.PlayerToughness);
```

- [ ] **Step 4: Clear blocks on Engage too**

In the Engage handler (around line 323–329, where `player.PlayerSiege = 0` and `commands.ClearStack()` already run), no change is needed — blocks are placed *after* Engage. Confirm by reading the method that blocks can only be set during `CombatPhase.Defend`.

- [ ] **Step 5: MANUAL — wire the tuning asset**

**Done by the user in the Unity editor.**

1. Open the `GameBoard` scene.
2. Select the GameObject holding the `CombatController` component.
3. Drag `EnemyTraitTuning.asset` (created in Task 8) onto the new **Trait Tuning** field.
4. Save the scene.

- [ ] **Step 6: Verify no pure test regressed**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/Enums/Enums/WoundDestination.cs Assets/Scripts/CardPlay/CombatPhase.cs Assets/Scripts/CardPlay/CombatRules.cs Assets/Scripts/CardPlay/CombatPhaseRules.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Scripts/CardPlay/WoundPlacementRules.cs Assets/Tests/EditMode/EnemyTraitAuraTests.cs Assets/Tests/EditMode/EnemyTraitValueTests.cs Assets/Tests/EditMode/EnemyTraitCounterattackTests.cs Assets/Tests/EditMode/EnemyTraitShareTests.cs Assets/Tests/EditMode/WoundPlacementRulesTests.cs Assets/Tests/EditMode/CombatAdvanceStateTests.cs`

Expected: all pass, 0 failed.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Managers/CombatController.cs
git commit -m "feat: wire the trait pipeline and wound placement into CombatController"
```

---

## Task 12: Vengeful and Harrying — the traits that fire outside the counterattack

Every other trait resolves inside `BuildPreview`. These two do not: **Vengeful** fires on an
Attack-phase kill, **Harrying** fires on a flee. Both need their own hook.

**Files:**
- Modify: `Assets/Scripts/CardPlay/EnemyTraitRules.cs`
- Modify: `Assets/Scripts/Managers/CombatController.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`
- Test: `Assets/Tests/EditMode/EnemyTraitHookTests.cs`

**Interfaces:**
- Consumes: `EnemyTrait`, `EnemyCombatant`, `EnemyTraitTuning` (Task 1), `EnemyTraitRules.EffectiveTraits` (Task 1).
- Produces: `EnemyTraitRules.VengefulWounds(EnemyCombatant, roster, tuning) → int`; `EnemyTraitRules.HarryPenalty(roster, tuning) → int`; `Player.PendingHandPenalty` (int, consumed at the next hand top-up).

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/EnemyTraitHookTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitHookTests
{
    static EnemyTraitTuning T() => new EnemyTraitTuning();
    static EnemyCombatant E(int atk, EnemyTrait traits = EnemyTrait.None) =>
        new EnemyCombatant { Attack = atk, HP = atk, Traits = traits, Blocked = false };

    [Test]
    public void Vengeful_CostsAWoundOnAttackKill()
    {
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Vengeful) };
        Assert.AreEqual(1, EnemyTraitRules.VengefulWounds(r[0], r, T()));
    }

    [Test]
    public void NonVengeful_CostsNothing()
    {
        var r = new List<EnemyCombatant> { E(3) };
        Assert.AreEqual(0, EnemyTraitRules.VengefulWounds(r[0], r, T()));
    }

    [Test]
    public void Harrying_OneSurvivor_OnePenalty()
    {
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Harrying) };
        Assert.AreEqual(1, EnemyTraitRules.HarryPenalty(r, T()));
    }

    [Test]
    public void Harrying_DoesNotStackPerEnemy()
    {
        // Two Harrying enemies must not cost two cards - the penalty is a
        // property of the fight you fled, not a per-enemy tax.
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Harrying), E(3, EnemyTrait.Harrying) };
        Assert.AreEqual(1, EnemyTraitRules.HarryPenalty(r, T()));
    }

    [Test]
    public void NoHarrying_NoPenalty()
    {
        var r = new List<EnemyCombatant> { E(3), E(4, EnemyTrait.Brutal) };
        Assert.AreEqual(0, EnemyTraitRules.HarryPenalty(r, T()));
    }

    [Test]
    public void BlockedHarryingEnemy_StillHarries()
    {
        // Blocking is not killing - you still fled a harrying enemy.
        var e = E(3, EnemyTrait.Harrying); e.Blocked = true;
        var r = new List<EnemyCombatant> { e };
        Assert.AreEqual(1, EnemyTraitRules.HarryPenalty(r, T()));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Scripts/CardPlay/CombatRules.cs Assets/Tests/EditMode/EnemyTraitHookTests.cs`

Expected: compile FAILS with "does not contain a definition for `VengefulWounds`".

- [ ] **Step 3: Implement the two hooks**

Append inside the `EnemyTraitRules` class:

```csharp
    // Vengeful punishes the ATTACK phase only, so the Siege wound-free promise
    // in mechanics.md is preserved and rewarded rather than broken. This is the
    // trait that makes Siege matter in a solo fight.
    public static int VengefulWounds(EnemyCombatant e, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
        => EffectiveTraits(e, roster).HasFlag(EnemyTrait.Vengeful) ? t.vengefulWounds : 0;

    // Harrying costs hand size on the turn AFTER a flee. Flat, not per-enemy:
    // it is a property of the fight you fled, so two harriers do not cost two
    // cards. Blocked enemies still count — blocking is not killing.
    public static int HarryPenalty(IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        for (int i = 0; i < roster.Count; i++)
            if (EffectiveTraits(roster[i], roster).HasFlag(EnemyTrait.Harrying))
                return t.harryHandPenalty;
        return 0;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Scripts/CardPlay/CombatRules.cs Assets/Tests/EditMode/EnemyTraitHookTests.cs`

Expected: `--- 6 passed, 0 failed ---`

- [ ] **Step 5: Add the pending hand penalty to Player**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`, add near the other stat properties:

```csharp
    // Harrying: a one-turn hand-size cut applied at the NEXT top-up, then
    // cleared. Hand size is derived from level + table and never stored, so this
    // is transient run state rather than a schema change (spec 5.1).
    public int PendingHandPenalty { get; set; }
```

**Note for the implementer:** find where the hand tops up to `PlayerHandSize` (search `PlayerHandSize` usages in `PlayerHand`). Subtract `PendingHandPenalty` from the target count for that one top-up, clamp the target to a minimum of 1, then set `PendingHandPenalty = 0`. Do not change `PlayerHandSize` itself — it is derived and must stay derived.

- [ ] **Step 6: Hook Vengeful into the Attack-phase kill**

In `Assets/Scripts/Managers/CombatController.cs`, find where a **Normal** (Attack-phase) defeat is banked. After the kill is recorded, add:

```csharp
        // Attack-phase kills are otherwise wound-free at point of use; Vengeful
        // is the one exception. Siege and Influence kills stay clean.
        int vengeful = EnemyTraitRules.VengefulWounds(card.ToCombatant(), Roster(), Tuning);
        for (int i = 0; i < vengeful; i++) hand.AddWound(WoundDestination.Hand);
        if (vengeful > 0)
            GameLog.Instance.Post($"It strikes as it falls — you take {vengeful} wound(s).");
```

- [ ] **Step 7: Hook Harrying into the flee**

In `Assets/Scripts/Managers/CombatController.cs`, find the withdraw/flee path (around line 441, where `cost` wounds are added). **Capture the roster before the enemies are cleared**, then after the flee wounds are added:

```csharp
        int harry = EnemyTraitRules.HarryPenalty(rosterBeforeFlee, Tuning);
        if (harry > 0)
        {
            player.PendingHandPenalty += harry;
            GameLog.Instance.Post($"They harry your retreat — you draw {harry} fewer card(s) next turn.");
        }
```

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/CardPlay/EnemyTraitRules.cs Assets/Scripts/Managers/CombatController.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs Assets/Tests/EditMode/EnemyTraitHookTests.cs
git commit -m "feat: Vengeful attack-kill wound and Harrying hand-size penalty"
```

---

## Task 13: The Defend button and badge row

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs`
- Modify: prefab `Assets/Prefabs/` — the enemy card prefab (**MANUAL, by the user**)

**Interfaces:**
- Consumes: `CombatPhaseRules.CanBlock` (Task 7), `IconMarkup.TraitBadgeTinted` / `EnemyTraitCopy.Split` (Task 6), `BlockCommand` (Task 10), `EnemyTraitRules.Threat` (Task 2).
- Produces: `EnemyCard.defendButton` (serialized), `EnemyCard.traitBadges` (serialized `TextMeshProUGUI`), `EnemyCard.RefreshTraitBadges()`, `EnemyCard.RefreshPhaseButtons(CombatPhase, int defendLeft, int blockCost)`.

- [ ] **Step 1: Add the serialized fields**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs`, add beside the existing button fields:

```csharp
    [SerializeField] public Button defendButton;          // Defend phase: block this enemy
    [SerializeField] TextMeshProUGUI traitBadges;         // compact badge row, no words
    [SerializeField] TextMeshProUGUI defendButtonText;    // shows the block cost
```

- [ ] **Step 2: Render the badge row**

Add to `EnemyCard`:

```csharp
    // Badges only — no words. The preview panel is the legend (spec §8.3), so a
    // bare letter is never a dead end.
    public void RefreshTraitBadges()
    {
        if (traitBadges == null) return;
        var parts = new System.Text.StringBuilder();
        foreach (var t in EnemyTraitCopy.Split(Traits))
        {
            if (parts.Length > 0) parts.Append(' ');
            parts.Append(IconMarkup.TraitBadgeTinted(t));
        }
        traitBadges.text = parts.ToString();
        traitBadges.gameObject.SetActive(parts.Length > 0);
    }
```

Call it at the end of `Start()`, after the existing `enemyHP.text` assignment.

- [ ] **Step 3: Drive the per-phase button state**

Add to `EnemyCard`:

```csharp
    // All four per-enemy buttons route through CombatPhaseRules so no button
    // manages its own phase state (spec §7.5).
    public void RefreshPhaseButtons(CombatPhase phase, int defendLeft, int blockCost)
    {
        if (siegeButton != null)     siegeButton.gameObject.SetActive(CombatPhaseRules.CanSiege(phase));
        if (influenceButton != null) influenceButton.gameObject.SetActive(
            CombatPhaseRules.CanInfluence(phase) && enemySO.canInfluence);
        if (fightButton != null)     fightButton.gameObject.SetActive(CombatPhaseRules.CanNormalAttack(phase));

        if (defendButton == null) return;
        bool live = CombatPhaseRules.CanBlock(phase) && !Blocked;
        defendButton.gameObject.SetActive(CombatPhaseRules.CanBlock(phase));

        bool affordable = live && defendLeft >= blockCost;
        defendButton.interactable = affordable;
        UiLock.Apply(defendButton.GetComponent<CanvasGroup>(), !affordable);

        if (defendButtonText != null)
            defendButtonText.text = Blocked
                ? IconMarkup.Tag(IconConcept.Defend) + " Blocked"
                : IconMarkup.Cost(IconConcept.Defend, blockCost);
    }
```

- [ ] **Step 4: Dispatch the block**

Add to `EnemyCard`:

```csharp
    // Wired to defendButton.onClick in the prefab.
    public void OnDefendClicked()
    {
        var controller = FindAnyObjectByType<CombatController>();
        var player = FindAnyObjectByType<Player>();
        if (controller == null || player == null) return;
        if (!CombatPhaseRules.CanBlock(controller.Phase) || Blocked) return;

        int cost = controller.BlockCostFor(this);
        if (player.PlayerDefend < cost) return;

        GameManager.Instance.commands.AddCommand(new BlockCommand(this, player, cost));
    }
```

**Note for the implementer:** `commands.AddCommand(...)` is this plan's name for however the existing stack executes-and-pushes a command. Match exactly what `PlayCommand` and `SkillCommand` call sites do — do not invent a new entry point.

- [ ] **Step 5: Expose the block cost from CombatController**

Add to `Assets/Scripts/Managers/CombatController.cs`:

```csharp
    // What this enemy costs to block: its trait-adjusted threat (spec §7.1).
    // This number IS the trait readout — the one place a player sees a trait's
    // cost as something they must actually pay.
    public int BlockCostFor(EnemyCard card)
    {
        var roster = Roster();
        var live = LiveEnemyCards();
        for (int i = 0; i < live.Count; i++)
            if (live[i] == card) return EnemyTraitRules.Threat(i, roster, Tuning);
        return card.EffectiveAttack;
    }
```

Then, wherever `CombatController` already refreshes enemy card visuals each frame or on state change, call:

```csharp
        var roster = Roster();
        var live = LiveEnemyCards();
        for (int i = 0; i < live.Count; i++)
            live[i].RefreshPhaseButtons(Phase, player.PlayerDefend,
                EnemyTraitRules.Threat(i, roster, Tuning));
```

- [ ] **Step 6: MANUAL — wire the prefab**

**Done by the user in the Unity editor. Never hand-edit prefab YAML.**

Give the user these instructions:

1. Open the enemy card prefab (the one with the existing Fight / Siege / Influence buttons — find it via the `EnemyCard` component).
2. Duplicate the existing **Siege** button; rename the copy to **DefendButton**. Duplicating keeps the styling and `CanvasGroup` consistent.
3. Position it in the button row beside the others.
4. On its child label (TMP), leave the text empty — code sets it.
5. Select the root object with the `EnemyCard` component and assign:
   - **Defend Button** → the new DefendButton
   - **Defend Button Text** → DefendButton's TMP child
6. On **DefendButton → Button → OnClick()**, add the root `EnemyCard` object and choose **EnemyCard → OnDefendClicked()**.
7. Add a new TMP text object named **TraitBadges** near the HP/Attack readouts. Small, one line, centered.
8. Assign it to the `EnemyCard` component's **Trait Badges** field.
9. Confirm **DefendButton** has a `CanvasGroup` component (`UiLock.Apply` needs one). Add one if the duplicate lacks it.
10. Save the prefab.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs Assets/Scripts/Managers/CombatController.cs
git commit -m "feat: per-enemy Defend button and trait badge row"
```

Then, after the user saves the prefab:

```bash
git add Assets/Prefabs
git commit -m "chore: wire the Defend button and trait badges into the enemy card prefab"
```

---

## Task 14: Preview panel and combat log

**Files:**
- Modify: the enemy preview panel script (find via `PreviewRules.CanPreview` usages)
- Modify: `Assets/Scripts/Managers/CombatController.cs`

**Interfaces:**
- Consumes: `EnemyTraitCopy.Split/Rule` (Task 6), `IconMarkup.TraitBadgeTinted/TraitName` (Task 6), `EnemyTraitRules` display values (Task 2).

- [ ] **Step 1: Find the preview panel**

Run: `grep -rn "CanPreview" Assets/Scripts --include=*.cs`

Note the MonoBehaviour that builds the preview text. That is the file to modify.

- [ ] **Step 2: Add trait lines to the preview**

In that panel's text-building method, append after the existing stat lines:

```csharp
        // The preview is the LEGEND for the card's bare badges (spec §8.3):
        // badge + name + generated rule. A badge is never shown anywhere the
        // player cannot reach a preview.
        foreach (var t in EnemyTraitCopy.Split(card.Traits))
            builder.AppendLine(IconMarkup.TraitBadgeTinted(t) + " " +
                               IconMarkup.TraitName(t) + " — " +
                               EnemyTraitCopy.Rule(t, tuning));
```

**Critical:** the stat numbers this panel shows must be the **roster-aware effective values** from `EnemyTraitRules` (`Threat`, `SiegeCost`, `AttackCost`), not raw SO fields. A Warlord changes the ogre's Attack and an Ironclad changes its Siege cost; showing raw values here is a lie the player only discovers after committing. Use the public `CombatController.Roster()` and `CombatController.Tuning` from Task 11.

- [ ] **Step 3: Log trait effects**

In `CombatController`, after the counterattack resolution from Task 11, extend the logging:

```csharp
        if (wounds > 0)
            GameLog.Instance.Post($"The enemies strike back! You are wounded {wounds} times.");
        if (discardWounds > 0)
            GameLog.Instance.Post($"Venom festers — {discardWounds} wound(s) rot in your discard.");
        if (stolen > 0)
            GameLog.Instance.Post($"Leeched! You lose {stolen} crystal(s).");
```

**Trait effects report through `GameLog`, never a modal** — required by the existing rule that no mid-fight event may pop a modal interrupting a Siege or Attack decision.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts Assets/Scripts/Managers/CombatController.cs
git commit -m "feat: trait lines in the enemy preview and trait effects in the combat log"
```

---

## Task 15: Documentation

**Files:**
- Modify: `.claude/skills/archons-rise-design/mechanics.md`
- Modify: `.claude/skills/archons-rise-design/content-rules.md`
- Modify: `.claude/skills/archons-rise-design/balance.md`
- Modify: `.claude/skills/archons-rise-roadmap/decisions-log.md`

- [ ] **Step 1: Update mechanics.md**

Three edits:

1. **Correct line 74.** It currently says losing a fight "shuffles **Wound** cards into the deck." That is stale — `AddWound` puts them in the **hand**. Replace with: wounds are added to the **hand**, and may be placed into the **discard** (Toxic). Both count toward wound-out.
2. **Rewrite the Defend-phase bullet** in the Combat section. It is no longer a single summed comparison: each enemy may be **blocked** for its trait-adjusted threat, and leftover Defend soaks the group counterattack from everyone still unblocked. Blocking does **not** suppress auras.
3. **Add a Traits subsection** under Combat covering self vs aura traits, the cap/surcharge symmetry, and that granting auras resolve to a per-enemy mask before the pipeline runs.

- [ ] **Step 2: Update content-rules.md**

1. Add `EnemyTrait` to the "Enums used below" list, marked append-only.
2. Add `traits` to the `EnemiesSO` field table.
3. Add an `EnemyTraitTuningSO` section listing the nine fields.
4. Add the §5.3 authoring rules: field enemies use self traits only; auras are guardian-only; aura enemies are low-HP (2–4); tier caps; never Elusive+Armored; **an Elusive enemy must have `canInfluence = true`**.
5. Under the UI-language section, note that **`IconMarkup.TraitBadge` is the sole owner of trait glyphs** (letters now, sprites later), so the "never hand-roll a glyph" rule covers badges.

- [ ] **Step 3: Update balance.md**

1. Add an **Enemy Traits** section with the nine starting tuning values.
2. Add the tier guidance (tier 1 ≤ 1 self trait, no auras; tier 2 one–two self or one weak aura; tier 3 aura + self).
3. Add the **~30% `canInfluence` target** as a standing map-wide authoring rule, with the reasoning: Influence dominates Siege on every axis (wound-free, full rewards, improvisable, can recruit), so availability is its only balance lever.

- [ ] **Step 4: Append to decisions-log.md**

Add the six decisions dated 2026-07-29:

1. Enemy traits are a `[Flags]` enum with magnitudes on one shared tuning asset, so a keyword means one fixed thing game-wide.
2. Swift is **capped** (raises the bar, not the punishment); Brutal is **surcharged** (raises the punishment, not the bar).
3. Granting auras grant **existing self traits** rather than inventing new ones — one vocabulary, and OR-ing makes them idempotent.
4. Unit wounds will **not** count toward wound-out, making units a wound sink that trades run-loss pressure for army capability.
5. Influence is balanced by **scarcity (~30% of enemies)**, never by trait-modified cost.
6. Defend is **per-enemy blocking with residual soak**, and blocking never suppresses auras.

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/archons-rise-design .claude/skills/archons-rise-roadmap
git commit -m "docs: record enemy traits, blocking and wound placement in the design bible"
```

---

## Task 16: Author traits onto enemies and verify in play

**Files:**
- Modify: enemy `.asset` files (**MANUAL, by the user, in the Unity editor**)

- [ ] **Step 1: List the current enemy assets and their influence ratio**

Run: `ls "Assets/Scripts/ScriptableObjectData"/**/*.asset 2>/dev/null | head -40` and locate the enemy assets. Then run:

`grep -l "canInfluence: 1" <enemy asset paths>`

Count them against the total. The spec's target is **~30%** `canInfluence`. Report the current ratio to the user before authoring — if it is far off, that is a content decision for them, not something to silently change.

- [ ] **Step 2: MANUAL — author traits**

**Done by the user in the Unity editor.** Give them this guidance:

- **Field (solo) enemies:** self traits only — Armored, Elusive, Hulking, Swift, Brutal, Toxic, Leech, Harrying, Vengeful. Never a granting aura (solo Miasma just equals Toxic — wasted authoring and a confusing first meeting with the keyword).
- **Guardian rosters (Keep/Castle):** this is where Warlord / Miasma / Ironclad / Outrider belong. Author aura enemies at **low HP (2–4)** so the Siege targeting puzzle stays winnable.
- **Tier 1:** at most one self trait, never an aura, never Elusive or Vengeful.
- **Tier 2:** one or two self traits, or one weak aura.
- **Tier 3:** an aura plus a self trait.
- **Never** Elusive together with Armored (the second is dead text).
- **Every Elusive enemy must have `canInfluence = true`** — otherwise it removes a choice rather than redirecting one.
- Start conservative: trait roughly **half** the enemy pool, leaving plain enemies as a baseline for comparison.

- [ ] **Step 3: MANUAL — verify in play**

Have the user run the game and confirm:

1. Trait badges appear on enemy cards; aura badges are tinted.
2. Hovering an enemy shows badge + name + rule for each trait, and the stat numbers reflect auras (a Warlord's ally shows raised Attack).
3. In the Defend phase, each enemy shows a Defend button with its block cost; a Swift enemy's cost is double its Attack.
4. The button is dimmed when Defend is short and clickable when it is enough.
5. Clicking blocks the enemy, drains Defend, and the advance button's wound preview drops.
6. Undo restores both the Defend pool and the wound preview.
7. Pressing the advance button commits — undo no longer reverts the block.
8. A Toxic enemy's wounds appear in the discard as well as the hand.
9. Fleeing a Harrying enemy leaves one fewer card in hand next turn.
10. Blocks are cleared by the next fight.

- [ ] **Step 4: Commit the authored assets**

```bash
git add Assets/Scripts/ScriptableObjectData
git commit -m "content: author enemy traits across the enemy pool"
```

---

## Execution Notes

**Task order matters.** Tasks 1–7 and 12's rules are pure and fully CLI-testable, so the entire combat math is proven before any MonoBehaviour or asset depends on it. Tasks 8–14 wire it into Unity. Tasks 15–16 document and author content.

**The single most important test** is `NoTraitsNoBlocks_MatchesLegacyGroupWoundCount` in Task 3. It sweeps every roster/defend/toughness combination and asserts the new pipeline equals today's `GroupWoundCount`. That is what lets traits and blocking ship without re-tuning a single existing enemy — if it ever fails, stop and fix the pipeline rather than adjusting the test.

**Four names this plan could not verify** (the implementer must find the real ones and must not create duplicates):
- `LiveEnemyCards()` — however `CombatController` already enumerates its logical live enemy set.
- `commands.AddCommand(...)` — however the existing undo stack executes-and-pushes.
- `SpendCrystals(int)` — however crystals are already removed for empower.
- `CombatController.Phase` — confirm it is **public** (Task 13's `OnDefendClicked` reads it from `EnemyCard`). If it is private, widen it; do not mirror the phase onto `EnemyCard`.
