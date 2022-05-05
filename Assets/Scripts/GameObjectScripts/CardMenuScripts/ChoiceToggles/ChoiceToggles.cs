using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class ChoiceToggles  : MonoBehaviour, CardMenuInterface
{
    Card _card;
    [SerializeField] protected Toggle thisToggle;
    [SerializeField] Text toggleText;
    [SerializeField] ToggleEvent onToggle_SetChoiceStatForPlay;

    protected StatType _cardType;
    void Start()
    {
        
    }
    // Update is called once per frame
    void Update()
    {
        if (_card is not null)
        {
            if(!thisToggle.interactable)
            {
                toggleText.enabled = false;
                thisToggle.isOn = false;
            }
            else
            {
                toggleText.enabled = true;
            }
            if(_card.IsPlayed)
                thisToggle.interactable = false;
        }
    }

    public void SetCard(Card card)
    {
        _card = card;
    }

    public void ActivateToggles(Card card)
    {   
        if (card is not null)
        foreach(StatType type in Enum.GetValues(typeof(StatType)))
        {
            if (card.cardSO.isChoice && card.cardSO.cardType.HasFlag(_cardType))
                thisToggle.interactable = true;
            else
                thisToggle.interactable = false;
        }
    }
    public void DeactivateToggle(Toggle toggle)
    {
        if(toggle.isOn)
            thisToggle.interactable = false;

        if(!toggle.isOn)
            SetDefaultToggleStatus();
    }

    public void SetDefaultToggleStatus()
    {
        ActivateToggles(_card);
    }

    public void Off()
    {
        if(thisToggle.interactable)
            thisToggle.interactable = false;
    }

    public void ResetToggle(Toggle toggle)
    {
        if(toggle.isOn)
        {
            this.gameObject.SetActive(false);
        }
        else
        {
            this.gameObject.SetActive(true);
        }
    }

    public void SetToggleListener()
    {
        onToggle_SetChoiceStatForPlay.Raise(thisToggle);
    }
}
