using UnityEngine;

// One undoable unit that plays a teleport card AND repositions the player (spec
// 2026-07-23). Built only when the player picks a hex in teleport targeting; cancelling
// never creates one (the card was never played). Teleport targets are visible-only, so
// nothing irreversible is revealed — this stays on the undo stack until a normal commit
// point, where Commit() discards the card exactly like a PlayCommand.
public class TeleportCommand : ICommands
{
    readonly PlayCommand play;
    readonly ExplorationController controller;
    readonly Vector3 from;
    readonly Vector3 to;

    public TeleportCommand(PlayCommand play, ExplorationController controller, Vector3 from, Vector3 to)
    {
        this.play = play;
        this.controller = controller;
        this.from = from;
        this.to = to;
    }

    public void Execute()
    {
        play.Execute();               // apply the card play (marks played, applies any stats)
        controller.ApplyTeleport(to); // reposition + raise position event (arms aggro)
    }

    public void Undo()
    {
        controller.ApplyTeleport(from);
        play.Undo();                  // un-play the card (returns it to hand)
    }

    // At an irreversible commit point the card can no longer be undone → discard it,
    // mirroring PlayCommand.Commit.
    public void Commit() => play.Commit();
}
