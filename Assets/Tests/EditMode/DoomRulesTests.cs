using NUnit.Framework;

public class DoomRulesTests
{
    static DoomTuning T() => new DoomTuning(); // defaults: max 20, bands 6/13, intervals 3/2/1, bonuses 1/2

    [Test] public void Add_ClampsAtMax() => Assert.AreEqual(20, DoomRules.Add(19, 3, T()));
    [Test] public void Add_FloorsAtZero() => Assert.AreEqual(0, DoomRules.Add(1, -5, T()));
    [Test] public void IsLoss_AtMax() => Assert.IsTrue(DoomRules.IsLoss(20, T()));
    [Test] public void IsLoss_BelowMax() => Assert.IsFalse(DoomRules.IsLoss(19, T()));

    [Test]
    public void SpawnInterval_Bands()
    {
        Assert.AreEqual(3, DoomRules.SpawnInterval(0, T()));
        Assert.AreEqual(3, DoomRules.SpawnInterval(6, T()));
        Assert.AreEqual(2, DoomRules.SpawnInterval(7, T()));
        Assert.AreEqual(2, DoomRules.SpawnInterval(13, T()));
        Assert.AreEqual(1, DoomRules.SpawnInterval(14, T()));
    }

    [Test]
    public void ShouldSpawn_CounterReachesInterval()
    {
        Assert.IsFalse(DoomRules.ShouldSpawn(0, 2, T()));
        Assert.IsTrue(DoomRules.ShouldSpawn(0, 3, T()));
    }

    [Test]
    public void MaxTier_Bands()
    {
        Assert.AreEqual(1, DoomRules.MaxTier(6, T()));
        Assert.AreEqual(2, DoomRules.MaxTier(7, T()));
        Assert.AreEqual(2, DoomRules.MaxTier(13, T()));
        Assert.AreEqual(3, DoomRules.MaxTier(14, T()));
    }

    [Test]
    public void StatBonus_Bands()
    {
        Assert.AreEqual(0, DoomRules.StatBonus(6, T()));
        Assert.AreEqual(1, DoomRules.StatBonus(7, T()));
        Assert.AreEqual(1, DoomRules.StatBonus(13, T()));
        Assert.AreEqual(2, DoomRules.StatBonus(14, T()));
    }

    [Test]
    public void Add_NegativeAmount_ClampsAtZero()
    {
        Assert.AreEqual(0, DoomRules.Add(1, -3, T()));
        Assert.AreEqual(4, DoomRules.Add(7, -3, T()));
    }

    [Test]
    public void TurnsForBand_Shrinks_As_Doom_Climbs()
    {
        var t = T();
        Assert.AreEqual(6, DoomRules.TurnsForBand(0, t));   // low band
        Assert.AreEqual(6, DoomRules.TurnsForBand(6, t));   // low band edge
        Assert.AreEqual(4, DoomRules.TurnsForBand(7, t));   // mid band
        Assert.AreEqual(4, DoomRules.TurnsForBand(13, t));  // mid band edge
        Assert.AreEqual(3, DoomRules.TurnsForBand(14, t));  // high band
        Assert.AreEqual(3, DoomRules.TurnsForBand(99, t));  // clamped high
    }
}
