using NUnit.Framework;

public class CardSnapshotTests
{
    static CardSnapshot Rally() =>
        // Attack|Influence choice card, yellow-empowerable. base 2/2, empower 4/4.
        new CardSnapshot(StatType.Attack | StatType.Influence, EmpowerType.Yellow, true,
            attack: 2, defend: 0, influence: 2, explore: 0,
            empowerAttack: 4, empowerDefend: 0, empowerInfluence: 4, empowerExplore: 0);

    [Test]
    public void BaseOf_ReturnsSingleStatValue()
    {
        var c = Rally();
        Assert.AreEqual(2, c.BaseOf(StatType.Attack));
        Assert.AreEqual(2, c.BaseOf(StatType.Influence));
        Assert.AreEqual(0, c.BaseOf(StatType.Defend));
    }

    [Test]
    public void EmpowerOf_ReturnsSingleStatValue()
    {
        var c = Rally();
        Assert.AreEqual(4, c.EmpowerOf(StatType.Attack));
        Assert.AreEqual(0, c.EmpowerOf(StatType.Explore));
    }
}
