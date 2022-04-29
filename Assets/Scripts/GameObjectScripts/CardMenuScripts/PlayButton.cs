using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PlayButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Button playButton;
    [SerializeField] CardEvent onPlay_SetCardDataToPlayer;
    [SerializeField] CardEvent onPlay_SetAttackDataToPlayer;
    [SerializeField] CardEvent onPlay_SetDefendDataToPlayer;
    [SerializeField] CardEvent onPlay_SetInfluenceDataToPlayer;
    [SerializeField] CardEvent onPlay_SetExploreDataToPlayer;
    Card card;
    ICommands playCommand;

    void Update()
    {
        if(card is not null)
        {
            if(card.IsPlayed == true) playButton.interactable = false;
        }
    }

    public void SetCard(Card card)
    {
        this.card = card;
        if(card.cardSO.isChoice)
            playButton.interactable = false;
        else
            playButton.interactable = true;
        AddListener();
    }

    private void AddListener()
    {
        playButton.onClick.RemoveAllListeners();
        playCommand = new PlayCommand(onPlay_SetCardDataToPlayer, card);
        playButton.onClick.AddListener(() => GameManager.Instance.commands.AddCommand(playCommand));
    }

    public void SetAttackListener(Toggle choiceToggle)
    {
        if(choiceToggle.isOn)
        {
            playButton.interactable = true;
            playButton.onClick.RemoveAllListeners();
            playCommand = new PlayCommand(onPlay_SetAttackDataToPlayer, card);
            playButton.onClick.AddListener(() => GameManager.Instance.commands.AddCommand(playCommand));
        }
        else
        {
            AddListener();
            playButton.interactable = false;
        }
    }
    public void SetDefendListener(Toggle choiceToggle)
    {
        if(choiceToggle.isOn)
        {
            playButton.interactable = true;
            playButton.onClick.RemoveAllListeners();
            playCommand = new PlayCommand(onPlay_SetDefendDataToPlayer, card);
            playButton.onClick.AddListener(() => GameManager.Instance.commands.AddCommand(playCommand));
        }
        else
        {
            AddListener();
            playButton.interactable = false;
        }
    }
    public void SetInfluenceListener(Toggle choiceToggle)
    {
        if(choiceToggle.isOn)
        {
            playButton.interactable = true;
            playButton.onClick.RemoveAllListeners();
            playCommand = new PlayCommand(onPlay_SetInfluenceDataToPlayer, card);
            playButton.onClick.AddListener(() => GameManager.Instance.commands.AddCommand(playCommand));
        }
        else
        {
            AddListener();
            playButton.interactable = false;
        }
    }
    public void SetExploreListener(Toggle choiceToggle)
    {
        if(choiceToggle.isOn)
        {
            playButton.interactable = true;
            playButton.onClick.RemoveAllListeners();
            playCommand = new PlayCommand(onPlay_SetExploreDataToPlayer, card);
            playButton.onClick.AddListener(() => GameManager.Instance.commands.AddCommand(playCommand));
        }
        else
        {
            AddListener();
            playButton.interactable = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(card.IsPlayed && !playButton.interactable)
            GameManager.Instance.ValidationMessage($"{card.cardSO.name} has already been played. Click Undo on the Gameboard to undo previous plays.");

        if(card.cardSO.isChoice && !playButton.interactable && !card.IsPlayed)
            GameManager.Instance.ValidationMessage($"{card.cardSO.name} requires a choice before playing.");

        if (card.cardSO.cardType == CardType.None)
            GameManager.Instance.ValidationMessage($"{card.cardSO.name} cannot be played.");
    }

    public void DisableForWounds(Card card)
    {
        if (card.cardSO.cardType == CardType.None)
            playButton.interactable = false;
    }
}
