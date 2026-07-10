using NUnit.Framework;

public class PlaceRulesTests
{
    [Test]
    public void AllowedServices_Town_RecruitHealCrystal()
    {
        Assert.AreEqual(PlaceService.Recruit | PlaceService.Heal | PlaceService.Crystal,
            PlaceRules.AllowedServices(PlaceType.Town));
    }

    [Test]
    public void AllowedServices_Keep_RecruitCrystal()
    {
        Assert.AreEqual(PlaceService.Recruit | PlaceService.Crystal,
            PlaceRules.AllowedServices(PlaceType.Keep));
    }

    [Test]
    public void AllowedServices_Castle_RecruitHealCardsCrystal()
    {
        Assert.AreEqual(PlaceService.Recruit | PlaceService.Heal | PlaceService.Cards | PlaceService.Crystal,
            PlaceRules.AllowedServices(PlaceType.Castle));
    }

    [Test]
    public void AllowedServices_Crystal_OfferedAtEveryPlace()
    {
        foreach (PlaceType type in System.Enum.GetValues(typeof(PlaceType)))
            Assert.IsTrue(PlaceRules.AllowedServices(type).HasFlag(PlaceService.Crystal),
                $"{type} should offer Crystal");
    }

    [Test]
    public void IsConquered_FalseBelowRoster_TrueAtRoster()
    {
        Assert.IsFalse(PlaceRules.IsConquered(0, 2));
        Assert.IsFalse(PlaceRules.IsConquered(1, 2));
        Assert.IsTrue(PlaceRules.IsConquered(2, 2));
    }

    [Test]
    public void IsConquered_EmptyRoster_TrueImmediately()
    {
        Assert.IsTrue(PlaceRules.IsConquered(0, 0)); // a Town has no guardians
    }

    [Test]
    public void RetreatWoundCount_IsThree()
    {
        Assert.AreEqual(3, PlaceRules.RetreatWoundCount);
    }
}
