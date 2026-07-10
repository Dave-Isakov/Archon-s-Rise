// End-of-run outcomes. None = the run continues.
public enum RunOutcome { None, Victory, DoomLoss, WoundOutLoss, WoundHandLoss }

// Pure win/lose evaluation. Thresholds are consts (PlaceRules pattern); the
// doom loss lives in DoomRules.IsLoss because its max is DoomTuning-owned.
public static class RunEndRules
{
    public const int CastlesToWin = 2;      // balance.md — Archon win threshold
    public const int WoundOutThreshold = 6; // balance.md — total wounds in the run

    public static bool IsVictory(int conqueredCastles) => conqueredCastles >= CastlesToWin;

    // totalWounds counts Wound cards across deck + hand + discard.
    public static bool IsWoundOut(int totalWounds) => totalWounds >= WoundOutThreshold;

    // A hand that topped up to full holding nothing but wounds is unplayable —
    // the run is lost (deferred wound-hand rule, closed by M2.5).
    public static bool IsWoundHand(int handCount, int woundsInHand, int handSize)
        => handCount >= handSize && handCount > 0 && woundsInHand >= handCount;
}
