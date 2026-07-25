namespace ArchonsRise.HexTooltipInfo
{
    // Pure builder of tooltip occupant lines (spec 2026-07-24, §1b). Every string
    // routes through IconMarkup so glyphs match the HUD; no hand-rolled sprite
    // tags. UnityEngine-free → mcs-testable. IconMarkup/IconConcept/EmpowerType
    // live in the global namespace (referenced via the ArchonsRise.UiLanguage /
    // ArchonsRise.Enums assembly references), so no using directive is needed.
    public static class TileDescriptor
    {
        // Priorities: a stand-on place outranks a passive tile when both claim a
        // cell (they never do in practice, but the tooltip picks deterministically).
        public const int PlacePriority = 30;
        public const int TilePriority = 20;

        public static string Hotspot(EmpowerType color, int remaining)
        {
            string gem = IconMarkup.CrystalTag(color);
            if (remaining == 0) return $"{gem} Depleted";
            string count = remaining < 0 ? "∞" : remaining.ToString();
            return $"{gem} ×{count}";
        }

        public static string Town(string name, IconConcept typeIcon, bool conquered)
        {
            string icon = IconMarkup.Tag(typeIcon);
            return conquered ? $"{icon} {name}" : $"{icon} {name} — guarded";
        }

        public static string Dungeon(string name, int defeated, int total)
        {
            string icon = IconMarkup.Tag(IconConcept.Dungeon);
            return $"{icon} {name} ({defeated}/{total})";
        }
    }
}
