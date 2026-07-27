using UnityEngine;
using UnityEngine.EventSystems;
using ArchonsRise.HexTooltipInfo;
using ArchonsRise.Shrines;

// Map-side shrine identity (spec 2026-07-24). Stand-on-cell, click-to-interact
// (DungeonToken pattern). Registers with ShrineTracker + HexOccupantRegistry on
// Start. Visual states: live / consumed-dormant / guarding.
public class ShrineToken : MonoBehaviour, IHexOccupant, IPointerClickHandler
{
    public ShrineSO shrineSO;
    // Stable identity over the seeded map; assigned by GridGeneration at spawn.
    public Vector3Int gridPos;
    [SerializeField] GameObject liveMarker;
    [SerializeField] GameObject dormantMarker;
    [SerializeField] GameObject guardingMarker;
    private PlayerPosition player;
    private Grid gameboard;

    // IHexOccupant: a shrine is place-like — it is used by standing on the cell,
    // so an adjacent click dispatches a move rather than walking through it.
    public Vector3Int Cell => gridPos;
    public bool BlocksMove => true;

    void Start()
    {
        player = FindAnyObjectByType<PlayerPosition>();
        gameboard = FindAnyObjectByType<Grid>();
        ShrineTracker.Instance.Register(gridPos, shrineSO.id);
        HexOccupantRegistry.Instance.Register(this);
        RefreshVisual();
    }

    void OnDestroy()
    {
        if (HexOccupantRegistry.Existing != null) HexOccupantRegistry.Existing.Unregister(this);
    }

    public HexDescriptor Describe()
        => new HexDescriptor(
            TileDescriptor.Shrine(ShrineTracker.Instance.State(gridPos), shrineSO.crystalCost),
            TileDescriptor.PlacePriority);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (MapFog.IsHidden(gridPos)) return; // hidden by fog → not interactable

        // During teleport targeting the interactor owns all clicks; let it handle this.
        if (HexInteractor.Instance != null && HexInteractor.Instance.IsTeleporting) return;

        // Stand-on-cell entry (DungeonToken pattern): if adjacent, treat as a move.
        if (gameboard.LocalToCell(player.transform.position) != gridPos)
        {
            if (ExplorationController.Instance != null && ExplorationController.Instance.IsAdjacent(gridPos))
                ExplorationController.Instance.Move(gridPos);
            else
                GameManager.Instance.ValidationMessage(
                    $"You must be standing at {shrineSO.cardName} to use it.");
            return;
        }

        // A consumed shrine only peeks; a guarding shrine points at its guardian.
        var state = ShrineTracker.Instance.State(gridPos);
        if (state != ShrineVisualState.Live)
        {
            GameManager.Instance.ValidationMessage(state == ShrineVisualState.Guarding
                ? "A guardian holds this shrine's reward — defeat it."
                : "This shrine is spent.");
            return;
        }

        // Opening is a free peek (spec 2026-07-22); the action commits when the
        // engage is confirmed inside the panel (ShrinePanel calls CommitVisitAction).
        if (TurnPhaseController.Instance != null)
            TurnPhaseController.Instance.BeginVisit();

        FindAnyObjectByType<ShrinePanel>(FindObjectsInactive.Include).Open(this);
    }

    public void RefreshVisual()
    {
        var s = ShrineTracker.Instance.State(gridPos);
        if (liveMarker != null) liveMarker.SetActive(s == ShrineVisualState.Live);
        if (dormantMarker != null) dormantMarker.SetActive(s == ShrineVisualState.ConsumedDormant);
        if (guardingMarker != null) guardingMarker.SetActive(s == ShrineVisualState.Guarding);
    }
}
