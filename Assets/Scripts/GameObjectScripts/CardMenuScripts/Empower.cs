using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Empower : MonoBehaviour, IPointerClickHandler
{
    Card card;
    [SerializeField] Toggle empowerToggle;
    [SerializeField] ToggleEvent onToggle_SendCardDataToCrystals;

    void Start()
    {
        empowerToggle.onValueChanged.AddListener(delegate {
        ToggleEmpower(empowerToggle);
        });
    }
    void Update()
    {
        if(card is not null)
            if(card.IsPlayed == true)        
                empowerToggle.interactable = false;
            else
                empowerToggle.interactable = true;
    }

    public void SetCard(Card card)
    {
        this.card = card;
        if(card.IsPlayed && card.IsEmpowered)
        {
            card.IsEmpowered = true;
            empowerToggle.interactable = false;
            empowerToggle.isOn = true;
        }
        else
        {
            card.IsEmpowered = false;
            empowerToggle.interactable = true;
            empowerToggle.isOn = false;
        }
    }

    public void ToggleEmpower(Toggle empower)
    {
        if(empower.isOn)
        {
            onToggle_SendCardDataToCrystals.Raise(empower);
            card.IsEmpowered = true;
        }
        else
        {
            empower.isOn = false;
            card.IsEmpowered = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(card.IsPlayed && !empowerToggle.interactable)
            GameManager.Instance.ValidationMessage($"{card.cardSO.name} has already been played. Click Undo on the Gameboard to undo previous plays.");
    }
}
