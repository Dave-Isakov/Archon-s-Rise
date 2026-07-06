using UnityEngine;
using UnityEngine.InputSystem;

// Global gamepad shortcuts for the turn-flow buttons: West = Undo, North = End
// Turn (or End Round when the turn can't end). Live on the board/fan, suppressed
// while the pop-out, a menu, or a validation message is open. Each shortcut calls
// the same handler the on-screen button uses, so all validation and interactable
// gating still applies.
public class TurnFlowShortcuts : MonoBehaviour
{
    [SerializeField] UndoButton undo;
    [SerializeField] EndTurnButton endTurn;
    [SerializeField] EndRoundButton endRound;

    void Update()
    {
        if (InputContextState.Current == InputContext.Inspector) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.mainMenuCanvas.enabled || gm.cardListCanvas.enabled
            || gm.messageCanvas.enabled) return;

        if (GameControls.Gameplay.Undo.WasPressedThisFrame())
            undo.Trigger();

        if (GameControls.Gameplay.EndTurn.WasPressedThisFrame())
        {
            // One button serves both: End Turn while the deck can refill the hand,
            // End Round when it can't — mirroring which on-screen button is usable.
            if (!endTurn.Trigger())
                endRound.Trigger();
        }
    }
}
