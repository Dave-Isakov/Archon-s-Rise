using NUnit.Framework;
using ArchonsRise.Hotspots;

public class HotspotRulesTests
{
    [Test]
    public void CanHarvest_TrueForPositiveCharges()
    {
        Assert.IsTrue(HotspotRules.CanHarvest(3));
        Assert.IsTrue(HotspotRules.CanHarvest(1));
    }

    [Test]
    public void CanHarvest_TrueForUnlimitedSentinel()
    {
        Assert.IsTrue(HotspotRules.CanHarvest(-1));
    }

    [Test]
    public void CanHarvest_FalseWhenDepleted()
    {
        Assert.IsFalse(HotspotRules.CanHarvest(0));
    }

    [Test]
    public void NextCharges_DecrementsPositive()
    {
        Assert.AreEqual(2, HotspotRules.NextCharges(3));
        Assert.AreEqual(0, HotspotRules.NextCharges(1));
    }

    [Test]
    public void NextCharges_UnlimitedStaysUnlimited()
    {
        Assert.AreEqual(-1, HotspotRules.NextCharges(-1));
    }

    [Test]
    public void NextCharges_FloorsAtZero()
    {
        Assert.AreEqual(0, HotspotRules.NextCharges(0));
    }
}
