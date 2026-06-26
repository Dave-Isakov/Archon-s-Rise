using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class DiscardPile : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI discardCount;
    List<Card> cards = new();
    
    void Update()
    {
        discardCount.text = cards.Count.ToString();
    }

    public List<Card> Cards => cards;

    public void AddCardToDiscard(Card card)
    {
        cards.Add(card);
        card.transform.SetParent(this.transform);
        card.InDiscard = true;
        card.gameObject.SetActive(false);
    }

    public void ReshuffleToDeck()
    {
        cards.Clear();
    }

    public void SetCardList()
    {
        if(GameManager.Instance.cardListCanvas.enabled)
        {
            foreach (var card in cards)
            {
                card.transform.SetParent(GameManager.Instance.cardListParent.transform);
                card.gameObject.SetActive(true);
            }
        }
        else
        {
            foreach (var card in cards)
            {
                card.transform.SetParent(this.transform);
                card.gameObject.SetActive(false);
            }
        }
    }
}
