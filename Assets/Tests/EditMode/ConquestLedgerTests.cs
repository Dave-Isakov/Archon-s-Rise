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

        ledger.RecordDefeat(keep);
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

        ledger.RecordDefeat(castleDone);
        ledger.RecordDefeat(castleDone);
        ledger.RecordDefeat(castleHalf);
        ledger.RecordDefeat(keepDone);

        Assert.AreEqual(1, ledger.ConqueredCastleCount());
    }

    [Test]
    public void Export_OnlyEntriesWithProgress()
    {
        var ledger = new ConquestLedger();
        ledger.Register(new Cell(2, 2), PlaceType.Castle, 2);
        ledger.Register(new Cell(5, 5), PlaceType.Keep, 1);
        ledger.RecordDefeat(new Cell(2, 2));

        var exported = ledger.Export();
        Assert.AreEqual(1, exported.Length);
        Assert.AreEqual(2, exported[0].x);
        Assert.AreEqual(2, exported[0].y);
        Assert.AreEqual(1, exported[0].defeatedCount);
    }

    [Test]
    public void ApplySavedCount_BeforeOrAfterRegister_BothRestore()
    {
        var before = new ConquestLedger();
        before.ApplySavedCount(6, 6, 1);
        before.Register(new Cell(6, 6), PlaceType.Castle, 2);
        Assert.AreEqual(1, before.DefeatedCount(new Cell(6, 6)));
        Assert.IsFalse(before.IsConquered(new Cell(6, 6)));

        var after = new ConquestLedger();
        after.Register(new Cell(6, 6), PlaceType.Castle, 2);
        after.ApplySavedCount(6, 6, 2);
        Assert.IsTrue(after.IsConquered(new Cell(6, 6)));
        Assert.AreEqual(1, after.ConqueredCastleCount());
    }
}
