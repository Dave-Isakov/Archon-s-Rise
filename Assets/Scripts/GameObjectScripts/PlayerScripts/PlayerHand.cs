using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEditor;

public class PlayerHand : MonoBehaviour
{
    [SerializeField] PlayerDeck deck;
    [SerializeField] Player player;
    [SerializeField] GameObject card;
    [SerializeField] CardsSO wound;
    private List<Card> healedWounds = new();
    // [SerializeField] MessagePopUp message;
    public List<CardsSO> cardsInHand = new List<CardsSO>();
    [SerializeField] Vector2 layoutAdjustment = new Vector2(2, 0);
    GameObject playerCard;
    public List<Card> cardsInPlay = new();
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
        if (deck.cards.Count >= 1 && cardsInPlay.Count < player.PlayerHandSize)
        {
            playerCard = Instantiate(card, new Vector3(0,0,0), Quaternion.identity);
            playerCard.name = playerCard.name.ToString() + cardID;
            cardID++;
            playerCard.transform.SetParent(this.transform, false);
            cardsInPlay.Add(playerCard.GetComponent<Card>());
            newCardDraw.Raise(playerCard);
            //adjusts spacing between the cards when drawn
            var playerHandSizeLayout = GetComponent<GridLayoutGroup>();
            playerHandSizeLayout.spacing += layoutAdjustment;
        }
        else
        {
            GameManager.Instance.ValidationMessage($"Your max hand size is {player.PlayerHandSize}, you cannot draw anymore cards.");
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
        cardsInPlay.Add(playerCard.GetComponent<Card>());
        playerCard.GetComponent<Card>().cardSO = wound;
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

    public void TownHeal(TownCard town)
    {  
        for(var i = 0; i < town.townSO.healLevel; i++)
            HealWound();
    }
}
