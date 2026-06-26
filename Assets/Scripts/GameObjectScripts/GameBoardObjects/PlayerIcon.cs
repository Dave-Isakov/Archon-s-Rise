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

    public void AnimateIcon(Unit unit)
    {
        if(unit.unitSO.cardType.HasFlag(type))
            iconAnimator.SetTrigger(trigger);
    }
}
