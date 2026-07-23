using UnityEngine;
using UnityEngine.InputSystem;

// Teleport targeting for HexInteractor (spec 2026-07-23). BeginTeleport holds the
// pending card play; picking a visible hex commits one undoable TeleportCommand;
// cancelling (right-click / Esc) discards the pending play so the card returns to hand.
public partial class HexInteractor
{
    PlayCommand pendingTeleportPlay;
    Card pendingTeleportCard;

    // Called by CardInspector.Play when a grantsTeleport card is played. The play is
    // NOT yet on the stack — it commits only when a hex is picked.
    public void BeginTeleport(PlayCommand pendingPlay, Card card)
    {
        pendingTeleportPlay = pendingPlay;
        pendingTeleportCard = card;
        teleportMode = true;
    }

    void CompleteTeleport(Vector3Int cell)
    {
        if (pendingTeleportPlay == null) { teleportMode = false; return; }
        // The player always stands at CellToWorld(PlayerCell) (moves/teleports snap
        // there), so this round-trips exactly on undo. Use CellToWorld — the same
        // convention ExplorationController.Move uses for the destination.
        var from = gameboard.CellToWorld(exploration.PlayerCell);
        var to = gameboard.CellToWorld(cell);
        GameManager.Instance.commands.AddCommand(
            new TeleportCommand(pendingTeleportPlay, exploration, from, to));
        EndTeleport();
    }

    void CancelTeleport()
    {
        // Nothing was ever added to the stack; the card was never played, so it simply
        // stays in hand. Drop the pending play and leave targeting.
        EndTeleport();
    }

    void EndTeleport()
    {
        pendingTeleportPlay = null;
        pendingTeleportCard = null;
        teleportMode = false;
        armedFogCell = null;
    }

    static bool CancelPressed()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        return (kb != null && kb.escapeKey.wasPressedThisFrame)
            || (mouse != null && mouse.rightButton.wasPressedThisFrame);
    }
}
