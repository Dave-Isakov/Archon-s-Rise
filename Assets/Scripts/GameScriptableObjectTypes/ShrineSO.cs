using System.Collections.Generic;
using UnityEngine;

// A shrine tile (spec 2026-07-24, §2): spend any 4 crystals, 50/50 for a 1x
// reward now vs. a tier-3 guardian owing 2x + its exp. One-shot.
[CreateAssetMenu(fileName = "Shrine", menuName = "ScriptableObjects/Shrine")]
public class ShrineSO : AllCards
{
    // Save identity is the inherited AllCards.id (a stable slug; never rename) —
    // do not re-declare it here or Unity errors on a duplicate serialized field.
    [Tooltip("Crystals to engage (any colors).")]
    public int crystalCost = 4;
    [Range(0f, 1f)]
    [Tooltip("Chance of the safe (instant 1x) result.")]
    public float goodRollChance = 0.5f;
    [Tooltip("Reward types this shrine can roll.")]
    public List<ShrineReward> rewardTypes = new()
        { ShrineReward.CardPick, ShrineReward.Unit, ShrineReward.LargeExp };
    [Tooltip("Candidate units when the rolled type is Unit.")]
    public List<UnitsSO> unitPool = new();
    [Tooltip("The '1x' large-exp payout (fight pays 2x).")]
    public int largeExp = 15;
    [Tooltip("Tier for the card-pick reward (reuses the Rewards card pools).")]
    public int cardTier = 3;
    [Tooltip("The tier-3 guardian summoned on the bad roll. Must be in the EnemyDeck pool.")]
    public EnemiesSO summonedEnemy;
}
