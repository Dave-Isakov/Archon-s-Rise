using System.Collections.Generic;

// The run's message history (spec 2026-07-28). A capped ring buffer, newest
// first, grouped by day. In-memory and NOT saved: the log answers "what did I
// just miss", which a session covers, and persisting it would cost a save-schema
// bump plus a migrator plus every migrator test that asserts the version.
//
// Day dividers are DERIVED (NeedsDayDivider) rather than stored as pseudo-
// entries, so eviction can never orphan a header.
public class PlayerLogCore
{
    public const int Capacity = 100;

    // Oldest-first storage; newestFirst is the render order, rebuilt on change.
    private readonly List<LogEntry> entries = new List<LogEntry>();
    private readonly List<LogEntry> newestFirst = new List<LogEntry>();

    public int Count { get { return entries.Count; } }
    public IReadOnlyList<LogEntry> Entries { get { return newestFirst; } }

    public void Append(int day, string text)
    {
        entries.Add(new LogEntry(day, text));
        if (entries.Count > Capacity) entries.RemoveAt(0);
        Rebuild();
    }

    public void Clear()
    {
        entries.Clear();
        Rebuild();
    }

    // True when the entry at this newest-first index should be preceded by a day
    // header: either it is the newest entry, or the entry above it is a different
    // day.
    public bool NeedsDayDivider(int index)
    {
        if (index < 0 || index >= newestFirst.Count) return false;
        if (index == 0) return true;
        return newestFirst[index].Day != newestFirst[index - 1].Day;
    }

    private void Rebuild()
    {
        newestFirst.Clear();
        for (int i = entries.Count - 1; i >= 0; i--) newestFirst.Add(entries[i]);
    }
}
