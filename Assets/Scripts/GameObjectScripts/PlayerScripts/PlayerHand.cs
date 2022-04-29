using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEditor;

public class PlayerHand : MonoBehaviour
{
    [SerializeField] PlayerDeck deck;
    [SerializeField] GameObject card;
    [SerializeField] CardsSO wound;
    // [SerializeField] MessagePopUp message;
    public List<CardsSO> cardsInHand = new List<CardsSO>();
    [SerializeField] Vector2 layoutAdjustment = new Vector2(2, 0);
    GameObject playerCard;
    public List<GameObject> playerCardsInPlay = new List<GameObject>();
    Card activeCard;
    GameManager gm;

    [Header("Events")]
    [SerializeField] GameObjectEvent newCardDraw;

    private int cardID = 0;


    // void Awake()
    // {
    //     gm = FindObjectOfType<GameManager>();
    // }
    void Start()
    {
    }

    // void Update() 
    // {
    // }
    public void DrawCard(PlayerDeck deck)
    {
        if (deck.cards.Count >= 1 && playerCardsInPlay.Count < DataManager.Instance.playerHandSize)
        {
            playerCard = Instantiate(card, new Vector3(0,0,0), Quaternion.identity);
            playerCard.name = playerCard.name.ToString() + cardID;
            cardID++;
            playerCard.transform.SetParent(this.transform, false);
            playerCardsInPlay.Add(playerCard);
            newCardDraw.Raise(playerCard);
            //adjusts spacing between the cards when drawn
            var playerHandSizeLayout = GetComponent<GridLayoutGroup>();
            playerHandSizeLayout.spacing += layoutAdjustment;
        }
        else
        {
            GameManager.Instance.ValidationMessage($"Your max hand size is {DataManager.Instance.playerHandSize}, you cannot draw anymore cards.");
        }

    }
        // else if (deck.cards.Count == 0)
        // {
        //     message.ValidationMessage("Your deck is empty.");
        // }
        // else if (cardsInHand.Count >= gm.playerHandSize)
        // {
        //     message.ValidationMessage($"Your hand is too big, you can only have {gm.playerHandSize} cards in your hand.");

    // public void DrawCards(int numberOfCardstoDraw)
    // {
    //     for (var i = 0; i < numberOfCardstoDraw; i++)
    //     {
    //         DrawCards();
    //     }
    // }

    // private void SetNewCardObjectData(ScriptableObject SO)
    // {
    //     activeCard = playerCard.gameObject.GetComponent<Card>();
    //     activeCard.inHand = true;
    //     cardsInHand.Add((CardsSO)SO);
    //     activeCard.cardSO = (CardsSO)SO;
    // }

    public void AddWound()
    {
        playerCard = Instantiate(card, new Vector3(0,0,0), Quaternion.identity);
        playerCard.name = playerCard.name.ToString() + cardID;
        cardID++;
        playerCard.transform.SetParent(this.transform, false);
        playerCardsInPlay.Add(playerCard);
        playerCard.GetComponent<Card>().cardSO = wound;
    }
}
