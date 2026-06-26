using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    public Sprite sprite;
    public Color color;
    public char unitLetter;
    public EmpowerType empowerType;

    public int[] GetUnitStats()
    {
        int[] allStats = new int[4] {attack, defend, influence, explore};
        Debug.Log($"{allStats[0]}, {allStats[1]}, {allStats[2]}, {allStats[3]}");
        return allStats;
    }
}
