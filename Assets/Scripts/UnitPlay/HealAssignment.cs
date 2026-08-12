using System.Collections.Generic;

// What one heal activation actually did. Heals used to undo by sign flip:
// RestoreHealedWound() N times. That breaks the moment the player chooses WHERE
// the healing went — the same problem conversion hit — so the applied amounts
// live here instead. Unity-free so it is mcs-CLI-testable; Unit is referenced
// only as object.
public class HealAssignment
{
    public int HandWoundsHealed { get; private set; }
    public readonly List<(object unit, int wounds)> UnitsHealed = new();

    public void RecordHand() => HandWoundsHealed++;

    public void RecordUnit(object unit, int wounds) => UnitsHealed.Add((unit, wounds));

    public bool IsEmpty => HandWoundsHealed == 0 && UnitsHealed.Count == 0;
}
