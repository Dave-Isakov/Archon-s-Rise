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
    private List<Card> healedWounds = new();
    [SerializeField] Vector2 layoutAdjustment = new Vector2(2, 0);
    GameObject playerCard;
    public List<Card> cardsInPlay = new();
    Card activeCard;
    [Header("Events")]
    [SerializeField] GameObjectEvent newCardDraw;

    void Start()
    {
        // layoutGroup.enabled = false;
        DrawCards(player.PlayerHandSize);
    }

    public void DrawCard()
    {
        if (deck.CardsInDeck.Count >= 1 && cardsInPlay.Count < player.PlayerHandSize)
        {
            var drawnCard = deck.CardsInDeck[0];
            drawnCard.gameObject.SetActive(true);
            cardsInPlay.Add(drawnCard);
            deck.CardsInDeck.Remove(drawnCard);
            drawnCard.InHand = true;
            drawnCard.InDeck = false;
            // AnimateCardDraw(drawnCard);
            drawnCard.transform.SetParent(GetComponentInChildren<GridLayoutGroup>().transform);
            // drawnCard.GetComponent<Animation>().Play();
            // newCardDraw.Raise(playerCard);
            //adjusts spacing between the cards when drawn
            // var playerHandSizeLayout = GetComponent<GridLayoutGroup>();
            // playerHandSizeLayout.spacing += layoutAdjustment;
        }
        else
        {
            GameManager.Instance.ValidationMessage($"Your max hand size is {player.PlayerHandSize}, you cannot draw anymore cards.");
        }
    }
    public void DrawCards(int numberOfCardstoDraw)
    {
        for (var i = 0; i < numberOfCardstoDraw; i++)
        {
            DrawCard();
        }
    }

    public void DrawCardsAtTurnEnd()
    {
        var cardDiff = player.PlayerHandSize - cardsInPlay.Count;
        DrawCards(cardDiff);
    }

    public void DrawCardsAtRoundEnd()
    {
        DrawCards(player.PlayerHandSize);
    }

    public void RemovePlayedCardsFromHand(Card card)
    {
        cardsInPlay.Remove(card);
    }

    public void AddWound()
    {
        playerCard = Instantiate(card, this.transform);
        var woundCard = playerCard.GetComponent<Card>();
        cardsInPlay.Add(woundCard);
        woundCard.cardSO = wound;
        playerCard.name = woundCard.name;
    }

    public void HealWound()
    {
        if(cardsInPlay.Exists(card => card.cardSO.cardType == StatType.Wound))
        {
            var healWound = GetHealedWound();
            healWound.SetActive(false);
        }
    }

    public void RestoreHealedWound()
    {
        if(healedWounds.Count > 0)
        {
            cardsInPlay.Add(healedWounds[0]);
            healedWounds[0].gameObject.SetActive(true);
            healedWounds.RemoveAt(0);
        }
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
        foreach(var inactiveCard in FindObjectsOfType<Card>(true))
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
                card.transform.SetParent(GetComponentInChildren<GridLayoutGroup>().transform);
        }
    }

    // public void AnimateCardDraw(Card card)
    // {
    //     var sequence = DOTween.Sequence();
    //     sequence.Append(card.transform.DOMove(cardPositions[cardsInPlay.IndexOf(card)].transform.position, 1.25f));
    //     sequence.Append(card.transform.DORotate(new Vector3(0, 90,0), .5f).OnComplete(() => card.GetComponentsInChildren<Image>()[3].gameObject.SetActive(false)));
    //     sequence.Append(card.transform.DORotate(new Vector3(0, 0,0), 1f));
    // }
}
