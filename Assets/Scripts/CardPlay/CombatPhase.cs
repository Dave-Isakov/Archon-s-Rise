// Sub-phase of a single fight (spec 2026-07-21, Spec 2). Siege -> (Defend, the
// instantaneous Engage transition) -> Attack -> Resolved. Lives in CardPlay so
// it is mcs/EditMode-testable alongside CombatRules.
public enum CombatPhase { Siege, Attack, Resolved }
