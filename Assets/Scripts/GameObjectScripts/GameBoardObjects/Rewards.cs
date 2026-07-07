using System.Collections.Generic;
using UnityEngine;

// Reward-granting service. Context methods pick which RewardsSO applies, then
// everything funnels through one Grant() that applies each reward flag once.
// Card rewards offer a choice through RewardCanvas and grant the chosen card
// via the single PlayerDeck.AddCard path.
public class Rewards : Deck<RewardsSO>
{
    public List<RewardsSO> rewards = new List<RewardsSO>();
    public CrystalInventory crystals;
    [SerializeField] Player player;
    [SerializeField] PlayerDeck deck;
    [SerializeField] RewardCanvas rewardCanvas;
    // Reward-eligible cards only (NOT starting cards or Wound). Kept separate
    // from DataManager.Cards (the complete save/load registry) so the resolver
    // can contain every card while rewards stay curated.
    [SerializeField] List<CardsSO> rewardPool = new List<CardsSO>();

    private void Start()
    {
        Shuffle(rewards);
    }

    // No-context reward (legacy entry point).
    public void GetReward()
    {
        Grant(rewards[0]);
        Shuffle(rewards);
    }

    // Wired to OnEnemyDefeat_GetRewards.
    public void GetReward(EnemyCard enemy)
    {
        var reward = enemy.enemySO.defeatRewards[Random.Range(0, enemy.enemySO.defeatRewards.Count)];
        Grant(reward);
    }

    public void GetReward(Dungeon dungeon)
    {
        var reward = dungeon.rewards[Random.Range(0, dungeon.rewards.Count)];
        Grant(reward);
        dungeon.rewards.Remove(reward);
    }

    private void Grant(RewardsSO reward)
    {
        if (reward.rewardType.HasFlag(RewardType.Experience))
            player.PlayerExp += reward.expAmount;

        if (reward.rewardType.HasFlag(RewardType.Crystals))
        {
            var types = new[] { EmpowerType.Green, EmpowerType.Yellow, EmpowerType.Red, EmpowerType.Purple, EmpowerType.None };
            crystals.CreateCrystal(types[Random.Range(0, types.Length)]);
        }

        if (reward.rewardType.HasFlag(RewardType.Cards))
            OfferCardChoice();

        Debug.Log($"Your reward is: {reward.cardDescription}");
    }

    // Card pick: choose 1 of 3 from the curated pool. Public because level-ups
    // grant the same pick (LevelUpController); onClosed lets the caller queue
    // the next reward after the screen resolves (chosen OR skipped).
    public void OfferCardChoice(System.Action onClosed = null)
    {
        // Draw from the curated rewardPool, NOT DataManager.Cards (which now
        // includes starting cards + Wound for save/load resolution).
        if (rewardPool == null || rewardPool.Count == 0) { onClosed?.Invoke(); return; }

        var candidates = new List<CardsSO>();
        for (int i = 0; i < 3; i++)
            candidates.Add(rewardPool[Random.Range(0, rewardPool.Count)]);

        rewardCanvas.Offer(candidates,
            so => { deck.AddCard(so, toTop: true); onClosed?.Invoke(); },
            () => onClosed?.Invoke());
    }
}
