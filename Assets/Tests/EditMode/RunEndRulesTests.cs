using NUnit.Framework;

public class RunEndRulesTests
{
    [Test]
    public void Victory_AtTwoCastles()
    {
        Assert.IsFalse(RunEndRules.IsVictory(1));
        Assert.IsTrue(RunEndRules.IsVictory(2));
    }

    [Test]
    public void WoundOut_AtSix()
    {
        Assert.IsFalse(RunEndRules.IsWoundOut(5));
        Assert.IsTrue(RunEndRules.IsWoundOut(6));
    }

    [Test] public void WoundHand_FullHandAllWounds() => Assert.IsTrue(RunEndRules.IsWoundHand(5, 5, 5));
    [Test] public void WoundHand_FullHandWithOneRealCard() => Assert.IsFalse(RunEndRules.IsWoundHand(5, 4, 5));
    [Test] public void WoundHand_ShortHandAllWounds() => Assert.IsFalse(RunEndRules.IsWoundHand(3, 3, 5));
    [Test] public void WoundHand_EmptyHand() => Assert.IsFalse(RunEndRules.IsWoundHand(0, 0, 0));
}
