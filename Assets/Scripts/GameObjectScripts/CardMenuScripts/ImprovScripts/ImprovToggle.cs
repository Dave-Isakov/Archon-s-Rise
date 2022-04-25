using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ImprovToggle : Improv, IPointerClickHandler
{
    [SerializeField] Toggle improvToggle;
    [SerializeField] ToggleEvent OnToggle_ButtonsToggleOnOff;
    [SerializeField] TextMeshProUGUI improvText;
    [SerializeField] TextMeshProUGUI improvButtonsText;
    void Start()
    {

    }

    void Update()
    {
        if(_card is not null)
        if(_card.IsPlayed == true)        
        {
            improvToggle.interactable = false;
            improvButtonsText.enabled = false;
        }
        else
            improvToggle.interactable = true;
    }
    
    public void SetToggle()
    {
        if(!improvToggle.isOn && !_card.IsPlayed)
        {
            improvToggle.isOn = false;
            improvButtonsText.enabled = false;
        }
        else if(improvToggle.isOn && _card.IsPlayed)
        {
            improvToggle.isOn = true;
            improvButtonsText.enabled = false;
        }
        else if (improvToggle.isOn)
            improvToggle.isOn = false;
    }

    public void ToggleImprovButtons()
    {
        if(improvToggle.isOn)
        {
            OnToggle_ButtonsToggleOnOff.Raise(improvToggle);
            improvText.enabled = true;
            improvButtonsText.enabled = true;
        }
        if(!improvToggle.isOn)
        {
            OnToggle_ButtonsToggleOnOff.Raise(improvToggle);
            improvText.enabled = false;
            improvButtonsText.enabled = false;
        }
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if(_card.IsPlayed && !improvToggle.interactable)
            GameManager.Instance.ValidationMessage($"{_card.cardSO.name} has already been played. Click Undo on the Gameboard to undo previous plays.");
    }
}
