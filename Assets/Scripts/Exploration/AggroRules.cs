// What made a token re-check whether the player is in its reach.
public enum AggroTrigger
{
    // The player took a step. Lingering inside reach across two steps is the
    // player's own choice, so this trigger alone can escalate to a forced fight.
    PlayerMoved,
    // Reach changed under a standing player: fog lifted off the token, or the
    // token spawned beside them.
    ReachChanged,
}

// The result of one arming check: whether the token ends up armed (halo pulsing,
// click engages) and whether the fight opens immediately whether the player likes
// it or not.
public readonly struct AggroOutcome
{
    public readonly bool Armed;
    public readonly bool Forced;

    public AggroOutcome(bool armed, bool forced)
    {
        Armed = armed;
        Forced = forced;
    }
}

// When an enemy token is armed against the player (spec 2026-08-01, extended
// 2026-08-11). Arming used to be computed inline on the movement event, which
// silently made movement the only thing that could ever arm a token — an enemy
// that came into reach because the fog lifted, or because it spawned there,
// stayed inert beside a standing player.
//
// Pure decision layer — the caller supplies the board facts, so this has no
// UnityEngine dependency and is trivially unit-testable, matching HexActionRules.
public static class AggroRules
{
    // fogHidden / adjacent describe the token against the player's current cell.
    // wasArmed is its state going in, and playerCellChanged is true when the player
    // stands somewhere other than the cell that armed it.
    public static AggroOutcome Resolve(bool fogHidden, bool adjacent, bool wasArmed,
                                       bool playerCellChanged, AggroTrigger trigger)
    {
        // Fog is absolute: a token the player cannot see neither arms nor forces.
        // It arms normally once the fog lifts — which is now a trigger of its own.
        if (fogHidden || !adjacent) return new AggroOutcome(false, false);

        // Armed already, and the player is standing somewhere other than the hex
        // that armed us: they have had a turn inside reach and stayed. Only their
        // own step counts — a reveal or a spawn hands the player a fight they may
        // take, never one they cannot refuse.
        bool forced = trigger == AggroTrigger.PlayerMoved && wasArmed && playerCellChanged;

        return new AggroOutcome(true, forced);
    }
}
