using NUnit.Framework;

public class UndoGateTests
{
    [Test]
    public void EmptyStack_IsBlocked()
    {
        Assert.IsFalse(UndoGate.Undo(stackCount: 0, inCombat: false, fightCommitted: false));
    }

    [Test]
    public void OutOfCombat_WithAStack_IsAllowed()
    {
        Assert.IsTrue(UndoGate.Undo(stackCount: 1, inCombat: false, fightCommitted: false));
    }

    [Test]
    public void UncommittedFight_IsBlocked()
    {
        // The free look: a cost can be validated at open and not yet paid, so
        // undoing the card that funded it would let the commit go negative.
        Assert.IsFalse(UndoGate.Undo(stackCount: 1, inCombat: true, fightCommitted: false));
    }

    [Test]
    public void CommittedFight_IsAllowed()
    {
        // The Defend window: Engage already committed AND cleared the stack, so
        // anything on it now is a Defend card or a block staged since — both
        // undoable by design until the counterattack lands.
        Assert.IsTrue(UndoGate.Undo(stackCount: 1, inCombat: true, fightCommitted: true));
    }

    [Test]
    public void CommittedFight_WithNothingStaged_IsStillBlocked()
    {
        Assert.IsFalse(UndoGate.Undo(stackCount: 0, inCombat: true, fightCommitted: true));
    }
}
