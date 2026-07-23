using UnityEngine;
using UnityEngine.EventSystems;

// Map-side dungeon identity (M2.9): assigned SO + grid cell, visual state
// (flagged / cleared markers), and the stand-on-cell entry rule (TownToken
// pattern — dungeons are place-like, adjacency is not enough).
public class DungeonToken : MonoBehaviour, IPointerClickHandler
{
    public DungeonsSO dungeonSO;
    // Stable identity over the seeded map; assigned by GridGeneration at spawn.
    public Vector3Int gridPos;
    [SerializeField] GameObject flagMarker;    // active while flagged, until cleared
    [SerializeField] GameObject clearedMarker; // active once complete
    private PlayerPosition player;
    private Grid gameboard;

    void Start()
    {
        player = FindAnyObjectByType<PlayerPosition>();
        gameboard = FindAnyObjectByType<Grid>();
        DungeonTracker.Instance.Register(gridPos, dungeonSO.id);
        RefreshVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (MapFog.IsHidden(gridPos)) return; // hidden by fog → not interactable

        // During teleport targeting the interactor owns all clicks; let it handle this.
        if (HexInteractor.Instance != null && HexInteractor.Instance.IsTeleporting) return;

        // Dungeons are entered by standing on the cell. If adjacent instead, treat the
        // click as a move request onto this cell (Explore-phase movement).
        if (gameboard.LocalToCell(player.transform.position) != gridPos)
        {
            if (ExplorationController.Instance != null && ExplorationController.Instance.IsAdjacent(gridPos))
                ExplorationController.Instance.Move(gridPos);
            else
                GameManager.Instance.ValidationMessage(
                    $"You must be standing at {dungeonSO.cardName} to enter it.");
            return;
        }

        // Opening the dungeon panel is a free peek (spec 2026-07-22): the turn's one
        // action is spent by pressing Delve, not by opening the menu. BeginVisit
        // snapshots whether this visit may act (only if the action is still unspent).
        if (TurnPhaseController.Instance != null)
            TurnPhaseController.Instance.BeginVisit();

        FindAnyObjectByType<DungeonPanel>(FindObjectsInactive.Include).Open(this);
    }

    public void RefreshVisual()
    {
        bool complete = DungeonTracker.Instance.IsComplete(gridPos);
        if (clearedMarker != null) clearedMarker.SetActive(complete);
        if (flagMarker != null) flagMarker.SetActive(!complete && DungeonTracker.Instance.IsFlagged(gridPos));
    }
}
