using NUnit.Framework;

public class RoundRulesTests
{
    [Test]
    public void Turns_Decrement_And_Floor_At_Zero()
    {
        Assert.AreEqual(2, RoundRules.NextTurnsRemaining(3));
        Assert.AreEqual(0, RoundRules.NextTurnsRemaining(1));
        Assert.AreEqual(0, RoundRules.NextTurnsRemaining(0));
    }

    [Test]
    public void Round_Over_When_Budget_Spent()
    {
        Assert.IsTrue(RoundRules.IsRoundOver(0, deckCanRefill: true));
        Assert.IsFalse(RoundRules.IsRoundOver(1, deckCanRefill: true));
    }

    [Test]
    public void Round_Over_When_Deck_Cannot_Refill()
    {
        // Budget remains but the deck is dry -> forced rest.
        Assert.IsTrue(RoundRules.IsRoundOver(2, deckCanRefill: false));
    }

    [Test]
    public void Deck_Can_Refill_Unless_Empty()
    {
        Assert.IsTrue(RoundRules.DeckCanRefill(DrawVerdict.Draw));
        Assert.IsTrue(RoundRules.DeckCanRefill(DrawVerdict.HandFull));
        Assert.IsFalse(RoundRules.DeckCanRefill(DrawVerdict.DeckEmpty));
    }
}
