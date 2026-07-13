using System.Collections.Generic;
using UnityEngine;

// Reward-granting service. Every enemy/dungeon reward derives from a tier
// (spec 2026-07-10): Experience is granted every time (bell-curve sampled from
// the tier's range), then a crystal and a card are rolled independently against
// the tier's odds. Card rewards offer a choice from that tier's pool through
// RewardCanvas and grant the chosen card via the single PlayerDeck.AddCard path.
public class Rewards : MonoBehaviour
{
    public CrystalInventory crystals;
    [SerializeField] Player player;
    [SerializeField] PlayerDeck deck;
    [SerializeField] RewardCanvas rewardCanvas;
    [SerializeField] RewardTuningSO tuning;

    // Wired to OnEnemyDefeat_GetRewards.
    public void GetReward(EnemyCard enemy) => Grant(enemy.enemySO.tier);

    // Wired to onDungeonReward_RewardPlayer. Each dungeon reward event grants a
    // tier reward and consumes one of the dungeon's remaining reward events.
    public void GetReward(Dungeon dungeon)
    {
        Grant(dungeon.RewardTier);
        dungeon.ConsumeReward();
    }

    void Grant(int tier)
    {
        player.PlayerExp += RewardRules.SampleExp(tier, tuning.Data, max => Random.Range(0, max));

        if (RewardRules.Roll(tuning.CrystalChance(tier), () => Random.value))
        {
            var types = new[] { EmpowerType.Green, EmpowerType.Yellow, EmpowerType.Red, EmpowerType.Purple, EmpowerType.None };
            crystals.CreateCrystal(types[Random.Range(0, types.Length)]);
        }

        if (RewardRules.Roll(tuning.CardChance(tier), () => Random.value))
            OfferCardChoice(tier);
    }

    // Card pick: choose 1 of 3 from the tier's pool. Public because level-ups
    // and dungeon bundles grant the same pick. Self-enqueues on the RewardQueue
    // (spec 2026-07-13) — callers must never wrap this in their own Enqueue.
    public void OfferCardChoice(int tier, System.Action onClosed = null)
    {
        var pool = tuning.CardPool(tier);
        if (pool == null || pool.Count == 0) { onClosed?.Invoke(); return; }

        RewardQueue.Instance.Enqueue(done =>
        {
            var candidates = new List<CardsSO>();
            for (int i = 0; i < 3; i++)
                candidates.Add(pool[Random.Range(0, pool.Count)]);

            rewardCanvas.Offer(candidates,
                so => { deck.AddCard(so, toTop: true); done(); onClosed?.Invoke(); },
                () => { done(); onClosed?.Invoke(); });
        });
    }

    // Level-up card pick: pool tier scales with player level (spec 2026-07-10).
    public void OfferCardChoiceForLevel(int level, System.Action onClosed = null)
        => OfferCardChoice(RewardRules.CardTierForLevel(level, tuning.Data), onClosed);
}
