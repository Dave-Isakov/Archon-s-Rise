// Pure "day" (round) budget math (spec 2026-07-21). The per-band starting
// budget comes from DoomRules.TurnsForBand; this class only counts it down and
// decides when the day is over. Unity-free / mcs-testable.
public static class RoundRules
{
    // One turn spent; never negative.
    public static int NextTurnsRemaining(int turnsRemaining)
        => turnsRemaining > 0 ? turnsRemaining - 1 : 0;

    // The day ends when the budget is spent OR the previous turn's hand refill
    // already fell short (a one-turn-delayed forced rest so a short deck can't
    // strand the player mid-day, while still letting the turn that discovers the
    // shortfall play out normally — spec 2026-07-30).
    public static bool IsRoundOver(int turnsRemainingAfterDecrement, bool deckShortfallPending)
        => turnsRemainingAfterDecrement <= 0 || deckShortfallPending;

    // Whether the end-of-turn top-up can fully restore the hand from the deck.
    // False means this turn's refill falls short, which carries into next turn
    // as a forced round-end (see IsRoundOver's second argument).
    public static bool CanFullyRefill(int deckCount, int neededDraws)
        => deckCount >= neededDraws;
}
