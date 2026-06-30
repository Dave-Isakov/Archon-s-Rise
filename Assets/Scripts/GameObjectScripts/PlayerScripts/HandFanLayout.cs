using System.Collections.Generic;
using UnityEngine;

// Applies FanMath slots to the live hand cards. Driven by PlayerHand (which owns
// card order). Focus is determined each LateUpdate by checking the mouse against
// the card's SLOT position (not its lifted position), which prevents the
// pointer-exit feedback loop that occurs when the card moves out from under the cursor.
public class HandFanLayout : MonoBehaviour
{
    [SerializeField] FanSettings fan = new FanSettings();
    [SerializeField] float focusLift = 40f;
    [SerializeField] float focusScale = 1.3f;
    [SerializeField] float dimBrightness = 0.86f;

    Card _focused;
    IReadOnlyList<Card> _last;

    public Transform Container => transform;

    public void Relayout(IReadOnlyList<Card> orderedCards)
    {
        _last = orderedCards;
        var inHand = new List<Card>();
        foreach (var c in orderedCards)
            if (c != null && c.transform.parent == transform && c.gameObject.activeSelf)
                inHand.Add(c);

        var slots = FanMath.Solve(inHand.Count, fan);
        for (int i = 0; i < inHand.Count; i++)
            Apply(inHand[i], slots[i], inHand[i] == _focused);

        if (_focused != null && _focused.transform.parent == transform)
            _focused.transform.SetAsLastSibling();
    }

    void LateUpdate()
    {
        if (_last == null) return;

        // When inspector is open, clear focus and stop
        if (GameManager.Instance != null && GameManager.Instance.cardCanvas.enabled)
        {
            if (_focused != null) { _focused = null; Relayout(_last); }
            return;
        }

        var container = (RectTransform)transform;
        var cam = GetComponentInParent<Canvas>()?.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                container, Input.mousePosition, cam, out var localMouse))
        {
            if (_focused != null) { _focused = null; Relayout(_last); }
            return;
        }

        var inHand = new List<Card>();
        foreach (var c in _last)
            if (c != null && c.transform.parent == transform && c.gameObject.activeSelf)
                inHand.Add(c);

        var slots = FanMath.Solve(inHand.Count, fan);
        Card newFocus = null;

        // Check front-to-back so the topmost (last sibling) card wins on overlap
        for (int i = inHand.Count - 1; i >= 0; i--)
        {
            if (inHand[i].cardSO.cardType == StatType.Wound) continue;

            var rt = (RectTransform)inHand[i].transform;
            var slotPos = slots[i].AnchoredPosition; // use slot, not lifted position
            var half = rt.rect.size * 0.5f;

            if (localMouse.x >= slotPos.x - half.x && localMouse.x <= slotPos.x + half.x &&
                localMouse.y >= slotPos.y - half.y && localMouse.y <= slotPos.y + half.y)
            {
                newFocus = inHand[i];
                break;
            }
        }

        if (newFocus == _focused) return;
        _focused = newFocus;
        Relayout(_last);
    }

    void Apply(Card card, FanSlot slot, bool focused)
    {
        var rt = (RectTransform)card.transform;
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

        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null)
            cg.alpha = (focused || _focused == null) ? 1f : dimBrightness;
    }
}
