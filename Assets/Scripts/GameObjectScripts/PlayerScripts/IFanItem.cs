using UnityEngine;

// One entry in a fan lane. Card and Unit both satisfy it, so FanLane lays out,
// focuses and hit-tests either lane with no branching on type.
public interface IFanItem
{
    RectTransform Rect { get; }
    CanvasGroup Group { get; }
    bool Selectable { get; }  // false for wounds and exhausted units
    void Activate();          // Submit, or a click inside the focused lane
}
