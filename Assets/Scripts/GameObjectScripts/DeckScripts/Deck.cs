using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Deck<T> : MonoBehaviour
{
    public static void Shuffle(List<T> cards)
    {
        int n = cards.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            T value = cards[k];
            cards[k] = cards[n];
            cards[n] = value;
        }
    }
}