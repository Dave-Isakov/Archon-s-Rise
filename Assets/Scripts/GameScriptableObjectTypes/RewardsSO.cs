using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RewardCards", menuName = "ScriptableObjects/Cards/RewardCards")]
public class RewardsSO : AllCards
{
    public RewardType rewardType;
    public RewardLevel rewardLevel;
    public int expAmount;
    public int numCrystals;
}