using UnityEngine;
using UnityEngine.InputSystem;

// Global gamepad shortcuts for the turn-flow buttons: West = Undo, North = End
// Turn. Live on the board/fan, suppressed while the pop-out, a menu, or a
// validation message is open. Each shortcut calls the same handler the on-screen
// button uses, so all validation and interactable gating still applies. End Round
// is gone (spec 2026-07-21): End Turn auto-ends the round when the day is over.
public class TurnFlowShortcuts : MonoBehaviour
{
    [SerializeField] UndoButton undo;
    [SerializeField] EndTurnButton endTurn;

    void Update()
    {
        if (InputContextState.Current == InputContext.Inspector) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.mainMenuCanvas.enabled || gm.cardListCanvas.enabled
            || gm.messageCanvas.enabled) return;

        if (GameControls.Gameplay.Undo.WasPressedThisFrame())
            undo.Trigger();

        if (GameControls.Gameplay.EndTurn.WasPressedThisFrame())
            endTurn.Trigger();
    }
}
