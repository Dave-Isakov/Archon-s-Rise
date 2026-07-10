// Pure rules for the place-type system. Thresholds and service tables live
// here so balance is centralized. No scene dependency.
public static class PlaceRules
{
    // Fleeing an assault with guardians remaining (vs. 1 wound for field-combat flee).
    public const int RetreatWoundCount = 3;

    public static PlaceService AllowedServices(PlaceType type)
    {
        // Crystal is sold at every Place — Influence, not place type, limits how
        // many crystals the player can buy (decision 2026-07-10).
        switch (type)
        {
            case PlaceType.Keep: return PlaceService.Recruit | PlaceService.Crystal;
            case PlaceType.Castle: return PlaceService.Recruit | PlaceService.Heal | PlaceService.Cards | PlaceService.Crystal;
            default: return PlaceService.Recruit | PlaceService.Heal | PlaceService.Crystal; // Town
        }
    }

    public static bool IsConquered(int defeatedCount, int rosterSize)
        => defeatedCount >= rosterSize;
}
