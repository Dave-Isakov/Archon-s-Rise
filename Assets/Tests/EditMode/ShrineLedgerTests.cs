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

    // --- the guardian's debt (2026-07-31) ---

    [Test]
    public void FreshShrine_OwesNothing()
    {
        var l = new ShrineLedger();
        l.Register(new Cell(3, 3), "shrine_a");
        Assert.AreEqual(ShrineLedger.NoReward, l.OwedReward(new Cell(3, 3)));
        Assert.AreEqual(ShrineLedger.NoReward, l.OwedReward(new Cell(9, 9)), "unknown cell owes nothing");
    }

    [Test]
    public void SetGuarding_RecordsTheDebtAndTheState()
    {
        var l = new ShrineLedger();
        l.Register(new Cell(3, 3), "shrine_a");
        l.SetGuarding(new Cell(3, 3), 0);   // 0 is a real reward (CardPick), not "none"
        Assert.AreEqual(ShrineVisualState.Guarding, l.State(new Cell(3, 3)));
        Assert.AreEqual(0, l.OwedReward(new Cell(3, 3)));
    }

    [Test]
    public void LeavingGuarding_SettlesTheDebt()
    {
        var l = new ShrineLedger();
        l.Register(new Cell(3, 3), "shrine_a");
        l.SetGuarding(new Cell(3, 3), 2);
        l.SetState(new Cell(3, 3), ShrineVisualState.ConsumedDormant);
        Assert.AreEqual(ShrineLedger.NoReward, l.OwedReward(new Cell(3, 3)),
            "a paid-out shrine must not still owe, or the reward could be granted twice");
    }

    [Test]
    public void Export_CarriesTheOwedReward()
    {
        var l = new ShrineLedger();
        l.Register(new Cell(2, 2), "guarded");
        l.SetGuarding(new Cell(2, 2), 1);
        var export = l.Export();
        Assert.AreEqual(1, export.Length);
        Assert.AreEqual(1, export[0].owedReward);
    }

    [Test]
    public void ApplySavedState_RestoresTheOwedReward()
    {
        var l = new ShrineLedger();
        l.Register(new Cell(2, 2), "guarded");
        l.ApplySavedState(new ShrineState
        {
            x = 2, y = 2, shrineId = "guarded",
            state = (int)ShrineVisualState.Guarding, owedReward = 2
        });
        Assert.AreEqual(2, l.OwedReward(new Cell(2, 2)));
    }

    [Test]
    public void ApplySavedState_IgnoresADebtOnANonGuardingShrine()
    {
        var l = new ShrineLedger();
        l.Register(new Cell(2, 2), "used");
        l.ApplySavedState(new ShrineState
        {
            x = 2, y = 2, shrineId = "used",
            state = (int)ShrineVisualState.ConsumedDormant, owedReward = 1
        });
        Assert.AreEqual(ShrineLedger.NoReward, l.OwedReward(new Cell(2, 2)));
    }
}
