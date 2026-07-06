using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EndRoundButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Button endRoundButton;
    [SerializeField] VoidEvent endTheRound;

    public void OnPointerClick(PointerEventData eventData)
    {
        // IPointerClickHandler fires even when the Button is not interactable;
        // don't commit the undo stack on a disabled button.
        if (!endRoundButton.interactable) return;
        GameManager.Instance.commands.ClearStack();
    }

    // Gamepad path; see EndTurnButton.Trigger.
    public bool Trigger()
    {
        if (!endRoundButton.interactable) return false;
        GameManager.Instance.commands.ClearStack();
        endTheRound.Raise();
        return true;
    }

    private void Start()
    {
        endRoundButton.onClick.RemoveAllListeners();
        endRoundButton.onClick.AddListener(() => endTheRound.Raise());
    }

    private void Update()
    {
        // The round can't end mid-fight.
        endRoundButton.interactable = TurnButtonGate.EndRound(
            GameManager.Instance.activeCombatant != null || GuardianAssault.AnyInProgress);
    }
}
