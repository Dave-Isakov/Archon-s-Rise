using UnityEngine;

// The one locked/unaffordable treatment (spec 2026-07-15): dim to 0.4 alpha,
// matching UnitOptionRow. Callers keep their Button.interactable behavior;
// this only standardizes the look. Null-safe so unwired panels degrade to
// their current appearance instead of throwing.
public static class UiLock
{
    public const float LockedAlpha = 0.4f;

    public static void Apply(CanvasGroup group, bool locked)
    {
        if (group != null) group.alpha = locked ? LockedAlpha : 1f;
    }
}
