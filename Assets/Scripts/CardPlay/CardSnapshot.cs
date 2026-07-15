public readonly struct CardSnapshot
{
    public readonly StatType CardType;
    public readonly EmpowerType EmpowerType;
    public readonly bool IsChoice;
    public readonly int Attack, Defend, Influence, Explore;
    public readonly int EmpowerAttack, EmpowerDefend, EmpowerInfluence, EmpowerExplore;
    public readonly StatType ConvertTo;             // None = no conversion
    public readonly StatType ConvertFrom;
    public readonly bool ConvertRequiresEmpower;

    public CardSnapshot(StatType cardType, EmpowerType empowerType, bool isChoice,
        int attack, int defend, int influence, int explore,
        int empowerAttack, int empowerDefend, int empowerInfluence, int empowerExplore,
        StatType convertTo = StatType.None, StatType convertFrom = StatType.None,
        bool convertRequiresEmpower = false)
    {
        CardType = cardType;
        EmpowerType = empowerType;
        IsChoice = isChoice;
        Attack = attack; Defend = defend; Influence = influence; Explore = explore;
        EmpowerAttack = empowerAttack; EmpowerDefend = empowerDefend;
        EmpowerInfluence = empowerInfluence; EmpowerExplore = empowerExplore;
        ConvertTo = convertTo;
        ConvertFrom = convertFrom;
        ConvertRequiresEmpower = convertRequiresEmpower;
    }

    public int BaseOf(StatType single)
    {
        if (single == StatType.Attack) return Attack;
        if (single == StatType.Defend) return Defend;
        if (single == StatType.Influence) return Influence;
        if (single == StatType.Explore) return Explore;
        return 0;
    }

    public int EmpowerOf(StatType single)
    {
        if (single == StatType.Attack) return EmpowerAttack;
        if (single == StatType.Defend) return EmpowerDefend;
        if (single == StatType.Influence) return EmpowerInfluence;
        if (single == StatType.Explore) return EmpowerExplore;
        return 0;
    }
}
