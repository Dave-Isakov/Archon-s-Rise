using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyCards", menuName = "ScriptableObjects/Cards/EnemyCards")]
public class EnemiesSO : AllCards
{
    public int enemyHP;
    public int enemyAttack;
    public bool canInfluence;
    public int influenceCost;
    // Optional unit form: when set AND the player owns Charismatic, paying the
    // influence cost recruits this unit instead of just paying the enemy off.
    // Null = pay-to-leave only (spec 2026-07-09).
    public UnitsSO recruitedUnit;
    // Doom-gated difficulty tier (1-3): tier 2 spawns at doom > lowBandMax,
    // tier 3 at doom > midBandMax (DoomRules.MaxTier). id (from AllCards) is
    // the stable save identity for mid-run spawns — never rename ids.
    public int tier = 1;

    private void Start() {
        if(!canInfluence)
        {
            influenceCost = 0;
        }
    }

}
