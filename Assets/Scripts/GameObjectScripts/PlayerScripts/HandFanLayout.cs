using System.Collections.Generic;
using UnityEngine;

// Applies FanMath slots to the live hand cards. Driven by PlayerHand (which owns
// card order). Geometry only: focus is WRITTEN by HandFocusController (the single
// owner of focus policy); this component renders it and answers slot hit-tests.
public class HandFanLayout : MonoBehaviour
{
    [SerializeField] FanSettings fan = new FanSettings();
    [SerializeField] float focusLift = 40f;
    [SerializeField] float focusScale = 1.3f;
    [SerializeField] float dimBrightness = 0.86f;

    Card _focused;
    IReadOnlyList<Card> _last;

    public Transform Container => transform;
    public Card Focused => _focused;

    public void SetFocus(Card card)
    {
        if (card == _focused) return;
        _focused = card;
        if (_last != null) Relayout(_last);
    }

    public void ClearFocus() => SetFocus(null);

    public void Relayout(IReadOnlyList<Card> orderedCards)
    {
        _last = orderedCards;
        var inHand = InHand();

        var slots = FanMath.Solve(inHand.Count, fan);
        for (int i = 0; i < inHand.Count; i++)
            Apply(inHand[i], slots[i], inHand[i] == _focused);

        if (_focused != null && _focused.transform.parent == transform)
            _focused.transform.SetAsLastSibling();
    }

    // The cards currently physically in the fan (parented here, active), in hand order.
    public List<Card> InHand()
    {
        var inHand = new List<Card>();
        if (_last == null) return inHand;
        foreach (var c in _last)
            if (c != null && c.transform.parent == transform && c.gameObject.activeSelf)
                inHand.Add(c);
        return inHand;
    }

    // Topmost non-wound card whose SLOT rect (not its lifted position) contains the
    // screen point; checking the slot prevents the pointer-exit feedback loop that
    // occurs when the lifted card moves out from under the cursor. Null if none.
    public Card HitTest(Vector2 screenPos)
    {
        if (_last == null) return null;
        var container = (RectTransform)transform;
        var cam = GetComponentInParent<Canvas>()?.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                container, screenPos, cam, out var local))
            return null;

        var inHand = InHand();
        var slots = FanMath.Solve(inHand.Count, fan);

        // Check front-to-back so the topmost (last sibling) card wins on overlap
        for (int i = inHand.Count - 1; i >= 0; i--)
        {
            if (inHand[i].cardSO.cardType == StatType.Wound) continue;

            var rt = (RectTransform)inHand[i].transform;
            var slotPos = slots[i].AnchoredPosition;
            var half = rt.rect.size * 0.5f;

            if (local.x >= slotPos.x - half.x && local.x <= slotPos.x + half.x &&
                local.y >= slotPos.y - half.y && local.y <= slotPos.y + half.y)
                return inHand[i];
        }
        return null;
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
