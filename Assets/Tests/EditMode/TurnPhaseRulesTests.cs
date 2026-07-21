using NUnit.Framework;

public class TurnPhaseRulesTests
{
    [Test]
    public void Move_Allowed_Only_In_Explore()
    {
        Assert.IsTrue(TurnPhaseRules.CanMove(TurnPhase.Explore));
        Assert.IsFalse(TurnPhaseRules.CanMove(TurnPhase.Action));
        Assert.IsFalse(TurnPhaseRules.CanMove(TurnPhase.End));
    }

    [Test]
    public void Interact_Allowed_Until_Action_Is_Spent()
    {
        // From Explore (implicit transition) or Action, only while not yet spent.
        Assert.IsTrue(TurnPhaseRules.CanInteract(TurnPhase.Explore, false));
        Assert.IsTrue(TurnPhaseRules.CanInteract(TurnPhase.Action, false));
        // Spent: no second interaction.
        Assert.IsFalse(TurnPhaseRules.CanInteract(TurnPhase.Explore, true));
        Assert.IsFalse(TurnPhaseRules.CanInteract(TurnPhase.Action, true));
        // Never in End.
        Assert.IsFalse(TurnPhaseRules.CanInteract(TurnPhase.End, false));
    }

    [Test]
    public void Move_Commits_Only_When_It_Reveals_New_Fog()
    {
        Assert.IsTrue(TurnPhaseRules.ShouldCommitOnMove(true));
        Assert.IsFalse(TurnPhaseRules.ShouldCommitOnMove(false));
    }
}
