using NUnit.Framework;
using UnityEngine;

public class StatPaletteTests
{
    [Test]
    public void EachActionStatHasADistinctColor()
    {
        var atk = StatPalette.For(StatType.Attack);
        var def = StatPalette.For(StatType.Defend);
        var inf = StatPalette.For(StatType.Influence);
        var exp = StatPalette.For(StatType.Explore);

        Assert.AreNotEqual(atk, def);
        Assert.AreNotEqual(atk, inf);
        Assert.AreNotEqual(atk, exp);
        Assert.AreNotEqual(def, inf);
        Assert.AreNotEqual(def, exp);
        Assert.AreNotEqual(inf, exp);
        Assert.AreNotEqual(atk, StatPalette.Empower);
    }

    [Test]
    public void AttackIsReddest_DefendIsViolet_ExploreIsGreenest()
    {
        var atk = StatPalette.For(StatType.Attack);
        Assert.Greater(atk.r, atk.g);          // red dominant
        Assert.Greater(atk.r, atk.b);

        var exp = StatPalette.For(StatType.Explore);
        Assert.Greater(exp.g, exp.r);          // green dominant
        Assert.Greater(exp.g, exp.b);

        var def = StatPalette.For(StatType.Defend);
        Assert.Greater(def.b, def.g);          // blue/violet over green
    }

    [Test]
    public void NonActionFlagsFallBackToMuted()
    {
        Assert.AreEqual(StatPalette.Muted, StatPalette.For(StatType.Wound));
        Assert.AreEqual(StatPalette.Muted, StatPalette.For(StatType.None));
    }
}
