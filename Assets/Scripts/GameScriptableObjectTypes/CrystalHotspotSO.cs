using UnityEngine;

// A crystal-hotspot tile (spec 2026-07-24): stands on the map showing a fixed
// color; standing on it at End Turn grants 1 crystal of that color. charges
// decrement per harvest; -1 = unlimited (rich vein).
[CreateAssetMenu(fileName = "Crystal Hotspot", menuName = "ScriptableObjects/CrystalHotspot")]
public class CrystalHotspotSO : AllCards
{
    // Save identity is the inherited AllCards.id (a stable slug; never rename) —
    // do not re-declare it here or Unity errors on a duplicate serialized field.
    [Tooltip("Single color this tile yields.")]
    public EmpowerType color = EmpowerType.Red;
    [Tooltip("Payouts before dormancy. -1 = unlimited (rich vein).")]
    public int charges = 3;
    [Tooltip("Crystal art for this color; the token shows it (color lives with its own asset).")]
    public Sprite crystalSprite;
}
