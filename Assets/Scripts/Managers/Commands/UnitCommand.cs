using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitCommand : ICommands
{
    Unit _unit;
    UnitEvent _unitAction;

    public UnitCommand(UnitEvent unitEvent, Unit unit)
    {
        _unitAction = unitEvent;
        _unit = unit;
    }
    public void Execute()
    {
        _unitAction.Raise(_unit);
    }

    public void Undo()
    {
        _unitAction.Raise(_unit);
    }
}
