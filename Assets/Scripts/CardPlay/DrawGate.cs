// Whether a card draw can happen right now, and if not, why. Drives both the
// validation message on a blocked draw and the End Turn button's enabled state
// (End Turn is pointless when the deck can't refill the hand — the round must
// end instead). Pure, no scene dependency.
public enum DrawVerdict
{
    Draw,
    HandFull,
    DeckEmpty,
}

public static class DrawGate
{
    // HandFull wins over DeckEmpty: a full hand needs no draw, so an empty deck
    // should not block ending the turn in that state.
    public static DrawVerdict Evaluate(int deckCount, int handCount, int handSize)
    {
        if (handCount >= handSize) return DrawVerdict.HandFull;
        if (deckCount <= 0) return DrawVerdict.DeckEmpty;
        return DrawVerdict.Draw;
    }
}
