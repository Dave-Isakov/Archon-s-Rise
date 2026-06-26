using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ExtensionClass
{
    public static bool IsBetween<T>(this T item, T start, T end)
    {
    return Comparer<T>.Default.Compare(item, start) >= 0
        && Comparer<T>.Default.Compare(item, end) <= 0;
    }
}
