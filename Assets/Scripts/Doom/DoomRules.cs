using System;

// Pure doom-clock math: band lookup, spawn cadence, tier gate, stat bonus,
// loss check. Unity-free so it is mcs-CLI-testable (CombatRules pattern).
public static class DoomRules
{
    public static int Add(int current, int amount, DoomTuning t)
        => Math.Max(0, Math.Min(t.doomMax, current + amount));

    public static bool IsLoss(int doom, DoomTuning t) => doom >= t.doomMax;

    // Rounds between mid-run spawns at this doom.
    public static int SpawnInterval(int doom, DoomTuning t)
        => doom <= t.lowBandMax ? t.lowSpawnInterval
         : doom <= t.midBandMax ? t.midSpawnInterval
         : t.highSpawnInterval;

    // roundsSinceSpawn is incremented once per round end before this check.
    public static bool ShouldSpawn(int doom, int roundsSinceSpawn, DoomTuning t)
        => roundsSinceSpawn >= SpawnInterval(doom, t);

    // Tier gate: tougher enemy tiers unlock as the bands escalate.
    public static int MaxTier(int doom, DoomTuning t)
        => doom > t.midBandMax ? 3 : doom > t.lowBandMax ? 2 : 1;

    // Turns in a round ("day" length) at this doom: longer in the low band,
    // shorter as the bands escalate (spec 2026-07-21).
    public static int TurnsForBand(int doom, DoomTuning t)
        => doom <= t.lowBandMax ? t.lowBandTurns
         : doom <= t.midBandMax ? t.midBandTurns
         : t.highBandTurns;

    // Flat +HP/+Attack applied to enemies spawned at this doom.
    public static int StatBonus(int doom, DoomTuning t)
        => doom > t.midBandMax ? t.highStatBonus : doom > t.lowBandMax ? t.midStatBonus : 0;
}
