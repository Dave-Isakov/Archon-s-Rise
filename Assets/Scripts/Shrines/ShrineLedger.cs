using System.Collections.Generic;
using ArchonsRise.SaveData;

namespace ArchonsRise.Shrines
{
    public enum ShrineVisualState { Live = 0, ConsumedDormant = 1, Guarding = 2 }

    // Pure per-run shrine state (spec 2026-07-24). Mirrors DungeonLedger: only
    // non-Live shrines are exported (a fresh shrine re-derives Live from the map
    // seed). ShrineTracker wraps it for the scene.
    public class ShrineLedger
    {
        private class Entry { public string shrineId; public ShrineVisualState state; }

        private readonly Dictionary<Cell, Entry> entries = new Dictionary<Cell, Entry>();

        public void Register(Cell cell, string shrineId) => GetOrCreate(cell).shrineId = shrineId;

        public ShrineVisualState State(Cell cell)
            => entries.TryGetValue(cell, out var e) ? e.state : ShrineVisualState.Live;

        public void SetState(Cell cell, ShrineVisualState state) => GetOrCreate(cell).state = state;

        public ShrineState[] Export()
        {
            var list = new List<ShrineState>();
            foreach (var kv in entries)
                if (kv.Value.state != ShrineVisualState.Live)
                    list.Add(new ShrineState
                    {
                        x = kv.Key.x,
                        y = kv.Key.y,
                        shrineId = kv.Value.shrineId,
                        state = (int)kv.Value.state
                    });
            return list.ToArray();
        }

        public bool ApplySavedState(ShrineState s)
        {
            if (!entries.TryGetValue(new Cell(s.x, s.y), out var e)) return false;
            if (e.shrineId != s.shrineId) return false;
            e.state = (ShrineVisualState)s.state;
            return true;
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
}
