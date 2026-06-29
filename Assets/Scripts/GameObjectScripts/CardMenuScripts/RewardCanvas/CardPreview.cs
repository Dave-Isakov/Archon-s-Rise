using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Display-only presentation of a CardsSO for selection contexts (reward screen).
// It renders card data and reports a click back via a callback. It never touches
// the deck or any game state itself.
public class CardPreview : MonoBehaviour, IPointerClickHandler
{
    public CardsSO cardSO;
    [SerializeField] private TextMeshProUGUI cardName;
    [SerializeField] private TextMeshProUGUI cardDescription;
    [Header("Empower Type Colors")]
    [SerializeField] private Color redColor;
    [SerializeField] private Color yellowColor;
    [SerializeField] private Color purpleColor;
    [SerializeField] private Color greenColor;

    private Action<CardsSO> onSelected;

    public void Bind(CardsSO so, Action<CardsSO> onSelected)
    {
        cardSO = so;
        this.onSelected = onSelected;
        cardName.text = so.cardName;
        cardDescription.text = so.cardDescription;
        CardVisuals.ApplyEmpowerColor(gameObject, so.empowerType,
            greenColor, redColor, purpleColor, yellowColor);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onSelected?.Invoke(cardSO);
    }
}
