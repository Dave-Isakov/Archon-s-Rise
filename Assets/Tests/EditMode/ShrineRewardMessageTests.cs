using NUnit.Framework;

public class ShrineRewardMessageTests
{
    [Test]
    public void Exp_UsesTheIconNotTheWord()
    {
        var msg = ShrineRewardMessage.Compose(30, null, 0);
        StringAssert.Contains(IconMarkup.Cost(IconConcept.Experience, 30), msg);
        StringAssert.DoesNotContain("experience", msg.ToLowerInvariant());
    }

    [Test]
    public void OneUnit_ReadsSingular()
    {
        var msg = ShrineRewardMessage.Compose(0, new[] { "Knight" }, 0);
        StringAssert.Contains("Knight joins your army", msg);
    }

    [Test]
    public void TwoUnits_AreBothNamed()
    {
        var msg = ShrineRewardMessage.Compose(0, new[] { "Knight", "Scout" }, 0);
        StringAssert.Contains("Knight and Scout join your army", msg);
    }

    [Test]
    public void CardPicks_CountTheChoicesToCome()
    {
        StringAssert.Contains("a new card to choose", ShrineRewardMessage.Compose(0, null, 1));
        StringAssert.Contains("2 new cards to choose", ShrineRewardMessage.Compose(0, null, 2));
    }

    [Test]
    public void NothingGranted_ComposesNothing()
    {
        // An empty authored unit pool grants nothing; the caller posts the empty
        // string, which GameLog ignores.
        Assert.AreEqual("", ShrineRewardMessage.Compose(0, new string[0], 0));
    }

    [Test]
    public void EveryPayout_NamesTheShrineAsTheSource()
    {
        StringAssert.StartsWith("The shrine's bargain pays out:", ShrineRewardMessage.Compose(15, null, 0));
        StringAssert.StartsWith("The shrine's bargain pays out:", ShrineRewardMessage.Compose(0, null, 2));
    }
}
