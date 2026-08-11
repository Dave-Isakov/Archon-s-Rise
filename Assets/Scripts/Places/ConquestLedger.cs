using System.Collections.Generic;
using ArchonsRise.SaveData;

// Pure conquest registry: grid cell -> (place type, roster size, WHICH roster
// slots are dead). The MonoBehaviour ConquestTracker wraps one of these per run.
// Restore order is not guaranteed (saved progress may arrive before the place
// registers itself), so entries are created on first touch from either side.
//
// Identities, not a count: a multi-guardian assault spawns the whole remaining
// roster at once, so the player can kill slot 1 before slot 0 (Siege especially).
// A count-only ledger re-spawned the corpse and skipped the survivor.
public class ConquestLedger
{
    private class Entry
    {
        public PlaceType type;
        public int rosterSize;
        public readonly HashSet<int> defeated = new HashSet<int>();
    }

    private readonly Dictionary<Cell, Entry> entries = new Dictionary<Cell, Entry>();

    public void Register(Cell cell, PlaceType type, int rosterSize)
    {
        var e = GetOrCreate(cell);
        e.type = type;
        e.rosterSize = rosterSize;
    }

    public int DefeatedCount(Cell cell)
        => entries.TryGetValue(cell, out var e) ? e.defeated.Count : 0;

    public bool IsDefeated(Cell cell, int rosterIndex)
        => entries.TryGetValue(cell, out var e) && e.defeated.Contains(rosterIndex);

    public void RecordDefeat(Cell cell, int rosterIndex)
        => GetOrCreate(cell).defeated.Add(rosterIndex);

    // Roster slots still standing, ascending. rosterSize comes from the caller's
    // live SO rather than the registered value so this is correct even if the
    // place has not registered yet.
    public List<int> RemainingIndices(Cell cell, int rosterSize)
    {
        var result = new List<int>();
        entries.TryGetValue(cell, out var e);
        for (int i = 0; i < rosterSize; i++)
            if (e == null || !e.defeated.Contains(i))
                result.Add(i);
        return result;
    }

    public bool IsConquered(Cell cell)
        => entries.TryGetValue(cell, out var e)
           && PlaceRules.IsConquered(e.defeated.Count, e.rosterSize);

    public int ConqueredCastleCount()
    {
        int count = 0;
        foreach (var e in entries.Values)
            if (e.type == PlaceType.Castle && PlaceRules.IsConquered(e.defeated.Count, e.rosterSize))
                count++;
        return count;
    }

    public PlaceConquest[] Export()
    {
        var result = new List<PlaceConquest>();
        foreach (var kv in entries)
        {
            if (kv.Value.defeated.Count == 0) continue;
            var indices = new List<int>(kv.Value.defeated);
            indices.Sort();
            result.Add(new PlaceConquest
            {
                x = kv.Key.x,
                y = kv.Key.y,
                defeatedCount = indices.Count,
                defeatedIndices = indices.ToArray()
            });
        }
        return result.ToArray();
    }

    public void ApplySaved(int x, int y, int[] defeatedIndices)
    {
        if (defeatedIndices == null) return;
        var e = GetOrCreate(new Cell(x, y));
        e.defeated.Clear();
        foreach (var i in defeatedIndices) e.defeated.Add(i);
    }

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
