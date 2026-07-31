using System.Collections.Generic;
using ArchonsRise.SaveData;

namespace ArchonsRise.Shrines
{
    public enum ShrineVisualState { Live = 0, ConsumedDormant = 1, Guarding = 2 }

    // Pure per-run shrine state (spec 2026-07-24). Mirrors DungeonLedger: only
    // non-Live shrines are exported (a fresh shrine re-derives Live from the map
    // seed). ShrineTracker wraps it for the scene.
    //
    // A Guarding shrine also carries the reward it OWES (2026-07-31): the bad
    // roll's guardian is shrine state, not a map token, so the debt lives here
    // rather than riding on a spawned EnemyToken.
    public class ShrineLedger
    {
        // No debt: a Live or already-consumed shrine owes nothing. Not 0, which
        // is a real ShrineReward (CardPick).
        public const int NoReward = -1;

        private class Entry
        {
            public string shrineId;
            public ShrineVisualState state;
            public int owedReward = NoReward;
        }

        private readonly Dictionary<Cell, Entry> entries = new Dictionary<Cell, Entry>();

        public void Register(Cell cell, string shrineId) => GetOrCreate(cell).shrineId = shrineId;

        public ShrineVisualState State(Cell cell)
            => entries.TryGetValue(cell, out var e) ? e.state : ShrineVisualState.Live;

        // The (int)ShrineReward this shrine's guardian owes, or NoReward.
        public int OwedReward(Cell cell)
            => entries.TryGetValue(cell, out var e) ? e.owedReward : NoReward;

        // A bad roll: the shrine starts guarding and remembers what its guardian
        // owes. The only way an entry gains a debt.
        public void SetGuarding(Cell cell, int owedReward)
        {
            var e = GetOrCreate(cell);
            e.state = ShrineVisualState.Guarding;
            e.owedReward = owedReward;
        }

        // Any other state settles the debt: a shrine that stops guarding has
        // either paid out or never owed anything, so no stale reward can survive
        // to be granted twice.
        public void SetState(Cell cell, ShrineVisualState state)
        {
            var e = GetOrCreate(cell);
            e.state = state;
            if (state != ShrineVisualState.Guarding) e.owedReward = NoReward;
        }

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
                        state = (int)kv.Value.state,
                        owedReward = kv.Value.owedReward
                    });
            return list.ToArray();
        }

        public bool ApplySavedState(ShrineState s)
        {
            if (!entries.TryGetValue(new Cell(s.x, s.y), out var e)) return false;
            if (e.shrineId != s.shrineId) return false;
            e.state = (ShrineVisualState)s.state;
            e.owedReward = e.state == ShrineVisualState.Guarding ? s.owedReward : NoReward;
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
