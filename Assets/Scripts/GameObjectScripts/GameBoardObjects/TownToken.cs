using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TownToken : MonoBehaviour, IPointerClickHandler
{
    public TownsSO townSO;
    // Stable identity over the seeded map; assigned by GridGeneration at spawn.
    public Vector3Int gridPos;
    [SerializeField] TownDeck deck;
    [SerializeField] TownEvent onClick_OpenTownMenu;
    [SerializeField] TownEvent onClick_GetTownData;
    private PlayerPosition player;
    private Grid gameboard;

    void Start()
    {
        player = FindAnyObjectByType<PlayerPosition>();
        gameboard = FindAnyObjectByType<Grid>();
        ConquestTracker.Instance.Register(gridPos, townSO.placeType, townSO.guardians.Count);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (MapFog.IsHidden(gridPos)) return; // hidden by fog → not interactable

        // During teleport targeting the interactor owns all clicks (you can teleport
        // onto a place cell); let it handle this one.
        if (HexInteractor.Instance != null && HexInteractor.Instance.IsTeleporting) return;

        // Places are entered by standing on the cell. If the player is adjacent instead,
        // treat the click as a move request onto this cell (Explore-phase movement); the
        // menu opens on the next click, once standing here.
        if (gameboard.LocalToCell(player.transform.position) != gridPos)
        {
            if (ExplorationController.Instance != null && ExplorationController.Instance.IsAdjacent(gridPos))
                ExplorationController.Instance.Move(gridPos);
            else
                GameManager.Instance.ValidationMessage(
                    $"You must be standing in {townSO.cardName} to enter it.");
            return;
        }

        // Opening a place is a free peek (spec 2026-07-22): the turn's one action is
        // spent by the first service committed inside (recruit/heal/buy/assault), not
        // by the menu open. BeginVisit snapshots whether this visit may act — only if
        // the action is still unspent; a whole visit still counts as the one action.
        if (TurnPhaseController.Instance != null)
            TurnPhaseController.Instance.BeginVisit();

        GameManager.Instance.townCanvas.enabled = true;
        deck.CreateTown(this);
        // Revive any button that hid itself on a previous open, so its listener
        // re-registers before the events below drive UpdateButtonText. Without
        // this, buttons that went inactive (e.g. Recruit on a not-yet-conquered
        // Keep) never re-appear once conditions change (the Keep is conquered).
        TownMenu.Instance.PrepareButtons();
        onClick_GetTownData.Raise(this);
        onClick_OpenTownMenu.Raise(this);
    }
}
