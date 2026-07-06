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

    // Gamepad path: commit the undo stack, then raise turn end — the same pair the
    // click path performs (OnPointerClick ClearStack + Button onClick raise).
    // Returns false only when gated off (combat / deck-empty) so the caller can fall
    // back to End Round; a full-hand block is "handled" (message shown) and returns true.
    public bool Trigger()
    {
        if (!endTurnButton.interactable) return false;
        if (HandFullUnplayed())
        {
            GameManager.Instance.ValidationMessage("You cannot end the turn with a full hand.");
            return true;
        }
        GameManager.Instance.commands.ClearStack();
        endTheTurn.Raise();
        return true;
    }

    // A full hand with nothing played this turn: ending the turn would draw nothing
    // and merely tick the counter, so it is disallowed. Played cards stay in
    // cardsInPlay (marked IsPlayed) until commit, so "count == handSize with no
    // IsPlayed card" is exactly a full hand of unplayed cards.
    bool HandFullUnplayed()
    {
        if (hand == null || player == null) return false;
        return hand.cardsInPlay.Count >= player.PlayerHandSize
            && !hand.cardsInPlay.Exists(c => c.IsPlayed);
    }

    private void Start()
    {
        endTurnButton.onClick.RemoveAllListeners();
        endTurnButton.onClick.AddListener(() =>
        {
            if (HandFullUnplayed())
            {
                GameManager.Instance.ValidationMessage("You cannot end the turn with a full hand.");
                return;
            }
            endTheTurn.Raise();
        });
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
