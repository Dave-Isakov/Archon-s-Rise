using NUnit.Framework;
using UnityEngine;

public class FanMathTests
{
    static FanSettings Settings() => new FanSettings
    {
        SpreadDegrees = 66f,
        CardSpacing = 120f,
        ArcDrop = 40f
    };

    [Test]
    public void Empty_ReturnsNoSlots()
    {
        Assert.AreEqual(0, FanMath.Solve(0, Settings()).Length);
    }

    [Test]
    public void SingleCard_IsCentredAndUntilted()
    {
        var slots = FanMath.Solve(1, Settings());
        Assert.AreEqual(1, slots.Length);
        Assert.AreEqual(0f, slots[0].AnchoredPosition.x, 0.001f);
        Assert.AreEqual(0f, slots[0].AnchoredPosition.y, 0.001f);
        Assert.AreEqual(0f, slots[0].TiltZ, 0.001f);
    }

    [Test]
    public void Edges_ReachFullSpreadAndMirror()
    {
        var slots = FanMath.Solve(5, Settings());
        // leftmost (index 0) and rightmost (index 4) tilt to ±33° and mirror.
        Assert.AreEqual(33f, slots[0].TiltZ, 0.001f);
        Assert.AreEqual(-33f, slots[4].TiltZ, 0.001f);
        Assert.AreEqual(-slots[0].AnchoredPosition.x, slots[4].AnchoredPosition.x, 0.001f);
        Assert.AreEqual(slots[0].AnchoredPosition.y, slots[4].AnchoredPosition.y, 0.001f);
    }

    [Test]
    public void Cards_AreEvenlySpacedAndCentred()
    {
        var slots = FanMath.Solve(4, Settings());
        // centred about x=0: spacing 120 -> x = {-180, -60, 60, 180}
        Assert.AreEqual(-180f, slots[0].AnchoredPosition.x, 0.001f);
        Assert.AreEqual(-60f, slots[1].AnchoredPosition.x, 0.001f);
        Assert.AreEqual(60f, slots[2].AnchoredPosition.x, 0.001f);
        Assert.AreEqual(180f, slots[3].AnchoredPosition.x, 0.001f);
    }

    [Test]
    public void EdgeCards_SitBelowCentre()
    {
        var slots = FanMath.Solve(3, Settings());
        Assert.AreEqual(0f, slots[1].AnchoredPosition.y, 0.001f);   // centre card at y=0
        Assert.AreEqual(-40f, slots[0].AnchoredPosition.y, 0.001f); // edges drop by ArcDrop
        Assert.AreEqual(-40f, slots[2].AnchoredPosition.y, 0.001f);
    }
}
