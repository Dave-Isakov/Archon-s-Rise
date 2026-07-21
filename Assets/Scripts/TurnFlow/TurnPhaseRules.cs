// Pure turn-phase gating (spec 2026-07-21). No Unity dependency so it is
// mcs-CLI-testable, matching the DrawGate/CombatRules pattern.
public static class TurnPhaseRules
{
    // Movement is Explore-only; taking the action ends exploring.
    public static bool CanMove(TurnPhase phase) => phase == TurnPhase.Explore;

    // Exactly one interaction per turn. Starting it from Explore performs the
    // implicit Explore->Action transition, so both phases allow it while the
    // action is unspent; End never does.
    public static bool CanInteract(TurnPhase phase, bool actionTaken)
        => !actionTaken && (phase == TurnPhase.Explore || phase == TurnPhase.Action);

    // A move commits the undo stack only when it uncovers previously-hidden
    // fog (irreversible knowledge); an ordinary move stays undoable.
    public static bool ShouldCommitOnMove(bool revealedNewFog) => revealedNewFog;
}
