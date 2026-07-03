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
        // Places are entered, not reached into: the player must be standing on
        // this cell (adjacency is enough for enemies, not for places).
        if (gameboard.LocalToCell(player.transform.position) != gridPos)
        {
            GameManager.Instance.ValidationMessage(
                $"You must be standing in {townSO.cardName} to enter it.");
            return;
        }

        GameManager.Instance.townCanvas.enabled = true;
        deck.CreateTown(this);
        onClick_GetTownData.Raise(this);
        onClick_OpenTownMenu.Raise(this);
    }
}
