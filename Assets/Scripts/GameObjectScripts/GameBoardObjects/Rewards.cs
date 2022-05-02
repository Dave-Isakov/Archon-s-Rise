using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rewards : Deck<RewardsSO>
{
    public List<RewardsSO> rewards = new List<RewardsSO>();
    public CrystalInventory crystals;
    [SerializeField] Player player;
    [SerializeField] VoidEvent OnNewCardReward_OpenRewardScreen;
    [SerializeField] VoidEvent GetCardRewards;

    private void Start()
    {
        Shuffle(rewards);
    }

    public void GetReward()
    {
        if(rewards[0].rewardType.HasFlag(RewardType.Experience))
            player.PlayerExp += rewards[0].expAmount;
        if(rewards[0].rewardType.HasFlag(RewardType.Crystals))
        {
            EmpowerType[] i = new[] { EmpowerType.Green, EmpowerType.Yellow, EmpowerType.Red, EmpowerType.Purple, EmpowerType.None };
            crystals.CreateCrystal(i[UnityEngine.Random.Range(0,5)]);
        }
        if(rewards[0].rewardType.HasFlag(RewardType.Cards))
            OnNewCardReward_OpenRewardScreen.Raise();
            GetCardRewards.Raise();
        GameManager.Instance.ValidationMessage($"Your reward is: {rewards[0].cardDescription} ");
        Shuffle(rewards);
    }
}
