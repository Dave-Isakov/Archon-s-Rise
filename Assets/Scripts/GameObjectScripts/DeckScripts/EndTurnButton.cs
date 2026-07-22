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
    DrawVerdict lastVerdict = DrawVerdict.Draw;
    PlayerDeck deck;
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
            GameManager.Instance.ValidationMessage("You cannot end the turn with a full hand.");
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
            if (HandFullUnplayed())
            {
                GameManager.Instance.ValidationMessage("You cannot end the turn with a full hand.");
                return;
            }
            TurnPhaseController.Instance.EndTurnPressed();
        });
        deck = FindAnyObjectByType<PlayerDeck>();
        hand = FindAnyObjectByType<PlayerHand>();
        player = FindAnyObjectByType<Player>();
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void Update()
    {
        if (deck == null || hand == null || player == null) return;
        // Deck-empty no longer disables End Turn (it auto-ends the round instead),
        // but the tutorial still teaches the dry-deck rest, so keep the one-shot
        // fire off the verdict.
        var verdict = DrawGate.Evaluate(deck.CardsInDeck.Count, hand.cardsInPlay.Count, player.PlayerHandSize);
        // Fire once per entry into DeckEmpty — Update polls every frame.
        if (verdict == DrawVerdict.DeckEmpty && lastVerdict != DrawVerdict.DeckEmpty
            && onDeckCannotRefillTutorial != null)
            onDeckCannotRefillTutorial.Raise();
        lastVerdict = verdict;
        // Disabled only mid-fight now.
        endTurnButton.interactable = TurnButtonGate.EndTurn(
            GameManager.Instance.activeCombatant != null || GuardianAssault.AnyInProgress);
        UpdateLabel(verdict);
    }

    // The button reads "End the Day" when the next press will end the round — the
    // last turn of the day, or a dry deck that forces the rest (spec 2026-07-21) —
    // and "End the Turn" otherwise. Mirrors RoundRules.IsRoundOver on the press.
    void UpdateLabel(DrawVerdict verdict)
    {
        if (label == null || TurnPhaseController.Instance == null) return;
        int next = RoundRules.NextTurnsRemaining(TurnPhaseController.Instance.TurnsRemaining);
        bool endsDay = RoundRules.IsRoundOver(next, RoundRules.DeckCanRefill(verdict));
        label.text = endsDay ? "End the Day" : "End the Turn";
    }
}
