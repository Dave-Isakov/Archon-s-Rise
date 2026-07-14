using NUnit.Framework;

public class CardListHoverMathTests
{
    [Test]
    public void CardAtCenter_NoPull()
    {
        float ox, oy;
        CardListHoverMath.PullOffset(10f, -20f, 10f, -20f, 0.25f, out ox, out oy);
        Assert.AreEqual(0f, ox);
        Assert.AreEqual(0f, oy);
    }

    [Test]
    public void PullsTowardCenter_ProportionalToDistance()
    {
        float ox, oy;
        CardListHoverMath.PullOffset(-400f, 100f, 0f, 0f, 0.25f, out ox, out oy);
        Assert.AreEqual(100f, ox);  // card left of centre pulls right
        Assert.AreEqual(-25f, oy);  // card above centre pulls down
    }

    [Test]
    public void ZeroStrength_NoPull()
    {
        float ox, oy;
        CardListHoverMath.PullOffset(-400f, 100f, 0f, 0f, 0f, out ox, out oy);
        Assert.AreEqual(0f, ox);
        Assert.AreEqual(0f, oy);
    }
}
