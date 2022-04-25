using System;

[Flags]
public enum EmpowerType
{
    None = 0,
    Red = 1,
    Yellow = 2,
    Green = 4,
    Purple = 8,
    All = ~None
}
