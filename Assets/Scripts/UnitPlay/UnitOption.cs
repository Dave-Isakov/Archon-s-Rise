// One authored option on a unit (spec 2026-07-09). crystalCost None = free;
// a color = requires 1 crystal of that color (wild satisfies, same rule as
// card empower); all-colors = any 1 crystal. grantColor only matters for
// Crystallize.
[System.Serializable]
public class UnitOption
{
    public UnitEffect effect;
    public int amount = 1;
    public EmpowerType grantColor;
    public EmpowerType crystalCost;
}
