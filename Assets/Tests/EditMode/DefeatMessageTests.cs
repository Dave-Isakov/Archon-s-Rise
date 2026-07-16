using NUnit.Framework;

public class DefeatMessageTests
{
    [Test]
    public void Exp_Only_Names_Experience_With_Icon()
    {
        var s = DefeatMessage.Compose("Goblin", 12, null, false);
        Assert.AreEqual(
            "Goblin has been defeated. You receive " + IconMarkup.Cost(IconConcept.Experience, 12) + ".",
            s);
    }

    [Test]
    public void Includes_Crystal_Tag_When_Present()
    {
        var s = DefeatMessage.Compose("Ogre", 5, EmpowerType.Red, false);
        StringAssert.Contains(IconMarkup.CrystalTag(EmpowerType.Red), s);
    }

    [Test]
    public void Mentions_Card_Pick_When_Pending()
    {
        var s = DefeatMessage.Compose("Wolf", 3, null, true);
        StringAssert.Contains("a new card to choose", s);
    }

    [Test]
    public void No_Card_Phrase_When_Not_Pending()
    {
        var s = DefeatMessage.Compose("Rat", 1, null, false);
        StringAssert.DoesNotContain("card", s);
    }
}
