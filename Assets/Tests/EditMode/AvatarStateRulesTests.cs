using NUnit.Framework;

public class AvatarStateRulesTests
{
    [Test]
    public void Priority_RanksHurtAboveFightAboveWalkAboveIdle()
    {
        Assert.Greater(AvatarStateRules.Priority(AvatarState.Hurt),
                       AvatarStateRules.Priority(AvatarState.Fight));
        Assert.Greater(AvatarStateRules.Priority(AvatarState.Fight),
                       AvatarStateRules.Priority(AvatarState.Walk));
        Assert.Greater(AvatarStateRules.Priority(AvatarState.Walk),
                       AvatarStateRules.Priority(AvatarState.Idle));
    }

    [Test]
    public void Hurt_InterruptsFight()
    {
        Assert.IsTrue(AvatarStateRules.ShouldPlay(AvatarState.Fight, AvatarState.Hurt));
    }

    [Test]
    public void Fight_DoesNotInterruptHurt()
    {
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Hurt, AvatarState.Fight));
    }

    [Test]
    public void Walk_DoesNotInterruptFightOrHurt()
    {
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Fight, AvatarState.Walk));
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Hurt, AvatarState.Walk));
    }

    [Test]
    public void Fight_InterruptsWalkAndIdle()
    {
        Assert.IsTrue(AvatarStateRules.ShouldPlay(AvatarState.Walk, AvatarState.Fight));
        Assert.IsTrue(AvatarStateRules.ShouldPlay(AvatarState.Idle, AvatarState.Fight));
    }

    [Test]
    public void AState_NeverRetriggersItself()
    {
        // Mirrors "Can Transition To Self off" on the Any State transitions.
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Fight, AvatarState.Fight));
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Hurt, AvatarState.Hurt));
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Walk, AvatarState.Walk));
    }

    [Test]
    public void OneShots_AreFightAndHurtOnly()
    {
        Assert.IsTrue(AvatarStateRules.IsOneShot(AvatarState.Fight));
        Assert.IsTrue(AvatarStateRules.IsOneShot(AvatarState.Hurt));
        Assert.IsFalse(AvatarStateRules.IsOneShot(AvatarState.Walk));
        Assert.IsFalse(AvatarStateRules.IsOneShot(AvatarState.Idle));
    }

    [Test]
    public void ResumeAfter_ReturnsToWalkWhileMovingElseIdle()
    {
        Assert.AreEqual(AvatarState.Walk, AvatarStateRules.ResumeAfter(true));
        Assert.AreEqual(AvatarState.Idle, AvatarStateRules.ResumeAfter(false));
    }
}
