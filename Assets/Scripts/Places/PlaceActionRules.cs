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

            // M2 stub: the slot is shown so the place reports itself honestly,
            // but buying is disabled until the purchase economics land.
            if ((allowed & PlaceService.Cards) != 0)
                list.Add(new PlaceAction(PlaceActionId.Cards, IconConcept.Card,
                    null, 0, false));

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

    // The ledger slot: always last, always enabled, and only for places that
    // actually have a detail menu (shrines do not, so they get no dead button).
    static void AppendMenu(List<PlaceAction> list, bool hasMenu)
    {
        if (hasMenu)
            list.Add(new PlaceAction(PlaceActionId.OpenMenu, IconConcept.Menu, null, 0, true));
    }
}
