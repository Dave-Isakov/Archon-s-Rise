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

    // Authored traits (spec 2026-07-29). NOT saved — read from this SO via the
    // stable AllCards.id exactly like enemyHP, so no save schema bump.
    // Authoring rules (spec §5.3): field enemies use self traits only; granting
    // auras are reserved for guardian rosters. Elusive needs NO canInfluence
    // pairing (that rule was dropped 2026-07-31) — closing the wound-free routes
    // and forcing Defend-then-Attack, or a flee, is the pressure it exists to apply.
    public EnemyTrait traits = EnemyTrait.None;

    // Per-enemy portrait shown on the combat card (spec 2026-07-24). Nullable:
    // an unauthored enemy shows the plain card, never a broken frame. Art is
    // authored later (M3 content).
    public Sprite cardArt;

    private void Start() {
        if(!canInfluence)
        {
            influenceCost = 0;
        }
    }

}
