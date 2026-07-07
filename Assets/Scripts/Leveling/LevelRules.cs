using System.Collections.Generic;

// Pure leveling rules. No scene/Unity dependency (mirrors CombatRules /
// PlaceRules). Hand size and army cap are DERIVED from level + table — never
// stored — so saves stay lean and can't drift out of sync with the table.
public static class LevelRules
{
    public const int BaseArmyCap = 1;

    // The table row for this exact level; null when the level grants nothing.
    public static LevelRewardEntry RewardsFor(int level, IReadOnlyList<LevelRewardEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].level == level) return entries[i];
        return null;
    }

    public static int DerivedHandSize(int baseHandSize, int level, IReadOnlyList<LevelRewardEntry> entries)
    {
        int size = baseHandSize;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].level <= level) size += entries[i].handSizeBonus;
        return size;
    }

    public static int DerivedArmyCap(int level, IReadOnlyList<LevelRewardEntry> entries)
    {
        int cap = BaseArmyCap;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].level <= level) cap += entries[i].armySizeBonus;
        return cap;
    }

    // Exp past the threshold carries into the next level (the old code reset to
    // 0 and discarded overflow). Clamped for safety against bad saved values.
    public static int CarriedExp(int exp, int expToNextLevel)
    {
        int carried = exp - expToNextLevel;
        return carried < 0 ? 0 : carried;
    }

    // Up to `count` distinct random picks from the pool, excluding owned.
    // Generic so it's Unity-free; callers pass SkillsSO lists, tests pass strings.
    public static List<T> DrawSkillChoices<T>(IReadOnlyList<T> pool, ICollection<T> owned,
        System.Random rng, int count = 3)
    {
        var candidates = new List<T>();
        for (int i = 0; i < pool.Count; i++)
            if (!owned.Contains(pool[i]) && !candidates.Contains(pool[i]))
                candidates.Add(pool[i]);

        var picks = new List<T>();
        while (picks.Count < count && candidates.Count > 0)
        {
            int idx = rng.Next(candidates.Count);
            picks.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }
        return picks;
    }
}
