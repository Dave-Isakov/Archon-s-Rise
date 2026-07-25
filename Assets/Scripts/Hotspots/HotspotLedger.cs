using System.Collections.Generic;
using ArchonsRise.SaveData;

namespace ArchonsRise.Hotspots
{
    // Pure per-run hotspot charge state (spec 2026-07-24). Mirrors DungeonLedger:
    // Cell-keyed, exports only tiles whose charges changed from their authored
    // start, HotspotTracker wraps it for the scene.
    public class HotspotLedger
    {
        private class Entry
        {
            public string hotspotId;
            public int startCharges;
            public int remaining;
        }

        private readonly Dictionary<Cell, Entry> entries = new Dictionary<Cell, Entry>();

        public void Register(Cell cell, string hotspotId, int charges)
        {
            var e = GetOrCreate(cell);
            e.hotspotId = hotspotId;
            e.startCharges = charges;
            e.remaining = charges;
        }

        public int Remaining(Cell cell)
            => entries.TryGetValue(cell, out var e) ? e.remaining : 0;

        public bool CanHarvest(Cell cell)
            => entries.TryGetValue(cell, out var e) && HotspotRules.CanHarvest(e.remaining);

        public void Harvest(Cell cell)
        {
            if (entries.TryGetValue(cell, out var e))
                e.remaining = HotspotRules.NextCharges(e.remaining);
        }

        // Only tiles that have been drawn down from their authored start (a
        // save-size optimisation identical to DungeonLedger.Export).
        public HotspotState[] Export()
        {
            var list = new List<HotspotState>();
            foreach (var kv in entries)
                if (kv.Value.remaining != kv.Value.startCharges)
                    list.Add(new HotspotState
                    {
                        x = kv.Key.x,
                        y = kv.Key.y,
                        hotspotId = kv.Value.hotspotId,
                        remainingCharges = kv.Value.remaining
                    });
            return list.ToArray();
        }

        // Restore one saved entry. False when the cell was never registered or
        // the saved id doesn't match the regenerated map (content drift) — the
        // caller warns and skips, like DungeonLedger.ApplySavedState.
        public bool ApplySavedState(HotspotState s)
        {
            if (!entries.TryGetValue(new Cell(s.x, s.y), out var e)) return false;
            if (e.hotspotId != s.hotspotId) return false;
            e.remaining = s.remainingCharges;
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
