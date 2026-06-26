using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rewards : Deck<RewardsSO>
{
    public List<RewardsSO> rewards = new List<RewardsSO>();
    public CrystalInventory crystals;
    [SerializeField] Player player;
    // [SerializeField] VoidEvent OnNewCardReward_OpenRewardScreen;
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
            GameManager.Instance.cardRewardCanvas.enabled = true;
            GetCardRewards.Raise();
        Debug.Log($"Your reward is: {rewards[0].cardDescription} ");
        Shuffle(rewards);
    }

    public void GetReward(EnemyCard enemy)
    {
        var reward = enemy.enemySO.defeatRewards[UnityEngine.Random.Range(0, enemy.enemySO.defeatRewards.Count)];
        if(reward.rewardType.HasFlag(RewardType.Experience))
            player.PlayerExp += reward.expAmount;
        if(reward.rewardType.HasFlag(RewardType.Crystals))
        {
            EmpowerType[] i = new[] { EmpowerType.Green, EmpowerType.Yellow, EmpowerType.Red, EmpowerType.Purple, EmpowerType.None };
            crystals.CreateCrystal(i[UnityEngine.Random.Range(0,5)]);
        }
        Debug.Log($"Your reward is: {reward.cardDescription} ");
    }

    public void GetReward(Dungeon dungeon)
    {
        var reward = dungeon.rewards[UnityEngine.Random.Range(0, dungeon.rewards.Count)];
        if(reward.rewardType.HasFlag(RewardType.Experience))
            player.PlayerExp += rewards[0].expAmount;
        if(reward.rewardType.HasFlag(RewardType.Crystals))
        {
            EmpowerType[] i = new[] { EmpowerType.Green, EmpowerType.Yellow, EmpowerType.Red, EmpowerType.Purple, EmpowerType.None };
            crystals.CreateCrystal(i[UnityEngine.Random.Range(0,5)]);
        }
        if(reward.rewardType.HasFlag(RewardType.Cards))
            GameManager.Instance.cardRewardCanvas.enabled = true;
            GetCardRewards.Raise();
        Debug.Log($"Your reward is: {rewards[0].cardDescription} ");
        dungeon.rewards.Remove(reward);
    }
}
