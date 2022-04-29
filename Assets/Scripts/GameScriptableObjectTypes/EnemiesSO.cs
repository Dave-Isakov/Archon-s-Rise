using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyCards", menuName = "ScriptableObjects/Cards/EnemyCards")]
public class EnemiesSO : AllCards
{
    public int enemyHP;
    public int enemyAttack;
    public RewardLevel reward;
    public bool canInfluence;
    public int influenceCost;

    private void Start() {
        if(!canInfluence)
        {
            influenceCost = 0;
        }
    }

}
