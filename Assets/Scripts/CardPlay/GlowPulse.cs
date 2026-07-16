using System;

// Adjacency-glow alpha (spec 2026-07-16). Pure/mcs-testable: no UnityEngine
// dependency so the pulse math is unit-tested via the CLI harness. EnemyToken
// feeds Time.time in and applies the result to the halo SpriteRenderer's alpha.
public static class GlowPulse
{
    // Alpha oscillates on a sine between min and max. speed is radians/second.
    public static float Alpha(float time, float min, float max, float speed)
    {
        float t = (float)((Math.Sin(time * speed) + 1.0) * 0.5); // 0..1
        return min + (max - min) * t;
    }
}
