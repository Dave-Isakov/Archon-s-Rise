using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class PlayerDeck : Deck<Card>, IPointerClickHandler
{
    public List<CardsSO> deckList = new();
    List<Card> cardsInDeck = new();
    [SerializeField] PlayerSO player;
    [SerializeField] TextMeshProUGUI deckCount;
    PlayManager command;
    ICommands drawCommand;
    [SerializeField] GameObject cardPrefab;
    GameObject playerCard;
    [Header("Deck Events")]
    [SerializeField] PlayerDeckEvent afterRoundEndShuffle_DrawCards;
    [SerializeField] PlayerDeckEvent drawNewCardEvent;

    public List<Card> CardsInDeck { get => cardsInDeck; set => cardsInDeck = value; }

    void Awake()
    {
        foreach(var card in player.StartingHand)
        {
            deckList.Add(card);
        }
        foreach(var card in deckList)
        {
            AddCardToDecklist(card);
        }
        Shuffle(cardsInDeck);
        drawCommand = new CardDrawCommand(drawNewCardEvent, this);
        command = new PlayManager();
    }

    private Card AddCardToDecklist(CardsSO card)
    {
        playerCard = Instantiate(cardPrefab, this.transform);
        var cardComponent = playerCard.GetComponent<Card>();
        CardsInDeck.Add(cardComponent);
        cardComponent.InDeck = true;
        cardComponent.InHand = false;
        cardComponent.cardSO = card;
        playerCard.name = card.cardName;
        playerCard.SetActive(false);
        return cardComponent;
    }

    private void Start()
    {
    }

    void Update()
    {
        deckCount.text = CardsInDeck.Count.ToString();
    }

    public void DataToDrawnCard(GameObject playerCard)
    {
        playerCard.GetComponent<Card>().cardSO = CardsInDeck[0].cardSO;
        CardsInDeck.RemoveAt(0);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        command.AddCommand(drawCommand);
        Debug.Log("Drawing from the deck.");
    }

    public void AddRandomCard()
    {
        var cards = DataManager.Instance.Cards.Items;
        deckList.Add(cards[Random.Range(0, cards.Count)]);
    }

    public void AddRewardToDeck(Card card)
    {
        CardsInDeck.Insert(0, card);
        deckList.Add(card.cardSO);
        AddCardToDecklist(card.cardSO);
    }

    public void EndOfRoundReshuffle()
    {
        foreach(var card in GameManager.Instance.cardListParent.GetComponentsInChildren<Card>())
        {
            CardsInDeck.Add(card);
        }
        Shuffle(CardsInDeck);
        afterRoundEndShuffle_DrawCards.Raise(this);
    }

    public void DeckToCardList()
    {
        if(GameManager.Instance.cardListCanvas.enabled)
        {
            foreach (var card in cardsInDeck)
            {
                card.transform.SetParent(GameManager.Instance.cardListParent.transform);
                card.gameObject.SetActive(true);
            }
        }
        else
        {
            foreach (var card in cardsInDeck)
            {
                card.transform.SetParent(this.transform);
                card.gameObject.SetActive(false);
            }
        }
    }
}