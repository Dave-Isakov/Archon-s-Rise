using System.Collections.Generic;
using UnityEngine;
using ArchonsRise.HexTooltipInfo;

// Cell -> occupants lookup so the tooltip / occupancy checks never scan the
// scene per frame (DungeonTracker singleton pattern). Scene-scoped: a new run
// starts blank. Multiple occupants per cell are tolerated; Best() returns the
// highest-priority descriptor.
public class HexOccupantRegistry : MonoBehaviour
{
    private readonly Dictionary<Vector3Int, List<IHexOccupant>> byCell = new();

    private static HexOccupantRegistry instance;
    public static HexOccupantRegistry Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("HexOccupantRegistry").AddComponent<HexOccupantRegistry>();
            return instance;
        }
    }

    public void Register(IHexOccupant occ)
    {
        if (!byCell.TryGetValue(occ.Cell, out var list))
        {
            list = new List<IHexOccupant>();
            byCell[occ.Cell] = list;
        }
        if (!list.Contains(occ)) list.Add(occ);
    }

    public void Unregister(IHexOccupant occ)
    {
        if (byCell.TryGetValue(occ.Cell, out var list))
            list.Remove(occ);
    }

    public bool Occupied(Vector3Int cell)
        => byCell.TryGetValue(cell, out var list) && list.Count > 0;

    // Move-blocking occupancy: true only when some occupant here is place-like
    // (towns/dungeons). A passive tile (crystal hotspot) never blocks movement.
    public bool Blocks(Vector3Int cell)
    {
        if (!byCell.TryGetValue(cell, out var list)) return false;
        foreach (var occ in list)
            if (occ.BlocksMove) return true;
        return false;
    }

    public bool Best(Vector3Int cell, out HexDescriptor best)
    {
        best = HexDescriptor.None;
        if (!byCell.TryGetValue(cell, out var list)) return false;
        bool found = false;
        foreach (var occ in list)
        {
            var d = occ.Describe();
            if (d.IsEmpty) continue;
            if (!found || d.Priority > best.Priority) { best = d; found = true; }
        }
        return found;
    }
}
