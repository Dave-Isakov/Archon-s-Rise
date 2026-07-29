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
