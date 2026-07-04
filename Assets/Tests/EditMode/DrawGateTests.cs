using NUnit.Framework;

public class DrawGateTests
{
    [Test]
    public void HandBelowSize_DeckHasCards_CanDraw()
    {
        Assert.AreEqual(DrawVerdict.Draw, DrawGate.Evaluate(deckCount: 10, handCount: 3, handSize: 5));
    }

    [Test]
    public void HandAtSize_ReportsHandFull()
    {
        Assert.AreEqual(DrawVerdict.HandFull, DrawGate.Evaluate(deckCount: 10, handCount: 5, handSize: 5));
    }

    [Test]
    public void DeckEmpty_HandBelowSize_ReportsDeckEmpty()
    {
        Assert.AreEqual(DrawVerdict.DeckEmpty, DrawGate.Evaluate(deckCount: 0, handCount: 3, handSize: 5));
    }

    [Test]
    public void DeckEmpty_HandAtSize_ReportsHandFull_NotDeckEmpty()
    {
        // A full hand needs no draw, so an empty deck should not block ending the turn.
        Assert.AreEqual(DrawVerdict.HandFull, DrawGate.Evaluate(deckCount: 0, handCount: 5, handSize: 5));
    }

    [Test]
    public void LastDeckCard_HandBelowSize_CanDraw()
    {
        Assert.AreEqual(DrawVerdict.Draw, DrawGate.Evaluate(deckCount: 1, handCount: 4, handSize: 5));
    }
}
