using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class PlayerHand : MonoBehaviour
{
    [SerializeField] PlayerDeck deck;
    [SerializeField] Player player;
    [SerializeField] GameObject card;
    [SerializeField] CardsSO wound;
    [SerializeField] GameObject[] cardPositions;
    [SerializeField] GridLayoutGroup layoutGroup;
    [SerializeField] HandFanLayout handLayout;
    private List<Card> healedWounds = new();
    [SerializeField] Vector2 layoutAdjustment = new Vector2(2, 0);
    GameObject playerCard;
    public List<Card> cardsInPlay = new();
    Card activeCard;
    [Header("Events")]
    [SerializeField] GameObjectEvent newCardDraw;

    void Start()
    {
        if (DataManager.Instance != null && DataManager.Instance.IsLoading) return; // hand rebuilt from save
        // layoutGroup.enabled = false;
        DrawCards(player.PlayerHandSize);
    }

    public void DrawCard()
    {
        TryDrawCard();
    }

    // Kept separate from DrawCard because the scene binds DrawCard to a UnityEvent,
    // which requires a void return.
    private bool TryDrawCard()
    {
        switch (DrawGate.Evaluate(deck.CardsInDeck.Count, cardsInPlay.Count, player.PlayerHandSize))
        {
            case DrawVerdict.HandFull:
                GameManager.Instance.ValidationMessage($"Your max hand size is {player.PlayerHandSize}, you cannot draw anymore cards.");
                return false;
            case DrawVerdict.DeckEmpty:
                GameManager.Instance.ValidationMessage("Your deck is empty. End the Round to reshuffle your discard pile and draw a new hand.");
                return false;
        }

        var drawnCard = deck.CardsInDeck[0];
        drawnCard.gameObject.SetActive(true);
        cardsInPlay.Add(drawnCard);
        deck.CardsInDeck.Remove(drawnCard);
        drawnCard.InHand = true;
        drawnCard.InDeck = false;
        // AnimateCardDraw(drawnCard);
        drawnCard.transform.SetParent(handLayout.Container);
        // drawnCard.GetComponent<Animation>().Play();
        // newCardDraw.Raise(playerCard);
        Relayout();
        return true;
    }

    public void DrawCards(int numberOfCardstoDraw)
    {
        for (var i = 0; i < numberOfCardstoDraw; i++)
        {
            if (!TryDrawCard()) break; // one blocked-draw message, not one per missing card
        }
    }

    public void DrawCardsAtTurnEnd()
    {
        var cardDiff = player.PlayerHandSize - cardsInPlay.Count;
        DrawCards(cardDiff);
        CheckWoundHand();
    }

    public void DrawCardsAtRoundEnd()
    {
        DrawCards(player.PlayerHandSize);
        CheckWoundHand();
    }

    // Round end is a full reset: unplayed hand cards go back into the deck too,
    // so the post-shuffle draw deals a fresh full hand.
    public void ReturnHandToDeck()
    {
        deck.ReturnCardsToDeck(cardsInPlay);
        Relayout();
    }

    public void Relayout() => handLayout.Relayout(cardsInPlay);

    public void RemovePlayedCardsFromHand(Card card)
    {
        cardsInPlay.Remove(card);
        Relayout();
    }

    public void AddWound()
    {
        // Parent to the fan container (like drawn/rebuilt cards) — HandFanLayout
        // only lays out cards whose parent is the container, so a wound parented
        // elsewhere stays stacked at the origin until something reparents it.
        playerCard = Instantiate(card, handLayout.Container);
        var woundCard = playerCard.GetComponent<Card>();
        woundCard.InHand = true;
        woundCard.InDeck = false;
        cardsInPlay.Add(woundCard);
        woundCard.cardSO = wound;
        playerCard.name = woundCard.name;
        Relayout();
        // Wound adds are not undoable commands, so a threshold crossed here
        // can never be un-done back under it — check at the moment of add.
        if (RunEndRules.IsWoundOut(TotalWoundCount()))
            RunEndController.RequestEnd(RunOutcome.WoundOutLoss);
    }

    // Wound-out counts every Wound card the run owns: hand + deck + discard.
    // healedWounds are excluded — they're out of the run unless an undo
    // restores them, and every wound ADD re-runs this check anyway.
    public int TotalWoundCount()
    {
        int count = 0;
        foreach (var c in cardsInPlay)
            if (c != null && c.cardSO != null && c.cardSO.cardType == StatType.Wound) count++;
        foreach (var c in deck.CardsInDeck)
            if (c != null && c.cardSO != null && c.cardSO.cardType == StatType.Wound) count++;
        var discardPile = FindAnyObjectByType<DiscardPile>();
        if (discardPile != null)
            foreach (var c in discardPile.Cards)
                if (c != null && c.cardSO != null && c.cardSO.cardType == StatType.Wound) count++;
        return count;
    }

    // A hand that tops up to full holding nothing but wounds is unplayable — loss.
    private void CheckWoundHand()
    {
        int wounds = 0;
        foreach (var c in cardsInPlay)
            if (c != null && c.cardSO != null && c.cardSO.cardType == StatType.Wound) wounds++;
        if (RunEndRules.IsWoundHand(cardsInPlay.Count, wounds, player.PlayerHandSize))
            RunEndController.RequestEnd(RunOutcome.WoundHandLoss);
    }

    public void HealWound()
    {
        if(cardsInPlay.Exists(card => card.cardSO.cardType == StatType.Wound))
        {
            var healWound = GetHealedWound();
            healWound.SetActive(false);
            Relayout();
        }
    }

    public void RestoreHealedWound()
    {
        if(healedWounds.Count > 0)
        {
            cardsInPlay.Add(healedWounds[0]);
            healedWounds[0].gameObject.SetActive(true);
            healedWounds.RemoveAt(0);
            Relayout();
        }
    }

    // Healed wounds are kept only so an undo of the heal can restore them. Called
    // when the undo stack clears: past that point they can never come back, and
    // leaving them would let a later heal-undo resurrect a wound that was
    // permanently healed in an earlier turn or round.
    public void PurgeHealedWounds()
    {
        foreach (var wound in healedWounds)
            if (wound != null) Destroy(wound.gameObject);
        healedWounds.Clear();
    }

    private GameObject GetHealedWound()
    {
        var healWound = cardsInPlay.FindAll(h => h.cardSO.cardType == StatType.Wound)[0];
        cardsInPlay.Remove(healWound);
        healedWounds.Add(healWound);
        return healWound.gameObject;
    }

    public void Heal(Card card)
    {
        if(card.cardSO.cardType == StatType.Heal && card.IsPlayed)
        {
            if(!card.IsEmpowered)
                for(var i = 0; i < card.cardSO.healAmount; i++)
                    HealWound();
            else if(card.IsEmpowered)
                for(var i = 0; i < card.cardSO.empowerHealAmount; i++)
                    HealWound();
        }

        else if(card.cardSO.cardType == StatType.Heal && !card.IsPlayed)
        {
            if(!card.IsEmpowered)
                for(var i = 0; i < card.cardSO.healAmount; i++)
                    RestoreHealedWound();
            else if(card.IsEmpowered)
                for(var i = 0; i < card.cardSO.empowerHealAmount; i++)
                    RestoreHealedWound();                    
        }
    }

    public void TownHeal(TownToken town)
    {  
        for(var i = 0; i < town.townSO.healLevel; i++)
            HealWound();
    }

    //Cleans up wounds that were set inactive during the turn due to healing.
    public void CleanUp()
    {
        foreach(var inactiveCard in FindObjectsByType<Card>(FindObjectsInactive.Include))
        {
            if(!inactiveCard.gameObject.activeSelf && inactiveCard.cardSO.cardType == StatType.Wound && inactiveCard.IsPlayed)
            {
                Destroy(inactiveCard.gameObject);
            }
        }
    }

    public void HandToCardList()
    {
        if(GameManager.Instance.cardListCanvas.enabled)
        {
            foreach (var card in cardsInPlay)
                card.transform.SetParent(GameManager.Instance.cardListParent.transform);
        }
        else
        {
            foreach (var card in cardsInPlay)
                card.transform.SetParent(handLayout.Container);
            Relayout();
        }
    }

    // public void AnimateCardDraw(Card card)
    // {
    //     var sequence = DOTween.Sequence();
    //     sequence.Append(card.transform.DOMove(cardPositions[cardsInPlay.IndexOf(card)].transform.position, 1.25f));
    //     sequence.Append(card.transform.DORotate(new Vector3(0, 90,0), .5f).OnComplete(() => card.GetComponentsInChildren<Image>()[3].gameObject.SetActive(false)));
    //     sequence.Append(card.transform.DORotate(new Vector3(0, 0,0), 1f));
    // }

    public void RebuildHand(List<CardsSO> cards)
    {
        foreach (var c in new List<Card>(cardsInPlay)) if (c != null) Destroy(c.gameObject);
        cardsInPlay.Clear();
        foreach (var so in cards)
        {
            var go = Instantiate(card, handLayout.Container);
            var comp = go.GetComponent<Card>();
            comp.cardSO = so;
            comp.InHand = true;
            comp.InDeck = false;
            go.name = so.cardName;
            go.SetActive(true);
            cardsInPlay.Add(comp);
        }
        Relayout();
    }
}
