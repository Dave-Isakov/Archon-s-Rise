using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public abstract class ImprovButtons : Improv
{
    [SerializeField] protected Button thisButton;
    protected ICommands playCommand;
    [SerializeField] TextMeshProUGUI buttonText;
    [SerializeField] CardEvent onPlay_SetCardImprovData;

    public void Start()
    {
        thisButton.interactable = false;
    }

    public void Update()
    {
        if(!thisButton.interactable)
        {
            buttonText.enabled = false;
        }
        else
        {
            buttonText.enabled = true;
        }

        if(_card is not null)
            if(_card.IsPlayed)
                thisButton.interactable = false;
    }

    public void ImprovButtonsToggle(Toggle improvToggle)
    {
        if(improvToggle.isOn)
        {
            thisButton.interactable = true;
        }
        else
        {
            thisButton.interactable = false;
        }
    }

    public void AddListener()
    {
        thisButton.onClick.RemoveAllListeners();
        playCommand = new PlayCommand(onPlay_SetCardImprovData, _card);
        thisButton.onClick.AddListener(() => GameManager.Instance.commands.AddCommand(playCommand));
    }
}
