using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class DiscardPile : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI discardCount;
    [SerializeField] GameObject cardPrefab; // EDITOR STEP: assign the Card prefab in the Inspector
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

    public void RebuildDiscard(List<CardsSO> cards)
    {
        foreach (var c in new List<Card>(this.cards)) if (c != null) Destroy(c.gameObject);
        this.cards.Clear();
        foreach (var so in cards)
        {
            var go = Instantiate(cardPrefab, this.transform);
            var comp = go.GetComponent<Card>();
            comp.cardSO = so;
            comp.InDiscard = true;
            go.name = so.cardName;
            go.SetActive(false);
            this.cards.Add(comp);
        }
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
