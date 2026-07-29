using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitShareTests
{
    static EnemyTraitTuning T() => new EnemyTraitTuning();
    static EnemyCombatant E(int atk, EnemyTrait traits = EnemyTrait.None) =>
        new EnemyCombatant { Attack = atk, HP = atk, Traits = traits, Blocked = false };

    static int Hand(List<EnemyCombatant> r, int d, int t) =>
        EnemyTraitRules.HandWounds(EnemyTraitRules.BuildPreview(r, T()), d, t);
    static int Discard(List<EnemyCombatant> r, int d, int t) =>
        EnemyTraitRules.DiscardWounds(EnemyTraitRules.BuildPreview(r, T()), d, t, T());

    [Test]
    public void Row6_SoloToxic_ExactDoubling()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Toxic) };
        Assert.AreEqual(1, Hand(r, 0, 2));
        Assert.AreEqual(1, Discard(r, 0, 2));
    }

    [Test]
    public void Row7_ToxicSpiderAndOgre_OgresClubIsNotPoisoned()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Toxic), E(4) };
        Assert.AreEqual(3, Hand(r, 0, 2));
        Assert.AreEqual(1, Discard(r, 0, 2)); // share 6*2/6 = 2 -> bite(2,2) = 1
    }

    [Test]
    public void Row8_MiasmaMakesEveryoneToxic_FullDoubling()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Toxic), E(4), E(1, EnemyTrait.Miasma) };
        Assert.AreEqual(4, Hand(r, 0, 2));
        Assert.AreEqual(4, Discard(r, 0, 2));
    }

    [Test]
    public void NoToxic_NoDiscardWounds()
    {
        var r = new List<EnemyCombatant> { E(4) };
        Assert.AreEqual(0, Discard(r, 0, 2));
    }

    [Test]
    public void FullyBlocked_NoDiscardWounds()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Toxic) };
        Assert.AreEqual(0, Discard(r, 99, 2));
    }

    [Test]
    public void EmptyRoster_GuardsDivideByZero()
    {
        var r = new List<EnemyCombatant>();
        Assert.AreEqual(0, Discard(r, 0, 2));
        Assert.AreEqual(0, Hand(r, 0, 2));
    }

    [Test]
    public void Leech_StealsOnItsShare()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Leech) };
        var p = EnemyTraitRules.BuildPreview(r, T());
        Assert.AreEqual(1, EnemyTraitRules.CrystalsStolen(p, 0, 2, T()));
    }

    [Test]
    public void Leech_StealsNothingWhenBlocked()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Leech) };
        var p = EnemyTraitRules.BuildPreview(r, T());
        Assert.AreEqual(0, EnemyTraitRules.CrystalsStolen(p, 99, 2, T()));
    }
}
