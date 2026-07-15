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
    public int siege;
    public int healAmount;
    public int numCrystals;
    public int empowerAttack;
    public int empowerDefend;
    public int empowerExplore;
    public int empowerInfluence;
    public int empowerSiege;
    public int empowerHealAmount;
    public int empowerNumCrystals;
    public StatType cardType;
    public EmpowerType empowerType;
    public bool isChoice;
    [Header("Conversion (spec 2026-07-14)")]
    public StatType convertTo;          // None = this card has no conversion
    public StatType convertFrom;        // action flags only; never contains convertTo
    public bool convertRequiresEmpower; // true = conversion only offered on the empowered play
    [Header("Refresh (spec 2026-07-14)")]
    public int refresh;
    public int empowerRefresh;

    ///<summary> Returns 5 stat values: attack[0], defend[1], influence[2], explore[3], siege[4]. </summary>
    public int[] GetCardStats(bool isEmpowered)
    {
        int[] allStats = new int[5] {ReturnAttack(isEmpowered), ReturnDefend(isEmpowered), ReturnInfluence(isEmpowered), ReturnExplore(isEmpowered), ReturnSiege(isEmpowered)};
        Debug.Log($"{allStats[0]}, {allStats[1]}, {allStats[2]}, {allStats[3]}, {allStats[4]}");
        return allStats;
    }

    public int ReturnAttack(bool isEmpowered)
    {
        if (cardType.HasFlag(StatType.Attack))
            if(isEmpowered) return empowerAttack;
            else return attack;
        else
        return 0;
    }

    public int ReturnDefend(bool isEmpowered)
    {
        if (cardType.HasFlag(StatType.Defend))
            if(isEmpowered) return empowerDefend;
            else return defend;
        else
        return 0;
    }

    public int ReturnInfluence(bool isEmpowered)
    {
        if (cardType.HasFlag(StatType.Influence))
            if(isEmpowered) return empowerInfluence;
            else return influence;
        else
        return 0;
    }
    public int ReturnExplore(bool isEmpowered)
    {
        if (cardType.HasFlag(StatType.Explore))
            if(isEmpowered) return empowerExplore;
            else return explore;
        else
        return 0;
    }

    public int ReturnSiege(bool isEmpowered)
    {
        if (cardType.HasFlag(StatType.Siege))
            if(isEmpowered) return empowerSiege;
            else return siege;
        else
        return 0;
    }

    public int ReturnRefresh(bool isEmpowered)
    {
        if (cardType.HasFlag(StatType.Refresh))
            if (isEmpowered) return empowerRefresh;
            else return refresh;
        else
        return 0;
    }

    void OnValidate()
    {
        if (convertTo != StatType.None)
        {
            if (isChoice)
                Debug.LogWarning($"{name}: a card cannot be both isChoice and a converter.", this);
            if (!ConvertRules.IsValid(convertFrom, convertTo))
                Debug.LogWarning($"{name}: conversion must target one action stat, draw from action stats, and never include the target in its sources.", this);
        }
        if ((refresh > 0 || empowerRefresh > 0) && !cardType.HasFlag(StatType.Refresh))
            Debug.LogWarning($"{name}: refresh values need the Refresh flag on cardType.", this);
    }
}