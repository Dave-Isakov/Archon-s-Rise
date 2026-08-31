using System.Collections.Generic;
using NUnit.Framework;
using ArchonsRise.SaveData;

public class SpawnRulesTests
{
    // rng that returns a scripted sequence (last value repeats; clamped to max-1).
    static System.Func<int, int> Rng(params int[] seq)
    {
        int i = 0;
        return max =>
        {
            int v = seq[System.Math.Min(i, seq.Length - 1)];
            i++;
            return v >= max ? max - 1 : v;
        };
    }

    static readonly List<Cell> Offsets = new List<Cell>
    {
        new Cell(-1, 1), new Cell(0, 1), new Cell(1, 0),
        new Cell(0, -1), new Cell(-1, -1), new Cell(-1, 0)
    };

    [Test]
    public void SeedZones_RespectsCountAndSpacing()
    {
        var candidates = new List<Cell>();
        for (int x = 0; x < 20; x += 2)
            for (int y = 0; y < 20; y += 2)
                candidates.Add(new Cell(x, y));
        var zones = SpawnRules.SeedZones(candidates, 4, 4, Rng(0));
        Assert.AreEqual(4, zones.Count);
        for (int a = 0; a < zones.Count; a++)
            for (int b = a + 1; b < zones.Count; b++)
                Assert.GreaterOrEqual(SpawnRules.Spacing(zones[a], zones[b]), 4);
    }

    [Test]
    public void SeedZones_FewerWhenCandidatesExhausted()
    {
        var candidates = new List<Cell> { new Cell(0, 0), new Cell(1, 1) }; // inside min spacing
        var zones = SpawnRules.SeedZones(candidates, 3, 4, Rng(0));
        Assert.AreEqual(1, zones.Count);
    }

    [Test]
    public void TryPickSpawnCell_SkipsBlocked()
    {
        var zone = new Cell(5, 5);
        var blocked = new HashSet<Cell> { zone };
        Assert.IsTrue(SpawnRules.TryPickSpawnCell(zone, Offsets, blocked, Rng(0), out var cell));
        Assert.AreNotEqual(zone, cell);
    }

    [Test]
    public void TryPickSpawnCell_FalseWhenSaturated()
    {
        var zone = new Cell(5, 5);
        var blocked = new HashSet<Cell> { zone };
        foreach (var o in Offsets) blocked.Add(new Cell(zone.x + o.x, zone.y + o.y));
        Assert.IsFalse(SpawnRules.TryPickSpawnCell(zone, Offsets, blocked, Rng(0), out _));
    }

    [Test]
    public void PickEnemyIndex_FiltersByTier()
    {
        var tiers = new List<int> { 1, 3, 2, 1 };
        Assert.AreEqual(0, SpawnRules.PickEnemyIndex(tiers, 1, Rng(0))); // eligible: {0,3}
        Assert.AreEqual(3, SpawnRules.PickEnemyIndex(tiers, 1, Rng(1)));
        Assert.AreEqual(1, SpawnRules.PickEnemyIndex(tiers, 3, Rng(1))); // eligible: {0,1,2,3}
    }

    [Test]
    public void PickEnemyIndex_NoneEligible()
    {
        var tiers = new List<int> { 2, 3 };
        Assert.AreEqual(-1, SpawnRules.PickEnemyIndex(tiers, 1, Rng(0)));
    }

    // --- M2.12 starter-enemy guarantee ---

    [Test]
    public void NeedsStarterEnemy_TrueWhenNoEnemyInRadius()
    {
        var enemies = new List<Cell> { new Cell(9, 9), new Cell(12, 3) };
        Assert.IsTrue(SpawnRules.NeedsStarterEnemy(enemies, new Cell(0, 0), 5));
    }

    [Test]
    public void NeedsStarterEnemy_FalseWhenOneIsWithinRadius()
    {
        var enemies = new List<Cell> { new Cell(9, 9), new Cell(3, 4) };
        Assert.IsFalse(SpawnRules.NeedsStarterEnemy(enemies, new Cell(0, 0), 5));
    }

    [Test]
    public void NeedsStarterEnemy_TrueWhenNoEnemiesAtAll()
    {
        Assert.IsTrue(SpawnRules.NeedsStarterEnemy(new List<Cell>(), new Cell(0, 0), 5));
    }

    [Test]
    public void TryPickStarterCell_PicksOnlyInsideTheRing()
    {
        // (1,0) is adjacent to start (excluded), (9,9) beyond the radius, (4,4) valid.
        var candidates = new List<Cell> { new Cell(1, 0), new Cell(9, 9), new Cell(4, 4) };
        Cell picked;
        Assert.IsTrue(SpawnRules.TryPickStarterCell(candidates, new Cell(0, 0), 5,
            new HashSet<Cell>(), Rng(0), out picked));
        Assert.AreEqual(new Cell(4, 4), picked);
    }

    [Test]
    public void TryPickStarterCell_RespectsBlockedCells()
    {
        var candidates = new List<Cell> { new Cell(4, 4), new Cell(2, 3) };
        var blocked = new HashSet<Cell> { new Cell(4, 4) };
        Cell picked;
        Assert.IsTrue(SpawnRules.TryPickStarterCell(candidates, new Cell(0, 0), 5,
            blocked, Rng(0), out picked));
        Assert.AreEqual(new Cell(2, 3), picked);
    }

    [Test]
    public void TryPickStarterCell_FalseWhenSaturated()
    {
        var candidates = new List<Cell> { new Cell(4, 4) };
        var blocked = new HashSet<Cell> { new Cell(4, 4) };
        Cell picked;
        Assert.IsFalse(SpawnRules.TryPickStarterCell(candidates, new Cell(0, 0), 5,
            blocked, Rng(0), out picked));
    }

    // --- Starter isolation (spec 2026-08-13) ---

    // The parity table must match PlayerPosition.UpdateCompass exactly, because the
    // quarantine is only correct if it uses the same adjacency combat does.
    [Test]
    public void HexNeighbors_EvenRowMatchesTheCompass()
    {
        CollectionAssert.AreEquivalent(
            new List<Cell>
            {
                new Cell(-1, 1), new Cell(0, 1), new Cell(1, 0),
                new Cell(0, -1), new Cell(-1, -1), new Cell(-1, 0)
            },
            SpawnRules.HexNeighbors(new Cell(0, 0)));
    }

    [Test]
    public void HexNeighbors_OddRowShiftsEast()
    {
        CollectionAssert.AreEquivalent(
            new List<Cell>
            {
                new Cell(0, 2), new Cell(1, 2), new Cell(1, 1),
                new Cell(1, 0), new Cell(0, 0), new Cell(-1, 1)
            },
            SpawnRules.HexNeighbors(new Cell(0, 1)));
    }

    [Test]
    public void SharesApproachHex_TrueForAdjacentEnemies()
    {
        // (0,2) and (1,2) are neighbours; (0,1) touches both — the natural first
        // step out of the start hex, and the exact cell that pulled two enemies.
        Assert.IsTrue(SpawnRules.SharesApproachHex(new Cell(0, 2), new Cell(1, 2)));
    }

    [Test]
    public void SharesApproachHex_TrueAtHexDistanceTwo()
    {
        // One cell apart in a straight line still leaves the cell between them.
        Assert.IsTrue(SpawnRules.SharesApproachHex(new Cell(5, 4), new Cell(7, 4)));
    }

    [Test]
    public void SharesApproachHex_FalseAtHexDistanceThree()
    {
        Assert.IsFalse(SpawnRules.SharesApproachHex(new Cell(5, 4), new Cell(8, 4)));
    }

    [Test]
    public void SharesApproachHex_FalseAgainstItself()
    {
        // A token never packs with itself, so the starter must not quarantine its
        // own cell out from under it.
        Assert.IsFalse(SpawnRules.SharesApproachHex(new Cell(5, 4), new Cell(5, 4)));
    }

    [Test]
    public void CellsWithin_GrowsAsHexDisks()
    {
        // 1, 1+6, 1+6+12 — the hex disk sizes. A Chebyshev box would give 9 and 25.
        Assert.AreEqual(1, SpawnRules.CellsWithin(new Cell(9, 9), 0).Count);
        Assert.AreEqual(7, SpawnRules.CellsWithin(new Cell(9, 9), 1).Count);
        Assert.AreEqual(19, SpawnRules.CellsWithin(new Cell(9, 9), 2).Count);
    }

    [Test]
    public void CellsWithin_SameDisksFromAnOddRow()
    {
        // Parity must not change the shape, only the offsets it is built from.
        Assert.AreEqual(7, SpawnRules.CellsWithin(new Cell(9, 8), 1).Count);
        Assert.AreEqual(19, SpawnRules.CellsWithin(new Cell(9, 8), 2).Count);
    }

    [Test]
    public void CellsWithin_IncludesTheOriginAndItsNeighbours()
    {
        var origin = new Cell(4, 6);
        var disk = SpawnRules.CellsWithin(origin, 1);
        CollectionAssert.Contains(disk, origin);
        foreach (var n in SpawnRules.HexNeighbors(origin))
            CollectionAssert.Contains(disk, n);
    }

    // The gentle-start opening is this disk clipped to the map. The start sits in the
    // corner, so only a sliver of it is ever on-map — worth pinning, because it is
    // what makes a single harsh roll there matter so much.
    [Test]
    public void CellsWithin_StartCornerLeavesOnlySixOnMapCells()
    {
        int onMap = 0;
        var start = new Cell(0, 0);
        foreach (var c in SpawnRules.CellsWithin(start, 2))
        {
            if (c.Equals(start)) continue;
            if (c.x >= 0 && c.x <= 19 && c.y >= 0 && c.y <= 19) onMap++;
        }
        Assert.AreEqual(6, onMap);
    }

    // The quarantine is the radius-2 disk minus the centre: 6 neighbours + 12 at
    // distance two.
    [Test]
    public void StarterQuarantine_CoversEighteenCellsAndExcludesTheStarter()
    {
        var starter = new Cell(9, 9);
        var q = SpawnRules.StarterQuarantine(starter);
        CollectionAssert.DoesNotContain(q, starter);
        CollectionAssert.AllItemsAreUnique(q);
        Assert.AreEqual(18, q.Count);
    }

    // The defining property: quarantine membership and the shared-approach test are
    // the same statement, so the placement rule can never drift from the combat rule.
    [Test]
    public void StarterQuarantine_ContainsExactlyTheSharedApproachCells()
    {
        var starter = new Cell(9, 9);
        var q = new HashSet<Cell>(SpawnRules.StarterQuarantine(starter));
        for (int x = 4; x <= 14; x++)
            for (int y = 4; y <= 14; y++)
            {
                var other = new Cell(x, y);
                if (other.Equals(starter)) continue;
                Assert.AreEqual(SpawnRules.SharesApproachHex(starter, other), q.Contains(other),
                    $"quarantine disagrees with SharesApproachHex at {x},{y}");
            }
    }

    // The payoff: with the starter's quarantine blocked, that zone cannot land a
    // second enemy anywhere in its own footprint — the pack becomes a loner via the
    // existing "skip, never force-place" path.
    [Test]
    public void TryPickSpawnCell_FalseForThePackmateOnceTheStarterIsQuarantined()
    {
        var zone = new Cell(9, 9);
        var starter = zone;
        var blocked = new HashSet<Cell> { starter };
        foreach (var c in SpawnRules.StarterQuarantine(starter)) blocked.Add(c);

        Cell packmate;
        Assert.IsFalse(SpawnRules.TryPickSpawnCell(zone, Offsets, blocked, Rng(0), out packmate));
    }

    // A zone far enough away is untouched by the quarantine, so the rest of the map
    // still populates normally.
    [Test]
    public void TryPickSpawnCell_StillPlacesOutsideTheQuarantine()
    {
        var starter = new Cell(9, 9);
        var blocked = new HashSet<Cell> { starter };
        foreach (var c in SpawnRules.StarterQuarantine(starter)) blocked.Add(c);

        Cell picked;
        Assert.IsTrue(SpawnRules.TryPickSpawnCell(new Cell(14, 14), Offsets, blocked, Rng(0), out picked));
    }
}
