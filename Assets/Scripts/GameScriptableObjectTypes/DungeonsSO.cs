using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Dungeons", menuName = "ScriptableObjects/Dungeons")]
public class DungeonsSO : AllCards
{
    public int exploreCost;
    public List<EnemiesSO> enemies;
}
