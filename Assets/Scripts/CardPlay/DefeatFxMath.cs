// Pure timing math for the defeat FX (spec 2026-07-21, Spec 2). The trig for
// the actual shake oscillation stays in the MonoBehaviour; this exposes the
// testable envelope + dissolve ramp, matching the GlowPulse pure-helper style.
public static class DefeatFxMath
{
    // Linearly-decaying shake amplitude: full at t=0, 0 at/after duration.
    public static float ShakeEnvelope(float t, float duration, float amplitude)
    {
        if (duration <= 0f || t >= duration) return 0f;
        if (t <= 0f) return amplitude;
        return amplitude * (1f - t / duration);
    }

    // Normalized dissolve/fade progress 0->1 over duration, clamped.
    public static float DissolveProgress(float t, float duration)
    {
        if (duration <= 0f) return 1f;
        float p = t / duration;
        if (p < 0f) return 0f;
        if (p > 1f) return 1f;
        return p;
    }
}
