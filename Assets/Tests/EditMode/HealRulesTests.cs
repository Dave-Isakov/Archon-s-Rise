using System.Collections.Generic;
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

    [Test]
    public void HealableCount_SumsHandWoundsAndUnitWounds()
    {
        Assert.AreEqual(0, HealRules.HealableCount(0, new int[0]));
        Assert.AreEqual(4, HealRules.HealableCount(4, new int[0]));
        Assert.AreEqual(3, HealRules.HealableCount(0, new[] { 1, 2 }));
        Assert.AreEqual(7, HealRules.HealableCount(4, new[] { 1, 2 }));
    }

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
}
