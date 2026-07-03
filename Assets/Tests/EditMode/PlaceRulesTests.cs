using NUnit.Framework;

public class PlaceRulesTests
{
    [Test]
    public void AllowedServices_Town_RecruitAndHeal()
    {
        Assert.AreEqual(PlaceService.Recruit | PlaceService.Heal,
            PlaceRules.AllowedServices(PlaceType.Town));
    }

    [Test]
    public void AllowedServices_Keep_RecruitOnly()
    {
        Assert.AreEqual(PlaceService.Recruit, PlaceRules.AllowedServices(PlaceType.Keep));
    }

    [Test]
    public void AllowedServices_Castle_RecruitHealCards()
    {
        Assert.AreEqual(PlaceService.Recruit | PlaceService.Heal | PlaceService.Cards,
            PlaceRules.AllowedServices(PlaceType.Castle));
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
