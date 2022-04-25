using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Improv : MonoBehaviour
{
    protected Card _card;

    public void SetCard(Card card)
    {
        _card = card;
    }
}
