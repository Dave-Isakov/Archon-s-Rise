// One authored option on a unit (spec 2026-07-09). crystalCost None = free;
// a color = requires 1 crystal of that color (wild satisfies, same rule as
// card empower); all-colors = any 1 crystal. grantColor only matters for
// Crystallize. influenceCost (spec 2026-07-14) is an in-turn Influence price;
// an option costs a crystal OR influence, never both.
[System.Serializable]
public class UnitOption
{
    public UnitEffect effect;
    public int amount = 1;
    public EmpowerType grantColor;
    public EmpowerType crystalCost;
    public int influenceCost;
}
