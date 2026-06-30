using System.Collections.Generic;
using UnityEngine;

// Applies FanMath slots to the live hand cards. Driven by PlayerHand (which owns
// card order). Focus lift/dim requires a CanvasGroup on each Card prefab root.
public class HandFanLayout : MonoBehaviour
{
    [SerializeField] FanSettings fan = new FanSettings();
    [SerializeField] float focusLift = 40f;
    [SerializeField] float focusScale = 1.3f;
    [SerializeField] float dimBrightness = 0.86f;

    Card _focused;
    IReadOnlyList<Card> _last;

    public Transform Container => transform;

    public void SetFocus(Card card)
    {
        if (_focused == card) return;
        _focused = card;
        if (_last != null) Relayout(_last);
    }

    public void ClearFocus(Card card)
    {
        if (_focused != card) return;
        _focused = null;
        if (_last != null) Relayout(_last);
    }

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
