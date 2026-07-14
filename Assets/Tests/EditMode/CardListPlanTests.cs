using NUnit.Framework;

public class CardListPlanTests
{
    [Test]
    public void SortsByTypeThenName()
    {
        var types = new[] { StatType.Heal, StatType.Attack, StatType.Attack };
        var names = new[] { "Mend", "Strike", "Charge" };

        var order = CardListPlan.Order(types, names);

        // Attack (1) before Heal (16); within Attack, Charge before Strike.
        Assert.AreEqual(new[] { 2, 1, 0 }, order);
    }

    [Test]
    public void DuplicateCards_KeepInputOrder()
    {
        var types = new[] { StatType.Attack, StatType.Attack, StatType.Attack };
        var names = new[] { "Strike", "Strike", "Strike" };

        var order = CardListPlan.Order(types, names);

        Assert.AreEqual(new[] { 0, 1, 2 }, order);
    }

    [Test]
    public void EmptyInput_ReturnsEmpty()
    {
        var order = CardListPlan.Order(new StatType[0], new string[0]);

        Assert.AreEqual(0, order.Length);
    }

    [Test]
    public void CombinedFlags_SortByNumericValue()
    {
        // Siege cards co-flag Attack (Siege|Attack = 129), sorting after plain types.
        var types = new[] { StatType.Siege | StatType.Attack, StatType.Wound, StatType.Attack };
        var names = new[] { "Catapult", "Wound", "Strike" };

        var order = CardListPlan.Order(types, names);

        Assert.AreEqual(new[] { 2, 1, 0 }, order);
    }

    [Test]
    public void WoundsGroupTogether_AfterActionCards()
    {
        var types = new[] { StatType.Wound, StatType.Explore, StatType.Wound };
        var names = new[] { "Wound", "Scout", "Wound" };

        var order = CardListPlan.Order(types, names);

        Assert.AreEqual(new[] { 1, 0, 2 }, order);
    }
}
