using System;
using System.Collections.Generic;
using ArchonsRise.SaveData;

// Pure spawn placement: zone seeding at map gen and per-spawn cell/enemy
// selection. Unity-free (uses the save system's Cell struct); callers pass
// an rng delegate — GridGeneration's seeded Rng for determinism at map gen,
// a fresh System.Random for mid-run spawns (saved explicitly, decision
// 2026-07-07).
public static class SpawnRules
{
    // Chebyshev distance — a simple spacing metric that's close enough on the
    // offset hex grid for zone spreading.
    public static int Spacing(Cell a, Cell b)
        => Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));

    // Pick up to `count` zone cells from candidates, enforcing min spacing.
    // Rejected picks are discarded from the pool (they can never qualify later).
    public static List<Cell> SeedZones(IReadOnlyList<Cell> candidates, int count, int minSpacing, Func<int, int> rng)
    {
        var pool = new List<Cell>(candidates);
        var zones = new List<Cell>();
        while (zones.Count < count && pool.Count > 0)
        {
            var pick = pool[rng(pool.Count)];
            pool.Remove(pick);
            bool tooClose = false;
            foreach (var z in zones)
                if (Spacing(z, pick) < minSpacing) { tooClose = true; break; }
            if (!tooClose) zones.Add(pick);
        }
        return zones;
    }

    // A spawn lands on the zone cell or one of its neighbors, skipping blocked
    // cells. False when the zone is saturated (spec: skip, never force-place).
    public static bool TryPickSpawnCell(Cell zone, IReadOnlyList<Cell> neighborOffsets,
        HashSet<Cell> blocked, Func<int, int> rng, out Cell result)
    {
        var open = new List<Cell>();
        if (!blocked.Contains(zone)) open.Add(zone);
        foreach (var o in neighborOffsets)
        {
            var c = new Cell(zone.x + o.x, zone.y + o.y);
            if (!blocked.Contains(c)) open.Add(c);
        }
        if (open.Count == 0) { result = default(Cell); return false; }
        result = open[rng(open.Count)];
        return true;
    }

    // Uniform pick among enemies whose tier passes the doom gate. -1 when none.
    public static int PickEnemyIndex(IReadOnlyList<int> tiers, int maxTier, Func<int, int> rng)
    {
        var eligible = new List<int>();
        for (int i = 0; i < tiers.Count; i++)
            if (tiers[i] <= maxTier) eligible.Add(i);
        return eligible.Count == 0 ? -1 : eligible[rng(eligible.Count)];
    }
}
