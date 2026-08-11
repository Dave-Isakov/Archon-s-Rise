using NUnit.Framework;

public class AggroRulesTests
{
    // Convenience: a player step, which is the only trigger that can force a fight.
    static AggroOutcome Step(bool fogHidden, bool adjacent, bool wasArmed, bool playerCellChanged)
        => AggroRules.Resolve(fogHidden, adjacent, wasArmed, playerCellChanged, AggroTrigger.PlayerMoved);

    // Convenience: reach changed under a standing player (fog lifted, or spawn).
    static AggroOutcome Reveal(bool fogHidden, bool adjacent, bool wasArmed, bool playerCellChanged)
        => AggroRules.Resolve(fogHidden, adjacent, wasArmed, playerCellChanged, AggroTrigger.ReachChanged);

    [Test]
    public void StepIntoReach_Arms_WithoutForcing()
    {
        var o = Step(fogHidden: false, adjacent: true, wasArmed: false, playerCellChanged: true);
        Assert.IsTrue(o.Armed);
        Assert.IsFalse(o.Forced);
    }

    [Test]
    public void SecondStepInsideReach_Forces()
    {
        var o = Step(fogHidden: false, adjacent: true, wasArmed: true, playerCellChanged: true);
        Assert.IsTrue(o.Armed);
        Assert.IsTrue(o.Forced);
    }

    // A repeat raise at the same cell is a no-op, not a cornering.
    [Test]
    public void ArmedAtTheSameCell_DoesNotForce()
    {
        var o = Step(fogHidden: false, adjacent: true, wasArmed: true, playerCellChanged: false);
        Assert.IsTrue(o.Armed);
        Assert.IsFalse(o.Forced);
    }

    [Test]
    public void StepOutOfReach_Disarms()
    {
        var o = Step(fogHidden: false, adjacent: false, wasArmed: true, playerCellChanged: true);
        Assert.IsFalse(o.Armed);
        Assert.IsFalse(o.Forced);
    }

    // Fog is absolute: what the player cannot see neither arms nor forces.
    [Test]
    public void FogHidden_NeverArms()
    {
        var o = Step(fogHidden: true, adjacent: true, wasArmed: true, playerCellChanged: true);
        Assert.IsFalse(o.Armed);
        Assert.IsFalse(o.Forced);
    }

    // Bug 1 (2026-08-11): the player scouts the fog off an enemy already standing
    // next to them. Nothing moved, so nothing used to arm it and it sat inert —
    // no halo, and a click that did nothing.
    [Test]
    public void FogLiftsOffAdjacentEnemy_Arms()
    {
        var o = Reveal(fogHidden: false, adjacent: true, wasArmed: false, playerCellChanged: false);
        Assert.IsTrue(o.Armed);
        Assert.IsFalse(o.Forced);
    }

    // Bug 2 (2026-08-11): the day's spawn lands next to a standing player. Same
    // inert token, same cause — the arming check is driven by movement alone.
    [Test]
    public void SpawnNextToStandingPlayer_Arms()
    {
        var o = Reveal(fogHidden: false, adjacent: true, wasArmed: false, playerCellChanged: true);
        Assert.IsTrue(o.Armed);
        Assert.IsFalse(o.Forced);
    }

    // The player never chose the risk, so a reveal or a spawn may hand them a
    // fight to take — never a fight they cannot refuse.
    [Test]
    public void ReachChanged_NeverForces_EvenWhenAlreadyArmed()
    {
        var o = Reveal(fogHidden: false, adjacent: true, wasArmed: true, playerCellChanged: true);
        Assert.IsTrue(o.Armed);
        Assert.IsFalse(o.Forced);
    }

    [Test]
    public void RevealedButDistantEnemy_StaysInert()
    {
        var o = Reveal(fogHidden: false, adjacent: false, wasArmed: false, playerCellChanged: false);
        Assert.IsFalse(o.Armed);
        Assert.IsFalse(o.Forced);
    }

    [Test]
    public void SpawnUnderFog_StaysHidden()
    {
        var o = Reveal(fogHidden: true, adjacent: true, wasArmed: false, playerCellChanged: false);
        Assert.IsFalse(o.Armed);
        Assert.IsFalse(o.Forced);
    }
}
