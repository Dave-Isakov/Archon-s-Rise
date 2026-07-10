using System;
using System.Collections.Generic;
using NUnit.Framework;

public class RewardRulesTests
{
    static RewardTuning T() => new RewardTuning(); // tier1 [1,5], tier2 [3,7], tier3 [6,10], K=3

    // Returns an rng delegate that yields the given sequence, then repeats the last.
    static Func<int, int> Seq(params int[] values)
    {
        int i = 0;
        return _ => values[Math.Min(i++, values.Length - 1)];
    }

    // --- Tier clamp ---
    [Test] public void Tier_ClampsLow()  => Assert.AreEqual(1, T().Tier(0).expMin);
    [Test] public void Tier_One()        => Assert.AreEqual(1, T().Tier(1).expMin);
    [Test] public void Tier_Two()        => Assert.AreEqual(3, T().Tier(2).expMin);
    [Test] public void Tier_Three()      => Assert.AreEqual(6, T().Tier(3).expMin);
    [Test] public void Tier_ClampsHigh() => Assert.AreEqual(6, T().Tier(99).expMin);

    // --- SampleExp: bounds ---
    [Test]
    public void SampleExp_AllMinDraws_ReturnsMin()
        // every draw = expMin (rng returns 0) → average = expMin
        => Assert.AreEqual(1, RewardRules.SampleExp(1, T(), _ => 0));

    [Test]
    public void SampleExp_AllMaxDraws_ReturnsMax()
        // tier1 span = 5, rng returns span-1=4 → each draw = 1+4 = 5
        => Assert.AreEqual(5, RewardRules.SampleExp(1, T(), _ => 4));

    [Test]
    public void SampleExp_NeverExceedsRange()
    {
        var rng = new System.Random(1234);
        var t = T();
        for (int i = 0; i < 500; i++)
        {
            int e = RewardRules.SampleExp(3, t, max => rng.Next(max));
            Assert.GreaterOrEqual(e, 6);
            Assert.LessOrEqual(e, 10);
        }
    }

    [Test]
    public void SampleExp_AveragesDraws()
    {
        // tier1, K=3, draws (offsets) 0,4,2 → values 1,5,3 → sum 9 → avg 3
        Assert.AreEqual(3, RewardRules.SampleExp(1, T(), Seq(0, 4, 2)));
    }

    [Test]
    public void SampleExp_RoundsHalfAwayFromZero()
    {
        var t = T();
        t.expBellSamples = 2; // K=2 so a .5 average is reachable
        // tier1 draws (offsets) 0,1 → values 1,2 → sum 3 → avg 1.5 → round → 2
        Assert.AreEqual(2, RewardRules.SampleExp(1, t, Seq(0, 1)));
    }

    [Test]
    public void SampleExp_MinEqualsMax_ReturnsThatValue()
    {
        var t = T();
        t.tier1 = new RewardTierTuning { expMin = 4, expMax = 4 };
        Assert.AreEqual(4, RewardRules.SampleExp(1, t, _ => 0));
    }

    // --- CardTierForLevel (level-up card picks scale with player level) ---
    [Test] public void CardTier_LowLevel_Tier1()  => Assert.AreEqual(1, RewardRules.CardTierForLevel(1, T()));
    [Test] public void CardTier_JustBelow2_Tier1() => Assert.AreEqual(1, RewardRules.CardTierForLevel(3, T()));
    [Test] public void CardTier_At2_Tier2()        => Assert.AreEqual(2, RewardRules.CardTierForLevel(4, T()));
    [Test] public void CardTier_JustBelow3_Tier2() => Assert.AreEqual(2, RewardRules.CardTierForLevel(6, T()));
    [Test] public void CardTier_At3_Tier3()        => Assert.AreEqual(3, RewardRules.CardTierForLevel(7, T()));
    [Test] public void CardTier_HighLevel_Tier3()  => Assert.AreEqual(3, RewardRules.CardTierForLevel(20, T()));

    // --- Roll ---
    [Test] public void Roll_ZeroChance_AlwaysFalse() => Assert.IsFalse(RewardRules.Roll(0f, () => 0f));
    [Test] public void Roll_FullChance_AlwaysTrue()  => Assert.IsTrue(RewardRules.Roll(1f, () => 0.99f));
    [Test] public void Roll_Below_True()             => Assert.IsTrue(RewardRules.Roll(0.5f, () => 0.3f));
    [Test] public void Roll_Above_False()            => Assert.IsFalse(RewardRules.Roll(0.5f, () => 0.7f));
    [Test] public void Roll_AtThreshold_False()      => Assert.IsFalse(RewardRules.Roll(0.5f, () => 0.5f));
}
