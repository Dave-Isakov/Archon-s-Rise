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
