using System.Collections.Generic;

// Pure preview-visibility rules. No scene/Unity dependency so the blind gate
// and the whole-panel aggregation are unit-testable via the CLI mcs harness
// (mirrors CombatRules).
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

    // Keeps a UI panel fully on-screen on one axis (used by EnemyTraitTooltip).
    // Given the panel's lower edge, its extent, the screen extent, and a margin,
    // returns the lower edge clamped so the whole panel stays within
    // [margin, screenSize - margin]. If the panel is too big to fit, it pins to
    // the margin (near edge visible). Float-only so it stays Unity-free; the
    // caller invokes it once per axis.
    public static float ClampAxis(float lowerEdge, float size, float screenSize, float margin)
    {
        float max = screenSize - margin - size;
        if (max < margin) return margin;      // too big to fit → pin to near edge
        if (lowerEdge < margin) return margin;
        if (lowerEdge > max) return max;
        return lowerEdge;
    }
}
