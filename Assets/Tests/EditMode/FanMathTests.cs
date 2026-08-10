using NUnit.Framework;
using UnityEngine;

public class FanMathTests
{
    static FanSettings Settings() => new FanSettings
    {
        SpreadDegrees = 66f,
        CardSpacing = 120f,
        ArcDrop = 40f,
        MaxWidth = 900f
    };

    static float Span(FanSlot[] slots) =>
        slots[slots.Length - 1].AnchoredPosition.x - slots[0].AnchoredPosition.x;

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

    [Test]
    public void Span_IsUncappedBelowTheThreshold()
    {
        // 8 cards need 7 * 120 = 840 <= 900, so spacing is untouched.
        Assert.AreEqual(840f, Span(FanMath.Solve(8, Settings())), 0.001f);
    }

    [Test]
    public void Span_CompressesAboveTheThreshold()
    {
        // 12 cards would need 11 * 120 = 1320; the cap pulls the span back to 900.
        Assert.AreEqual(900f, Span(FanMath.Solve(12, Settings())), 0.001f);
    }

    [Test]
    public void Span_NeverExceedsMaxWidth()
    {
        for (int count = 2; count <= 20; count++)
            Assert.LessOrEqual(Span(FanMath.Solve(count, Settings())), 900.001f,
                "count " + count + " overran MaxWidth");
    }

    [Test]
    public void MaxWidthOfZero_DisablesTheCap()
    {
        var s = Settings();
        s.MaxWidth = 0f;
        Assert.AreEqual(1320f, Span(FanMath.Solve(12, s)), 0.001f);
    }
}
