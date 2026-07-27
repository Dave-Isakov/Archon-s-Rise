using NUnit.Framework;
using ArchonsRise.Shrines;
using ArchonsRise.SaveData;

public class ShrineLedgerTests
{
    [Test]
    public void NewShrine_IsLive()
    {
        var l = new ShrineLedger();
        l.Register(new Cell(3, 3), "shrine_a");
        Assert.AreEqual(ShrineVisualState.Live, l.State(new Cell(3, 3)));
    }

    [Test]
    public void SetState_Persists()
    {
        var l = new ShrineLedger();
        l.Register(new Cell(3, 3), "shrine_a");
        l.SetState(new Cell(3, 3), ShrineVisualState.Guarding);
        Assert.AreEqual(ShrineVisualState.Guarding, l.State(new Cell(3, 3)));
    }

    [Test]
    public void Export_OnlyEmitsNonLive()
    {
        var l = new ShrineLedger();
        l.Register(new Cell(1, 1), "live_one");                 // Live → skipped
        l.Register(new Cell(2, 2), "used");
        l.SetState(new Cell(2, 2), ShrineVisualState.ConsumedDormant);
        var export = l.Export();
        Assert.AreEqual(1, export.Length);
        Assert.AreEqual(2, export[0].x);
        Assert.AreEqual((int)ShrineVisualState.ConsumedDormant, export[0].state);
    }

    [Test]
    public void ApplySavedState_RestoresState()
    {
        var l = new ShrineLedger();
        l.Register(new Cell(2, 2), "used");
        bool ok = l.ApplySavedState(new ShrineState { x = 2, y = 2, shrineId = "used", state = (int)ShrineVisualState.Guarding });
        Assert.IsTrue(ok);
        Assert.AreEqual(ShrineVisualState.Guarding, l.State(new Cell(2, 2)));
    }

    [Test]
    public void ApplySavedState_FalseOnIdMismatch()
    {
        var l = new ShrineLedger();
        l.Register(new Cell(2, 2), "used");
        Assert.IsFalse(l.ApplySavedState(new ShrineState { x = 2, y = 2, shrineId = "other", state = 1 }));
    }
}
