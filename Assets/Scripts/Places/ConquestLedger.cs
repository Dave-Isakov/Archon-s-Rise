using System.Collections.Generic;
using ArchonsRise.SaveData;

// Pure conquest registry: grid cell -> (place type, roster size, guardians
// defeated). The MonoBehaviour ConquestTracker wraps one of these per run.
// Restore order is not guaranteed (a saved count may arrive before the place
// registers itself), so entries are created on first touch from either side.
public class ConquestLedger
{
    private class Entry
    {
        public PlaceType type;
        public int rosterSize;
        public int defeatedCount;
    }

    private readonly Dictionary<Cell, Entry> entries = new Dictionary<Cell, Entry>();

    public void Register(Cell cell, PlaceType type, int rosterSize)
    {
        var e = GetOrCreate(cell);
        e.type = type;
        e.rosterSize = rosterSize;
    }

    public int DefeatedCount(Cell cell)
        => entries.TryGetValue(cell, out var e) ? e.defeatedCount : 0;

    public void RecordDefeat(Cell cell) => GetOrCreate(cell).defeatedCount++;

    public bool IsConquered(Cell cell)
        => entries.TryGetValue(cell, out var e)
           && PlaceRules.IsConquered(e.defeatedCount, e.rosterSize);

    public int ConqueredCastleCount()
    {
        int count = 0;
        foreach (var e in entries.Values)
            if (e.type == PlaceType.Castle && PlaceRules.IsConquered(e.defeatedCount, e.rosterSize))
                count++;
        return count;
    }

    public PlaceConquest[] Export()
    {
        var result = new List<PlaceConquest>();
        foreach (var kv in entries)
            if (kv.Value.defeatedCount > 0)
                result.Add(new PlaceConquest
                {
                    x = kv.Key.x,
                    y = kv.Key.y,
                    defeatedCount = kv.Value.defeatedCount
                });
        return result.ToArray();
    }

    public void ApplySavedCount(int x, int y, int defeatedCount)
        => GetOrCreate(new Cell(x, y)).defeatedCount = defeatedCount;

    private Entry GetOrCreate(Cell cell)
    {
        if (!entries.TryGetValue(cell, out var e))
        {
            e = new Entry();
            entries[cell] = e;
        }
        return e;
    }
}
