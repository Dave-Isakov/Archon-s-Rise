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
        drawCommand = new CardDrawCommand(drawNewCardEvent, this);
        command = new PlayManager();

        if (DataManager.Instance != null && DataManager.Instance.IsLoading) return; // deck rebuilt from save

        foreach(var card in player.StartingHand)
        {
            deckList.Add(card);
        }
        foreach(var card in deckList)
        {
            AddCardToDecklist(card);
        }
        Shuffle(cardsInDeck);
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

    // The single path for granting a card into the deck from card data.
    // Used by rewards (and any future grant). toTop=true makes it the next draw.
    public Card AddCard(CardsSO so, bool toTop = false)
    {
        var card = AddCardToDecklist(so); // instantiates, appends to CardsInDeck, sets flags, inactive
        deckList.Add(so);
        if (toTop)
        {
            CardsInDeck.Remove(card);
            CardsInDeck.Insert(0, card);
        }
        return card;
    }

    // Round end is a full reset: discard pile and unplayed hand both return to the
    // deck, then the shuffleEvent draws a fresh full hand. Orchestrated here in one
    // sequence because listener order on the round-end event is not guaranteed.
    public void EndOfRoundReshuffle()
    {
        var discard = FindAnyObjectByType<DiscardPile>();
        if (discard != null) discard.ReshuffleToDeck();
        var hand = FindAnyObjectByType<PlayerHand>();
        if (hand != null) hand.ReturnHandToDeck();
        Shuffle(CardsInDeck);
        afterRoundEndShuffle_DrawCards.Raise(this);
    }

    // Single path for cards re-entering the deck from another zone. Clears per-play
    // state so a recycled card behaves like a fresh draw (a discarded card keeps
    // IsEmpowered set, which would otherwise replay with empowered values for free).
    public void ReturnCardsToDeck(List<Card> cards)
    {
        foreach (var card in cards)
        {
            card.InDeck = true;
            card.InHand = false;
            card.InDiscard = false;
            card.IsPlayed = false;
            card.IsEmpowered = false;
            card.transform.SetParent(this.transform);
            card.gameObject.SetActive(false);
        }
        CardZonePlan.TransferAll(cards, CardsInDeck);
    }

    public void RebuildDeck(List<CardsSO> orderedCards)
    {
        foreach (var c in new List<Card>(CardsInDeck)) if (c != null) Destroy(c.gameObject);
        CardsInDeck.Clear();
        deckList.Clear();
        foreach (var so in orderedCards)
        {
            deckList.Add(so);
            AddCardToDecklist(so); // appends in order; sets InDeck=true, inactive
        }
    }
}