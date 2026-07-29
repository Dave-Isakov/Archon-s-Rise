using UnityEngine;

// One shared asset holding every trait magnitude (spec §3.2), wired onto
// CombatController. Same split as RewardTuningSO: the pure EnemyTraitTuning
// holds the numbers, this only makes them an inspector-editable asset.
//
// Enemies tick trait boxes; this owns the magnitudes. That is deliberate —
// "Armored" then means one fixed thing game-wide, so the keyword is learnable
// after a single fight and retuning all armor is one field.
[CreateAssetMenu(fileName = "EnemyTraitTuning", menuName = "ScriptableObjects/EnemyTraitTuning")]
public class EnemyTraitTuningSO : ScriptableObject
{
    public EnemyTraitTuning tuning = new EnemyTraitTuning();
}
