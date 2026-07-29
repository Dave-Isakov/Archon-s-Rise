// Blocking one enemy in the Defend phase (spec §7.6). Undoable like a card play
// or a unit use: undo refunds the Defend and returns the enemy to the unblocked
// set, so the advance button's wound preview reverts with it.
//
// Blocks stop being undoable when the advance button commits the counterattack
// and clears the stack — the same commit rule Engage already uses.
public class BlockCommand : ICommands
{
    readonly EnemyCard _card;
    readonly Player _player;
    readonly int _cost;

    public BlockCommand(EnemyCard card, Player player, int cost)
    {
        _card = card;
        _player = player;
        _cost = cost;
    }

    public void Execute()
    {
        _player.PlayerDefend -= _cost;
        _card.Blocked = true;
    }

    public void Undo()
    {
        _player.PlayerDefend += _cost;
        _card.Blocked = false;
    }
}
