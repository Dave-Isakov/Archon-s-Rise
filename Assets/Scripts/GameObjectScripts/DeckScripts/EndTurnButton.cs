using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class EndTurnButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Button endTurnButton;
    [SerializeField] VoidEvent endTheTurn;
    PlayerDeck deck;
    PlayerHand hand;
    Player player;

    public void OnPointerClick(PointerEventData eventData)
    {
        // IPointerClickHandler fires even when the Button is not interactable;
        // don't commit the undo stack on a disabled button.
        if (!endTurnButton.interactable) return;
        GameManager.Instance.commands.ClearStack();
    }

    private void Start()
    {
        endTurnButton.onClick.RemoveAllListeners();
        endTurnButton.onClick.AddListener(() => endTheTurn.Raise());
        deck = FindAnyObjectByType<PlayerDeck>();
        hand = FindAnyObjectByType<PlayerHand>();
        player = FindAnyObjectByType<Player>();
    }

    private void Update()
    {
        if (deck == null || hand == null || player == null) return;
        // Disabled mid-fight, and when the deck can't refill the hand (ending the
        // turn would only tick the turn counter — the round has to end instead).
        endTurnButton.interactable = TurnButtonGate.EndTurn(
            GameManager.Instance.activeCombatant != null || GuardianAssault.AnyInProgress,
            DrawGate.Evaluate(deck.CardsInDeck.Count, hand.cardsInPlay.Count, player.PlayerHandSize));
    }
}
