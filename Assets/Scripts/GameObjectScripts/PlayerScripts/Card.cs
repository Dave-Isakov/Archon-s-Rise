using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Card : MonoBehaviour, IPointerClickHandler
{
    public CardsSO cardSO;
    private bool isPlayed;
    private bool isReward;
    [SerializeField] private bool isEmpowered;
    private bool isMaximized;
    private Vector2 startPosition;
    public int cardIndex = 0;
    // [SerializeField] private GameEvent gameEvent;
    [SerializeField] private TextMeshProUGUI cardName;
    [SerializeField] private TextMeshProUGUI cardDescription;
    
    [Header("Card Events")]
    [SerializeField] CardEvent onClick_OpenCardMenu;
    [SerializeField] CardEvent onClick_CloseCardMenu;
    [SerializeField] CardEvent onOpenCardMenu_MaximizeCard;
    [SerializeField] CardEvent onCloseCardMenu_MinimizeCard;
    [SerializeField] CardEvent onRewardSelect_AddCardToDeck;

    [Header("Empower Type Colors")]
    [SerializeField] private Color redColor;
    [SerializeField] private Color yellowColor;
    [SerializeField] private Color purpleColor;
    [SerializeField] private Color greenColor;

    public bool IsPlayed
    {
        get
        {
            return isPlayed;
        }
        set
        {
            isPlayed = value;
        }
    }
    public bool IsEmpowered
    {
        get
        {
            return isEmpowered;
        }
        set
        {
            isEmpowered = value;
        }
    }

    public bool IsReward
    {
        get
        {
            return isReward;
        }
        set
        {
            isReward = value;
        }
    }
    void Start() 
    {
        cardName.text = cardSO.cardName;
        cardDescription.text = cardSO.cardDescription;
        GetEmpowerTypeColor(this.gameObject);
        Debug.Log(cardSO.cardName + " (" + this.gameObject.name + ")");
    }

    void Update()
    {

        // if (Keyboard.current.spaceKey.isPressed && isMaximized && !cardSO.isChoice)
        // {
        //     var playButton = FindObjectOfType<PlayButton>();
        //     playButton.PlayCard();
        // }
        // else if(Keyboard.current.spaceKey.isPressed && isMaximized && cardSO.isChoice)
        // {
        //     var button = GameObject.Find("PlayButton").GetComponent<PlayButton>();
        //     button.CannotPlay();
        // }

        //Removes card object from the board when played and moves it to the discard pile.
        // if(isPlayed == true)
        // {
        //     Toggle empowerToggle = GameObject.Find("EmpowerToggle").GetComponent<Toggle>();
        //     Toggle improvToggle = GameObject.Find("ImprovToggle").GetComponent<Toggle>();
        //     if(cardSO.isChoice && !improvToggle.isOn)
        //     {
        //         var choiceToggles = FindObjectOfType<ChoiceToggles>();
        //         choiceToggles.SetDefaultToggle();
        //     }
        //     empowerToggle.isOn = false;
        //     improvToggle.isOn = false;
        //     playerHand.playerCardsInPlay.Remove(playerHand.playerCardsInPlay[cardIndex]);
        //     isPlayed = false;
        //     Destroy(card, 10f * Time.deltaTime);
        //     gm.cardCanvas.SetActive(false);
        // }
        // if (isDragging)
        // {
        //     transform.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        //}
    }

    private void GetEmpowerTypeColor(GameObject card)
    {
        var frontImage = card.GetComponentsInChildren<Image>()[1];
        if(cardSO.empowerType == EmpowerType.Green)
            frontImage.color = greenColor;
        if(cardSO.empowerType == EmpowerType.Red)
            frontImage.color = redColor;
        if(cardSO.empowerType == EmpowerType.Purple)
            frontImage.color = purpleColor;
        if(cardSO.empowerType == EmpowerType.Yellow)
            frontImage.color = yellowColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(!isReward)
        {
            if(isMaximized)
            {
                onCloseCardMenu_MinimizeCard.Raise(this);
                onClick_CloseCardMenu.Raise(this);
            }
            else if(!isMaximized)
            {
                onClick_OpenCardMenu.Raise(this);
                onOpenCardMenu_MaximizeCard.Raise(this);
            }
        }
        else if(isReward)
        {
            onRewardSelect_AddCardToDeck.Raise(this);
        }
    }

    //Maximizes the card under the card menu canvas.
    public void SetCardObjectToMax(Card card)
    {
        card.gameObject.transform.SetParent(GameManager.Instance.enlargeCardPosition.transform, true);
        card.gameObject.transform.position = GameManager.Instance.enlargeCardPosition.transform.position;
        card.gameObject.transform.localScale = new Vector3(4, 4, 0);
        card.gameObject.tag = "Card";
        card.isMaximized = true;
    }

    //Returns the card to normal size in the player hand.
    public void SetCardObjectToNormal(Card card)
    {
        card.gameObject.transform.SetParent(GameManager.Instance.playerHand.transform, false);
        card.gameObject.transform.localScale = new Vector3(1, 1, 0);
        card.gameObject.tag = "Untagged";
        card.isMaximized = false;
    }
}