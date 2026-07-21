using NUnit.Framework;

public class TurnButtonGateTests
{
    [Test]
    public void EndTurn_InCombat_Disabled()
    {
        Assert.IsFalse(TurnButtonGate.EndTurn(inCombat: true));
    }

    [Test]
    public void EndTurn_OutOfCombat_Enabled()
    {
        Assert.IsTrue(TurnButtonGate.EndTurn(inCombat: false));
    }
}
