using NUnit.Framework;

public class CombatAdvanceStateTests
{
    [Test]
    public void Siege_NothingStaged_ShowsEngage()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Siege, playerSiege: 0, anySiegeKillable: false,
            playerDefend: 0, enemyAttackTotal: 5, toughness: 1);
        Assert.AreEqual(AdvanceKind.Engage, s.Kind);
    }

    [Test]
    public void Siege_StagedAndKillable_Hides()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Siege, playerSiege: 3, anySiegeKillable: true,
            playerDefend: 0, enemyAttackTotal: 5, toughness: 1);
        Assert.AreEqual(AdvanceKind.Hidden, s.Kind);
    }

    [Test]
    public void Siege_StagedButNothingKillable_KeepsEngage()
    {
        // Leftover siege that can't kill anything must not trap the player.
        var s = CombatPhaseRules.Advance(CombatPhase.Siege, playerSiege: 2, anySiegeKillable: false,
            playerDefend: 0, enemyAttackTotal: 5, toughness: 1);
        Assert.AreEqual(AdvanceKind.Engage, s.Kind);
    }

    [Test]
    public void Defend_ShortfallShowsTakeHit_WithWoundCount()
    {
        // defend 2 vs 6 attack, toughness 2 -> shortfall 4 -> 2 wounds.
        var s = CombatPhaseRules.Advance(CombatPhase.Defend, playerSiege: 0, anySiegeKillable: false,
            playerDefend: 2, enemyAttackTotal: 6, toughness: 2);
        Assert.AreEqual(AdvanceKind.TakeHit, s.Kind);
        Assert.AreEqual(2, s.Wounds);
    }

    [Test]
    public void Defend_MeetsOrBeatsAttack_FlipsToCounterattack()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Defend, playerSiege: 0, anySiegeKillable: false,
            playerDefend: 6, enemyAttackTotal: 6, toughness: 2);
        Assert.AreEqual(AdvanceKind.Counterattack, s.Kind);
        Assert.AreEqual(0, s.Wounds);
    }

    [Test]
    public void Attack_Hides()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Attack, playerSiege: 0, anySiegeKillable: false,
            playerDefend: 0, enemyAttackTotal: 6, toughness: 1);
        Assert.AreEqual(AdvanceKind.Hidden, s.Kind);
    }

    [Test]
    public void Resolved_Hides()
    {
        var s = CombatPhaseRules.Advance(CombatPhase.Resolved, playerSiege: 0, anySiegeKillable: false,
            playerDefend: 0, enemyAttackTotal: 0, toughness: 1);
        Assert.AreEqual(AdvanceKind.Hidden, s.Kind);
    }
}
