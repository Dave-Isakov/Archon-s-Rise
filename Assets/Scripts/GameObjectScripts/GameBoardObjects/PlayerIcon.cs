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
        if(card.cardSO.cardType.HasFlag(type))
            iconAnimator.SetTrigger(trigger);
    }

    // Unit options apply a single effect through the pop-out (no Card), so the
    // pop-out flow pulses icons by StatType. Same gate as AnimateIcon(Card):
    // only the icon whose stat matches the applied effect fires.
    public void AnimateStat(StatType stat)
    {
        if(stat.HasFlag(type))
            iconAnimator.SetTrigger(trigger);
    }
}
