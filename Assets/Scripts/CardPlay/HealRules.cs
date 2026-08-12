using System.Collections.Generic;

// Pure heal math for the card play path. Unity-free so it is mcs-CLI-testable.
public static class HealRules
{
    // Wounds this card play heals (0 for non-heal cards). The same count is
    // restored when the play is undone. Flag test, not equality: combined
    // types like Heal|Crystal (Mending Light) must still heal.
    public static int HealCount(StatType cardType, bool empowered, int healAmount, int empowerHealAmount)
    {
        if (cardType.HasFlag(StatType.Heal))
            return empowered ? empowerHealAmount : healAmount;
        return 0;
    }

    public static int HealableCount(int handWounds, IReadOnlyList<int> unitWoundCounts)
    {
        int total = handWounds > 0 ? handWounds : 0;
        if (unitWoundCounts != null)
            for (int i = 0; i < unitWoundCounts.Count; i++)
                if (unitWoundCounts[i] > 0) total += unitWoundCounts[i];
        return total;
    }

    public static bool CanHeal(int handWounds, IReadOnlyList<int> unitWoundCounts)
        => HealableCount(handWounds, unitWoundCounts) > 0;
}
