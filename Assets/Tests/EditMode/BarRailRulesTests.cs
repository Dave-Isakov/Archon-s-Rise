using System.Collections.Generic;
using NUnit.Framework;

public class BarRailRulesTests
{
    // false == selectable. Units are the LEFT lane, cards the RIGHT lane.
    static List<bool> Open(int count) => new List<bool>(new bool[count]);

    static void AssertAt(RailPos pos, BarLane lane, int index)
    {
        Assert.IsFalse(pos.IsNone, "expected a position, got none");
        Assert.AreEqual(lane, pos.Lane);
        Assert.AreEqual(index, pos.Index);
    }

    [Test]
    public void StepLeft_FromLeftmostCard_LandsOnRightmostUnit()
    {
        var pos = BarRailRules.Step(new RailPos(BarLane.Cards, 0), -1, Open(3), Open(4));
        AssertAt(pos, BarLane.Units, 2);
    }

    [Test]
    public void StepRight_FromRightmostUnit_LandsOnLeftmostCard()
    {
        var pos = BarRailRules.Step(new RailPos(BarLane.Units, 2), +1, Open(3), Open(4));
        AssertAt(pos, BarLane.Cards, 0);
    }

    [Test]
    public void StepLeft_FromLeftmostUnit_WrapsToRightmostCard()
    {
        var pos = BarRailRules.Step(new RailPos(BarLane.Units, 0), -1, Open(3), Open(4));
        AssertAt(pos, BarLane.Cards, 3);
    }

    [Test]
    public void StepRight_FromRightmostCard_WrapsToLeftmostUnit()
    {
        var pos = BarRailRules.Step(new RailPos(BarLane.Cards, 3), +1, Open(3), Open(4));
        AssertAt(pos, BarLane.Units, 0);
    }

    [Test]
    public void Step_SkipsBlockedEntries()
    {
        // The rightmost unit is exhausted, so crossing left lands on the one before it.
        var units = new List<bool> { false, false, true };
        var pos = BarRailRules.Step(new RailPos(BarLane.Cards, 0), -1, units, Open(4));
        AssertAt(pos, BarLane.Units, 1);
    }

    [Test]
    public void Step_WithEmptyUnitLane_StaysInCards()
    {
        var pos = BarRailRules.Step(new RailPos(BarLane.Cards, 0), -1, Open(0), Open(3));
        AssertAt(pos, BarLane.Cards, 2);
    }

    [Test]
    public void Step_WithEveryUnitBlocked_SkipsTheWholeLane()
    {
        var units = new List<bool> { true, true };
        var pos = BarRailRules.Step(new RailPos(BarLane.Cards, 0), -1, units, Open(3));
        AssertAt(pos, BarLane.Cards, 2);
    }

    [Test]
    public void Step_WithNothingSelectable_ReturnsNone()
    {
        var units = new List<bool> { true };
        var cards = new List<bool> { true, true };
        Assert.IsTrue(BarRailRules.Step(new RailPos(BarLane.Cards, 0), -1, units, cards).IsNone);
    }

    [Test]
    public void Step_FromNone_LandsNearTheMiddleOfTheBar()
    {
        // Combined length 6; the first press lands at flat index 3 == cards[1].
        var pos = BarRailRules.Step(RailPos.None, +1, Open(2), Open(4));
        AssertAt(pos, BarLane.Cards, 1);
    }

    [Test]
    public void ClampAfterChange_CanLandAcrossTheBoundary()
    {
        // Both cards became wounds; the nearest survivor is in the unit lane.
        var units = new List<bool> { false, false };
        var cards = new List<bool> { true, true };
        var pos = BarRailRules.ClampAfterChange(new RailPos(BarLane.Cards, 0), units, cards);
        AssertAt(pos, BarLane.Units, 1);
    }
}
