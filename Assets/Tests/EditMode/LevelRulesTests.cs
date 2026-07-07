using System.Collections.Generic;
using NUnit.Framework;

public class LevelRulesTests
{
    static List<LevelRewardEntry> Table() => new List<LevelRewardEntry>
    {
        new LevelRewardEntry { level = 2, skillPicks = 1 },
        new LevelRewardEntry { level = 3, hpBonus = 1, cardPicks = 1 },
        new LevelRewardEntry { level = 4, handSizeBonus = 1, armySizeBonus = 1 },
        new LevelRewardEntry { level = 7, skillPicks = 1, armySizeBonus = 1 },
    };

    [Test]
    public void RewardsFor_ReturnsMatchingEntryOrNull()
    {
        Assert.AreEqual(1, LevelRules.RewardsFor(2, Table()).skillPicks);
        Assert.AreEqual(1, LevelRules.RewardsFor(3, Table()).cardPicks);
        Assert.IsNull(LevelRules.RewardsFor(5, Table()));   // no entry for 5
        Assert.IsNull(LevelRules.RewardsFor(99, Table()));  // past the table
    }

    [Test]
    public void DerivedHandSize_SumsBonusesUpToLevel()
    {
        Assert.AreEqual(5, LevelRules.DerivedHandSize(5, 1, Table()));
        Assert.AreEqual(5, LevelRules.DerivedHandSize(5, 3, Table()));  // bonus is at 4
        Assert.AreEqual(6, LevelRules.DerivedHandSize(5, 4, Table()));
        Assert.AreEqual(6, LevelRules.DerivedHandSize(5, 9, Table()));  // no later bonuses
    }

    [Test]
    public void DerivedArmyCap_StartsAtOneAndSums()
    {
        Assert.AreEqual(1, LevelRules.DerivedArmyCap(1, Table()));
        Assert.AreEqual(2, LevelRules.DerivedArmyCap(4, Table()));
        Assert.AreEqual(3, LevelRules.DerivedArmyCap(7, Table()));
        Assert.AreEqual(3, LevelRules.DerivedArmyCap(20, Table()));
    }

    [Test]
    public void CarriedExp_KeepsOverflowAndClampsAtZero()
    {
        Assert.AreEqual(3, LevelRules.CarriedExp(18, 15)); // overflow carries
        Assert.AreEqual(0, LevelRules.CarriedExp(15, 15)); // exact
        Assert.AreEqual(0, LevelRules.CarriedExp(10, 15)); // defensive clamp
    }

    [Test]
    public void DrawSkillChoices_ExcludesOwnedNoDuplicates()
    {
        var pool = new List<string> { "a", "b", "c", "d", "e" };
        var owned = new List<string> { "b", "d" };
        var rng = new System.Random(42);
        var picks = LevelRules.DrawSkillChoices(pool, owned, rng, 3);

        Assert.AreEqual(3, picks.Count);
        CollectionAssert.AllItemsAreUnique(picks);
        CollectionAssert.IsNotSubsetOf(new[] { "b" }, picks);
        CollectionAssert.IsNotSubsetOf(new[] { "d" }, picks);
    }

    [Test]
    public void DrawSkillChoices_ReturnsFewerWhenPoolRunsDry()
    {
        var pool = new List<string> { "a", "b" };
        var rng = new System.Random(1);
        Assert.AreEqual(2, LevelRules.DrawSkillChoices(pool, new List<string>(), rng, 3).Count);
        Assert.AreEqual(0, LevelRules.DrawSkillChoices(pool, new List<string> { "a", "b" }, rng, 3).Count);
    }
}
