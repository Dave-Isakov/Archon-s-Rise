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
    Crystal = 64
}
