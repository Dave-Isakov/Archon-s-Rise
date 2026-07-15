using NUnit.Framework;

public class RefreshRulesTests
{
    [Test]
    public void PickCost_UsesInfluenceCost()
    {
        Assert.AreEqual(3, RefreshRules.PickCost(3));
    }

    [Test]
    public void PickCost_FloorsAtOne()
    {
        Assert.AreEqual(1, RefreshRules.PickCost(0));
        Assert.AreEqual(1, RefreshRules.PickCost(-2));
    }

    [Test]
    public void CanPick_ExhaustedAndAffordable()
    {
        Assert.IsTrue(RefreshRules.CanPick(true, 3, 3));
    }

    [Test]
    public void CanPick_RejectsReadyUnit()
    {
        Assert.IsFalse(RefreshRules.CanPick(false, 3, 6));
    }

    [Test]
    public void CanPick_RejectsOverBudget()
    {
        Assert.IsFalse(RefreshRules.CanPick(true, 4, 3));
    }

    [Test]
    public void CanPick_ZeroCostUnitNeedsBudgetOfOne()
    {
        Assert.IsTrue(RefreshRules.CanPick(true, 0, 1));
        Assert.IsFalse(RefreshRules.CanPick(true, 0, 0));
    }
}
