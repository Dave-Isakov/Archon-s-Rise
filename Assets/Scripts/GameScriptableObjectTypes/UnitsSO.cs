using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Units", menuName = "ScriptableObjects/Units")]
public class UnitsSO : AllCards
{
    // The unit's authored options; the pop-out renders exactly these
    // (spec 2026-07-09). Using any option exhausts the unit for the round.
    public List<UnitOption> options = new();
    // Recruit price at towns (per-unit, replaces the town's flat recruitLevel).
    public int influenceCost;
    public Sprite sprite;
    public Color color;
    public char unitLetter;

    void OnValidate()
    {
        if (options == null) return;
        foreach (var o in options)
            if (o != null && o.crystalCost != EmpowerType.None && o.influenceCost > 0)
                Debug.LogWarning($"{name}: an option may cost a crystal OR influence, not both.", this);
    }
}
