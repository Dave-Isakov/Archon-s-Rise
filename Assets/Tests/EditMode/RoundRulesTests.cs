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
        Assert.IsTrue(RoundRules.IsRoundOver(0, deckShortfallPending: false));
        Assert.IsFalse(RoundRules.IsRoundOver(1, deckShortfallPending: false));
    }

    [Test]
    public void Round_Over_When_Deck_Shortfall_Pending()
    {
        // Budget remains but last turn's refill already fell short -> forced rest.
        Assert.IsTrue(RoundRules.IsRoundOver(2, deckShortfallPending: true));
    }

    [Test]
    public void CanFullyRefill_FullHandNeedsNothing_AlwaysTrue()
    {
        // Zero (or negative) needed draws is trivially satisfied regardless of deck size.
        Assert.IsTrue(RoundRules.CanFullyRefill(deckCount: 0, neededDraws: 0));
    }

    [Test]
    public void CanFullyRefill_DeckShortOfNeeded_False()
    {
        Assert.IsFalse(RoundRules.CanFullyRefill(deckCount: 0, neededDraws: 2));
        Assert.IsFalse(RoundRules.CanFullyRefill(deckCount: 3, neededDraws: 4));
    }

    [Test]
    public void CanFullyRefill_DeckMeetsOrExceedsNeeded_True()
    {
        Assert.IsTrue(RoundRules.CanFullyRefill(deckCount: 5, neededDraws: 5));
        Assert.IsTrue(RoundRules.CanFullyRefill(deckCount: 4, neededDraws: 3));
    }
}
