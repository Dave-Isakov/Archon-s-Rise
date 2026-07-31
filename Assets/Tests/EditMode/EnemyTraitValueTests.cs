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

    // The predicate the Siege button branches on. SiegeCost's int.MaxValue is a
    // sentinel for the arithmetic; the UI needs the fact itself so it can say
    // "cannot be Sieged" instead of quoting an absurd number.
    [Test]
    public void Elusive_CannotBeSieged_PlainCan()
    {
        var r = new List<EnemyCombatant> { E(3, 6, EnemyTrait.Elusive), E(3, 6) };
        Assert.IsFalse(EnemyTraitRules.CanBeSieged(0, r));
        Assert.IsTrue(EnemyTraitRules.CanBeSieged(1, r));
    }

    // The regression this exists for: the kill path priced every enemy at raw HP,
    // so 4 Siege removed a 4 HP Elusive enemy. One entry point now picks the cost,
    // and Siege/Attack must not read each other's trait.
    [Test]
    public void CostFor_RoutesByKind()
    {
        var armored = new List<EnemyCombatant> { E(3, 6, EnemyTrait.Armored) };
        Assert.AreEqual(12, EnemyTraitRules.CostFor(AttackKind.Siege, 0, armored, T()));
        Assert.AreEqual(6,  EnemyTraitRules.CostFor(AttackKind.Normal, 0, armored, T()));

        var hulking = new List<EnemyCombatant> { E(3, 6, EnemyTrait.Hulking) };
        Assert.AreEqual(6,  EnemyTraitRules.CostFor(AttackKind.Siege, 0, hulking, T()));
        Assert.AreEqual(12, EnemyTraitRules.CostFor(AttackKind.Normal, 0, hulking, T()));
    }

    // Elusive redirects to Attack/Influence, it does not make the enemy immortal.
    [Test]
    public void Elusive_AmpleSiegeStillCannotDefeat_ButAttackCan()
    {
        var r = new List<EnemyCombatant> { E(3, 4, EnemyTrait.Elusive) };
        Assert.IsFalse(CombatRules.CanDefeat(AttackKind.Siege, 0, 4,
            EnemyTraitRules.CostFor(AttackKind.Siege, 0, r, T())),
            "4 Siege must not cover a 4 HP Elusive enemy");
        Assert.IsTrue(CombatRules.CanDefeat(AttackKind.Normal, 4, 0,
            EnemyTraitRules.CostFor(AttackKind.Normal, 0, r, T())));
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
