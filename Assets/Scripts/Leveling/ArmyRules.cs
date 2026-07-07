// Pure army-cap rules (mirrors TurnButtonGate). The cap itself comes from
// LevelRules.DerivedArmyCap; these gates decide what the Recruit flow does.
public static class ArmyRules
{
    public static bool CanRecruit(int unitCount, int cap) => unitCount < cap;

    // At (or somehow above) cap, hiring requires disbanding an existing unit.
    public static bool NeedsDisband(int unitCount, int cap) => unitCount >= cap;
}
