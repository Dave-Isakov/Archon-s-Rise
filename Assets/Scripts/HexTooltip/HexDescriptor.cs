namespace ArchonsRise.HexTooltipInfo
{
    // What a hex occupant reports to the tooltip: one icon-marked line plus a
    // priority (highest wins when a cell is described by more than one source).
    // Pure data — no UnityEngine, no UI.
    public readonly struct HexDescriptor
    {
        public readonly string Line;
        public readonly int Priority;
        public HexDescriptor(string line, int priority) { Line = line; Priority = priority; }

        public bool IsEmpty => string.IsNullOrEmpty(Line);
        public static readonly HexDescriptor None = new HexDescriptor(null, int.MinValue);
    }
}
