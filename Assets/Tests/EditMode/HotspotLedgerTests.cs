using NUnit.Framework;
using ArchonsRise.Hotspots;
using ArchonsRise.SaveData;

public class HotspotLedgerTests
{
    [Test]
    public void Harvest_DecrementsRemaining()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(2, 3), "red_node", 3);
        l.Harvest(new Cell(2, 3));
        Assert.AreEqual(2, l.Remaining(new Cell(2, 3)));
    }

    [Test]
    public void Harvest_UnlimitedStaysUnlimited()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(0, 0), "rich_vein", -1);
        l.Harvest(new Cell(0, 0));
        Assert.AreEqual(-1, l.Remaining(new Cell(0, 0)));
        Assert.IsTrue(l.CanHarvest(new Cell(0, 0)));
    }

    [Test]
    public void CanHarvest_FalseOnceDepleted()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(1, 1), "one_shot", 1);
        l.Harvest(new Cell(1, 1));
        Assert.IsFalse(l.CanHarvest(new Cell(1, 1)));
    }

    [Test]
    public void Export_OnlyEmitsChangedOrDepleted()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(4, 4), "full", 3);   // untouched → not exported
        l.Register(new Cell(5, 5), "used", 3);
        l.Harvest(new Cell(5, 5));               // changed → exported
        var export = l.Export();
        Assert.AreEqual(1, export.Length);
        Assert.AreEqual(5, export[0].x);
        Assert.AreEqual(2, export[0].remainingCharges);
    }

    [Test]
    public void ApplySavedState_RestoresRemaining()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(5, 5), "used", 3);
        bool ok = l.ApplySavedState(new HotspotState { x = 5, y = 5, hotspotId = "used", remainingCharges = 1 });
        Assert.IsTrue(ok);
        Assert.AreEqual(1, l.Remaining(new Cell(5, 5)));
    }

    [Test]
    public void ApplySavedState_FalseOnIdMismatch()
    {
        var l = new HotspotLedger();
        l.Register(new Cell(5, 5), "used", 3);
        bool ok = l.ApplySavedState(new HotspotState { x = 5, y = 5, hotspotId = "other", remainingCharges = 1 });
        Assert.IsFalse(ok);
    }
}
