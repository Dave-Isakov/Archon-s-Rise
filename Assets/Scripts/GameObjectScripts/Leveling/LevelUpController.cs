using System.Collections.Generic;
using UnityEngine;

// Enqueues the level-up payout on the shared RewardQueue: announcement message,
// then skill pick(s), then card pick(s) — strictly in order, one modal at a
// time (spec 2026-07-13; replaces the M2.4 private queue + busy-wait poll).
// Fixed bonuses (HP/hand/army) never enter the queue: Player applies HP
// directly and the sizes are derived.
public class LevelUpController : MonoBehaviour
{
    [SerializeField] Player player;
    [SerializeField] LevelUpModal modal;
    [SerializeField] Rewards rewards;

    readonly System.Random rng = new System.Random();

    // Save gate: mid-payout is not a settled state. The queue owns all pending modals.
    public bool Busy => RewardQueue.Instance.Busy;

    public void EnqueueLevelRewards(int level, LevelRewardEntry entry)
    {
        GameManager.Instance.ValidationMessage($"You reached level {level}!");
        if (entry == null) return;

        for (int i = 0; i < entry.skillPicks; i++)
            RewardQueue.Instance.Enqueue(OfferSkillPick);
        // OfferCardChoice self-enqueues; calling it here preserves order
        // (message, skills, cards) because all enqueues happen in sequence.
        for (int i = 0; i < entry.cardPicks; i++)
            rewards.OfferCardChoiceForLevel(player.PlayerLevel);
    }

    void OfferSkillPick(System.Action done)
    {
        // Choices are drawn at open time so earlier picks in the same payout
        // are excluded from later draws.
        var choices = LevelRules.DrawSkillChoices(player.LevelRewards.SkillPool,
            new List<SkillsSO>(player.Skills), rng, 3);
        if (choices.Count == 0) { done(); return; } // pool exhausted: skip the pick
        modal.Offer(choices, chosen => { player.AddSkill(chosen); done(); });
    }
}
