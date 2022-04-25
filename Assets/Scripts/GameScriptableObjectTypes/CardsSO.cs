using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "PlayerCards", menuName = "ScriptableObjects/Cards/PlayerCards")]
public class CardsSO : AllCards
{
    private int[] stats = new int[4];
    public int attack;
    public int defend;
    public int explore;
    public int influence;
    public int empowerAttack;
    public int empowerDefend;
    public int empowerExplore;
    public int empowerInfluence;
    public CardType cardType;
    public EmpowerType empowerType;
    public bool isChoice;

    ///<summary> Method returns 4 values from the specific scriptable card object where attack[0], defend[1], influence[2], and explore[3] are the int values returned for each of the stats in the array position. </summary>
    public int[] GetCardStats(bool isEmpowered)
    {
        int[] allStats = new int[4] {ReturnAttack(isEmpowered), ReturnDefend(isEmpowered), ReturnInfluence(isEmpowered), ReturnExplore(isEmpowered)};
        Debug.Log($"{allStats[0]}, {allStats[1]}, {allStats[2]}, {allStats[3]}");
        return allStats;
    }

    public int ReturnAttack(bool isEmpowered)
    {
        if (cardType.HasFlag(CardType.Attack))
            if(isEmpowered) return empowerAttack;
            else return attack;
        else
        return 0;
    }

    public int ReturnDefend(bool isEmpowered)
    {
        if (cardType.HasFlag(CardType.Defend))
            if(isEmpowered) return empowerDefend;
            else return defend;
        else
        return 0;
    }

    public int ReturnInfluence(bool isEmpowered)
    {
        if (cardType.HasFlag(CardType.Influence))
            if(isEmpowered) return empowerInfluence;
            else return influence;
        else
        return 0;
    }
    public int ReturnExplore(bool isEmpowered)
    {
        if (cardType.HasFlag(CardType.Explore))
            if(isEmpowered) return empowerExplore;
            else return explore;
        else
        return 0;
    }
}
