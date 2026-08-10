using System;
using System.Collections.Generic;

public static class ShopRules
{
    public static List<T> PickUnique<T>(IReadOnlyList<T> pool, int count, Func<int, int> rng)
        where T : class
    {
        var picked = new List<T>();
        if (pool == null || count <= 0) return picked;

        var distinct = new List<T>();
        for (int i = 0; i < pool.Count; i++)
        {
            var entry = pool[i];
            if (entry == null) continue;
            if (!Contains(distinct, entry)) distinct.Add(entry);
        }

        int take = Math.Min(count, distinct.Count);
        for (int i = 0; i < take; i++)
        {
            int j = i + rng(distinct.Count - i);
            var swap = distinct[j];
            distinct[j] = distinct[i];
            distinct[i] = swap;
            picked.Add(swap);
        }
        return picked;
    }

    static bool Contains<T>(List<T> list, T entry) where T : class
    {
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], entry)) return true;
        return false;
    }
}
