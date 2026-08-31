using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIcon : MonoBehaviour
{
    [SerializeField] Animator iconAnimator;
    [SerializeField] StatType type;
    [SerializeField] string trigger;

    public void AnimateIcon(Card card)
    {
        if (card == null || card.cardSO == null) return;
        if (IconPulseRules.ShouldPulse(card.cardSO.cardType, type, card.IsUndoingPlay))
            Pulse();
    }

    // The choice/improvise events name the stat directly, so the card's own type
    // needn't match — but the undo gate still applies.
    public void AnimateChosenStat(Card card)
    {
        if (card == null) return;
        if (IconPulseRules.ShouldPulse(type, type, card.IsUndoingPlay))
            Pulse();
    }

    // Unit options apply a single effect through the pop-out (no Card), so the
    // pop-out flow pulses icons by StatType. Same gate as AnimateIcon(Card):
    // only the icon whose stat matches the applied effect fires. Callers
    // (PulseStatIcon, PulseConvert) are apply-only, so there is no undo case.
    public void AnimateStat(StatType stat)
    {
        if (IconPulseRules.ShouldPulse(stat, type, false))
            Pulse();
    }

    void Pulse()
    {
        if (iconAnimator != null && !string.IsNullOrEmpty(trigger))
            iconAnimator.SetTrigger(trigger);
    }
}
