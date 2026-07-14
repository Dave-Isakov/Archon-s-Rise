using System.Linq;

// Display order for the read-only card list: grouped by card type (ascending
// enum value), then by name, stable for equal keys. Returns indices into the
// parallel input arrays so callers can map back to their card data. Draw-pile
// order is deliberately discarded — the list must never reveal upcoming draws.
// Pure, no scene dependency.
public static class CardListPlan
{
    public static int[] Order(StatType[] types, string[] names)
    {
        return Enumerable.Range(0, types.Length)
            .OrderBy(i => (int)types[i])
            .ThenBy(i => names[i], System.StringComparer.Ordinal)
            .ToArray();
    }
}
