using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class EndTurnButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Button endTurnButton;
    [SerializeField] VoidEvent endTheTurn;
    [SerializeField] VoidEvent onDeckCannotRefillTutorial; // M2.12 one-shot trigger
    [SerializeField] TextMeshProUGUI label; // button caption; auto-found if unassigned
    bool lastDeckShortfallPending;
    PlayerHand hand;
    Player player;

    // The controller commits the undo stack itself (EndTurnPressed), so the click
    // path no longer needs to ClearStack here.
    public void OnPointerClick(PointerEventData eventData) { /* no-op: controller commits */ }

    // Gamepad path: routes through the controller, which commits, runs the turn-end
    // chain, decrements the day, and auto-ends the round when the budget is spent.
    // A full-hand block is "handled" (message shown) and returns true.
    public bool Trigger()
    {
        if (!endTurnButton.interactable) return false;
        if (HandFullUnplayed())
        {
            GameLog.Instance.Post("You cannot end the turn with a full hand.");
            return true;
        }
        TurnPhaseController.Instance.EndTurnPressed();
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
            if (InputContextState.MapOpen) return; // map mode: look, don't touch
            if (HandFullUnplayed())
            {
                GameLog.Instance.Post("You cannot end the turn with a full hand.");
                return;
            }
            TurnPhaseController.Instance.EndTurnPressed();
        });
        hand = FindAnyObjectByType<PlayerHand>();
        player = FindAnyObjectByType<Player>();
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void Update()
    {
        if (hand == null || player == null || TurnPhaseController.Instance == null) return;
        bool deckShortfallPending = TurnPhaseController.Instance.DeckShortfallPending;
        // Fire once on the false->true transition (spec 2026-07-30): the start of
        // the buffer turn, when the label is about to read "End Day" for a deck
        // reason (not just a spent budget).
        if (deckShortfallPending && !lastDeckShortfallPending && onDeckCannotRefillTutorial != null)
            onDeckCannotRefillTutorial.Raise();
        lastDeckShortfallPending = deckShortfallPending;
        // Disabled only mid-fight now.
        endTurnButton.interactable = TurnButtonGate.EndTurn(
            GameManager.Instance.activeCombatant != null
            || (CombatController.Instance != null && CombatController.Instance.InCombat));
        UpdateLabel();
    }

    // The button reads "End Day" when the next press will end the round — the
    // last turn of the day, or the deck-shortfall buffer turn (spec 2026-07-30) —
    // and "End Turn" otherwise. Mirrors TurnPhaseController.EndTurnPressed exactly,
    // since both read the same NextPressEndsRound computation.
    void UpdateLabel()
    {
        if (label == null || TurnPhaseController.Instance == null) return;
        label.text = TurnPhaseController.Instance.NextPressEndsRound ? "End Day" : "End Turn";
    }
}
