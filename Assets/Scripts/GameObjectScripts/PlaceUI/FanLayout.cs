using System.Collections.Generic;
using UnityEngine;

// The shared arc renderer (spec 2026-07-28). Owns ONLY geometry: given some
// RectTransforms and the fan settings, seat them along the arc in order.
//
// Deliberately not a MonoBehaviour and deliberately ignorant of what the seats
// mean — ShrinePanel's seats are cycling payment slots, PlaceFan's are actions.
// Sharing geometry is the whole win; sharing semantics would force a mode flag
// into both.
//
// No per-hex projection is needed by either caller: a place is entered by
// STANDING on it and the camera rides PlayerPosition, so the place is always
// screen centre. Park the container at its authored offset and the arc lands.
public static class FanLayout
{
    public static void Place(IReadOnlyList<RectTransform> items, FanSettings fan)
    {
        if (items == null || items.Count == 0) return;
        var solved = FanMath.Solve(items.Count, fan);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;
            items[i].anchoredPosition = solved[i].AnchoredPosition;
            items[i].localRotation = Quaternion.Euler(0f, 0f, solved[i].TiltZ);
        }
    }
}
