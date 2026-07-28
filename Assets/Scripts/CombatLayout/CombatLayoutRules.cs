using System;

// Pure geometry for arranging enemy cards during combat (spec 2026-07-27). No
// UnityEngine dependency so it compiles under the mcs pure-test harness, like
// CombatRules / DefeatFxMath. The applier in CombatController converts Slot into
// anchoredPosition / scale / rotation, in a space where (0,0) is the player.
public static class CombatLayoutRules
{
    // One card's placement relative to the fan's centre: +X right, +Y up, in
    // pixels; Scale is uniform; TiltDeg is a Z rotation (degrees).
    public struct Slot { public float X, Y, Scale, TiltDeg; }

    // A plain point (px, +X right / +Y up). Also used for the deltas the
    // cluster-safety passes return.
    public struct Anchor { public float X, Y; }

    // An axis-aligned box in the same pixel space. Used for the card cluster's
    // footprint, the on-screen safe area, and the parked buttons' keep-out.
    public struct Box
    {
        public float MinX, MinY, MaxX, MaxY;
        public Box(float minX, float minY, float maxX, float maxY)
        { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }

        public static Box FromCentre(float cx, float cy, float width, float height)
            => new Box(cx - width * 0.5f, cy - height * 0.5f, cx + width * 0.5f, cy + height * 0.5f);

        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;
    }

    // Cards past this count shrink so a large roster never overlaps.
    public const int CrowdThreshold = 3;

    // Degrees between neighbouring slots. The fan widens with the roster instead
    // of respreading a fixed width, so 2 enemies stay tight overhead and 4 land
    // on the wide ring the placement spec pictures (spec 2026-07-27).
    public const float SlotSpacingDegrees = 55f;

    // Hard ceiling on the fan spread. At 180 the outermost cards sit exactly on
    // the centre's horizontal; anything wider would swing them below it.
    public const float MaxArcDegrees = 170f;

    // Uniform scale for a fight of `count` cards: full up to the crowd threshold,
    // then a gentle step-down floored so cards never vanish.
    public static float ScaleFor(int count)
    {
        if (count <= CrowdThreshold) return 1f;
        float s = 1f - 0.12f * (count - CrowdThreshold);
        return s < 0.6f ? 0.6f : s;
    }

    // Total fan width for `count` cards at `spacingDeg` between neighbours,
    // capped at MaxArcDegrees. 1 -> 0 (straight up), 2 -> 55, 3 -> 110,
    // 4 -> 165, 5+ -> 170 with the gaps tightening.
    public static float FanArc(int count, float spacingDeg)
    {
        if (count <= 1) return 0f;
        float spacing = spacingDeg < 0f ? 0f : spacingDeg;
        float arc = spacing * (count - 1);
        return arc > MaxArcDegrees ? MaxArcDegrees : arc;
    }

    // Angle (degrees from +X, CCW) of card `index` of `count`, fanned evenly
    // across `arcDegrees` and centred on straight-up (90). Index 0 is leftmost
    // (angle > 90); the last index is rightmost (angle < 90). The spread is
    // clamped to [0, MaxArcDegrees] so no slot ever drops below the centre.
    public static float AngleFor(int index, int count, float arcDegrees)
    {
        if (count <= 1) return 90f;
        float arc = ClampArc(arcDegrees);
        float step = arc / (count - 1);
        return 90f + arc * 0.5f - step * index;
    }

    public static float ClampArc(float arcDegrees)
    {
        if (arcDegrees < 0f) return 0f;
        return arcDegrees > MaxArcDegrees ? MaxArcDegrees : arcDegrees;
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

    // Convenience: the slot for a fan whose width comes from the spacing rule.
    public static Slot SpacedSlotFor(int index, int count, float radius, float spacingDeg)
        => SlotFor(index, count, radius, FanArc(count, spacingDeg));

    // --- Cluster safety -----------------------------------------------------
    // Both passes return a delta applied to the WHOLE cluster, so the fan
    // translates rigidly and cards never move relative to one another.

    // Delta that slides `cluster` fully inside `safe`. Zero when it already is.
    // A cluster larger than the safe area on an axis is pinned to that axis'
    // min edge rather than jittering between two impossible constraints.
    public static Anchor ShiftIntoSafeArea(Box cluster, Box safe)
    {
        Anchor d;
        d.X = AxisShift(cluster.MinX, cluster.MaxX, safe.MinX, safe.MaxX);
        d.Y = AxisShift(cluster.MinY, cluster.MaxY, safe.MinY, safe.MaxY);
        return d;
    }

    static float AxisShift(float lo, float hi, float safeLo, float safeHi)
    {
        if (hi - lo > safeHi - safeLo) return safeLo - lo; // too big to fit: pin to the min edge
        if (lo < safeLo) return safeLo - lo;
        if (hi > safeHi) return safeHi - hi;
        return 0f;
    }

    // Delta that pushes `cluster` clear of `keepOut` (the parked buttons). Zero
    // when they are already disjoint. Vertical escapes are preferred over
    // horizontal at equal cost — the play space is wider than it is tall — and
    // up wins over down so a push never drives cards toward the hand.
    public static Anchor ShiftOutOfKeepOut(Box cluster, Box keepOut)
    {
        Anchor d;
        d.X = 0f; d.Y = 0f;
        if (cluster.MinX >= keepOut.MaxX || cluster.MaxX <= keepOut.MinX
            || cluster.MinY >= keepOut.MaxY || cluster.MaxY <= keepOut.MinY)
            return d; // disjoint

        float up = keepOut.MaxY - cluster.MinY;    // > 0
        float down = keepOut.MinY - cluster.MaxY;  // < 0
        float right = keepOut.MaxX - cluster.MinX; // > 0
        float left = keepOut.MinX - cluster.MaxX;  // < 0

        float bestV = up <= -down ? up : down;              // ties -> up
        float bestH = right <= -left ? right : left;        // ties -> right
        if (Math.Abs(bestV) <= Math.Abs(bestH)) d.Y = bestV; // ties -> vertical
        else d.X = bestH;
        return d;
    }
}
