using NUnit.Framework;

public class ArmyRulesTests
{
    [Test]
    public void CanRecruit_OnlyBelowCap()
    {
        Assert.IsTrue(ArmyRules.CanRecruit(0, 1));
        Assert.IsFalse(ArmyRules.CanRecruit(1, 1));
        Assert.IsFalse(ArmyRules.CanRecruit(2, 1)); // over-cap (bad state) still blocks
    }

    [Test]
    public void NeedsDisband_AtOrAboveCap()
    {
        Assert.IsFalse(ArmyRules.NeedsDisband(0, 1));
        Assert.IsTrue(ArmyRules.NeedsDisband(1, 1));
        Assert.IsTrue(ArmyRules.NeedsDisband(3, 2));
    }
}
