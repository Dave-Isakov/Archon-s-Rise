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
}
