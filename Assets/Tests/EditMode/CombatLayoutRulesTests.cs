using NUnit.Framework;

public class CombatLayoutRulesTests
{
    [Test]
    public void SingleCard_SitsDeadCentreTop()
    {
        var s = CombatLayoutRules.SlotFor(0, 1, 300f, 120f);
        Assert.AreEqual(0f, s.X, 1e-3f);   // cos(90) == 0
        Assert.AreEqual(300f, s.Y, 1e-3f); // sin(90) * radius
        Assert.AreEqual(1f, s.Scale, 1e-4f);
    }

    [Test]
    public void TwoCards_AreSymmetricAcrossTop()
    {
        var left = CombatLayoutRules.SlotFor(0, 2, 300f, 120f);
        var right = CombatLayoutRules.SlotFor(1, 2, 300f, 120f);
        Assert.AreEqual(left.X, -right.X, 1e-3f); // mirror across x=0
        Assert.AreEqual(left.Y, right.Y, 1e-3f);  // same height
    }

    [Test]
    public void Index0_IsLeft_XIncreasesRightward()
    {
        // Angle sweeps left (index 0, larger angle) to right; X strictly increases.
        var a = CombatLayoutRules.SlotFor(0, 3, 300f, 120f);
        var b = CombatLayoutRules.SlotFor(1, 3, 300f, 120f);
        var c = CombatLayoutRules.SlotFor(2, 3, 300f, 120f);
        Assert.Less(a.X, b.X);
        Assert.Less(b.X, c.X);
        Assert.AreEqual(0f, b.X, 1e-3f); // middle card centred
    }

    [Test]
    public void Scale_FullUpToThreshold_ThenStepsDown()
    {
        Assert.AreEqual(1f, CombatLayoutRules.ScaleFor(1), 1e-4f);
        Assert.AreEqual(1f, CombatLayoutRules.ScaleFor(CombatLayoutRules.CrowdThreshold), 1e-4f);
        Assert.Less(CombatLayoutRules.ScaleFor(CombatLayoutRules.CrowdThreshold + 1), 1f);
    }

    [Test]
    public void Scale_IsFlooredForLargeRosters()
    {
        Assert.GreaterOrEqual(CombatLayoutRules.ScaleFor(20), 0.6f);
    }

    [Test]
    public void AngleFor_CountOneOrZero_IsTop()
    {
        Assert.AreEqual(90f, CombatLayoutRules.AngleFor(0, 1, 120f), 1e-4f);
        Assert.AreEqual(90f, CombatLayoutRules.AngleFor(0, 0, 120f), 1e-4f);
    }
}
