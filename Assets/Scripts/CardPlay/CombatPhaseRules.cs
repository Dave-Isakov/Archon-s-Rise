// Pure phase gating for the phased combat model (spec 2026-07-21, Spec 2).
// No Unity dependency, matching the CombatRules/TurnPhaseRules pattern.
public static class CombatPhaseRules
{
    // Siege and Influence are wound-free removals available only BEFORE Engage.
    public static bool CanSiege(CombatPhase phase)     => phase == CombatPhase.Siege;
    public static bool CanInfluence(CombatPhase phase) => phase == CombatPhase.Siege;

    // Normal attacks land only after the counterattack (Attack phase).
    public static bool CanNormalAttack(CombatPhase phase) => phase == CombatPhase.Attack;

    // The single multi-purpose button's caption per phase. Retired with the
    // multiButton in the phase-controls rework (spec 2026-07-24) — kept until
    // CombatController stops calling it.
    public static string ButtonLabel(CombatPhase phase)
    {
        if (phase == CombatPhase.Siege)  return "Engage";
        if (phase == CombatPhase.Defend) return "Defend";
        if (phase == CombatPhase.Attack) return "Withdraw";
        return "";
    }

    // The advance button's display state, as pure data so the MonoBehaviour only
    // renders (spec 2026-07-24 phase controls). Siege hides the button while the
    // player has staged siege that can actually kill something (they should spend
    // it on a target); Defend previews the incoming wounds and flips to a
    // strike-back prompt once Defend covers the group attack; Attack/Resolved
    // hide it (the win comes from clearing enemies, and Withdraw is its own
    // control).
    public static AdvanceState Advance(CombatPhase phase, int playerSiege, bool anySiegeKillable,
        int playerDefend, int enemyAttackTotal, int toughness)
    {
        if (phase == CombatPhase.Siege)
        {
            bool hide = playerSiege > 0 && anySiegeKillable;
            return new AdvanceState(hide ? AdvanceKind.Hidden : AdvanceKind.Engage, 0);
        }
        if (phase == CombatPhase.Defend)
        {
            int wounds = CombatRules.GroupWoundCount(playerDefend, enemyAttackTotal, toughness);
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
