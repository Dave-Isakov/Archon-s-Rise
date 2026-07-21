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

    // Exposed so the save system can read/restore fog-of-war reveal state (all
    // DirectionButtons reference the same map/fog tilemaps).
    public Tilemap Map => map;
    public Tilemap Fog => fog;

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
        // Movement is Explore-phase only (spec 2026-07-21); once the action is
        // taken the turn is committed to it and the map locks.
        if(TurnPhaseController.Instance != null && !TurnPhaseController.Instance.CanMove)
        {
            GameManager.Instance.ValidationMessage("You can only move during the Explore phase.");
            return;
        }

        // A visible enemy standing on the destination blocks the move outright —
        // the player must fight it (step adjacent to trigger combat), never walk
        // over it. Checked before spending explore so a blocked click costs nothing.
        var target = player.gridPos + player.compass[direction];
        if(EnemyOccupies(target))
        {
            GameManager.Instance.ValidationMessage("An enemy blocks the way — attack it instead!");
            return;
        }

        if(playerExplore < explore)
        {
            GameManager.Instance.ValidationMessage($"Need {explore} to explore!");
            return;
        }

        var adjTile = player.gridPos + player.compass[this.direction];
        player.UpdateCompass(adjTile, compass);

        if(fog.HasTile(adjTile))
        {
            // Fog-reveal step: spend explore, uncover fog, and COMMIT — revealed
            // knowledge can't be undone (TurnPhaseRules.ShouldCommitOnMove(true)).
            playerExplore -= explore;
            onSuccessfulExplore_AdjustPlayersExplore.Raise(playerExplore);
            foreach(Directions d in Enum.GetValues(typeof(Directions)))
            {
                fog.SetTile(adjTile + compass[d], null);
            }
            var tile = adjTile + compass[this.direction];
            player.UpdateCompass(tile, compass);
            foreach(Directions d in Enum.GetValues(typeof(Directions)))
            {
                fog.SetTile(tile + compass[d], null);
            }
            GameManager.Instance.commands.ClearStack();
        }
        else
        {
            // Ordinary move onto already-revealed ground: undoable.
            var from = player.transform.position;
            var to = gameboard.CellToWorld(gameboard.LocalToCell(from) + player.compass[direction]);
            GameManager.Instance.commands.AddCommand(new MoveCommand(this, from, to, explore));
        }
    }

    // Reposition the player and adjust the explore pool. Used by MoveCommand for
    // both execute (spend) and undo (refund). Raises the position + explore events
    // so the map buttons and HUD stay in sync.
    public void ApplyMove(Vector3 worldPos, int exploreDelta, bool refund = false)
    {
        player.transform.position = worldPos;
        playerExplore += refund ? exploreDelta : -exploreDelta;
        onSuccessfulExplore_AdjustPlayersExplore.Raise(playerExplore);
        sendNewPositionOfPlayer.Raise(player);
    }

    public void Move()
    {
        var gridPos = gameboard.LocalToCell(player.transform.position);
        gridPos += player.compass[direction];
        // Defense in depth: never let the player land on an enemy's cell, even if
        // Move is invoked directly. Combat is entered by standing adjacent, not on top.
        if(EnemyOccupies(gridPos))
        {
            GameManager.Instance.ValidationMessage("An enemy blocks the way — attack it instead!");
            return;
        }
        player.transform.position = gameboard.CellToWorld(gridPos);
        sendNewPositionOfPlayer.Raise(player);
    }

    // True when a visible enemy token stands on this cell. Enemies hidden under fog
    // don't block — the player is only revealing fog there, not moving onto it.
    private bool EnemyOccupies(Vector3Int cell)
    {
        if(MapFog.IsHidden(cell)) return false;
        foreach(var token in FindObjectsByType<EnemyToken>())
            if(token.gridPos == cell) return true;
        return false;
    }

    public void SetExplore(int explore)
    {
        playerExplore = explore;
    }
}
