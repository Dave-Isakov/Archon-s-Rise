using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitAuraTests
{
    static EnemyCombatant E(int atk, EnemyTrait traits = EnemyTrait.None) =>
        new EnemyCombatant { Attack = atk, HP = atk, Traits = traits, Blocked = false };

    [Test]
    public void NoAuras_GrantsNothing()
    {
        var roster = new List<EnemyCombatant> { E(3), E(4, EnemyTrait.Armored) };
        Assert.AreEqual(EnemyTrait.None, EnemyTraitRules.GrantedByAuras(roster));
    }

    [Test]
    public void Miasma_GrantsToxicToEveryone()
    {
        var roster = new List<EnemyCombatant> { E(1, EnemyTrait.Miasma), E(4) };
        var ogre = roster[1];
        Assert.IsTrue(EnemyTraitRules.EffectiveTraits(ogre, roster).HasFlag(EnemyTrait.Toxic));
    }

    [Test]
    public void Ironclad_GrantsArmored_Outrider_GrantsSwift()
    {
        var roster = new List<EnemyCombatant> { E(1, EnemyTrait.Ironclad), E(1, EnemyTrait.Outrider), E(4) };
        var t = EnemyTraitRules.EffectiveTraits(roster[2], roster);
        Assert.IsTrue(t.HasFlag(EnemyTrait.Armored));
        Assert.IsTrue(t.HasFlag(EnemyTrait.Swift));
    }

    [Test]
    public void GrantingIsIdempotent_TwoMiasmaSameAsOne()
    {
        var one = new List<EnemyCombatant> { E(1, EnemyTrait.Miasma), E(4) };
        var two = new List<EnemyCombatant> { E(1, EnemyTrait.Miasma), E(1, EnemyTrait.Miasma), E(4) };
        Assert.AreEqual(EnemyTraitRules.GrantedByAuras(one), EnemyTraitRules.GrantedByAuras(two));
    }

    [Test]
    public void EffectiveTraits_PreservesOwnTraits()
    {
        var roster = new List<EnemyCombatant> { E(1, EnemyTrait.Miasma), E(4, EnemyTrait.Brutal) };
        var t = EnemyTraitRules.EffectiveTraits(roster[1], roster);
        Assert.IsTrue(t.HasFlag(EnemyTrait.Brutal));
        Assert.IsTrue(t.HasFlag(EnemyTrait.Toxic));
    }

    [Test]
    public void BlockedAuraEnemy_StillGrants()
    {
        // A blocked enemy is ALIVE, so its aura persists (spec 7.4).
        var herald = E(1, EnemyTrait.Miasma); herald.Blocked = true;
        var roster = new List<EnemyCombatant> { herald, E(4) };
        Assert.IsTrue(EnemyTraitRules.EffectiveTraits(roster[1], roster).HasFlag(EnemyTrait.Toxic));
    }
}
