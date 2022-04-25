using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Locations", menuName = "ScriptableObjects/LocationsSO")]
public class LocationsSO : AllCards
{
    public int exploreCost;
    public List<EnemiesSO> enemies;
    public List<TownsSO> towns;
    public List<EnemiesSO> bosses;
}
