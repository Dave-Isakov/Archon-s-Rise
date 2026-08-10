using System.Collections.Generic;

// Which actions a place offers right now, in canonical order. Pure: no scene,
// no Unity. Order is authored here so the fan never reshuffles between opens.
//
// The turn's action gates every SERVICE but never the ledger — opening a place
// is a free peek (spec 2026-07-22), so a player who already acted can still look.
public static class PlaceActionRules
{
    public static List<PlaceAction> ForTown(TownActionSnapshot s)
    {
        var list = new List<PlaceAction>();

        if (!s.Conquered)
        {
            // The badge is a guardian COUNT, so it carries no cost icon.
            list.Add(new PlaceAction(PlaceActionId.Assault, IconConcept.Attack,
                null, s.GuardiansRemaining, s.VisitCanAct));
        }
        else
        {
            var allowed = PlaceRules.AllowedServices(s.PlaceType);

            if ((allowed & PlaceService.Recruit) != 0)
                list.Add(new PlaceAction(PlaceActionId.Recruit, IconConcept.Army,
                    null, 0, s.AnyUnitAffordable && s.VisitCanAct));

            if ((allowed & PlaceService.Heal) != 0)
                list.Add(new PlaceAction(PlaceActionId.Heal, IconConcept.Heal,
                    IconConcept.Influence, s.HealCost,
                    s.Influence >= s.HealCost && s.VisitCanAct));

            if (s.SellsCards)
                list.Add(new PlaceAction(PlaceActionId.Cards, IconConcept.Card,
                    IconConcept.Influence, s.CardCost,
                    s.Influence >= s.CardCost && s.VisitCanAct));

            // Which COLORS are available is shown inside the pop-out; the price
            // is per-crystal and identical whatever the colour, so it belongs on
            // the slot. CrystalButton always gated on it and influence does not
            // clamp at zero, so dropping the gate would sell into the negative.
            if ((allowed & PlaceService.Crystal) != 0)
                list.Add(new PlaceAction(PlaceActionId.Crystal, IconConcept.Crystal,
                    IconConcept.Influence, s.CrystalCost,
                    s.Influence >= s.CrystalCost && s.VisitCanAct));
        }

        AppendMenu(list, s.HasMenu);
        return list;
    }

    public static List<PlaceAction> ForDungeon(DungeonActionSnapshot s)
    {
        var list = new List<PlaceAction>();
        if (!s.Complete)
            list.Add(new PlaceAction(PlaceActionId.Delve, IconConcept.Dungeon,
                IconConcept.Explore, s.DelveCost,
                s.Explore >= s.DelveCost && s.VisitCanAct));

        AppendMenu(list, s.HasMenu);
        return list;
    }

    // A shrine shows one slot. Live or spent, that is Engage (the crystal
    // bargain), locked when it can't be taken rather than firing a message, so
    // the click is never a no-op. Once a sour bargain leaves a guardian on it,
    // the slot becomes Assault instead: the fight, at no further crystal cost.
    // Shrines have no detail menu, hence no ledger slot.
    //
    // The Assault slot is deliberately NEVER disabled. A player who already
    // spent the turn's action may still open the guardian to read it — that
    // opens preview-only, which is the dispatcher's call, not a reason to hide
    // the button (2026-07-31).
    public static List<PlaceAction> ForShrine(ShrineActionSnapshot s)
    {
        var list = new List<PlaceAction>();

        if (s.IsGuarded)
            list.Add(new PlaceAction(PlaceActionId.Assault, IconConcept.Attack, null, 0, true));
        else
            list.Add(new PlaceAction(PlaceActionId.Engage, IconConcept.Crystal,
                IconConcept.Crystal, s.CrystalCost, s.IsLive && s.VisitCanAct));

        AppendMenu(list, false);
        return list;
    }

    // The ledger slot: always last, always enabled, and only for places that
    // actually have a detail menu (shrines do not, so they get no dead button).
    static void AppendMenu(List<PlaceAction> list, bool hasMenu)
    {
        if (hasMenu)
            list.Add(new PlaceAction(PlaceActionId.OpenMenu, IconConcept.Menu, null, 0, true));
    }
}
