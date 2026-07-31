// Pure phase gating for the phased combat model (spec 2026-07-21, Spec 2).
// No Unity dependency, matching the CombatRules/TurnPhaseRules pattern.
public static class CombatPhaseRules
{
    // Siege and Influence are wound-free removals available only BEFORE Engage.
    public static bool CanSiege(CombatPhase phase)     => phase == CombatPhase.Siege;
    public static bool CanInfluence(CombatPhase phase) => phase == CombatPhase.Siege;

    // Normal attacks land only after the counterattack (Attack phase).
    public static bool CanNormalAttack(CombatPhase phase) => phase == CombatPhase.Attack;

    // Blocking is the Defend phase's per-enemy action (spec §7.5), the same
    // shape as CanSiege/CanInfluence/CanNormalAttack.
    public static bool CanBlock(CombatPhase phase) => phase == CombatPhase.Defend;

    // The advance button's display state, as pure data so the MonoBehaviour only
    // renders (spec 2026-07-24 phase controls). Siege hides the button while the
    // player has staged siege that can actually kill something (they should spend
    // it on a target); Defend previews the incoming wounds and flips to a
    // strike-back prompt once the unblocked enemies are covered; Attack/Resolved
    // hide it (the win comes from clearing enemies, and Withdraw is its own
    // control).
    //
    // Takes a CounterattackPreview rather than a raw attack total (spec §7.5):
    // the parameter list was already six long, and blocking adds three more
    // numbers that always travel together.
    //
    // canCommit false is a PREVIEW-ONLY fight (2026-07-31): the player opened a
    // guardian they can no longer act against this turn. Nothing can be advanced,
    // so the button never appears — showing an Engage that refuses the press
    // would be a lie. Defaults true so ordinary fights read unchanged.
    public static AdvanceState Advance(CombatPhase phase, int playerSiege, bool anySiegeKillable,
        int defendLeft, CounterattackPreview preview, int toughness, bool canCommit = true)
    {
        if (!canCommit) return new AdvanceState(AdvanceKind.Hidden, 0);

        if (phase == CombatPhase.Siege)
        {
            bool hide = playerSiege > 0 && anySiegeKillable;
            return new AdvanceState(hide ? AdvanceKind.Hidden : AdvanceKind.Engage, 0);
        }
        if (phase == CombatPhase.Defend)
        {
            int wounds = EnemyTraitRules.HandWounds(preview, defendLeft, toughness);
            return wounds == 0
                ? new AdvanceState(AdvanceKind.Counterattack, 0)
                : new AdvanceState(AdvanceKind.TakeHit, wounds);
        }
        return new AdvanceState(AdvanceKind.Hidden, 0);
    }
}

// What the advance button is doing this frame. Hidden = not shown; Engage =
// commit the Siege phase; TakeHit = resolve the counterattack, taking Wounds;
// Counterattack = same press but Defend fully covers the hit (zero wounds).
public enum AdvanceKind { Hidden, Engage, TakeHit, Counterattack }

public struct AdvanceState
{
    public AdvanceKind Kind;
    public int Wounds;
    public AdvanceState(AdvanceKind kind, int wounds) { Kind = kind; Wounds = wounds; }
}
