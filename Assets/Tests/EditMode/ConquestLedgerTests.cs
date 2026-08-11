using NUnit.Framework;
using ArchonsRise.SaveData;

public class ConquestLedgerTests
{
    [Test]
    public void Progression_AdvancesAndConquersAtRosterSize()
    {
        var ledger = new ConquestLedger();
        var keep = new Cell(4, 7);
        ledger.Register(keep, PlaceType.Keep, 1);

        Assert.AreEqual(0, ledger.DefeatedCount(keep));
        Assert.IsFalse(ledger.IsConquered(keep));

        ledger.RecordDefeat(keep, 0);
        Assert.AreEqual(1, ledger.DefeatedCount(keep));
        Assert.IsTrue(ledger.IsConquered(keep));
    }

    [Test]
    public void Town_WithEmptyRoster_IsConqueredImmediately()
    {
        var ledger = new ConquestLedger();
        var town = new Cell(1, 1);
        ledger.Register(town, PlaceType.Town, 0);
        Assert.IsTrue(ledger.IsConquered(town));
    }

    [Test]
    public void UnregisteredCell_NotConquered_ZeroCount()
    {
        var ledger = new ConquestLedger();
        Assert.IsFalse(ledger.IsConquered(new Cell(9, 9)));
        Assert.AreEqual(0, ledger.DefeatedCount(new Cell(9, 9)));
    }

    [Test]
    public void ConqueredCastleCount_CountsOnlyConqueredCastles()
    {
        var ledger = new ConquestLedger();
        var castleDone = new Cell(2, 2);
        var castleHalf = new Cell(3, 3);
        var keepDone = new Cell(4, 4);
        ledger.Register(castleDone, PlaceType.Castle, 2);
        ledger.Register(castleHalf, PlaceType.Castle, 2);
        ledger.Register(keepDone, PlaceType.Keep, 1);

        ledger.RecordDefeat(castleDone, 0);
        ledger.RecordDefeat(castleDone, 1);
        ledger.RecordDefeat(castleHalf, 0);
        ledger.RecordDefeat(keepDone, 0);

        Assert.AreEqual(1, ledger.ConqueredCastleCount());
    }

    [Test]
    public void Export_OnlyEntriesWithProgress()
    {
        var ledger = new ConquestLedger();
        ledger.Register(new Cell(2, 2), PlaceType.Castle, 2);
        ledger.Register(new Cell(5, 5), PlaceType.Keep, 1);
        ledger.RecordDefeat(new Cell(2, 2), 0);

        var exported = ledger.Export();
        Assert.AreEqual(1, exported.Length);
        Assert.AreEqual(2, exported[0].x);
        Assert.AreEqual(2, exported[0].y);
        Assert.AreEqual(1, exported[0].defeatedCount);
    }

    // The Castle Brune bug (2026-08-10): the roster's SECOND guardian was killed
    // during Siege and the player fled. A count-only ledger re-spawned roster[1]
    // — the corpse — and treated the untouched roster[0] as dead.
    [Test]
    public void OutOfOrderDefeat_LeavesTheSurvivorRemaining()
    {
        var ledger = new ConquestLedger();
        var castle = new Cell(8, 8);
        ledger.Register(castle, PlaceType.Castle, 2);

        ledger.RecordDefeat(castle, 1);

        Assert.AreEqual(1, ledger.DefeatedCount(castle));
        Assert.IsFalse(ledger.IsConquered(castle));
        Assert.IsTrue(ledger.IsDefeated(castle, 1));
        Assert.IsFalse(ledger.IsDefeated(castle, 0));
        CollectionAssert.AreEqual(new[] { 0 }, ledger.RemainingIndices(castle, 2));
    }

    [Test]
    public void RecordDefeat_IsIdempotentPerIndex()
    {
        var ledger = new ConquestLedger();
        var castle = new Cell(7, 7);
        ledger.Register(castle, PlaceType.Castle, 3);

        ledger.RecordDefeat(castle, 2);
        ledger.RecordDefeat(castle, 2);

        Assert.AreEqual(1, ledger.DefeatedCount(castle));
        CollectionAssert.AreEqual(new[] { 0, 1 }, ledger.RemainingIndices(castle, 3));
    }

    [Test]
    public void RemainingIndices_EmptyWhenAllDefeated()
    {
        var ledger = new ConquestLedger();
        var keep = new Cell(3, 1);
        ledger.Register(keep, PlaceType.Keep, 2);
        ledger.RecordDefeat(keep, 1);
        ledger.RecordDefeat(keep, 0);

        Assert.IsTrue(ledger.IsConquered(keep));
        CollectionAssert.IsEmpty(ledger.RemainingIndices(keep, 2));
    }

    [Test]
    public void SaveRoundTrip_PreservesWhichGuardianDied()
    {
        var before = new ConquestLedger();
        before.Register(new Cell(8, 8), PlaceType.Castle, 2);
        before.RecordDefeat(new Cell(8, 8), 1);

        var exported = before.Export();
        Assert.AreEqual(1, exported.Length);
        CollectionAssert.AreEqual(new[] { 1 }, exported[0].defeatedIndices);

        var after = new ConquestLedger();
        after.ApplySaved(exported[0].x, exported[0].y, exported[0].defeatedIndices);
        after.Register(new Cell(8, 8), PlaceType.Castle, 2);

        Assert.IsFalse(after.IsConquered(new Cell(8, 8)));
        CollectionAssert.AreEqual(new[] { 0 }, after.RemainingIndices(new Cell(8, 8), 2));
    }

    [Test]
    public void ApplySaved_BeforeOrAfterRegister_BothRestore()
    {
        var before = new ConquestLedger();
        before.ApplySaved(6, 6, new[] { 0 });
        before.Register(new Cell(6, 6), PlaceType.Castle, 2);
        Assert.AreEqual(1, before.DefeatedCount(new Cell(6, 6)));
        Assert.IsFalse(before.IsConquered(new Cell(6, 6)));

        var after = new ConquestLedger();
        after.Register(new Cell(6, 6), PlaceType.Castle, 2);
        after.ApplySaved(6, 6, new[] { 0, 1 });
        Assert.IsTrue(after.IsConquered(new Cell(6, 6)));
        Assert.AreEqual(1, after.ConqueredCastleCount());
    }
}
