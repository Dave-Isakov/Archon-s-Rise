using System;
using System.Collections.Generic;

namespace ArchonsRise.Shrines
{
    // Pure shrine resolution math (spec 2026-07-24, §2). Unity-free → mcs-testable.
    // The coin decides delivery; the type decides what; the count is the 1x/2x
    // safe-vs-fight multiplier.
    public static class ShrineRules
    {
        // Safe (good) result: roll strictly under the chance.
        public static bool IsGood(float goodRollChance, float roll01) => roll01 < goodRollChance;

        // The reward TYPE, drawn uniformly from the authored pool.
        public static ShrineReward RollType(IReadOnlyList<ShrineReward> pool, Func<int, int> rng)
            => pool[rng(pool.Count)];

        // Safe pays 1x; the guardian (fight) pays 2x.
        public static int RewardCount(bool good) => good ? 1 : 2;
    }
}
