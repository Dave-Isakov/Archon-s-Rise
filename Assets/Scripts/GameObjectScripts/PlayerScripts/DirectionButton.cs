using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public partial class DirectionButton : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI exploreCost;
    [SerializeField] Button thisButton;
    [SerializeField] PlayerPosition player;
    [SerializeField] List<HexRuleTile> ruleTiles;
    [SerializeField] Grid gameboard;
    [SerializeField] Tilemap map;
    [SerializeField] Tilemap fog;
    [SerializeField] Directions direction;
    private Vector3 position;
    [SerializeField] PlayerPositionEvent sendNewPositionOfPlayer;
    [SerializeField] IntEvent onSuccessfulExplore_AdjustPlayersExplore;
    private Dictionary<Directions, Vector3Int> compass = new()
    {
        {Directions.Northwest, new Vector3Int(-1,1)},
        {Directions.Northeast, new Vector3Int(0,1)},
        {Directions.East, new Vector3Int(1,0)},
        {Directions.Southeast, new Vector3Int(0,-1)},
        {Directions.Southwest, new Vector3Int(-1,-1)},
        {Directions.West, new Vector3Int(-1,0)}
    };
    private int explore;
    public int playerExplore = 0;
    
    private void Start()
    {

    }

    private void Update() 
    {
        UpdateExploreCost();
        if(!map.HasTile(player.gridPos + player.compass[direction]))
        {
            thisButton.interactable = false;
            exploreCost.text = null;
        }
        else
        {
            thisButton.interactable = true;
        }
    }

    public void UpdateExploreCost()
    {
        player.UpdateCompass(player.gridPos, player.compass);
        if(map.HasTile(player.gridPos + player.compass[direction]))
            explore = map.GetTile<HexRuleTile>(player.gridPos + player.compass[direction]).exploreCost;
            exploreCost.text = explore.ToString();
        // foreach (var tile in ruleTiles)
        // {
        //     if(map.GetTile(player.gridPos + player.compass[direction]) == tile)
        //     {
        //         exploreCost.text = tile.exploreCost.ToString();
        //     }
        // }
        
        if(fog.HasTile(player.gridPos + player.compass[direction]))
        {
            explore = 2;
            exploreCost.text = explore.ToString();
        }
    }

    public void Explore()
    {
        if(playerExplore >= explore)
        {
            playerExplore -= explore;
            onSuccessfulExplore_AdjustPlayersExplore.Raise(playerExplore);
            var adjTile = player.gridPos + player.compass[this.direction];
            player.UpdateCompass(adjTile, compass);
            if(fog.HasTile(adjTile))
            {
                foreach(Directions direction in Enum.GetValues(typeof(Directions)))
                {
                    fog.SetTile(adjTile + compass[direction], null);
                }
                var tile = adjTile + compass[this.direction];
                player.UpdateCompass(tile, compass);
                foreach(Directions direction in Enum.GetValues(typeof(Directions)))
                {
                    fog.SetTile(tile + compass[direction], null);
                }
            }
            else
                Move();
        }
        else
        {
            GameManager.Instance.ValidationMessage($"Need {explore} to explore!");
        }
    }

    public void Move()
    {
        var gridPos = gameboard.LocalToCell(player.transform.position);
        gridPos += player.compass[direction];
        player.transform.position = gameboard.CellToWorld(gridPos);
        sendNewPositionOfPlayer.Raise(player);
    }

    public void SetExplore(int explore)
    {
        playerExplore = explore;
    }
}
