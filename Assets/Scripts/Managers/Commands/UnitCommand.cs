using UnityEngine;

// One unit-option use on the undo stack. Owns the reserved crystal for costed
// options: Execute consumes it (fly + hide), Undo refunds it. Effects go
// through Player.ApplyUnitOption / RevertUnitOption symmetrically.
public class UnitCommand : ICommands
{
    readonly Player _player;
    readonly CrystalInventory _crystals;
    readonly Unit _unit;
    readonly UnitOption _option;
    readonly Crystal _reserved; // null when the option is free

    public UnitCommand(Player player, CrystalInventory crystals, Unit unit, UnitOption option, Crystal reserved)
    {
        _player = player;
        _crystals = crystals;
        _unit = unit;
        _option = option;
        _reserved = reserved;
    }

    public void Execute()
    {
        if (_reserved != null) _crystals.SpendUnitCrystal(_reserved, _unit.transform.position);
        _player.ApplyUnitOption(_unit, _option);
    }

    public void Undo()
    {
        _player.RevertUnitOption(_unit, _option);
        if (_reserved != null) _crystals.RefundUnitCrystal();
    }
}
