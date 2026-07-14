using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The dungeon place menu (M2.9): progress, next-enemy preview (through the
// PreviewRules blind gate), flagged banner, and the Delve button that spends
// the dungeon's exploreCost and starts the fight. Opened by DungeonToken when
// the player stands on the cell.
public class DungeonPanel : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI descriptionText;
    [SerializeField] TextMeshProUGUI progressText;
    [SerializeField] TextMeshProUGUI previewText;
    [SerializeField] TextMeshProUGUI flaggedText;
    [SerializeField] Button delveButton;
    [SerializeField] TextMeshProUGUI delveButtonText;

    private DungeonToken current;

    public void Open(DungeonToken token)
    {
        current = token;
        GameManager.Instance.dungeonCanvas.enabled = true;
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
        flaggedText.text = "Corrupted — +1 Doom each round until cleared";
        flaggedText.gameObject.SetActive(!complete && tracker.IsFlagged(current.gridPos));

        delveButton.gameObject.SetActive(!complete);
        delveButtonText.text = $"Delve ({so.exploreCost} Explore)";
        var player = FindAnyObjectByType<Player>();
        delveButton.interactable = player != null && player.PlayerExplore >= so.exploreCost;

        if (complete) { previewText.text = ""; return; }
        var next = so.enemies[cleared];
        previewText.text = PreviewRules.CanPreview()
            ? $"Next: {next.cardName}   <sprite=\"Sword\" index=0> {next.enemyAttack}   <sprite=\"shield\" index=0> {next.enemyHP}"
            : "You cannot see the enemy you are about to confront.";
    }
}
