using NUnit.Framework;

public class TurnButtonGateTests
{
    [Test]
    public void EndTurn_InCombat_Disabled_EvenWhenDrawIsFine()
    {
        Assert.IsFalse(TurnButtonGate.EndTurn(inCombat: true, verdict: DrawVerdict.Draw));
    }

    [Test]
    public void EndTurn_OutOfCombat_DeckEmpty_Disabled()
    {
        Assert.IsFalse(TurnButtonGate.EndTurn(inCombat: false, verdict: DrawVerdict.DeckEmpty));
    }

    [Test]
    public void EndTurn_OutOfCombat_CanDraw_Enabled()
    {
        Assert.IsTrue(TurnButtonGate.EndTurn(inCombat: false, verdict: DrawVerdict.Draw));
    }

    [Test]
    public void EndTurn_OutOfCombat_HandFull_Enabled()
    {
        // A full hand needs no draw; End Turn stays available.
        Assert.IsTrue(TurnButtonGate.EndTurn(inCombat: false, verdict: DrawVerdict.HandFull));
    }

    [Test]
    public void EndRound_InCombat_Disabled()
    {
        Assert.IsFalse(TurnButtonGate.EndRound(inCombat: true));
    }

    [Test]
    public void EndRound_OutOfCombat_Enabled()
    {
        Assert.IsTrue(TurnButtonGate.EndRound(inCombat: false));
    }
}

