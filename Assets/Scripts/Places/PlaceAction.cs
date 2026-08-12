// One slot on a place fan: what glyph it shows, what it costs, and whether it
// can be pressed right now. Pure — the fan renders this, the token dispatches it.
public readonly struct PlaceAction
{
    public readonly PlaceActionId Id;
    public readonly IconConcept Icon;
    // null renders the amount as a bare number (e.g. guardians remaining, which
    // is a count, not a price). Ignored entirely when CostAmount is 0.
    public readonly IconConcept? CostIcon;
    public readonly int CostAmount;   // 0 = no badge
    public readonly bool Enabled;     // false => UiLock dim + non-interactable

    public PlaceAction(PlaceActionId id, IconConcept icon, IconConcept? costIcon,
        int costAmount, bool enabled)
    {
        Id = id;
        Icon = icon;
        CostIcon = costIcon;
        CostAmount = costAmount;
        Enabled = enabled;
    }
}

public readonly struct TownActionSnapshot
{
    public readonly PlaceType PlaceType;
    public readonly bool Conquered;
    public readonly int GuardiansRemaining;
    public readonly int Influence;
    public readonly int HealCost;
    // TownsSO.resourceLevel: the influence one crystal costs. Player.Influence
    // subtracts without clamping, so the slot has to gate on this or a broke
    // player can buy into negative influence.
    public readonly int CrystalCost;
    public readonly bool SellsCards;
    public readonly int CardCost;
    public readonly bool AnyUnitAffordable;
    public readonly bool AnyWoundToHeal;
    public readonly bool VisitCanAct;
    public readonly bool HasMenu;

    public TownActionSnapshot(PlaceType placeType, bool conquered, int guardiansRemaining,
        int influence, int healCost, int crystalCost, bool sellsCards, int cardCost,
        bool anyUnitAffordable, bool anyWoundToHeal, bool visitCanAct, bool hasMenu)
    {
        PlaceType = placeType;
        Conquered = conquered;
        GuardiansRemaining = guardiansRemaining;
        Influence = influence;
        HealCost = healCost;
        CrystalCost = crystalCost;
        SellsCards = sellsCards;
        CardCost = cardCost;
        AnyUnitAffordable = anyUnitAffordable;
        AnyWoundToHeal = anyWoundToHeal;
        VisitCanAct = visitCanAct;
        HasMenu = hasMenu;
    }
}

public readonly struct DungeonActionSnapshot
{
    public readonly bool Complete;
    public readonly int Explore;
    public readonly int DelveCost;
    public readonly bool VisitCanAct;
    public readonly bool HasMenu;

    public DungeonActionSnapshot(bool complete, int explore, int delveCost,
        bool visitCanAct, bool hasMenu)
    {
        Complete = complete;
        Explore = explore;
        DelveCost = delveCost;
        VisitCanAct = visitCanAct;
        HasMenu = hasMenu;
    }
}

// Two bools rather than ShrineVisualState on purpose: it keeps
// ArchonsRise.Places independent of ArchonsRise.Shrines, and the rule only cares
// whether the shrine can still be engaged and whether a guardian is standing on
// its bargain. Both false = spent (ConsumedDormant).
public readonly struct ShrineActionSnapshot
{
    public readonly bool IsLive;
    public readonly bool IsGuarded;
    public readonly int CrystalCost;
    public readonly bool VisitCanAct;

    public ShrineActionSnapshot(bool isLive, bool isGuarded, int crystalCost, bool visitCanAct)
    {
        IsLive = isLive;
        IsGuarded = isGuarded;
        CrystalCost = crystalCost;
        VisitCanAct = visitCanAct;
    }
}
