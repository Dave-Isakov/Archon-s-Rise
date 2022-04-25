using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class PlayerDeck : Deck<CardsSO>, IPointerClickHandler
{
    public List<CardsSO> cards = new();
    [SerializeField] private PlayerSO player;

    [Header("Deck Events")]
    [SerializeField] private PlayerDeckEvent shuffleEvent;
    [SerializeField] private PlayerDeckEvent drawNewCardEvent;
    [SerializeField] TextMeshProUGUI deckCount;
    public bool inDeck;
    PlayManager command;
    ICommands drawCommand;
    
    void Awake()
    {
        foreach(var card in player.StartingHand)
        {
            cards.Add(card);
        }
        Shuffle(cards);
        drawCommand = new CardDrawCommand(drawNewCardEvent, this);
        command = new PlayManager();
    }

    void Update()
    {
        deckCount.text = cards.Count.ToString();
    }

    public void RemoveTopCard(CardsSO card)
    {
        cards.Remove(card);
    }

    public void DataToDrawnCard(GameObject playerCard)
    {
        playerCard.GetComponent<Card>().cardSO = cards[0];
        cards.RemoveAt(0);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        command.AddCommand(drawCommand);
        Debug.Log("Drawing from the deck.");
    }
}