using System.Collections.Generic;
using UnityEngine;

// Applies FanMath slots to the live items of one lane. Driven by whoever owns
// the lane's order. Geometry only: focus is WRITTEN by BarFocusController (the
// single owner of focus policy); this component renders it and answers slot
// hit-tests.
public class FanLane : MonoBehaviour
{
    [SerializeField] FanSettings fan = new FanSettings();
    [SerializeField] float focusLift = 40f;
    [SerializeField] float focusScale = 1.3f;
    [SerializeField] float dimBrightness = 0.86f;

    IFanItem _focused;
    IReadOnlyList<IFanItem> _last;

    public Transform Container => transform;
    public IFanItem Focused => Alive(_focused) ? _focused : null;

    // An interface reference bypasses Unity's null-equality overload, so a
    // destroyed item would still read as non-null. Everything that dereferences
    // an IFanItem goes through here.
    static bool Alive(IFanItem item) => item is MonoBehaviour mb && mb != null;

    public void SetFocus(IFanItem item)
    {
        if (ReferenceEquals(item, _focused)) return;
        _focused = item;
        if (_last != null) Relayout(_last);
    }

    public void ClearFocus() => SetFocus(null);

    public void Relayout(IReadOnlyList<IFanItem> orderedItems)
    {
        _last = orderedItems;
        var inLane = InLane();

        var slots = FanMath.Solve(inLane.Count, fan);
        for (int i = 0; i < inLane.Count; i++)
            Apply(inLane[i], slots[i], ReferenceEquals(inLane[i], _focused));

        if (Alive(_focused) && _focused.Rect.parent == transform)
            _focused.Rect.SetAsLastSibling();
    }

    // The items currently physically in the fan (parented here, active), in order.
    public List<IFanItem> InLane()
    {
        var inLane = new List<IFanItem>();
        if (_last == null) return inLane;
        foreach (var item in _last)
            if (Alive(item) && item.Rect.parent == transform && item.Rect.gameObject.activeSelf)
                inLane.Add(item);
        return inLane;
    }

    // Topmost selectable item whose SLOT rect (not its lifted position) contains
    // the screen point; checking the slot prevents the pointer-exit feedback loop
    // that occurs when the lifted item moves out from under the cursor.
    public IFanItem HitTest(Vector2 screenPos)
    {
        if (_last == null) return null;
        var container = (RectTransform)transform;
        var cam = GetComponentInParent<Canvas>()?.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                container, screenPos, cam, out var local))
            return null;

        var inLane = InLane();
        var slots = FanMath.Solve(inLane.Count, fan);

        // Front-to-back so the topmost (last sibling) item wins on overlap.
        for (int i = inLane.Count - 1; i >= 0; i--)
        {
            if (!inLane[i].Selectable) continue;

            var slotPos = slots[i].AnchoredPosition;
            var half = inLane[i].Rect.rect.size * 0.5f;

            if (local.x >= slotPos.x - half.x && local.x <= slotPos.x + half.x &&
                local.y >= slotPos.y - half.y && local.y <= slotPos.y + half.y)
                return inLane[i];
        }
        return null;
    }

    void Apply(IFanItem item, FanSlot slot, bool focused)
    {
        var rt = item.Rect;
        if (focused)
        {
            rt.anchoredPosition = slot.AnchoredPosition + new Vector2(0f, focusLift);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * focusScale;
        }
        else
        {
            rt.anchoredPosition = slot.AnchoredPosition;
            rt.localRotation = Quaternion.Euler(0f, 0f, slot.TiltZ);
            rt.localScale = Vector3.one;
        }

        var cg = item.Group;
        if (cg != null)
            cg.alpha = (focused || !Alive(_focused)) ? 1f : dimBrightness;
    }
}
