namespace ArchonsRise.Hotspots
{
    // Pure crystal-hotspot charge math (spec 2026-07-24). Unity-free so it is
    // mcs-CLI-testable (DungeonRules pattern). Sentinel: remaining == -1 means
    // an unlimited "rich vein" that never depletes.
    public static class HotspotRules
    {
        public const int Unlimited = -1;

        // A hotspot yields a crystal while it has charges left, or forever when
        // unlimited. A depleted (0) tile is dormant.
        public static bool CanHarvest(int remaining) => remaining != 0;

        // Charges after one harvest: unlimited stays unlimited; a finite count
        // steps down and never goes negative.
        public static int NextCharges(int remaining)
            => remaining == Unlimited ? Unlimited : (remaining > 0 ? remaining - 1 : 0);
    }
}
