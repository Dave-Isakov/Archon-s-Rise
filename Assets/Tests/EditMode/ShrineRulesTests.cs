using System.Collections.Generic;
using NUnit.Framework;
using ArchonsRise.Shrines;

public class ShrineRulesTests
{
    [Test]
    public void IsGood_TrueBelowChance()
    {
        Assert.IsTrue(ShrineRules.IsGood(0.5f, 0.2f));
        Assert.IsFalse(ShrineRules.IsGood(0.5f, 0.7f));
    }

    [Test]
    public void IsGood_BoundaryIsBad()
    {
        // roll exactly at the chance is NOT good (strict less-than).
        Assert.IsFalse(ShrineRules.IsGood(0.5f, 0.5f));
    }

    [Test]
    public void RollType_PicksFromPoolByIndex()
    {
        var pool = new List<ShrineReward> { ShrineReward.CardPick, ShrineReward.Unit, ShrineReward.LargeExp };
        Assert.AreEqual(ShrineReward.Unit, ShrineRules.RollType(pool, _ => 1));
        Assert.AreEqual(ShrineReward.LargeExp, ShrineRules.RollType(pool, _ => 2));
    }

    [Test]
    public void RewardCount_OneWhenGood_TwoWhenFight()
    {
        Assert.AreEqual(1, ShrineRules.RewardCount(true));
        Assert.AreEqual(2, ShrineRules.RewardCount(false));
    }
}
