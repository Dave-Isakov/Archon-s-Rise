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

        // Places are entered, not reached into: the player must be standing on
        // this cell (adjacency is enough for enemies, not for places).
        if (gameboard.LocalToCell(player.transform.position) != gridPos)
        {
            GameManager.Instance.ValidationMessage(
                $"You must be standing in {townSO.cardName} to enter it.");
            return;
        }

        // Entering a place is the turn's one action (spec 2026-07-21). The whole
        // visit (recruit/heal/buy/assault inside the open menu) counts as one — only
        // the menu open spends the action; the services within it do not.
        if (TurnPhaseController.Instance != null)
        {
            if (!TurnPhaseController.Instance.CanInteract)
            {
                GameManager.Instance.ValidationMessage("You've already taken your action this turn.");
                return;
            }
            TurnPhaseController.Instance.BeginAction();
        }

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
