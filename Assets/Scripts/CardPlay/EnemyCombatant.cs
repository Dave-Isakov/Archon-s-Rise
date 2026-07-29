// The pure, Unity-free view of one enemy in a fight. CombatController builds
// these from EnemyCard so every rule below stays testable from the CLI.
// Attack/HP are the EFFECTIVE values (SO value + doom-scaling bonus).
public struct EnemyCombatant
{
    public int Attack;
    public int HP;
    public EnemyTrait Traits;
    public bool Blocked;
}

// Every trait magnitude. Mirrors EnemyTraitTuningSO's serialized instance, the
// same split RewardTuning/RewardTuningSO already uses: pure math reads this,
// Unity only stores it.
[System.Serializable]
public class EnemyTraitTuning
{
    public int armorSiegeMult = 2;
    public int hulkAttackMult = 2;
    public int swiftThreatMult = 2;
    public int brutalSurchargeMult = 1;
    public int warlordBonus = 1;
    public int toxicCopies = 1;
    public int leechCrystals = 1;
    public int vengefulWounds = 1;
    public int harryHandPenalty = 1;
}
