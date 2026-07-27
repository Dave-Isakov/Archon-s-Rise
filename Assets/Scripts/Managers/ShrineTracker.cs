using UnityEngine;
using ArchonsRise.Shrines;
using ArchonsRise.SaveData;

// Runtime shrine registry for the current run: wraps the pure ShrineLedger
// (DungeonTracker/HotspotTracker pattern). Scene-scoped; a new run starts blank.
public class ShrineTracker : MonoBehaviour
{
    private readonly ShrineLedger ledger = new ShrineLedger();

    private static ShrineTracker instance;
    public static ShrineTracker Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("ShrineTracker").AddComponent<ShrineTracker>();
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null) instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Register(Vector3Int cell, string id) => ledger.Register(ToCell(cell), id);
    public ShrineVisualState State(Vector3Int cell) => ledger.State(ToCell(cell));

    public void SetState(Vector3Int cell, ShrineVisualState s)
    {
        ledger.SetState(ToCell(cell), s);
        RefreshTokenVisuals();
    }

    public ShrineState[] Export() => ledger.Export();

    public void ApplySave(ShrineState[] shrines)
    {
        if (shrines == null) return;
        foreach (var s in shrines)
            if (!ledger.ApplySavedState(s))
                Debug.LogWarning($"Shrine restore: cell ({s.x},{s.y}) id '{s.shrineId}' doesn't match the regenerated map — skipped.");
        RefreshTokenVisuals();
    }

    public void RefreshTokenVisuals()
    {
        foreach (var t in FindObjectsByType<ShrineToken>(FindObjectsSortMode.None))
            t.RefreshVisual();
    }

    private static Cell ToCell(Vector3Int v) => new Cell(v.x, v.y);
}
