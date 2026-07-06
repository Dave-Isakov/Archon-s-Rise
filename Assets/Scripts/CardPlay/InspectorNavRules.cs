// Pure navigation graph for the card pop-out. The physical layout is the map:
// Choice banner top, Improvise panel left, Empower right, Play bar bottom.
// A direction first cycles within the current section along its own axis; past
// the ends (or on the cross axis) it jumps to the section that lies that way.
// Unreachable (hidden/locked) sections are never landed on — the move stays put.

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

    // dx/dy in {-1, 0, +1}; dy > 0 is up. Play options: 0 = Play, 1 = Back.
    public static InspectorNavPosition Move(InspectorNavPosition pos, int dx, int dy,
        bool choiceReachable, bool improviseReachable, bool empowerReachable,
        int choiceOptions, int improviseOptions)
    {
        switch (pos.Section)
        {
            case InspectorSection.Play:
                if (dx != 0)
                    return new InspectorNavPosition(InspectorSection.Play, pos.Option == 0 ? 1 : 0);
                if (dy > 0)
                {
                    // Up from Play reaches the first reachable section so every card
                    // shape (non-choice, non-empowerable) can leave the Play bar.
                    if (choiceReachable)    return new InspectorNavPosition(InspectorSection.Choice, 0);
                    if (improviseReachable) return new InspectorNavPosition(InspectorSection.Improvise, 0);
                    if (empowerReachable)   return new InspectorNavPosition(InspectorSection.Empower, 0);
                }
                return pos;

            case InspectorSection.Choice:
                if (dx > 0)
                    return pos.Option + 1 < choiceOptions
                        ? new InspectorNavPosition(InspectorSection.Choice, pos.Option + 1)
                        : JumpOrStay(pos, InspectorSection.Empower, empowerReachable);
                if (dx < 0)
                    return pos.Option > 0
                        ? new InspectorNavPosition(InspectorSection.Choice, pos.Option - 1)
                        : JumpOrStay(pos, InspectorSection.Improvise, improviseReachable);
                if (dy < 0)
                    return new InspectorNavPosition(InspectorSection.Play, 0);
                return pos;

            case InspectorSection.Improvise:
                if (dy > 0)
                    return pos.Option > 0
                        ? new InspectorNavPosition(InspectorSection.Improvise, pos.Option - 1)
                        : JumpOrStay(pos, InspectorSection.Choice, choiceReachable);
                if (dy < 0)
                    return pos.Option + 1 < improviseOptions
                        ? new InspectorNavPosition(InspectorSection.Improvise, pos.Option + 1)
                        : new InspectorNavPosition(InspectorSection.Play, 0);
                if (dx > 0)
                    return JumpOrStay(pos, InspectorSection.Empower, empowerReachable);
                return pos;

            case InspectorSection.Empower:
                if (dx < 0) return JumpOrStay(pos, InspectorSection.Improvise, improviseReachable);
                if (dy > 0) return JumpOrStay(pos, InspectorSection.Choice, choiceReachable);
                if (dy < 0) return new InspectorNavPosition(InspectorSection.Play, 0);
                return pos;
        }
        return pos;
    }

    static InspectorNavPosition JumpOrStay(InspectorNavPosition from, InspectorSection to, bool reachable)
        => reachable ? new InspectorNavPosition(to, 0) : from;
}
