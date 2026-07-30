using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The dungeon place menu (M2.9): progress, flagged banner, and the Delve
// button that spends the dungeon's exploreCost and starts the fight. Opened
// by DungeonToken when the player stands on the cell.
public class DungeonPanel : MonoBehaviour, IGameEventListener<int>
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI descriptionText;
    [SerializeField] TextMeshProUGUI progressText;
    [SerializeField] TextMeshProUGUI flaggedText;
    [SerializeField] Button delveButton;
    [SerializeField] TextMeshProUGUI delveButtonText;

    // OnExploreEvent_GetCurrentExplore: broadcast on every explore change with the
    // current total, so the Delve gate updates live as the player pays cards while
    // the panel is open (instead of a one-shot check when the menu opens).
    [SerializeField] IntEvent onExploreChanged;

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
        // The M2.12 tutorial one-shot moved to DungeonToken.OnFanOpening: the fan
        // is the normal entry now, so keying it off the panel would rarely fire.
        Refresh();
    }

    // Wired to the panel's Close/Leave button.
    public void Close()
    {
        current = null;
        GameManager.Instance.dungeonCanvas.enabled = false;
    }

    // Wired to the panel's Delve button's OnClick.
    public void Delve()
    {
        if (current == null) return;
        var token = current;
        Close();
        PerformDelve(token);
    }

    // The delve itself, shared by the panel button and the place fan's Delve slot.
    public static void PerformDelve(DungeonToken token)
    {
        if (token == null) return;
        var player = FindAnyObjectByType<Player>();
        int cost = token.dungeonSO.exploreCost;
        if (player.PlayerExplore < cost)
        {
            GameLog.Instance.Post(
                $"You need {cost} Explore to delve into {token.dungeonSO.cardName}.");
            return;
        }

        // Opening the delve is now a free look (spec 2026-07-30 §2.3): the
        // affordability check above still gates opening, but paying Explore,
        // committing the visit's action, and locking the undo stack all wait
        // for a real commit inside the fight.
        DungeonDelve.Instance.Begin(token, onCommit: () =>
        {
            player.PlayerExplore -= cost;
            player.GetCurrentExplore();
            if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
            // Delving is a firm decision: commit all pending plays so the explore
            // that paid for it can't be undone into a negative total.
            GameManager.Instance.commands.ClearStack();
        });
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
}
