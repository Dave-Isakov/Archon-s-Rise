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

    // M2.12 starter guarantee. At map generation doom is 0, so every initial
    // enemy is tier 1 by construction — presence within the radius is the
    // whole check.
    public static bool NeedsStarterEnemy(IReadOnlyList<Cell> enemyCells, Cell start, int starterRadius)
    {
        foreach (var c in enemyCells)
            if (Spacing(c, start) <= starterRadius) return false;
        return true;
    }

    // Valid starter cells sit at spacing [2, starterRadius] from start (never
    // on or adjacent to it, matching the zone-spawn rule) and are not blocked.
    // False when the radius is saturated — the caller just skips (spec: place
    // at a valid cell, never force).
    public static bool TryPickStarterCell(IReadOnlyList<Cell> candidates, Cell start,
        int starterRadius, HashSet<Cell> blocked, Func<int, int> rng, out Cell result)
    {
        var open = new List<Cell>();
        foreach (var c in candidates)
        {
            int d = Spacing(c, start);
            if (d < 2 || d > starterRadius) continue;
            if (blocked.Contains(c)) continue;
            open.Add(c);
        }
        if (open.Count == 0) { result = default(Cell); return false; }
        result = open[rng(open.Count)];
        return true;
    }

    // --- Starter isolation (spec 2026-08-13) ---
    //
    // Note these use REAL hex adjacency, unlike Spacing/the caller-supplied offsets
    // above: those are a deliberate approximation for spreading zones around, where
    // being a cell out costs nothing. Isolation is different — it has to agree with
    // the adjacency combat actually resolves against, or it protects the wrong cells.

    // Parity-correct neighbours on the odd-r offset grid, mirroring
    // PlayerPosition.UpdateCompass (including its sign behaviour on negative rows,
    // so an off-map cell resolves the same way here as it does in the scene).
    public static List<Cell> HexNeighbors(Cell c)
    {
        bool oddRow = c.y % 2 != 0;
        return oddRow
            ? new List<Cell>
            {
                new Cell(c.x, c.y + 1),     // NW
                new Cell(c.x + 1, c.y + 1), // NE
                new Cell(c.x + 1, c.y),     // E
                new Cell(c.x + 1, c.y - 1), // SE
                new Cell(c.x, c.y - 1),     // SW
                new Cell(c.x - 1, c.y)      // W
            }
            : new List<Cell>
            {
                new Cell(c.x - 1, c.y + 1),
                new Cell(c.x, c.y + 1),
                new Cell(c.x + 1, c.y),
                new Cell(c.x, c.y - 1),
                new Cell(c.x - 1, c.y - 1),
                new Cell(c.x - 1, c.y)
            };
    }

    // True when a single cell touches both — i.e. a hex the player could stand on
    // that would drag BOTH into one field encounter (FieldEncounterRules).
    // A cell never packs with itself.
    public static bool SharesApproachHex(Cell a, Cell b)
    {
        if (a.Equals(b)) return false;
        var na = new HashSet<Cell>(HexNeighbors(a));
        foreach (var n in HexNeighbors(b))
            if (na.Contains(n)) return true;
        return false;
    }

    // Every cell within `radius` hex steps of `origin`, the origin included, by
    // breadth-first walk over real adjacency. Off-map cells come back too — callers
    // that care about map bounds filter them, since this layer knows no map.
    public static List<Cell> CellsWithin(Cell origin, int radius)
    {
        var seen = new HashSet<Cell> { origin };
        var result = new List<Cell> { origin };
        var frontier = new List<Cell> { origin };
        for (int step = 0; step < radius; step++)
        {
            var next = new List<Cell>();
            foreach (var c in frontier)
                foreach (var n in HexNeighbors(c))
                    if (seen.Add(n)) { result.Add(n); next.Add(n); }
            frontier = next;
        }
        return result;
    }

    // Every cell that must stay empty for `starter` to be fightable one-on-one.
    //
    // Falls out of the definition: b shares an approach hex with `starter` exactly
    // when b is a neighbour of one of `starter`'s neighbours — which is the radius-2
    // disk. Minus the starter's own cell, that is 18 cells.
    public static List<Cell> StarterQuarantine(Cell starter)
    {
        var result = new List<Cell>();
        foreach (var c in CellsWithin(starter, 2))
            if (!c.Equals(starter)) result.Add(c);
        return result;
    }
}
