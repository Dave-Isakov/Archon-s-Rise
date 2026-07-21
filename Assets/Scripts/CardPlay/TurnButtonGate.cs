// Enabled state for the End Turn button. Pure, no scene dependency.
public static class TurnButtonGate
{
    // End Turn is available except mid-fight. (Deck-empty no longer blocks it —
    // pressing End Turn auto-ends the round instead; spec 2026-07-21.)
    public static bool EndTurn(bool inCombat) => !inCombat;
}
