// Row/label text for a unit option. UI-framework-free so it is mcs-testable
// and reusable by the pop-out rows, the Use bar, and the recruit panel.
public static class UnitOptionText
{
    public static string Describe(UnitOption o)
    {
        string body = o.effect == UnitEffect.Crystallize
            ? $"Crystallize: {o.amount} {o.grantColor}"
            : $"{o.effect} {o.amount}";

        if (o.crystalCost == EmpowerType.None) return body;
        string cost = o.crystalCost.IsAllColors() ? "1 crystal (any color)" : $"1 {o.crystalCost} crystal";
        return $"{body} — {cost}";
    }
}
