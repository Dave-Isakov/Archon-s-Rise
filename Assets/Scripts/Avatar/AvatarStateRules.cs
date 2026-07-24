// Pure avatar interrupt/priority rules. No Unity dependency, matching the
// CombatRules / TurnPhaseRules pattern, so PlayerAvatar stays a thin shell over
// testable logic.
public static class AvatarStateRules
{
    // Higher wins. Hurt outranks Fight because taking a hit should always read,
    // even mid-swing; both outrank locomotion.
    public static int Priority(AvatarState state)
    {
        if (state == AvatarState.Hurt)  return 3;
        if (state == AvatarState.Fight) return 2;
        if (state == AvatarState.Walk)  return 1;
        return 0;
    }

    // One-shots play to completion and then resume; Idle/Walk are looping
    // locomotion states driven by a bool.
    public static bool IsOneShot(AvatarState state)
        => state == AvatarState.Fight || state == AvatarState.Hurt;

    // Whether an incoming request takes over from what is playing. A request
    // that loses is DROPPED, not queued — a stale animation firing seconds
    // later reads worse than not firing at all.
    public static bool ShouldPlay(AvatarState current, AvatarState incoming)
    {
        // Never retrigger the playing state: mirrors "Can Transition To Self
        // off" on the Any State transitions, so a second kill in the same fight
        // cannot restart the swing mid-play.
        if (incoming == current) return false;
        return Priority(incoming) > Priority(current);
    }

    // Where a finished one-shot lands: still walking -> Walk, otherwise Idle.
    public static AvatarState ResumeAfter(bool isMoving)
        => isMoving ? AvatarState.Walk : AvatarState.Idle;
}
