using System.Collections.Generic;

// One boosted stat to echo: which stat, and how much it changed.
public readonly struct StatAmount
{
    public readonly StatType Stat;
    public readonly int Amount;
    public StatAmount(StatType stat, int amount) { Stat = stat; Amount = amount; }
}

// Turns a [attack, defend, influence, explore] stat array (as produced by
// CardPlaySelection.PreviewStats) into one StatAmount per non-zero entry, in stat
// order. Drives one floating "+N" label per boosted stat. Pure, no scene dependency.
public static class StatEchoPlan
{
    static readonly StatType[] Order =
        { StatType.Attack, StatType.Defend, StatType.Influence, StatType.Explore };

    public static List<StatAmount> NonZero(int[] stats)
    {
        var result = new List<StatAmount>();
        if (stats == null) return result;
        for (int i = 0; i < Order.Length && i < stats.Length; i++)
            if (stats[i] != 0) result.Add(new StatAmount(Order[i], stats[i]));
        return result;
    }
}
