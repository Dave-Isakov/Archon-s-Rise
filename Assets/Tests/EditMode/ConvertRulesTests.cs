using NUnit.Framework;

public class ConvertRulesTests
{
    static readonly StatType AllActions =
        StatType.Attack | StatType.Defend | StatType.Influence | StatType.Explore;

    [Test]
    public void IsValid_SingleActionSourceToActionTarget()
    {
        Assert.IsTrue(ConvertRules.IsValid(StatType.Defend, StatType.Attack));
    }

    [Test]
    public void IsValid_MultiSourceToTarget()
    {
        Assert.IsTrue(ConvertRules.IsValid(
            StatType.Attack | StatType.Defend | StatType.Explore, StatType.Influence));
    }

    [Test]
    public void IsValid_RejectsTargetInSources()
    {
        Assert.IsFalse(ConvertRules.IsValid(AllActions, StatType.Influence));
    }

    [Test]
    public void IsValid_RejectsNoneSource()
    {
        Assert.IsFalse(ConvertRules.IsValid(StatType.None, StatType.Attack));
    }

    [Test]
    public void IsValid_RejectsSiegeTarget()
    {
        Assert.IsFalse(ConvertRules.IsValid(StatType.Attack, StatType.Siege));
    }

    [Test]
    public void IsValid_RejectsSiegeSource()
    {
        Assert.IsFalse(ConvertRules.IsValid(StatType.Siege, StatType.Attack));
    }

    [Test]
    public void IsValid_RejectsNonActionSourceMixedIn()
    {
        Assert.IsFalse(ConvertRules.IsValid(StatType.Defend | StatType.Heal, StatType.Attack));
    }

    [Test]
    public void IsValid_RejectsMultiFlagTarget()
    {
        Assert.IsFalse(ConvertRules.IsValid(StatType.Explore, StatType.Attack | StatType.Defend));
    }

    [Test]
    public void IndexOf_MapsPoolOrder()
    {
        Assert.AreEqual(0, ConvertRules.IndexOf(StatType.Attack));
        Assert.AreEqual(1, ConvertRules.IndexOf(StatType.Defend));
        Assert.AreEqual(2, ConvertRules.IndexOf(StatType.Influence));
        Assert.AreEqual(3, ConvertRules.IndexOf(StatType.Explore));
        Assert.AreEqual(-1, ConvertRules.IndexOf(StatType.Siege));
    }

    [Test]
    public void Moved_DrainsSingleSourceLeavesTargetZero()
    {
        int[] pools = { 2, 9, 4, 1 }; // atk, def, inf, exp
        int[] moved = ConvertRules.Moved(pools, StatType.Defend, StatType.Attack);
        Assert.AreEqual(new[] { 0, 9, 0, 0 }, moved);
    }

    [Test]
    public void Moved_DrainsAllFlaggedSources()
    {
        int[] pools = { 2, 3, 4, 5 };
        int[] moved = ConvertRules.Moved(pools,
            StatType.Attack | StatType.Defend | StatType.Explore, StatType.Influence);
        Assert.AreEqual(new[] { 2, 3, 0, 5 }, moved);
    }

    [Test]
    public void Moved_EmptyPoolsMoveNothing()
    {
        int[] moved = ConvertRules.Moved(new[] { 0, 0, 0, 0 }, StatType.Defend, StatType.Attack);
        Assert.AreEqual(new[] { 0, 0, 0, 0 }, moved);
    }

    [Test]
    public void Moved_InvalidConversionMovesNothing()
    {
        int[] moved = ConvertRules.Moved(new[] { 5, 5, 5, 5 }, StatType.None, StatType.Attack);
        Assert.AreEqual(new[] { 0, 0, 0, 0 }, moved);
    }

    [Test]
    public void MovedTotal_Sums()
    {
        Assert.AreEqual(10, ConvertRules.MovedTotal(new[] { 2, 3, 0, 5 }));
    }

    [Test]
    public void Describe_SingleSource()
    {
        Assert.AreEqual("Convert all Defend → Attack",
            ConvertRules.Describe(StatType.Defend, StatType.Attack));
    }

    [Test]
    public void Describe_MultiSource()
    {
        Assert.AreEqual("Convert all Attack, Defend, Explore → Influence",
            ConvertRules.Describe(StatType.Attack | StatType.Defend | StatType.Explore,
                StatType.Influence));
    }
}
