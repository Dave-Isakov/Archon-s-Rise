using UnityEngine;

// Pure fan-arc solver. Given a card count and geometry, returns each card's
// local position + tilt. Index 0 is the leftmost card. No scene dependency.
public static class FanMath
{
    public static FanSlot[] Solve(int count, FanSettings s)
    {
        var slots = new FanSlot[count < 0 ? 0 : count];
        if (count <= 0) return slots;

        float spacing = EffectiveSpacing(count, s);

        for (int i = 0; i < count; i++)
        {
            // t in [-0.5, 0.5]; single card -> 0 (centred).
            float t = count == 1 ? 0f : (float)i / (count - 1) - 0.5f;

            float x = (i - (count - 1) * 0.5f) * spacing;
            float y = -s.ArcDrop * (2f * t) * (2f * t); // parabolic dip, edges lowest
            float tilt = -t * s.SpreadDegrees;           // leftmost -> +half-spread

            slots[i] = new FanSlot(new Vector2(x, y), tilt);
        }
        return slots;
    }

    // MaxWidth caps the centre-to-centre span, not the total pixel width — the
    // solver knows slot positions, not item dimensions. An oversized hand
    // tightens rather than sprawling off screen.
    static float EffectiveSpacing(int count, FanSettings s)
    {
        if (count <= 1 || s.MaxWidth <= 0f) return s.CardSpacing;
        return Mathf.Min(s.CardSpacing, s.MaxWidth / (count - 1));
    }
}
