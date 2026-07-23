using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Board movement + facts hub (spec 2026-07-23). Absorbs the non-arrow duties of the
// retired DirectionButton: the explore pool, Map/Fog exposure, ApplyMove (MoveCommand),
// undoable moves, and the fog-scout reveal+commit. HexInteractor (input/feedback) calls
// the query + action methods here; MoveCommand calls ApplyMove; MapFog/DataManager reach
// the fog tilemap through this component.
public class ExplorationController : MonoBehaviour
{
    public static ExplorationController Instance { get; private set; }

    [SerializeField] Grid gameboard;
    [SerializeField] Tilemap ground;
    [SerializeField] Tilemap water;
    [SerializeField] Tilemap mountains;
    [SerializeField] Tilemap fog;
    [SerializeField] PlayerPosition player;
    [SerializeField] int fogCost = 2;
    [SerializeField] PlayerPositionEvent sendNewPositionOfPlayer;
    [SerializeField] IntEvent onSuccessfulExplore_AdjustPlayersExplore;

    int playerExplore;

    // Same map/fog tilemaps the save system reads (formerly via DirectionButton).
    public Tilemap Map => ground;
    public Tilemap Fog => fog;
    public int FogCost => fogCost;
    public int PlayerExplore => playerExplore;
    public Vector3Int PlayerCell => gameboard.LocalToCell(player.transform.position);

    // Parity-correct hex neighbour offsets for a given cell (reuses PlayerPosition's compass).
    readonly Dictionary<Directions, Vector3Int> compass = new()
    {
        {Directions.Northwest, new Vector3Int(-1,1)},
        {Directions.Northeast, new Vector3Int(0,1)},
        {Directions.East, new Vector3Int(1,0)},
        {Directions.Southeast, new Vector3Int(0,-1)},
        {Directions.Southwest, new Vector3Int(-1,-1)},
        {Directions.West, new Vector3Int(-1,0)}
    };

    void Awake() { Instance = this; }

    // Explore pool sync (listener target, formerly DirectionButton.SetExplore).
    public void SetExplore(int explore) => playerExplore = explore;

    // True if any terrain tilemap holds a tile here; out cost is its HexRuleTile.exploreCost.
    // Ground (plains/forest/desert/town/dungeon), then water (5), then mountain (4).
    public bool TryTerrain(Vector3Int cell, out int cost)
    {
        if (ground.HasTile(cell)) { cost = Cost(ground, cell); return true; }
        if (water.HasTile(cell))  { cost = Cost(water, cell);  return true; }
        if (mountains.HasTile(cell)) { cost = Cost(mountains, cell); return true; }
        cost = 0;
        return false;
    }

    static int Cost(Tilemap map, Vector3Int cell)
    {
        var t = map.GetTile<HexRuleTile>(cell);
        return t != null ? t.exploreCost : 0;
    }

    public bool IsAdjacent(Vector3Int cell)
    {
        var origin = PlayerCell;
        player.UpdateCompass(origin, compass);
        foreach (Directions d in Enum.GetValues(typeof(Directions)))
            if (origin + compass[d] == cell) return true;
        return false;
    }

    // The six parity-correct neighbour cells of the player, for the affordable-ring
    // highlight. Directions has exactly six values, so the array is always full.
    public Vector3Int[] PlayerNeighbors()
    {
        var origin = PlayerCell;
        player.UpdateCompass(origin, compass);
        var result = new Vector3Int[6];
        int i = 0;
        foreach (Directions d in Enum.GetValues(typeof(Directions)))
            result[i++] = origin + compass[d];
        return result;
    }

    // A visible enemy token stands on this cell. Enemies hidden under fog don't count.
    public bool EnemyOccupies(Vector3Int cell)
    {
        if (MapFog.IsHidden(cell)) return false;
        foreach (var token in FindObjectsByType<EnemyToken>())
            if (token.gridPos == cell) return true;
        return false;
    }

    bool CanMovePhase()
    {
        if (TurnPhaseController.Instance != null && !TurnPhaseController.Instance.CanMove)
        {
            GameManager.Instance.ValidationMessage("You can only move during the Explore phase.");
            return false;
        }
        return true;
    }

    // Undoable step onto adjacent revealed terrain. Re-validates phase / enemy / cost
    // as defence in depth (HexInteractor only dispatches Move for affordable cells).
    public void Move(Vector3Int targetCell)
    {
        if (!CanMovePhase()) return;
        if (EnemyOccupies(targetCell))
        {
            GameManager.Instance.ValidationMessage("An enemy blocks the way — attack it instead!");
            return;
        }
        if (!TryTerrain(targetCell, out int cost)) return;
        if (playerExplore < cost)
        {
            GameManager.Instance.ValidationMessage($"Need {cost} to explore!");
            return;
        }
        var from = player.transform.position;
        var to = gameboard.CellToWorld(targetCell);
        GameManager.Instance.commands.AddCommand(new MoveCommand(this, from, to, cost));
    }

    // Reveal an adjacent fog hex in place (does NOT relocate the player). Irreversible:
    // spends fogCost, uncovers the scouted cell + its neighbours, commits the stack.
    public void ScoutFog(Vector3Int targetCell)
    {
        if (!CanMovePhase()) return;
        if (playerExplore < fogCost)
        {
            GameManager.Instance.ValidationMessage($"Need {fogCost} to scout this fog!");
            return;
        }
        playerExplore -= fogCost;
        onSuccessfulExplore_AdjustPlayersExplore.Raise(playerExplore);

        player.UpdateCompass(targetCell, compass);
        fog.SetTile(targetCell, null);
        foreach (Directions d in Enum.GetValues(typeof(Directions)))
            fog.SetTile(targetCell + compass[d], null);

        GameManager.Instance.commands.ClearStack(); // revealed knowledge can't be undone
    }

    // Reposition + adjust explore. Used by MoveCommand for execute (spend) and undo
    // (refund). Raises position + explore events so map + HUD stay in sync.
    public void ApplyMove(Vector3 worldPos, int exploreDelta, bool refund = false)
    {
        player.transform.position = worldPos;
        playerExplore += refund ? exploreDelta : -exploreDelta;
        onSuccessfulExplore_AdjustPlayersExplore.Raise(playerExplore);
        sendNewPositionOfPlayer.Raise(player);
    }

    // Free reposition (teleport, Task 5). Raises the position event so every enemy
    // re-runs CheckAggro — landing adjacent arms combat exactly like a walked step.
    public void ApplyTeleport(Vector3 worldPos)
    {
        player.transform.position = worldPos;
        sendNewPositionOfPlayer.Raise(player);
    }
}
