// Pure phase gating for the phased combat model (spec 2026-07-21, Spec 2).
// No Unity dependency, matching the CombatRules/TurnPhaseRules pattern.
public static class CombatPhaseRules
{
    // Siege and Influence are wound-free removals available only BEFORE Engage.
    public static bool CanSiege(CombatPhase phase)     => phase == CombatPhase.Siege;
    public static bool CanInfluence(CombatPhase phase) => phase == CombatPhase.Siege;

    // Normal attacks land only after the counterattack (Attack phase).
    public static bool CanNormalAttack(CombatPhase phase) => phase == CombatPhase.Attack;

    // The single multi-purpose button's caption per phase.
    public static string ButtonLabel(CombatPhase phase)
    {
        if (phase == CombatPhase.Siege)  return "Engage";
        if (phase == CombatPhase.Defend) return "Defend";
        if (phase == CombatPhase.Attack) return "Withdraw";
        return "";
    }
}
