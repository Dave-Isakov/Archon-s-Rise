using System.Collections.Generic;
using UnityEngine;

// Runs the level-up payout queue. Rewards resolve strictly in order — skill
// pick(s), then card pick(s), then whatever the next pending level enqueued —
// one modal at a time. Fixed bonuses (HP/hand/army) never enter the queue:
// Player applies HP directly and the sizes are derived.
public class LevelUpController : MonoBehaviour
{
    [SerializeField] Player player;
    [SerializeField] LevelUpModal modal;
    [SerializeField] Rewards rewards;

    readonly Queue<System.Action> pending = new();
    readonly System.Random rng = new System.Random();
    bool resolving;

    // Save gate: mid-payout is not a settled state.
    public bool Busy => resolving || pending.Count > 0;

    public void EnqueueLevelRewards(int level, LevelRewardEntry entry)
    {
        GameManager.Instance.ValidationMessage($"You reached level {level}!");
        if (entry == null) return;

        for (int i = 0; i < entry.skillPicks; i++) pending.Enqueue(OfferSkillPick);
        for (int i = 0; i < entry.cardPicks; i++) pending.Enqueue(OfferCardPick);
        TryNext();
    }

    void TryNext()
    {
        if (resolving || pending.Count == 0) return;
        // Wait until the screen is clear before opening a pick: the level-up
        // announcement (or any other message) must be dismissed, and any card
        // reward already up must resolve first. An enemy defeat opens its card
        // reward synchronously while the exp it granted levels the player a
        // frame later — without this check the skill modal opens underneath it.
        var gm = GameManager.Instance;
        bool screenBusy = gm.messageCanvas.enabled
            || (gm.cardRewardCanvas != null && gm.cardRewardCanvas.enabled);
        if (screenBusy) { Invoke(nameof(TryNext), 0.25f); return; }
        resolving = true;
        pending.Dequeue().Invoke();
    }

    void OfferSkillPick()
    {
        var choices = LevelRules.DrawSkillChoices(player.LevelRewards.SkillPool,
            new List<SkillsSO>(player.Skills), rng, 3);
        if (choices.Count == 0) { Done(); return; } // pool exhausted: skip the pick
        modal.Offer(choices, chosen => { player.AddSkill(chosen); Done(); });
    }

    void OfferCardPick()
    {
        rewards.OfferCardChoiceForLevel(player.PlayerLevel, Done);
    }

    void Done()
    {
        resolving = false;
        TryNext();
    }
}
