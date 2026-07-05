using System.Collections.Generic;

// Pure preview-visibility rules. No scene/Unity dependency so the blind gate,
// the whole-panel aggregation, and the roster-tail math are unit-testable via
// the CLI mcs harness (mirrors CombatRules). The MonoBehaviour panel reads
// EnemiesSO and passes these primitive values in.
public static class PreviewRules
{
    // The single blind gate. One enemy is previewable unless something hides it.
    // Returns true for every enemy today (nothing sets the flag); a future
    // blindness source (enemy trait, player debuff, map fog) passes true here.
    public static bool CanPreview(bool enemyHiddenFromPreview = false)
        => !enemyHiddenFromPreview;

    // Encounter-level aggregation: the panel shows stats only when EVERY
    // previewed enemy is visible. Any blind enemy blinds the whole panel.
    public static bool EncounterVisible(IReadOnlyList<bool> perEnemyVisible)
    {
        for (int i = 0; i < perEnemyVisible.Count; i++)
            if (!perEnemyVisible[i]) return false;
        return true;
    }

    // The guardians still standing: the tail of the roster after the first
    // defeatedCount have fallen. Generic so it stays Unity-free and testable.
    public static IReadOnlyList<T> RemainingGuardians<T>(IReadOnlyList<T> roster, int defeatedCount)
    {
        int start = defeatedCount < 0 ? 0 : defeatedCount;
        var remaining = new List<T>();
        for (int i = start; i < roster.Count; i++)
            remaining.Add(roster[i]);
        return remaining;
    }
}
