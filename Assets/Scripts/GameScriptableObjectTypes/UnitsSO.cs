using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "Units", menuName = "ScriptableObjects/Units")]
public class UnitsSO : AllCards
{
    public int attack;
    public int defend;
    public int explore;
    public int influence;
    public int healAmount;
    public int numCrystals;
    public StatType cardType;
    public EmpowerType empowerType;
}
