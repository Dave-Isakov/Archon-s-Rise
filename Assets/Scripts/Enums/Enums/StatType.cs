using System;

[Flags]
public enum StatType
{
    None = 0,
    Attack = 1,
    Defend = 2,
    Explore = 4,
    Influence = 8,
    Heal = 16,
    Wound = 32,
    Crystal = 64,
    Siege = 128,
    // Immediate effect flag (like Heal/Crystal, not a per-turn pool): the card
    // readies spent units via the refresh picker (spec 2026-07-14).
    Refresh = 256
}
