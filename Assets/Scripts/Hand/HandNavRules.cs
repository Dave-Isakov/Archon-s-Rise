using System.Collections.Generic;

// Pure index rules for gamepad/keyboard focus in the hand fan. Cards are given as
// a wound-flag list in fan order; wounds are never focusable (the same rule the
// mouse hit-test applies). Every method returns -1 when nothing is focusable.
public static class HandNavRules
{
    // Nearest focusable card to the middle of the fan; the first navigate press
    // lands here.
    public static int First(IReadOnlyList<bool> wounds) => Nearest(wounds, wounds.Count / 2);

    // Step left (-1) or right (+1) from current, skipping wounds, wrapping at the
    // ends. current == -1 (no focus yet) behaves like First().
    public static int Step(int current, int direction, IReadOnlyList<bool> wounds)
    {
        if (wounds.Count == 0) return -1;
        if (current < 0 || current >= wounds.Count) return First(wounds);
        for (int i = 1; i <= wounds.Count; i++)
        {
            int candidate = ((current + direction * i) % wounds.Count + wounds.Count) % wounds.Count;
            if (!wounds[candidate]) return candidate;
        }
        return -1;
    }

    // After draw/discard/heal/play, keep focus on the nearest focusable card to
    // where it was instead of letting it vanish.
    public static int ClampAfterChange(int previous, IReadOnlyList<bool> wounds)
        => Nearest(wounds, previous);

    static int Nearest(IReadOnlyList<bool> wounds, int target)
    {
        if (wounds.Count == 0) return -1;
        if (target < 0) target = 0;
        if (target >= wounds.Count) target = wounds.Count - 1;
        for (int offset = 0; offset < wounds.Count; offset++)
        {
            int right = target + offset;
            if (right < wounds.Count && !wounds[right]) return right;
            int left = target - offset;
            if (left >= 0 && !wounds[left]) return left;
        }
        return -1;
    }
}
