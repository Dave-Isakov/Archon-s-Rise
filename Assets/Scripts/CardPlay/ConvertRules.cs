using System.Collections.Generic;

// Pure conversion math (spec 2026-07-14). Conversion is always 1:1 and only
// moves the four action pools — Siege/Heal/Crystal/Wound never participate.
// Pool arrays use the CardPlaySelection order [attack, defend, influence,
// explore]. Unity-free so it is mcs-CLI-testable.
public static class ConvertRules
{
    public static readonly StatType[] ActionStats =
        { StatType.Attack, StatType.Defend, StatType.Influence, StatType.Explore };

    const StatType ActionMask =
        StatType.Attack | StatType.Defend | StatType.Influence | StatType.Explore;

    // Index of a single action flag in the pools array; -1 for anything else.
    public static int IndexOf(StatType single)
    {
        for (int i = 0; i < ActionStats.Length; i++)
            if (ActionStats[i] == single) return i;
        return -1;
    }

    // Authorable when: target is exactly one action stat, sources are one or
    // more action stats, and the target is not among the sources.
    public static bool IsValid(StatType from, StatType to)
    {
        if (IndexOf(to) < 0) return false;
        if (from == StatType.None) return false;
        if ((from & ~ActionMask) != 0) return false;
        if (from.HasFlag(to)) return false;
        return true;
    }

    // Per-pool amounts the conversion moves: each flagged source drains fully;
    // the target index stays 0 (the caller adds MovedTotal to the target).
    public static int[] Moved(int[] pools, StatType from, StatType to)
    {
        var moved = new int[4];
        if (!IsValid(from, to)) return moved;
        for (int i = 0; i < ActionStats.Length; i++)
            if (from.HasFlag(ActionStats[i]) && pools[i] > 0)
                moved[i] = pools[i];
        return moved;
    }

    public static int MovedTotal(int[] moved)
    {
        int total = 0;
        for (int i = 0; i < moved.Length; i++) total += moved[i];
        return total;
    }

    // Banner / description text in icon language (spec 2026-07-15): the source
    // stat glyphs, space-separated, then an arrow to the target glyph, e.g.
    // "<shield> → <sword>". TMP renders the arrow and sprite tags directly.
    public static string Describe(StatType from, StatType to)
    {
        var parts = new List<string>();
        for (int i = 0; i < ActionStats.Length; i++)
            if (from.HasFlag(ActionStats[i]) && IconMarkup.TryForStat(ActionStats[i], out var c))
                parts.Add(IconMarkup.Tag(c));
        string target = IconMarkup.TryForStat(to, out var tc) ? IconMarkup.Tag(tc) : to.ToString();
        return string.Join(" ", parts.ToArray()) + " → " + target;
    }
}
