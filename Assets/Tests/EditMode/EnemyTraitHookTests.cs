using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitHookTests
{
    static EnemyTraitTuning T() => new EnemyTraitTuning();
    static EnemyCombatant E(int atk, EnemyTrait traits = EnemyTrait.None) =>
        new EnemyCombatant { Attack = atk, HP = atk, Traits = traits, Blocked = false };

    [Test]
    public void Vengeful_CostsAWoundOnAttackKill()
    {
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Vengeful) };
        Assert.AreEqual(1, EnemyTraitRules.VengefulWounds(r[0], r, T()));
    }

    [Test]
    public void NonVengeful_CostsNothing()
    {
        var r = new List<EnemyCombatant> { E(3) };
        Assert.AreEqual(0, EnemyTraitRules.VengefulWounds(r[0], r, T()));
    }

    [Test]
    public void Harrying_OneSurvivor_OnePenalty()
    {
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Harrying) };
        Assert.AreEqual(1, EnemyTraitRules.HarryPenalty(r, T()));
    }

    [Test]
    public void Harrying_DoesNotStackPerEnemy()
    {
        // Two Harrying enemies must not cost two cards - the penalty is a
        // property of the fight you fled, not a per-enemy tax.
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Harrying), E(3, EnemyTrait.Harrying) };
        Assert.AreEqual(1, EnemyTraitRules.HarryPenalty(r, T()));
    }

    [Test]
    public void NoHarrying_NoPenalty()
    {
        var r = new List<EnemyCombatant> { E(3), E(4, EnemyTrait.Brutal) };
        Assert.AreEqual(0, EnemyTraitRules.HarryPenalty(r, T()));
    }

    [Test]
    public void BlockedHarryingEnemy_StillHarries()
    {
        // Blocking is not killing - you still fled a harrying enemy.
        var e = E(3, EnemyTrait.Harrying); e.Blocked = true;
        var r = new List<EnemyCombatant> { e };
        Assert.AreEqual(1, EnemyTraitRules.HarryPenalty(r, T()));
    }
}
