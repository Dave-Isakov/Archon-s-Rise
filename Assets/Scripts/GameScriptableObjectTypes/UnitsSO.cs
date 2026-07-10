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
}
