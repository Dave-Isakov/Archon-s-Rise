using System.Collections.Generic;
using UnityEngine;

// Reward-granting service for enemy defeats and dungeon completions (M2.9).
// Every reward derives from a tier (spec 2026-07-10): Experience is granted
// every time (bell-curve sampled from the tier's range), then a crystal and a
// card are rolled independently against the tier's odds. Card rewards offer a
// choice from that tier's pool through RewardCanvas and grant the chosen card
// via the single PlayerDeck.AddCard path.
public class Rewards : MonoBehaviour
{
    public CrystalInventory crystals;
    [SerializeField] Player player;
    [SerializeField] PlayerDeck deck;
    [SerializeField] RewardCanvas rewardCanvas;
    [SerializeField] RewardTuningSO tuning;

    // Wired to OnEnemyDefeat_GetRewards. Dungeon fights pay experience only —
    // the dungeon's reward is completion-gated (spec 2026-07-13).
    public void GetReward(EnemyCard enemy)
    {
        if (DungeonDelve.AnyInProgress) { GrantExpOnly(enemy.enemySO.tier); return; }
        Grant(enemy.enemySO.tier);
    }

    // Per-fight dungeon grant: the tier's bell exp sample, no bonus rolls.
    public void GrantExpOnly(int tier)
    {
        player.PlayerExp += RewardRules.SampleExp(tier, tuning.Data, max => Random.Range(0, max));
    }

    // Dungeon completion bundle (spec 2026-07-13): guaranteed, no dice — one
    // exp roll at the dungeon's tier, then rewardCount crystals and
    // rewardCount card picks. Exp/crystals apply instantly; the picks resolve
    // one at a time through the RewardQueue.
    public void GrantDungeonCompletion(int tier, int rewardCount)
    {
        GrantExpOnly(tier);
        for (int i = 0; i < rewardCount; i++)
            crystals.CreateCrystal(RandomCrystalColor());
        for (int i = 0; i < rewardCount; i++)
            OfferCardChoice(tier);
    }

    private static readonly EmpowerType[] crystalColors =
        { EmpowerType.Green, EmpowerType.Yellow, EmpowerType.Red, EmpowerType.Purple, EmpowerType.None };
    private static EmpowerType RandomCrystalColor()
        => crystalColors[Random.Range(0, crystalColors.Length)];

    void Grant(int tier)
    {
        player.PlayerExp += RewardRules.SampleExp(tier, tuning.Data, max => Random.Range(0, max));

        if (RewardRules.Roll(tuning.CrystalChance(tier), () => Random.value))
            crystals.CreateCrystal(RandomCrystalColor());

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
