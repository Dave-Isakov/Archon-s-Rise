// Sub-phase of a single fight (spec 2026-07-21, Spec 2; Defend split added
// 2026-07-22). Siege -> Defend (Engage commits Siege, opens a window to play
// defense) -> Attack (the Defend press resolves the counterattack) -> Resolved.
// Lives in CardPlay so it is mcs/EditMode-testable alongside CombatRules.
public enum CombatPhase { Siege, Defend, Attack, Resolved }
