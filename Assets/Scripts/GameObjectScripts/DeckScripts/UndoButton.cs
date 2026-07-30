using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UndoButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Button undoButton;

    public void OnPointerClick(PointerEventData eventData)
    {
        if(!undoButton.interactable)
            GameLog.Instance.Post("There is nothing to undo.");
    }

    // Gamepad path: same behavior as clicking the button, including the
    // nothing-to-undo message. Re-checks the combat block itself rather than
    // trusting undoButton.interactable, since Trigger() bypasses the button.
    public void Trigger()
    {
        if (undoButton.interactable && !BlockedByCombat()) GameManager.Instance.commands.UndoCommand();
        else GameLog.Instance.Post("There is nothing to undo.");
    }

    private void Start()
    {
        undoButton.onClick.AddListener(() => GameManager.Instance.commands.UndoCommand());
    }

    // Undo is fully blocked while a fight is open (before AND after commit).
    // Task 5 deferred the undo-stack clear from fight-open to the onCommit
    // callback (correctly — it's part of the deferred cost), but that left the
    // free-look window exploitable: play a card granting Explore, open a fight
    // whose affordability check captures the cost, undo the Explore-granting
    // card during the free look, then Engage — the cost is spent with no
    // re-validation, going negative. Gating the button itself restores the
    // same effective protection the old eager ClearStack() used to provide,
    // without reintroducing that eager clear.
    static bool BlockedByCombat()
        => CombatController.Instance != null && CombatController.Instance.InCombat;

    private void Update()
    {
        if(GameManager.Instance.commands is not null)
        if (GameManager.Instance.commands.GetStackCount() <= 0 || BlockedByCombat())
        {
            undoButton.interactable = false;
        }
        else
        {
            undoButton.interactable = true;
        }
    }
}
