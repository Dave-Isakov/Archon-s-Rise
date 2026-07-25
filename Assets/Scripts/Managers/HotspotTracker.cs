using UnityEngine;
using ArchonsRise.Hotspots;
using ArchonsRise.SaveData;

// Runtime hotspot registry for the current run: wraps the pure HotspotLedger
// (DungeonTracker pattern). Also caches each cell's yielded color so the harvest
// hook needs only the cell. Scene-scoped; a new run starts blank.
public class HotspotTracker : MonoBehaviour
{
    private readonly HotspotLedger ledger = new HotspotLedger();
    private readonly System.Collections.Generic.Dictionary<Cell, EmpowerType> colors = new();

    private static HotspotTracker instance;
    public static HotspotTracker Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("HotspotTracker").AddComponent<HotspotTracker>();
            return instance;
        }
    }

    public void Register(Vector3Int cell, string id, int charges, EmpowerType color)
    {
        ledger.Register(ToCell(cell), id, charges);
        colors[ToCell(cell)] = color;
    }

    public bool CanHarvest(Vector3Int cell) => ledger.CanHarvest(ToCell(cell));
    public int Remaining(Vector3Int cell) => ledger.Remaining(ToCell(cell));
    public EmpowerType ColorAt(Vector3Int cell)
        => colors.TryGetValue(ToCell(cell), out var c) ? c : EmpowerType.None;

    public void Harvest(Vector3Int cell) => ledger.Harvest(ToCell(cell));

    public HotspotState[] Export() => ledger.Export();

    public void ApplySave(HotspotState[] hotspots)
    {
        if (hotspots == null) return;
        foreach (var h in hotspots)
            if (!ledger.ApplySavedState(h))
                Debug.LogWarning($"Hotspot restore: cell ({h.x},{h.y}) id '{h.hotspotId}' doesn't match the regenerated map — skipped.");
        RefreshTokenVisuals();
    }

    public void RefreshTokenVisuals()
    {
        foreach (var t in FindObjectsByType<CrystalHotspotToken>())
            t.RefreshVisual();
    }

    private static Cell ToCell(Vector3Int v) => new Cell(v.x, v.y);
}
