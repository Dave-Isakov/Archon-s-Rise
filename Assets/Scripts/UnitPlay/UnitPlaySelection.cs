using System.Collections.Generic;

// Pure state of one open unit pop-out: which row is selected and whether it
// can be used. Affordability is computed once at Open (the pop-out is modal,
// so crystal counts cannot change underneath it). Locked (unaffordable) rows
// are selectable — the player can focus them to read the cost — but CanUse
// stays false on them.
public class UnitPlaySelection
{
    readonly IReadOnlyList<UnitOption> _options;
    readonly IReadOnlyList<bool> _affordable;

    public int SelectedIndex { get; private set; }

    public UnitPlaySelection(IReadOnlyList<UnitOption> options, IReadOnlyList<bool> affordable)
    {
        _options = options;
        _affordable = affordable;
        SelectedIndex = options.Count == 0 ? -1 : 0;
        for (int i = 0; i < options.Count; i++)
            if (affordable[i]) { SelectedIndex = i; break; }
    }

    public int Count => _options.Count;
    public UnitOption Selected => SelectedIndex >= 0 ? _options[SelectedIndex] : null;
    public bool CanUse => SelectedIndex >= 0 && _affordable[SelectedIndex];

    public void Select(int index)
    {
        if (index < 0 || index >= _options.Count) return;
        SelectedIndex = index;
    }

    public bool IsAffordable(int index) => index >= 0 && index < _affordable.Count && _affordable[index];

    public string Describe(int index) => UnitOptionText.Describe(_options[index]);
}
