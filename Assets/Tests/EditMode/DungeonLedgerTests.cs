using NUnit.Framework;
using ArchonsRise.SaveData;

public class DungeonLedgerTests
{
    static DungeonLedger Fresh()
    {
        var l = new DungeonLedger();
        l.Register(new Cell(2, 3), "dungeon-a");
        l.Register(new Cell(8, 9), "dungeon-b");
        return l;
    }

    [Test]
    public void Defeats_AccumulateToCompletion()
    {
        var l = Fresh();
        var c = new Cell(2, 3);
        Assert.AreEqual(0, l.DefeatedCount(c));
        l.RecordDefeat(c); l.RecordDefeat(c);
        Assert.IsFalse(l.IsComplete(c));
        l.RecordDefeat(c);
        Assert.IsTrue(l.IsComplete(c));
    }

    [Test]
    public void FlaggedCount_IgnoresCompletedDungeons()
    {
        var l = Fresh();
        var c = new Cell(2, 3);
        l.SetFlagged(c);
        l.SetFlagged(new Cell(8, 9));
        Assert.AreEqual(2, l.FlaggedCount());
        l.RecordDefeat(c); l.RecordDefeat(c); l.RecordDefeat(c);
        Assert.AreEqual(1, l.FlaggedCount()); // a cleared dungeon stops ticking
    }

    [Test]
    public void FlagCandidates_ExcludesFlaggedAndComplete()
    {
        var l = Fresh();
        l.SetFlagged(new Cell(2, 3));
        CollectionAssert.AreEquivalent(new[] { new Cell(8, 9) }, l.FlagCandidates());
    }

    [Test]
    public void Export_OnlyTouchedEntries_RoundTripsThroughApply()
    {
        var l = Fresh();
        l.RecordDefeat(new Cell(2, 3));
        l.SetFlagged(new Cell(2, 3));
        var exported = l.Export();
        Assert.AreEqual(1, exported.Length); // untouched dungeon-b is not saved

        var restored = Fresh();
        Assert.IsTrue(restored.ApplySavedState(exported[0]));
        Assert.AreEqual(1, restored.DefeatedCount(new Cell(2, 3)));
        Assert.IsTrue(restored.IsFlagged(new Cell(2, 3)));
    }

    [Test]
    public void ApplySavedState_RejectsUnknownCellAndIdMismatch()
    {
        var l = Fresh();
        Assert.IsFalse(l.ApplySavedState(new DungeonState { x = 5, y = 5, dungeonId = "dungeon-a" }));
        Assert.IsFalse(l.ApplySavedState(new DungeonState { x = 2, y = 3, dungeonId = "dungeon-zzz" }));
    }
}
