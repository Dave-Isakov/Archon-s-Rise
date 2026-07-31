// Enabled state for the Undo button. Pure, no scene dependency — the sibling of
// TurnButtonGate.
public static class UndoGate
{
    // Undo is blocked while a fight is open but UNCOMMITTED — the free look.
    // In that window a cost can be validated at open and paid later: play a card
    // granting Explore, open a delve whose gate reads that Explore, undo the
    // card, then commit, and the cost is deducted with no re-validation and goes
    // negative. Gating here is what replaced the eager fight-open ClearStack().
    //
    // Once the fight COMMITS, undo comes back (2026-07-31). Every commit point —
    // Engage, the counterattack resolve, a kill — clears the stack as it fires,
    // so whatever sits on the stack afterwards was staged since the last spend
    // and reverses cleanly. That is exactly the Defend window: a Defend card, or
    // a block, both of which BlockCommand's contract says stay undoable until
    // the counterattack lands. Blocking the whole fight broke that contract.
    public static bool Undo(int stackCount, bool inCombat, bool fightCommitted)
        => stackCount > 0 && (!inCombat || fightCommitted);
}
