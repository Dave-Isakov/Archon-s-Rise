using NUnit.Framework;

public class HandNavRulesTests
{
    static bool[] NoWounds(int n) => new bool[n];

    [Test]
    public void First_PicksMiddleCard()
    {
        Assert.AreEqual(2, HandNavRules.First(NoWounds(5)));
        Assert.AreEqual(1, HandNavRules.First(NoWounds(3)));
        Assert.AreEqual(0, HandNavRules.First(NoWounds(1)));
    }

    [Test]
    public void First_SkipsWoundAtMiddle()
    {
        // middle (index 2) is a wound; nearest focusable is 3
        Assert.AreEqual(3, HandNavRules.First(new[] { false, false, true, false, false }));
    }

    [Test]
    public void First_AllWoundsOrEmpty_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, HandNavRules.First(new[] { true, true }));
        Assert.AreEqual(-1, HandNavRules.First(new bool[0]));
    }

    [Test]
    public void Step_MovesAndWraps()
    {
        Assert.AreEqual(1, HandNavRules.Step(0, +1, NoWounds(3)));
        Assert.AreEqual(0, HandNavRules.Step(2, +1, NoWounds(3))); // wrap right
        Assert.AreEqual(2, HandNavRules.Step(0, -1, NoWounds(3))); // wrap left
    }

    [Test]
    public void Step_SkipsWounds()
    {
        Assert.AreEqual(2, HandNavRules.Step(0, +1, new[] { false, true, false }));
        Assert.AreEqual(0, HandNavRules.Step(2, +1, new[] { false, true, false })); // wrap over wound
    }

    [Test]
    public void Step_FromNoFocus_ActsLikeFirst()
    {
        Assert.AreEqual(1, HandNavRules.Step(-1, +1, NoWounds(3)));
    }

    [Test]
    public void Step_AllWounds_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, HandNavRules.Step(0, +1, new[] { true, true }));
    }

    [Test]
    public void ClampAfterChange_FindsNearestSurvivor()
    {
        Assert.AreEqual(2, HandNavRules.ClampAfterChange(4, NoWounds(3))); // hand shrank
        Assert.AreEqual(1, HandNavRules.ClampAfterChange(1, NoWounds(3))); // unchanged
        Assert.AreEqual(1, HandNavRules.ClampAfterChange(2, new[] { false, false, true })); // landed on wound
        Assert.AreEqual(-1, HandNavRules.ClampAfterChange(0, new[] { true }));
    }
}
