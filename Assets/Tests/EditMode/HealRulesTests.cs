using NUnit.Framework;

public class HealRulesTests
{
    [Test]
    public void PureHealCard_HealsBaseAmount()
    {
        Assert.AreEqual(1, HealRules.HealCount(StatType.Heal, false, 1, 2));
    }

    [Test]
    public void PureHealCard_EmpoweredHealsEmpowerAmount()
    {
        Assert.AreEqual(2, HealRules.HealCount(StatType.Heal, true, 1, 2));
    }

    // Mending Light regression (2026-07-15): a Heal|Crystal card granted its
    // crystals but never healed, because the heal check compared the whole
    // flags value against StatType.Heal instead of testing the flag.
    [Test]
    public void CombinedHealCrystalCard_StillHeals()
    {
        Assert.AreEqual(1, HealRules.HealCount(StatType.Heal | StatType.Crystal, false, 1, 2));
    }

    [Test]
    public void CombinedHealCrystalCard_EmpoweredStillHeals()
    {
        Assert.AreEqual(2, HealRules.HealCount(StatType.Heal | StatType.Crystal, true, 1, 2));
    }

    [Test]
    public void CardWithoutHealFlag_HealsNothing()
    {
        Assert.AreEqual(0, HealRules.HealCount(StatType.Attack | StatType.Crystal, false, 1, 2));
    }

    [Test]
    public void WoundCard_HealsNothing()
    {
        Assert.AreEqual(0, HealRules.HealCount(StatType.Wound, false, 1, 2));
    }
}
