// Pure navigation graph for the card pop-out (hybrid model). Section ENTRY is by
// dedicated button (R1 -> Choice, L1 -> Improvise), not by direction; Empower is a
// global toggle button, never a focus target. Direction only cycles options WITHIN
// the focused section, or drops to Play. The physical layout still maps: Choice
// banner top (horizontal), Improvise panel left (vertical), Play bar bottom.
//
// InspectorSection keeps all four values so already-wired scene references stay
// valid, but these rules never produce Empower.

public enum InspectorSection { Choice, Improvise, Empower, Play }

public readonly struct InspectorNavPosition
{
    public readonly InspectorSection Section;
    public readonly int Option;

    public InspectorNavPosition(InspectorSection section, int option)
    {
        Section = section;
        Option = option;
    }
}

public static class InspectorNavRules
{
    // Initial focus when the pop-out opens: the Play button (bottom, nearest the fan).
    public static InspectorNavPosition Open() => new InspectorNavPosition(InspectorSection.Play, 0);

    // R1: enter the Choice section at option 0 if it is reachable, else stay put.
    public static InspectorNavPosition EnterChoice(InspectorNavPosition pos, bool choiceReachable)
        => choiceReachable ? new InspectorNavPosition(InspectorSection.Choice, 0) : pos;

    // L1: enter the Improvise section at option 0 if it is reachable, else stay put.
    public static InspectorNavPosition EnterImprovise(InspectorNavPosition pos, bool improviseReachable)
        => improviseReachable ? new InspectorNavPosition(InspectorSection.Improvise, 0) : pos;

    // Direction cycles options within the focused section only. dx/dy in {-1,0,+1};
    // dy > 0 is up. Choice is horizontal (dx cycles, wraps; down -> Play); Improvise
    // is vertical (dy cycles, no wrap; down past the last -> Play); Play is inert.
    public static InspectorNavPosition Move(InspectorNavPosition pos, int dx, int dy,
        int choiceOptions, int improviseOptions)
    {
        switch (pos.Section)
        {
            case InspectorSection.Choice:
                if (dx != 0 && choiceOptions > 0)
                {
                    int step = dx > 0 ? 1 : -1;
                    int next = ((pos.Option + step) % choiceOptions + choiceOptions) % choiceOptions;
                    return new InspectorNavPosition(InspectorSection.Choice, next);
                }
                if (dy < 0) return new InspectorNavPosition(InspectorSection.Play, 0); // down -> Play
                return pos;

            case InspectorSection.Improvise:
                if (dy > 0) // up
                    return pos.Option > 0
                        ? new InspectorNavPosition(InspectorSection.Improvise, pos.Option - 1)
                        : pos; // at top, stay
                if (dy < 0) // down
                    return pos.Option + 1 < improviseOptions
                        ? new InspectorNavPosition(InspectorSection.Improvise, pos.Option + 1)
                        : new InspectorNavPosition(InspectorSection.Play, 0); // past last -> Play
                return pos; // left/right stay
        }
        return pos; // Play (and defensive Empower) are inert
    }

    // Called every frame: if the focused section is no longer reachable (Choice locked
    // while Improvise is active, or Improvise locked after empowering), or is the
    // never-focusable Empower, snap focus back to Play.
    public static InspectorNavPosition ClampReachable(InspectorNavPosition pos,
        bool choiceReachable, bool improviseReachable)
    {
        if (pos.Section == InspectorSection.Choice && !choiceReachable)
            return new InspectorNavPosition(InspectorSection.Play, 0);
        if (pos.Section == InspectorSection.Improvise && !improviseReachable)
            return new InspectorNavPosition(InspectorSection.Play, 0);
        if (pos.Section == InspectorSection.Empower)
            return new InspectorNavPosition(InspectorSection.Play, 0);
        return pos;
    }
}
