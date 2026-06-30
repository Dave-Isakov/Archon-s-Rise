using UnityEngine;

public static class StatPalette
{
    static readonly Color Attack    = new Color32(0xff, 0x5a, 0x5a, 0xff); // red
    static readonly Color Defend    = new Color32(0xb0, 0x6b, 0xff, 0xff); // purple
    static readonly Color Influence = new Color32(0xff, 0xd2, 0x4d, 0xff); // yellow
    static readonly Color Explore   = new Color32(0x54, 0xd9, 0x8c, 0xff); // green

    public static readonly Color Empower = new Color32(0x5f, 0xd0, 0xe6, 0xff); // cyan
    public static readonly Color Muted   = new Color32(0xae, 0xb8, 0xcc, 0xff); // unselected base
    public static readonly Color Locked  = new Color32(0x39, 0x40, 0x5a, 0xff); // dim/locked tint

    public static Color For(StatType stat)
    {
        if (stat == StatType.Attack)    return Attack;
        if (stat == StatType.Defend)    return Defend;
        if (stat == StatType.Influence) return Influence;
        if (stat == StatType.Explore)   return Explore;
        return Muted;
    }
}
