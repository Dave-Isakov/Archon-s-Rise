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
}
