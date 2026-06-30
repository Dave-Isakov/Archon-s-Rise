using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

[System.Serializable]
public class Card : MonoBehaviour, IPointerClickHandler
{
    public CardsSO cardSO;
    private bool isPlayed;
    [SerializeField] bool inDeck;
    [SerializeField] bool inDiscard;
    [SerializeField] bool inHand;
    [SerializeField] private bool isEmpowered;
    private bool isMaximized;
    private Vector2 startPosition;
    public int cardIndex = 0;
    public int siblingIndex;
    [SerializeField] private TextMeshProUGUI cardName;
    [SerializeField] private TextMeshProUGUI cardDescription;
    [SerializeField] Image playedIcon;
    
    [Header("Card Events")]
    [SerializeField] CardEvent onClick_OpenCardMenu;
    [SerializeField] CardEvent onClick_CloseCardMenu;
    [SerializeField] CardEvent onOpenCardMenu_MaximizeCard;
    [SerializeField] CardEvent onCloseCardMenu_MinimizeCard;
    [SerializeField] CardEvent onTurnEnd_CleanUpPlayedCard;
    [Header("Empower Type Colors")]
    [SerializeField] private Color redColor;
    [SerializeField] private Color yellowColor;
    [SerializeField] private Color purpleColor;
    [SerializeField] private Color greenColor;
    [SerializeField] private Color woundGrey = new Color(0.55f, 0.55f, 0.55f, 1f);

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
    public bool InDeck { get => inDeck; set => inDeck = value; }
    public bool InHand { get => inHand; set => inHand = value; }
    public bool InDiscard { get => inDiscard; set => inDiscard = value; }

    void Start() 
    {
        siblingIndex = this.gameObject.transform.GetSiblingIndex();
        cardName.text = cardSO.cardName;
        cardDescription.text = cardSO.cardDescription;
        GetEmpowerTypeColor(this.gameObject);
        Debug.Log(cardSO.cardName + " (" + this.gameObject.name + ")");
    }

    void Update()
    {
        if(isPlayed)
        {
            playedIcon.enabled = true;
        }
        else
        {
            playedIcon.enabled = false;
        }
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
        if (cardSO.cardType == StatType.Wound)
        {
            CardVisuals.ApplyWoundStyle(card, woundGrey);
            return;
        }
        CardVisuals.ApplyEmpowerColor(card, cardSO.empowerType,
            greenColor, redColor, purpleColor, yellowColor);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameManager.Instance.cardListCanvas.enabled) return;

        var inspector = FindAnyObjectByType<CardInspector>();
        if (inspector == null) return;
        if (isMaximized)
        {
            inspector.Close(); // Close() returns this card to the hand and clears isMaximized
        }
        else if (GameManager.Instance.cardCanvas.enabled)
        {
            // Another card's menu is already open. Without this guard each click
            // maximizes a new card and pulls it out of the hand, draining the hand.
            return;
        }
        else if (!isPlayed)
        {
            onOpenCardMenu_MaximizeCard.Raise(this);
            inspector.Open(this);
            isMaximized = true;
        }
        else
        {
            GameManager.Instance.ValidationMessage(
                $"{cardSO.name} has already been played. Click Undo on the Gameboard to undo previous plays.");
        }
    }

    // Arcs the card from its fan slot to the centre, scaling up over ~0.25s.
    public void SetCardObjectToMax(Card card)
    {
        var t = card.gameObject.transform;
        t.SetParent(GameManager.Instance.enlargeCardPosition.transform, true);
        t.DOKill();
        Vector3 target = GameManager.Instance.enlargeCardPosition.transform.position;
        t.DOMove(target, 0.25f).SetEase(Ease.OutBack);
        t.DOScale(new Vector3(4f, 4f, 1f), 0.25f).SetEase(Ease.OutBack);
        t.DOLocalRotate(Vector3.zero, 0.25f).SetEase(Ease.OutBack);
        card.isMaximized = true;
    }

    // Returns the card to its fan slot (tweened). Reparents first so Relayout can
    // compute the slot pose, then animates from the remembered centre position back.
    public void SetCardObjectToNormal(Card card)
    {
        var t = card.gameObject.transform;
        Vector3 fromWorld = t.position;

        var hand = GameManager.Instance.playerHand.GetComponentInChildren<HandFanLayout>();
        t.SetParent(hand.Container, false);
        card.isMaximized = false;
        GameManager.Instance.playerHand.GetComponent<PlayerHand>().Relayout();

        Vector3 toWorld      = t.position;
        Vector3 toScale      = t.localScale;
        Vector3 toLocalEuler = t.localEulerAngles;

        t.position   = fromWorld;
        t.localScale = new Vector3(4f, 4f, 1f);
        t.DOKill();
        t.DOMove(toWorld, 0.22f).SetEase(Ease.OutCubic);
        t.DOScale(toScale, 0.22f).SetEase(Ease.OutCubic);
        t.DOLocalRotate(toLocalEuler, 0.22f).SetEase(Ease.OutCubic);
    }

    // Returns the enlarged card to the hand and clears the maximized flag. Invoked by the
    // inspector on every close path (Back, click-off, Play), so a played card
    // (IsPlayed == true) lands back in the fan and can't be reopened to re-trigger Play.
    public void ReturnToHand()
    {
        onCloseCardMenu_MinimizeCard.Raise(this);
        isMaximized = false;
    }

    //Events for discarded cards due to being played
    public void PlayedCardDiscard()
    {
        if(this.isPlayed)
        {
            onTurnEnd_CleanUpPlayedCard.Raise(this);
            this.isPlayed = false;
            this.inHand = false;
        }
    }
}