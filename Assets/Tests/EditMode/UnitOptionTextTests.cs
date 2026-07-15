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
        Assert.AreEqual("<sprite=\"Sword\" index=0> Attack 2",
            UnitOptionText.Describe(Opt(UnitEffect.Attack, 2, EmpowerType.None, 0)));
    }

    [Test]
    public void Describe_CrystalCost()
    {
        Assert.AreEqual("<sprite=\"Sword\" index=0> Attack 4 — <sprite=\"crystal\" index=0 color=#E5484D>1",
            UnitOptionText.Describe(Opt(UnitEffect.Attack, 4, EmpowerType.Red, 0)));
    }

    [Test]
    public void Describe_AnyColorCrystalCost()
    {
        var all = EmpowerType.Red | EmpowerType.Yellow | EmpowerType.Green | EmpowerType.Purple;
        Assert.AreEqual("<sprite=\"shield\" index=0> Defend 3 — <sprite=\"crystal\" index=0>1 (any color)",
            UnitOptionText.Describe(Opt(UnitEffect.Defend, 3, all, 0)));
    }

    [Test]
    public void Describe_InfluenceCost()
    {
        Assert.AreEqual("<sprite=\"Sword\" index=0> Attack 5 — <sprite=\"gem\" index=0>3",
            UnitOptionText.Describe(Opt(UnitEffect.Attack, 5, EmpowerType.None, 3)));
    }

    [Test]
    public void Describe_Crystallize()
    {
        var o = Opt(UnitEffect.Crystallize, 2, EmpowerType.None, 0);
        o.grantColor = EmpowerType.Green;
        Assert.AreEqual("<sprite=\"crystal\" index=0 color=#46A758> Crystallize 2",
            UnitOptionText.Describe(o));
    }
}
