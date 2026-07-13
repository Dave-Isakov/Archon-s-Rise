using System;
using System.Collections.Generic;
using ArchonsRise.SaveData;

// Pure dungeon math (M2.9, spec 2026-07-13): delve progression, the flagged
// round tick, completion relief, band-entry detection, and flag-target picks.
// Unity-free so it is mcs-CLI-testable (DoomRules pattern).
public static class DungeonRules
{
    public const int DelveCount = 3;

    // Tier of the next delve fight: 1/2/3 after 0/1/2 wins; 0 once complete.
    public static int NextTier(int defeatedCount)
        => defeatedCount < 0 || defeatedCount >= DelveCount ? 0 : defeatedCount + 1;

    public static bool IsComplete(int defeatedCount) => defeatedCount >= DelveCount;

    // Round doom tick: the base +1 plus +1 per flagged, uncleared dungeon.
    public static int RoundTick(int flaggedCount) => 1 + Math.Max(0, flaggedCount);

    // Doom relief on completion (applied as a negative DoomClock.Add).
    public static int Relief(bool flagged, DoomTuning t)
        => flagged ? t.flaggedCompleteRelief : t.dungeonCompleteRelief;

    // Band-entry detection over one doom change. "Entering" means crossing the
    // band edge upward; moving within a band or dropping back never fires.
    public static void BandsEntered(int before, int after, DoomTuning t,
        out bool enteredMid, out bool enteredHigh)
    {
        enteredMid = before <= t.lowBandMax && after > t.lowBandMax;
        enteredHigh = before <= t.midBandMax && after > t.midBandMax;
    }

    // Random distinct picks from candidates; returns fewer when they run short.
    public static List<Cell> PickFlagTargets(IReadOnlyList<Cell> candidates, int count, Func<int, int> rng)
    {
        var pool = new List<Cell>(candidates);
        var picks = new List<Cell>();
        while (picks.Count < count && pool.Count > 0)
        {
            var pick = pool[rng(pool.Count)];
            pool.Remove(pick);
            picks.Add(pick);
        }
        return picks;
    }
}
