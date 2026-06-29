using System.Collections.Generic;

public class CardPlaySelection
{
    static readonly StatType[] ActionStats =
        { StatType.Attack, StatType.Defend, StatType.Influence, StatType.Explore };
    const int ImproviseValue = 1;

    readonly CardSnapshot _card;

    public PlayMode Mode { get; private set; }
    public StatType ChoiceStat { get; private set; }
    public StatType ImproviseStat { get; private set; }
    public bool Empowered { get; private set; }

    public CardPlaySelection(CardSnapshot card)
    {
        _card = card;
        Mode = PlayMode.Normal;
        ChoiceStat = FirstFlag(card.CardType);
        ImproviseStat = StatType.Attack;
        Empowered = false;
    }

    public void SetMode(PlayMode mode) => Mode = mode;

    public void SetChoiceStat(StatType stat)
    {
        ChoiceStat = stat;
        Mode = PlayMode.Choice;
    }

    public void SetImproviseStat(StatType stat)
    {
        ImproviseStat = stat;
        Mode = PlayMode.Improvise;
    }

    public void SetEmpowered(bool value) => Empowered = value;

    public bool CanEmpower() =>
        _card.EmpowerType != EmpowerType.None && Mode != PlayMode.Improvise;

    public bool EffectiveEmpowered() => Empowered && CanEmpower();

    public bool IsPlayable()
    {
        foreach (var s in ActionStats)
            if (_card.CardType.HasFlag(s)) return true;
        return false;
    }

    public StatType ResolvedStat() =>
        Mode == PlayMode.Improvise ? ImproviseStat : ChoiceStat;

    public int[] ResolveStats()
    {
        var result = new int[4]; // [attack, defend, influence, explore]
        bool emp = EffectiveEmpowered();

        switch (Mode)
        {
            case PlayMode.Improvise:
                AddStat(result, ImproviseStat, ImproviseValue);
                break;

            case PlayMode.Choice:
                AddStat(result, ChoiceStat, emp ? _card.EmpowerOf(ChoiceStat) : _card.BaseOf(ChoiceStat));
                break;

            default: // Normal — every set action flag contributes
                foreach (var s in ActionStats)
                    if (_card.CardType.HasFlag(s))
                        AddStat(result, s, emp ? _card.EmpowerOf(s) : _card.BaseOf(s));
                break;
        }
        return result;
    }

    public string Describe()
    {
        var parts = new List<string>();
        var stats = ResolveStats();
        string[] names = { "Attack", "Defend", "Influence", "Explore" };
        for (int i = 0; i < 4; i++)
            if (stats[i] != 0) parts.Add($"+{stats[i]} {names[i]}");

        if (parts.Count == 0) return "—"; // em dash for no output
        string body = string.Join(", ", parts);
        return Mode == PlayMode.Improvise ? body + " (improvised)" : body;
    }

    static void AddStat(int[] result, StatType single, int value)
    {
        if (single == StatType.Attack)    result[0] += value;
        else if (single == StatType.Defend)    result[1] += value;
        else if (single == StatType.Influence) result[2] += value;
        else if (single == StatType.Explore)   result[3] += value;
    }

    static StatType FirstFlag(StatType type)
    {
        foreach (var s in ActionStats)
            if (type.HasFlag(s)) return s;
        return StatType.Attack;
    }
}
