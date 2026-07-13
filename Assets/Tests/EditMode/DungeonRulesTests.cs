using System.Collections.Generic;
using NUnit.Framework;
using ArchonsRise.SaveData;

public class DungeonRulesTests
{
    static DoomTuning T() => new DoomTuning(); // lowBandMax 6, midBandMax 13

    [Test]
    public void NextTier_Progression()
    {
        Assert.AreEqual(1, DungeonRules.NextTier(0));
        Assert.AreEqual(2, DungeonRules.NextTier(1));
        Assert.AreEqual(3, DungeonRules.NextTier(2));
        Assert.AreEqual(0, DungeonRules.NextTier(3)); // complete: no next fight
    }

    [Test]
    public void IsComplete_Boundaries()
    {
        Assert.IsFalse(DungeonRules.IsComplete(2));
        Assert.IsTrue(DungeonRules.IsComplete(3));
    }

    [Test]
    public void RoundTick_AddsOnePerFlagged()
    {
        Assert.AreEqual(1, DungeonRules.RoundTick(0));
        Assert.AreEqual(3, DungeonRules.RoundTick(2));
        Assert.AreEqual(1, DungeonRules.RoundTick(-1)); // defensive
    }

    [Test]
    public void Relief_PicksTheRightKnob()
    {
        Assert.AreEqual(1, DungeonRules.Relief(false, T()));
        Assert.AreEqual(3, DungeonRules.Relief(true, T()));
    }

    [Test]
    public void BandsEntered_CrossingMatrix()
    {
        bool mid, high;
        DungeonRules.BandsEntered(6, 7, T(), out mid, out high);
        Assert.IsTrue(mid); Assert.IsFalse(high);
        DungeonRules.BandsEntered(13, 14, T(), out mid, out high);
        Assert.IsFalse(mid); Assert.IsTrue(high);
        DungeonRules.BandsEntered(6, 14, T(), out mid, out high);
        Assert.IsTrue(mid); Assert.IsTrue(high);   // one add can cross both
        DungeonRules.BandsEntered(7, 8, T(), out mid, out high);
        Assert.IsFalse(mid); Assert.IsFalse(high); // already inside mid
        DungeonRules.BandsEntered(8, 6, T(), out mid, out high);
        Assert.IsFalse(mid); Assert.IsFalse(high); // relief never "enters"
        DungeonRules.BandsEntered(5, 6, T(), out mid, out high);
        Assert.IsFalse(mid); Assert.IsFalse(high); // 6 is still the low band
    }

    [Test]
    public void PickFlagTargets_DistinctShortPoolAndEmpty()
    {
        var candidates = new List<Cell> { new Cell(1, 1), new Cell(2, 2), new Cell(3, 3) };
        var picks = DungeonRules.PickFlagTargets(candidates, 2, max => 0);
        Assert.AreEqual(2, picks.Count);
        Assert.AreNotEqual(picks[0], picks[1]);
        Assert.AreEqual(3, DungeonRules.PickFlagTargets(candidates, 5, max => 0).Count);
        Assert.AreEqual(0, DungeonRules.PickFlagTargets(new List<Cell>(), 2, max => 0).Count);
    }
}
