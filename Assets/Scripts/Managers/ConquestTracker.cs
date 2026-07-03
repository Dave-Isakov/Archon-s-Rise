using UnityEngine;
using ArchonsRise.SaveData;

// Runtime conquest registry for the current run. A dedicated component (not
// bolted onto GameManager) wrapping the pure ConquestLedger. Lazily creates
// its own scene GameObject so no scene edit is needed; being scene-scoped
// (no DontDestroyOnLoad) means a new run naturally starts blank.
public class ConquestTracker : MonoBehaviour
{
    private readonly ConquestLedger ledger = new ConquestLedger();

    private static ConquestTracker instance;
    public static ConquestTracker Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("ConquestTracker").AddComponent<ConquestTracker>();
            return instance;
        }
    }

    public void Register(Vector3Int cell, PlaceType type, int rosterSize)
        => ledger.Register(ToCell(cell), type, rosterSize);

    public int DefeatedCount(Vector3Int cell) => ledger.DefeatedCount(ToCell(cell));

    public void RecordDefeat(Vector3Int cell) => ledger.RecordDefeat(ToCell(cell));

    public bool IsConquered(Vector3Int cell) => ledger.IsConquered(ToCell(cell));

    public int ConqueredCastleCount() => ledger.ConqueredCastleCount();

    public PlaceConquest[] ExportPlaces() => ledger.Export();

    public void ApplySave(PlaceConquest[] places)
    {
        if (places == null) return;
        foreach (var p in places)
            ledger.ApplySavedCount(p.x, p.y, p.defeatedCount);
    }

    private static Cell ToCell(Vector3Int v) => new Cell(v.x, v.y);
}
