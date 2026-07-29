using System.Collections.Generic;
using NUnit.Framework;

public class CombatAdvanceStateTests
{
    // A trait-free, unblocked roster of one enemy with the given Attack.
    // Threat == Basis == Attack, so this reproduces the old enemyAttackTotal.
    static CounterattackPreview P(int enemyAttackTotal)
    {
        var roster = new List<EnemyCombatant>
        {
            new EnemyCombatant { Attack = enemyAttackTotal, HP = enemyAttackTotal,
                                 Traits = EnemyTrait.None, Blocked = false }
        };
        return EnemyTraitRules.BuildPreview(roster, new EnemyTraitTuning());
    }

    [Test]
    public void Siege_NothingStaged_ShowsEngage()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Siege, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 0, preview: P(5), toughness: 1);
        Assert.AreEqual(AdvanceKind.Engage, s.Kind);
    }

    [Test]
    public void Siege_StagedAndKillable_Hides()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Siege, playerSiege: 3, anySiegeKillable: true,
            defendLeft: 0, preview: P(5), toughness: 1);
        Assert.AreEqual(AdvanceKind.Hidden, s.Kind);
    }

    [Test]
    public void Siege_StagedButNothingKillable_KeepsEngage()
    {
        // Leftover siege that can't kill anything must not trap the player.
        var s = CombatPhaseRules.Advance(CombatPhase.Siege, playerSiege: 2, anySiegeKillable: false,
            defendLeft: 0, preview: P(5), toughness: 1);
        Assert.AreEqual(AdvanceKind.Engage, s.Kind);
    }

    [Test]
    public void Defend_ShortfallShowsTakeHit_WithWoundCount()
    {
        // defend 2 vs 6 attack, toughness 2 -> shortfall 4 -> 2 wounds.
        var s = CombatPhaseRules.Advance(CombatPhase.Defend, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 2, preview: P(6), toughness: 2);
        Assert.AreEqual(AdvanceKind.TakeHit, s.Kind);
        Assert.AreEqual(2, s.Wounds);
    }

    [Test]
    public void Defend_MeetsOrBeatsAttack_FlipsToCounterattack()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Defend, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 6, preview: P(6), toughness: 2);
        Assert.AreEqual(AdvanceKind.Counterattack, s.Kind);
        Assert.AreEqual(0, s.Wounds);
    }

    [Test]
    public void Attack_Hides()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Attack, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 0, preview: P(6), toughness: 1);
        Assert.AreEqual(AdvanceKind.Hidden, s.Kind);
    }

    [Test]
    public void Resolved_Hides()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Resolved, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 0, preview: P(0), toughness: 1);
        Assert.AreEqual(AdvanceKind.Hidden, s.Kind);
    }

    [Test]
    public void Defend_BlockingEveryone_FlipsToCounterattack()
    {
        var roster = new List<EnemyCombatant>
        {
            new EnemyCombatant { Attack = 6, HP = 6, Traits = EnemyTrait.None, Blocked = true }
        };
        var preview = EnemyTraitRules.BuildPreview(roster, new EnemyTraitTuning());
        var s = CombatPhaseRules.Advance(CombatPhase.Defend, playerSiege: 0, anySiegeKillable: false,
            defendLeft: 0, preview: preview, toughness: 2);
        Assert.AreEqual(AdvanceKind.Counterattack, s.Kind);
    }
}
