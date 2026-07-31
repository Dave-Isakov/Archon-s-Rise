using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UndoButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Button undoButton;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!undoButton.interactable) PostWhyNot();
    }

    // Gamepad path: same behavior as pressing the button, including the message.
    // Re-checks the gate itself rather than trusting undoButton.interactable,
    // since Trigger() bypasses the button.
    public void Trigger()
    {
        if (CanUndo()) GameManager.Instance.commands.UndoCommand();
        else PostWhyNot();
    }

    private void Start()
    {
        undoButton.onClick.AddListener(() => GameManager.Instance.commands.UndoCommand());
    }

    static bool InCombat
        => CombatController.Instance != null && CombatController.Instance.InCombat;

    static bool FightCommitted
        => CombatController.Instance != null && CombatController.Instance.Committed;

    static bool CanUndo()
        => GameManager.Instance.commands is not null
           && UndoGate.Undo(GameManager.Instance.commands.GetStackCount(), InCombat, FightCommitted);

    // The disabled button used to always say "nothing to undo", which was a lie
    // whenever the fight gate was the real reason — the player could be looking
    // at a card they had just played (2026-07-31). Name the actual reason.
    void PostWhyNot()
    {
        bool blockedByFreeLook = InCombat && !FightCommitted;
        GameLog.Instance.Post(blockedByFreeLook
            ? "You can't undo while sizing up a fight — commit to it or step away first."
            : "There is nothing to undo.");
    }

    private void Update()
    {
        undoButton.interactable = CanUndo();
    }
}
