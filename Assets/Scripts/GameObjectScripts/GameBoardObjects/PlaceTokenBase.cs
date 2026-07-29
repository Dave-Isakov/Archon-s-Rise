using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using ArchonsRise.HexTooltipInfo;

// The one place-entry path (spec 2026-07-28). Towns, dungeons and shrines each
// carried a near-identical OnPointerClick; this is that sequence, once.
//
// A new place type implements PlaceName / Describe / BuildActions / Dispatch and
// nothing else. Dispatch lives on the TOKEN rather than in PlaceFan so the fan
// never learns about place types — that is what keeps this extensible.
public abstract class PlaceTokenBase : MonoBehaviour, IPointerClickHandler, IHexOccupant, IPlaceFanHost
{
    // Stable identity over the seeded map; assigned by GridGeneration at spawn.
    public Vector3Int gridPos;

    protected PlayerPosition player;
    protected Grid gameboard;

    // PlaceFan calls BuildActions EVERY FRAME while a fan is open, so the stat
    // lookup it needs must not be a scene search. Resolved once, and re-resolved
    // if the Player is ever respawned.
    Player playerStats;
    protected Player PlayerStats
        => playerStats != null ? playerStats : (playerStats = FindAnyObjectByType<Player>());

    // IHexOccupant: places are entered by standing on the cell, so an adjacent
    // click dispatches a move rather than walking through them.
    public Vector3Int Cell => gridPos;
    public virtual bool BlocksMove => true;

    protected abstract string PlaceName { get; }
    public abstract HexDescriptor Describe();

    // IPlaceFanHost
    public abstract List<PlaceAction> BuildActions();
    public abstract void Dispatch(PlaceActionId id);

    // A service may only be committed while the current visit still owns the
    // turn's action (spec 2026-07-22). Null-safe so tokens behave normally with
    // no controller in the scene.
    protected static bool CanActThisVisit
        => TurnPhaseController.Instance == null || TurnPhaseController.Instance.VisitCanAct;

    // Subclasses that need their own Start work override this; the base still
    // runs first so player/gameboard/registry are always set up.
    protected virtual void OnStart() { }

    // Hook for one-shots that used to key off a PANEL opening. The fan is the
    // normal path now, so anything that fired on panel-open must fire here or it
    // never fires again once players stop opening panels.
    protected virtual void OnFanOpening() { }

    void Start()
    {
        player = FindAnyObjectByType<PlayerPosition>();
        gameboard = FindAnyObjectByType<Grid>();
        HexOccupantRegistry.Instance.Register(this);
        OnStart();
    }

    void OnDestroy()
    {
        if (HexOccupantRegistry.Existing != null) HexOccupantRegistry.Existing.Unregister(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (MapFog.IsHidden(gridPos)) return; // hidden by fog → not interactable

        // During teleport targeting the interactor owns all clicks (you can
        // teleport onto a place cell); let it handle this one.
        if (HexInteractor.Instance != null && HexInteractor.Instance.IsTeleporting) return;

        // Places are entered by standing on the cell. If the player is adjacent
        // instead, treat the click as a move request onto this cell.
        if (gameboard.LocalToCell(player.transform.position) != gridPos)
        {
            if (ExplorationController.Instance != null && ExplorationController.Instance.IsAdjacent(gridPos))
                ExplorationController.Instance.Move(gridPos);
            else
                GameLog.Instance.Post(
                    $"You must be standing at {PlaceName} to enter it.");
            return;
        }

        // Opening a place is a free peek (spec 2026-07-22): the turn's one action
        // is spent by the service committed inside, not by opening the fan.
        if (TurnPhaseController.Instance != null)
            TurnPhaseController.Instance.BeginVisit();

        OnFanOpening();
        if (PlaceFan.Instance != null) PlaceFan.Instance.Open(this);
    }
}
