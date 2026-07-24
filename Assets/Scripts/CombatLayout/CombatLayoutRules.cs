using System;

// Pure geometry for arranging enemy cards in a semicircle around the
// screen-centre player during combat (spec 2026-07-24). No UnityEngine
// dependency so it compiles under the mcs pure-test harness, like CombatRules /
// DefeatFxMath. The applier in CombatController converts Slot into
// anchoredPosition / scale / rotation.
public static class CombatLayoutRules
{
    // One card's placement in the arc origin's local UI space: +X right, +Y up,
    // in pixels; Scale is uniform; TiltDeg is a Z rotation (degrees).
    public struct Slot { public float X, Y, Scale, TiltDeg; }

    // Cards past this count shrink so a large roster never overlaps.
    public const int CrowdThreshold = 3;

    // Uniform scale for a fight of `count` cards: full up to the crowd threshold,
    // then a gentle step-down floored so cards never vanish.
    public static float ScaleFor(int count)
    {
        if (count <= CrowdThreshold) return 1f;
        float s = 1f - 0.12f * (count - CrowdThreshold);
        return s < 0.6f ? 0.6f : s;
    }

    // Angle (degrees from +X, CCW) of card `index` of `count`, fanned evenly
    // across `arcDegrees` and centred on straight-up (90). Index 0 is leftmost
    // (angle > 90); the last index is rightmost (angle < 90).
    public static float AngleFor(int index, int count, float arcDegrees)
    {
        if (count <= 1) return 90f;
        float step = arcDegrees / (count - 1);
        return 90f + arcDegrees * 0.5f - step * index;
    }

    // A plain UI anchor point (px, +X right / +Y up) in the arc origin's local
    // space. Separate from Slot because the advance button needs only a position.
    public struct Anchor { public float X, Y; }

    // The advance-button anchor: mirror the enemy cluster's centroid through the
    // origin (screen-centre = the player) and sit `distance` px out along that
    // ray, so the button rests opposite the enemies near the heart of combat
    // (spec 2026-07-24 phase controls). A centroid at the origin is degenerate —
    // fall straight down so the anchor is always defined.
    public static Anchor OppositeAnchor(float centroidX, float centroidY, float distance)
    {
        double len = Math.Sqrt(centroidX * centroidX + centroidY * centroidY);
        Anchor a;
        if (len < 1e-4)
        {
            a.X = 0f;
            a.Y = -distance;
            return a;
        }
        a.X = -(float)(centroidX / len) * distance;
        a.Y = -(float)(centroidY / len) * distance;
        return a;
    }

    public static Slot SlotFor(int index, int count, float radius, float arcDegrees)
    {
        float deg = AngleFor(index, count, arcDegrees);
        double rad = deg * Math.PI / 180.0;
        Slot slot;
        slot.X = radius * (float)Math.Cos(rad);
        slot.Y = radius * (float)Math.Sin(rad);
        slot.Scale = ScaleFor(count);
        slot.TiltDeg = (90f - deg) * 0.15f; // slight outward lean along the arc
        return slot;
    }
}
