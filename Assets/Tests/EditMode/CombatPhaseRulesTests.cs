using NUnit.Framework;

public class CombatPhaseRulesTests
{
    [Test]
    public void Siege_And_Influence_Only_In_Siege_Phase()
    {
        Assert.IsTrue(CombatPhaseRules.CanSiege(CombatPhase.Siege));
        Assert.IsTrue(CombatPhaseRules.CanInfluence(CombatPhase.Siege));
        Assert.IsFalse(CombatPhaseRules.CanSiege(CombatPhase.Defend));
        Assert.IsFalse(CombatPhaseRules.CanInfluence(CombatPhase.Defend));
        Assert.IsFalse(CombatPhaseRules.CanSiege(CombatPhase.Attack));
        Assert.IsFalse(CombatPhaseRules.CanInfluence(CombatPhase.Attack));
        Assert.IsFalse(CombatPhaseRules.CanSiege(CombatPhase.Resolved));
    }

    [Test]
    public void NormalAttack_Only_In_Attack_Phase()
    {
        Assert.IsFalse(CombatPhaseRules.CanNormalAttack(CombatPhase.Siege));
        Assert.IsFalse(CombatPhaseRules.CanNormalAttack(CombatPhase.Defend));
        Assert.IsTrue(CombatPhaseRules.CanNormalAttack(CombatPhase.Attack));
        Assert.IsFalse(CombatPhaseRules.CanNormalAttack(CombatPhase.Resolved));
    }

    [Test]
    public void Button_Label_Tracks_Phase()
    {
        Assert.AreEqual("Engage", CombatPhaseRules.ButtonLabel(CombatPhase.Siege));
        Assert.AreEqual("Defend", CombatPhaseRules.ButtonLabel(CombatPhase.Defend));
        Assert.AreEqual("Withdraw", CombatPhaseRules.ButtonLabel(CombatPhase.Attack));
        Assert.AreEqual("", CombatPhaseRules.ButtonLabel(CombatPhase.Resolved));
    }
}
