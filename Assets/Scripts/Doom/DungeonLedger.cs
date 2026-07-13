using System.Collections.Generic;
using ArchonsRise.SaveData;

// Pure per-run dungeon state: delve progress, flags, and the once-per-run
// band-fire bookkeeping (M2.9, spec 2026-07-13). Mirrors ConquestLedger;
// DungeonTracker wraps it for the scene.
public class DungeonLedger
{
    private class Entry
    {
        public string dungeonId;
        public int defeatedCount;
        public bool flagged;
    }

    private readonly Dictionary<Cell, Entry> entries = new Dictionary<Cell, Entry>();

    // Doom-band firings happen once per run; relief dropping doom back below
    // a band edge must never re-arm them.
    public bool MidFlagsFired { get; set; }
    public bool HighFlagsFired { get; set; }

    public void Register(Cell cell, string dungeonId) => GetOrCreate(cell).dungeonId = dungeonId;

    public int DefeatedCount(Cell cell)
    {
        Entry e;
        return entries.TryGetValue(cell, out e) ? e.defeatedCount : 0;
    }

    public void RecordDefeat(Cell cell) => GetOrCreate(cell).defeatedCount++;

    public bool IsComplete(Cell cell) => DungeonRules.IsComplete(DefeatedCount(cell));

    public bool IsFlagged(Cell cell)
    {
        Entry e;
        return entries.TryGetValue(cell, out e) && e.flagged;
    }

    public void SetFlagged(Cell cell) => GetOrCreate(cell).flagged = true;

    // Flags stop counting toward the round tick once their dungeon is cleared.
    public int FlaggedCount()
    {
        int n = 0;
        foreach (var kv in entries)
            if (kv.Value.flagged && !DungeonRules.IsComplete(kv.Value.defeatedCount)) n++;
        return n;
    }

    // Uncleared, unflagged dungeons — the eligible flag targets.
    public List<Cell> FlagCandidates()
    {
        var list = new List<Cell>();
        foreach (var kv in entries)
            if (!kv.Value.flagged && !DungeonRules.IsComplete(kv.Value.defeatedCount))
                list.Add(kv.Key);
        return list;
    }

    public DungeonState[] Export()
    {
        var list = new List<DungeonState>();
        foreach (var kv in entries)
            if (kv.Value.defeatedCount > 0 || kv.Value.flagged)
                list.Add(new DungeonState
                {
                    x = kv.Key.x,
                    y = kv.Key.y,
                    dungeonId = kv.Value.dungeonId,
                    defeatedCount = kv.Value.defeatedCount,
                    flagged = kv.Value.flagged
                });
        return list.ToArray();
    }

    // Restore one saved entry. False when the cell was never registered or the
    // saved id doesn't match what the regenerated map put there (content
    // drift) — the caller warns and skips, like EnemySpawner.RestoreSpawned.
    public bool ApplySavedState(DungeonState s)
    {
        Entry e;
        if (!entries.TryGetValue(new Cell(s.x, s.y), out e)) return false;
        if (e.dungeonId != s.dungeonId) return false;
        e.defeatedCount = s.defeatedCount;
        e.flagged = s.flagged;
        return true;
    }

    private Entry GetOrCreate(Cell cell)
    {
        Entry e;
        if (!entries.TryGetValue(cell, out e))
        {
            e = new Entry();
            entries[cell] = e;
        }
        return e;
    }
}
