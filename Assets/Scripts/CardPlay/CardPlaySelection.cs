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
        ChoiceStat = FirstFlag(card.CardType);
        // Choice cards must resolve to a single picked stat, never the Normal-mode sum of
        // every flag. Start them in Choice mode on the first stat (the banner shows it
        // selected and lets the player switch); everything else starts Normal.
        Mode = card.IsChoice ? PlayMode.Choice : PlayMode.Normal;
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

    // Playable if the card produces any usable effect. Action-stat cards, plus
    // Crystal and Heal cards (which carry no action flags and resolve through the
    // Normal play route). Wounds are the only unplayable card.
    public bool IsPlayable()
    {
        if (_card.CardType.HasFlag(StatType.Wound)) return false;
        if (_card.CardType.HasFlag(StatType.Crystal)) return true;
        if (_card.CardType.HasFlag(StatType.Heal)) return true;
        foreach (var s in ActionStats)
            if (_card.CardType.HasFlag(s)) return true;
        return false;
    }

    public StatType ResolvedStat() =>
        Mode == PlayMode.Improvise ? ImproviseStat : ChoiceStat;

    public int[] ResolveStats() => PreviewStats(EffectiveEmpowered());

    // Read-only preview: the [atk,def,inf,exp] this selection would apply if its
    // empower flag were `empowered`. Does not mutate. Improvise ignores `empowered`
    // (flat +1). Used live by ResolveStats and by the Empower panel's "+N -> +N".
    public int[] PreviewStats(bool empowered)
    {
        var result = new int[4]; // [attack, defend, influence, explore]

        switch (Mode)
        {
            case PlayMode.Improvise:
                AddStat(result, ImproviseStat, ImproviseValue);
                break;

            case PlayMode.Choice:
                AddStat(result, ChoiceStat, empowered ? _card.EmpowerOf(ChoiceStat) : _card.BaseOf(ChoiceStat));
                break;

            default: // Normal — every set action flag contributes
                foreach (var s in ActionStats)
                    if (_card.CardType.HasFlag(s))
                        AddStat(result, s, empowered ? _card.EmpowerOf(s) : _card.BaseOf(s));
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
