using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitShelterTests
{
    static EnemyTraitTuning Tuning() => new EnemyTraitTuning();

    static List<EnemyCombatant> Roster(params EnemyCombatant[] e) => new List<EnemyCombatant>(e);

    static EnemyCombatant Enemy(int attack, int hp, EnemyTrait traits = EnemyTrait.None)
        => new EnemyCombatant { Attack = attack, HP = hp, Traits = traits, Blocked = false };

    [Test]
    public void ZeroCommitted_MatchesShippedPipeline()
    {
        var roster = Roster(Enemy(4, 3, EnemyTrait.Toxic), Enemy(2, 2, EnemyTrait.Leech));
        var t = Tuning();
        var p = EnemyTraitRules.BuildPreview(roster, t);

        var o = EnemyTraitRules.Resolve(p, defendLeft: 1, soak: 0, toughness: 2, t: t, committedUnits: 0);

        Assert.AreEqual(EnemyTraitRules.HandWounds(p, 1, 2), o.HandWounds);
        Assert.AreEqual(EnemyTraitRules.DiscardWounds(p, 1, 2, t), o.DiscardWounds);
        Assert.AreEqual(EnemyTraitRules.CrystalsStolen(p, 1, 2, t), o.CrystalsStolen);
    }

    [Test]
    public void Soak_ReducesHandWounds()
    {
        var roster = Roster(Enemy(5, 4));
        var t = Tuning();
        var p = EnemyTraitRules.BuildPreview(roster, t);

        var bare = EnemyTraitRules.Resolve(p, 0, soak: 0, toughness: 2, t: t, committedUnits: 0);
        var held = EnemyTraitRules.Resolve(p, 0, soak: 2, toughness: 2, t: t, committedUnits: 1);

        Assert.AreEqual(3, bare.HandWounds);
        Assert.AreEqual(2, held.HandWounds);
        Assert.AreEqual(1, held.WoundsPerCommittedUnit);
    }

    [Test]
    public void ToxicTransfers_AndLeechIgnoresSoak()
    {
        var roster = Roster(Enemy(4, 3, EnemyTrait.Toxic), Enemy(2, 2, EnemyTrait.Leech));
        var t = Tuning();
        var p = EnemyTraitRules.BuildPreview(roster, t);

        var bare = EnemyTraitRules.Resolve(p, 1, soak: 0, toughness: 2, t: t, committedUnits: 0);
        var held = EnemyTraitRules.Resolve(p, 1, soak: 3, toughness: 2, t: t, committedUnits: 1);

        Assert.AreEqual(3, bare.HandWounds);
        Assert.AreEqual(2, bare.DiscardWounds);
        Assert.AreEqual(1, bare.CrystalsStolen);

        Assert.AreEqual(1, held.HandWounds);
        Assert.AreEqual(0, held.DiscardWounds, "toxic transfers to the units");
        Assert.AreEqual(1, held.CrystalsStolen, "leech reads the PRE-soak number");
        Assert.AreEqual(2, held.WoundsPerCommittedUnit, "toxic makes it 2 per unit");
    }

    [Test]
    public void ToxicDoesNotTransfer_WhenNobodyIsCommitted()
    {
        var roster = Roster(Enemy(4, 3, EnemyTrait.Toxic));
        var t = Tuning();
        var p = EnemyTraitRules.BuildPreview(roster, t);

        var o = EnemyTraitRules.Resolve(p, 0, soak: 0, toughness: 2, t: t, committedUnits: 0);

        Assert.Greater(o.DiscardWounds, 0);
    }

    [Test]
    public void SoakingToZero_KillsBrutalSurcharge()
    {
        var roster = Roster(Enemy(3, 3, EnemyTrait.Brutal));
        var t = Tuning();
        var p = EnemyTraitRules.BuildPreview(roster, t);

        var bare = EnemyTraitRules.Resolve(p, 2, soak: 0, toughness: 2, t: t, committedUnits: 0);
        var held = EnemyTraitRules.Resolve(p, 2, soak: 3, toughness: 2, t: t, committedUnits: 1);

        Assert.AreEqual(2, bare.HandWounds);
        Assert.AreEqual(0, held.HandWounds);
    }

    [Test]
    public void Toxic_AddsDiscardCopies_RatherThanDivertingFromHand()
    {
        var toxic = Roster(Enemy(4, 3, EnemyTrait.Toxic));
        var plain = Roster(Enemy(4, 3));
        var t = Tuning();

        var pToxic = EnemyTraitRules.BuildPreview(toxic, t);
        var pPlain = EnemyTraitRules.BuildPreview(plain, t);

        Assert.AreEqual(EnemyTraitRules.HandWounds(pPlain, 0, 2),
                        EnemyTraitRules.HandWounds(pToxic, 0, 2),
                        "hand wounds are unchanged by Toxic — the copies are extra");
        Assert.Greater(EnemyTraitRules.DiscardWounds(pToxic, 0, 2, t), 0);
    }
}
