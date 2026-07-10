using System;

// Pure combat-reward math: bell-curve exp sampling and independent bonus-roll
// gates. Unity-free so it is mcs-CLI-testable (DoomRules pattern). Callers pass
// rng delegates for determinism: an int rng returning [0, exclusiveMax) for
// exp draws, and a float rng returning [0,1) for the bonus rolls.
public static class RewardRules
{
    // Bell-curve exp: average K uniform draws in [expMin, expMax] and round.
    // K = expBellSamples (higher K = tighter bell on the range's centre).
    // rng(exclusiveMax) returns [0, exclusiveMax), matching SpawnRules' convention.
    public static int SampleExp(int tier, RewardTuning t, Func<int, int> rng)
    {
        var cfg = t.Tier(tier);
        int span = cfg.expMax - cfg.expMin + 1;
        if (span <= 1) return cfg.expMin;
        int k = Math.Max(1, t.expBellSamples);
        long sum = 0;
        for (int i = 0; i < k; i++)
            sum += cfg.expMin + rng(span);
        return (int)Math.Round((double)sum / k, MidpointRounding.AwayFromZero);
    }

    // Card-pool tier for a level-up pick at this player level (tunable bands).
    public static int CardTierForLevel(int level, RewardTuning t)
        => level >= t.levelTier3 ? 3 : level >= t.levelTier2 ? 2 : 1;

    // Independent bonus gate. chance<=0 never fires, chance>=1 always fires;
    // otherwise the roll succeeds when rng01() (a [0,1) draw) is below chance.
    public static bool Roll(float chance, Func<float> rng01)
    {
        if (chance <= 0f) return false;
        if (chance >= 1f) return true;
        return rng01() < chance;
    }
}
