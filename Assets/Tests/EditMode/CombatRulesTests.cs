using NUnit.Framework;

public class CombatRulesTests
{
    [Test]
    public void Normal_Defeats_When_Attack_Covers_HP()
    {
        Assert.IsTrue(CombatRules.CanDefeat(AttackKind.Normal, 5, 0, 5));
    }

    [Test]
    public void Normal_Defeats_When_Attack_Plus_Siege_Cover_HP()
    {
        // Siege makes up an Attack shortfall for a Normal attack.
        Assert.IsTrue(CombatRules.CanDefeat(AttackKind.Normal, 4, 1, 5));
        Assert.IsTrue(CombatRules.CanDefeat(AttackKind.Normal, 0, 5, 5));
        // Combined pool still falls short.
        Assert.IsFalse(CombatRules.CanDefeat(AttackKind.Normal, 2, 2, 5));
    }

    [Test]
    public void Normal_Drains_Attack_Before_Siege()
    {
        // Attack covers it: borrow no Siege.
        Assert.AreEqual(0, CombatRules.SiegeSpentOnNormal(5, 5));
        Assert.AreEqual(0, CombatRules.SiegeSpentOnNormal(9, 5));
        // Attack shortfall of 3 is covered by Siege.
        Assert.AreEqual(3, CombatRules.SiegeSpentOnNormal(2, 5));
        // No Attack at all: Siege pays the whole cost.
        Assert.AreEqual(5, CombatRules.SiegeSpentOnNormal(0, 5));
    }

    [Test]
    public void Siege_Defeats_On_Siege_Pool_Not_Attack()
    {
        Assert.IsTrue(CombatRules.CanDefeat(AttackKind.Siege, 0, 5, 5));
        Assert.IsFalse(CombatRules.CanDefeat(AttackKind.Siege, 99, 4, 5)); // Attack pool must not help Siege
    }

    [Test]
    public void Siege_Is_Always_Wound_Free()
    {
        // Massive Defend shortfall, still zero wounds.
        Assert.AreEqual(0, CombatRules.WoundCount(AttackKind.Siege, 0, 10, 3));
    }

    [Test]
    public void Normal_No_Wounds_When_Defend_Covers_Attack()
    {
        Assert.AreEqual(0, CombatRules.WoundCount(AttackKind.Normal, 5, 5, 3));
        Assert.AreEqual(0, CombatRules.WoundCount(AttackKind.Normal, 6, 5, 3));
    }

    [Test]
    public void Normal_Wounds_Chunked_By_PlayerHP()
    {
        // shortfall 6, HP 3 -> 2 wounds (i=0,3)
        Assert.AreEqual(2, CombatRules.WoundCount(AttackKind.Normal, 0, 6, 3));
        // shortfall 4, HP 3 -> 2 wounds (i=0,3)
        Assert.AreEqual(2, CombatRules.WoundCount(AttackKind.Normal, 0, 4, 3));
        // shortfall 1, HP 5 -> 1 wound
        Assert.AreEqual(1, CombatRules.WoundCount(AttackKind.Normal, 0, 1, 5));
    }
}
