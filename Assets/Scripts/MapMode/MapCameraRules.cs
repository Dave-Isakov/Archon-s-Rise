// Pure pan/clamp arithmetic for map mode (spec 2026-07-31). Floats only, no
// UnityEngine dependency, so the CLI test harness needs no UnityEngine.dll and
// the rules are trivially unit-testable while the editor holds its lock.
public static class MapCameraRules
{
    // Scales an input vector with magnitude > 1 down to exactly 1, so holding two
    // keys diagonally never pans faster than holding one. Sub-unit input (analogue
    // stick part-way) passes through untouched.
    public static void NormalizeInput(float ix, float iy, out float nx, out float ny)
    {
        float sqr = ix * ix + iy * iy;
        if (sqr <= 1f)
        {
            nx = ix;
            ny = iy;
            return;
        }
        float len = (float)System.Math.Sqrt(sqr);
        nx = ix / len;
        ny = iy / len;
    }

    // One frame of pan on one axis: integrate, then clamp. Clamping after the add
    // (rather than the caller clamping later) means a camera already outside the
    // limits is pulled back even when the input is zero.
    public static float StepAxis(float current, float input, float speed, float dt,
        float min, float max)
        => ClampAxis(current + input * speed * dt, min, max);

    // min > max means a degenerate map — a single row/column of terrain, or none at
    // all. Locking to the midpoint keeps that axis centred instead of returning NaN.
    public static float ClampAxis(float v, float min, float max)
    {
        if (min > max) return (min + max) * 0.5f;
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }
}
