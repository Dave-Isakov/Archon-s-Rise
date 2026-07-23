using UnityEngine;

// Undoable board move (spec 2026-07-21, re-homed 2026-07-23). Execute repositions the
// player and spends explore; Undo restores both. Only the no-new-fog branch of
// ExplorationController.Move builds one of these — a fog-revealing scout commits the
// stack instead (irreversible knowledge), so a MoveCommand never re-hides fog.
public class MoveCommand : ICommands
{
    readonly ExplorationController controller;
    readonly Vector3 from;
    readonly Vector3 to;
    readonly int exploreCost;

    public MoveCommand(ExplorationController controller, Vector3 from, Vector3 to, int exploreCost)
    {
        this.controller = controller;
        this.from = from;
        this.to = to;
        this.exploreCost = exploreCost;
    }

    public void Execute() => controller.ApplyMove(to, exploreCost);
    public void Undo()    => controller.ApplyMove(from, exploreCost, refund: true);
}
