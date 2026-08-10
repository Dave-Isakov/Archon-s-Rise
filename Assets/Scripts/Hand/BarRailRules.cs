using System.Collections.Generic;

public enum BarLane { Units, Cards }

// Where focus sits on the bar. Index < 0 means nothing is focusable.
public readonly struct RailPos
{
    public readonly BarLane Lane;
    public readonly int Index;

    public RailPos(BarLane lane, int index) { Lane = lane; Index = index; }

    public bool IsNone => Index < 0;
    public static RailPos None => new RailPos(BarLane.Cards, -1);
}

// Pure rail rules for the bottom bar. The bar is two lanes — units on the left,
// cards on the right — walked as one left/right continuum. Flattening them into
// a single virtual list is what makes crossing the boundary an ordinary step:
// HandNavRules already skips blocked entries and wraps, and it does both here
// over the whole bar rather than one lane. `true` in a mask means "skip".
public static class BarRailRules
{
    public static RailPos Step(RailPos current, int direction,
        IReadOnlyList<bool> unitsBlocked, IReadOnlyList<bool> cardsBlocked)
    {
        int flat = HandNavRules.Step(ToFlat(current, unitsBlocked.Count), direction,
            Combine(unitsBlocked, cardsBlocked));
        return FromFlat(flat, unitsBlocked.Count, cardsBlocked.Count);
    }

    public static RailPos ClampAfterChange(RailPos previous,
        IReadOnlyList<bool> unitsBlocked, IReadOnlyList<bool> cardsBlocked)
    {
        int flat = HandNavRules.ClampAfterChange(ToFlat(previous, unitsBlocked.Count),
            Combine(unitsBlocked, cardsBlocked));
        return FromFlat(flat, unitsBlocked.Count, cardsBlocked.Count);
    }

    static List<bool> Combine(IReadOnlyList<bool> units, IReadOnlyList<bool> cards)
    {
        var combined = new List<bool>(units.Count + cards.Count);
        for (int i = 0; i < units.Count; i++) combined.Add(units[i]);
        for (int i = 0; i < cards.Count; i++) combined.Add(cards[i]);
        return combined;
    }

    static int ToFlat(RailPos pos, int unitCount)
    {
        if (pos.IsNone) return -1;
        return pos.Lane == BarLane.Units ? pos.Index : unitCount + pos.Index;
    }

    static RailPos FromFlat(int flat, int unitCount, int cardCount)
    {
        if (flat < 0 || flat >= unitCount + cardCount) return RailPos.None;
        return flat < unitCount
            ? new RailPos(BarLane.Units, flat)
            : new RailPos(BarLane.Cards, flat - unitCount);
    }
}
