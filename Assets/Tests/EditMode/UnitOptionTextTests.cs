using NUnit.Framework;

public class UnitOptionTextTests
{
    static UnitOption Opt(UnitEffect effect, int amount, EmpowerType crystal, int influence)
    {
        var o = new UnitOption();
        o.effect = effect;
        o.amount = amount;
        o.crystalCost = crystal;
        o.influenceCost = influence;
        return o;
    }

    [Test]
    public void Describe_FreeOption()
    {
        Assert.AreEqual("Attack 2", UnitOptionText.Describe(Opt(UnitEffect.Attack, 2, EmpowerType.None, 0)));
    }

    [Test]
    public void Describe_CrystalCostUnchanged()
    {
        Assert.AreEqual("Attack 4 — 1 Red crystal",
            UnitOptionText.Describe(Opt(UnitEffect.Attack, 4, EmpowerType.Red, 0)));
    }

    [Test]
    public void Describe_InfluenceCost()
    {
        Assert.AreEqual("Attack 5 — 3 Influence",
            UnitOptionText.Describe(Opt(UnitEffect.Attack, 5, EmpowerType.None, 3)));
    }
}
