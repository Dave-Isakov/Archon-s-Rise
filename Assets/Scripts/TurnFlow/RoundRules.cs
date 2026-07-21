// Pure "day" (round) budget math (spec 2026-07-21). The per-band starting
// budget comes from DoomRules.TurnsForBand; this class only counts it down and
// decides when the day is over. Unity-free / mcs-testable.
public static class RoundRules
{
    // One turn spent; never negative.
    public static int NextTurnsRemaining(int turnsRemaining)
        => turnsRemaining > 0 ? turnsRemaining - 1 : 0;

    // The day ends when the budget is spent OR the deck can no longer refill the
    // hand (a forced rest so a short deck can't strand the player mid-day).
    public static bool IsRoundOver(int turnsRemainingAfterDecrement, bool deckCanRefill)
        => turnsRemainingAfterDecrement <= 0 || !deckCanRefill;

    public static bool DeckCanRefill(DrawVerdict verdict)
        => verdict != DrawVerdict.DeckEmpty;
}
