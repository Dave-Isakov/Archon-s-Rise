// Enabled state for the End Turn / Round End buttons. Neither makes sense
// mid-fight, and End Turn additionally requires the deck to be able to refill
// the hand (otherwise the round must end instead). Pure, no scene dependency.
public static class TurnButtonGate
{
    public static bool EndTurn(bool inCombat, DrawVerdict verdict)
    {
        return !inCombat && verdict != DrawVerdict.DeckEmpty;
    }

    public static bool EndRound(bool inCombat)
    {
        return !inCombat;
    }
}
