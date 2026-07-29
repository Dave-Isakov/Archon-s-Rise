using System.Collections.Generic;

// Produces the placement list the combat controller consumes. Today this is a
// pure rule; the unit-wounds phase swaps in a producer that can open the unit
// picker. The CONSUMER depends only on IReadOnlyList<WoundDestination>, which
// is why that phase costs no rework (spec §6.2, §6.3 rule 3).
public static class WoundPlacementRules
{
    public static List<WoundDestination> Place(int handWounds, int discardWounds)
    {
        var list = new List<WoundDestination>();
        for (int i = 0; i < handWounds; i++) list.Add(WoundDestination.Hand);
        for (int i = 0; i < discardWounds; i++) list.Add(WoundDestination.Discard);
        return list;
    }
}
