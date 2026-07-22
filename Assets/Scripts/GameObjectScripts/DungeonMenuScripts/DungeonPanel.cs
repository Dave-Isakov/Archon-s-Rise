using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The dungeon place menu (M2.9): progress, next-enemy preview (through the
// PreviewRules blind gate), flagged banner, and the Delve button that spends
// the dungeon's exploreCost and starts the fight. Opened by DungeonToken when
// the player stands on the cell.
public class DungeonPanel : MonoBehaviour, IGameEventListener<int>
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI descriptionText;
    [SerializeField] TextMeshProUGUI progressText;
    [SerializeField] TextMeshProUGUI previewText;
    [SerializeField] TextMeshProUGUI flaggedText;
    [SerializeField] Button delveButton;
    [SerializeField] TextMeshProUGUI delveButtonText;

    // OnExploreEvent_GetCurrentExplore: broadcast on every explore change with the
    // current total, so the Delve gate updates live as the player pays cards while
    // the panel is open (instead of a one-shot check when the menu opens).
    [SerializeField] IntEvent onExploreChanged;
    [SerializeField] VoidEvent onDungeonOpenTutorial; // M2.12 one-shot trigger

    private DungeonToken current;

    private void OnEnable()
    {
        if (onExploreChanged != null) onExploreChanged.RegisterListener(this);
    }

    private void OnDisable()
    {
        if (onExploreChanged != null) onExploreChanged.UnRegisterListener(this);
    }

    // Fired by OnExploreEvent_GetCurrentExplore whenever the player's explore total
    // changes. Only re-gate while the panel is actually open on a dungeon.
    public void OnEventRaised(int currentExplore)
    {
        if (current == null) return;
        UpdateDelveInteractable(currentExplore);
    }

    public void Open(DungeonToken token)
    {
        current = token;
        GameManager.Instance.dungeonCanvas.enabled = true;
        if (onDungeonOpenTutorial != null) onDungeonOpenTutorial.Raise();
        Refresh();
    }

    // Wired to the panel's Close/Leave button.
    public void Close()
    {
        current = null;
        GameManager.Instance.dungeonCanvas.enabled = false;
    }

    // Wired to the Delve button's OnClick.
    public void Delve()
    {
        if (current == null) return;
        var player = FindAnyObjectByType<Player>();
        int cost = current.dungeonSO.exploreCost;
        if (player.PlayerExplore < cost)
        {
            GameManager.Instance.ValidationMessage(
                $"You need {cost} Explore to delve into {current.dungeonSO.cardName}.");
            return;
        }
        player.PlayerExplore -= cost;
        player.GetCurrentExplore();
        // Delving is the visit's committed action (spec 2026-07-22): spend the turn's
        // action now (the Delve button is gated so this only fires when the visit
        // still owns it). This also commits the movement stack.
        if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
        // Delving is a firm decision: commit all pending plays so the explore
        // that paid for it can't be undone into a negative total.
        GameManager.Instance.commands.ClearStack();

        var token = current;
        Close();
        DungeonDelve.Instance.Begin(token);
    }

    private void Refresh()
    {
        var so = current.dungeonSO;
        var tracker = DungeonTracker.Instance;
        int cleared = tracker.DefeatedCount(current.gridPos);
        bool complete = tracker.IsComplete(current.gridPos);

        nameText.text = so.cardName;
        descriptionText.text = so.cardDescription;
        progressText.text = complete ? "Cleared!" : $"Depth {cleared + 1} of {DungeonRules.DelveCount}";
        flaggedText.text = $"Corrupted — +{IconMarkup.Cost(IconConcept.Doom, 1)} each round until cleared";
        flaggedText.gameObject.SetActive(!complete && tracker.IsFlagged(current.gridPos));

        delveButton.gameObject.SetActive(!complete);
        delveButtonText.text = $"Delve — {IconMarkup.Cost(IconConcept.Explore, so.exploreCost)}";
        var player = FindAnyObjectByType<Player>();
        UpdateDelveInteractable(player != null ? player.PlayerExplore : 0);

        if (complete) { previewText.text = ""; return; }

        UpdatePreview(so, cleared);
    }

    // Gate the Delve button on the player having enough explore. Called on open and
    // again on every explore change while the panel is open.
    private void UpdateDelveInteractable(int currentExplore)
    {
        if (current == null) return;
        // Also gated on the visit still owning the turn's action (spec 2026-07-22):
        // opening the panel is a free peek, so a delve after the action is spent is
        // locked. Null-safe for scenes without a controller.
        bool visitCanAct = TurnPhaseController.Instance == null || TurnPhaseController.Instance.VisitCanAct;
        delveButton.interactable = currentExplore >= current.dungeonSO.exploreCost && visitCanAct;
    }

    private void UpdatePreview(DungeonsSO so, int cleared)
    {
        var next = so.enemies[cleared];
        previewText.text = PreviewRules.CanPreview()
            ? $"Next: {next.cardName}   {IconMarkup.Cost(IconConcept.Attack, next.enemyAttack)}   {IconMarkup.Cost(IconConcept.Hp, next.enemyHP)}"
            : "You cannot see the enemy you are about to confront.";
    }
}
