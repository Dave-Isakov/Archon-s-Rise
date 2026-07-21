using UnityEngine;

// Undoable board move (spec 2026-07-21). Execute repositions the player and
// spends explore; Undo restores both. Only the no-new-fog branch of
// DirectionButton.Explore builds one of these — a fog-revealing step commits the
// stack instead (irreversible knowledge), so a MoveCommand never re-hides fog.
public class MoveCommand : ICommands
{
    readonly DirectionButton button;
    readonly Vector3 from;
    readonly Vector3 to;
    readonly int exploreCost;

    public MoveCommand(DirectionButton button, Vector3 from, Vector3 to, int exploreCost)
    {
        this.button = button;
        this.from = from;
        this.to = to;
        this.exploreCost = exploreCost;
    }

    public void Execute() => button.ApplyMove(to, exploreCost);
    public void Undo()    => button.ApplyMove(from, exploreCost, refund: true);
}
