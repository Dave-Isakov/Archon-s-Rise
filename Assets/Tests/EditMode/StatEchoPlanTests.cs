using System.Collections.Generic;
using NUnit.Framework;

public class StatEchoPlanTests
{
    [Test]
    public void AllZero_ProducesNoEchoes()
    {
        Assert.IsEmpty(StatEchoPlan.NonZero(new[] { 0, 0, 0, 0 }));
    }

    [Test]
    public void SingleStat_ProducesOneEchoWithStatAndAmount()
    {
        var plan = StatEchoPlan.NonZero(new[] { 0, 0, 3, 0 }); // influence = index 2
        Assert.AreEqual(1, plan.Count);
        Assert.AreEqual(StatType.Influence, plan[0].Stat);
        Assert.AreEqual(3, plan[0].Amount);
    }

    [Test]
    public void MultiStat_ProducesOnePerNonZero_InStatOrder()
    {
        var plan = StatEchoPlan.NonZero(new[] { 2, 0, 0, 5 }); // attack=2, explore=5
        Assert.AreEqual(2, plan.Count);
        Assert.AreEqual(StatType.Attack,  plan[0].Stat);
        Assert.AreEqual(2,                plan[0].Amount);
        Assert.AreEqual(StatType.Explore, plan[1].Stat);
        Assert.AreEqual(5,                plan[1].Amount);
    }

    [Test]
    public void NullArray_ProducesNoEchoes()
    {
        Assert.IsEmpty(StatEchoPlan.NonZero(null));
    }
}
