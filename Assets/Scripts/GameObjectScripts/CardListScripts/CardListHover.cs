using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Hover feedback for card-list clones: the card tweens up in scale and pulls
// toward the viewport centre so it reads at full size without a separate
// preview. Added at RUNTIME by CardListController — it must never live on the
// Card prefab, which the hand shares. Render-on-top uses an override-sorting
// Canvas instead of SetAsLastSibling because GridLayoutGroup derives slot
// positions from sibling order — reordering would reshuffle the grid.
public class CardListHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    float hoverScale = 1.35f;
    float pullStrength = 0.15f;
    float duration = 0.15f;

    RectTransform rt;
    ScrollRect scroll;
    Vector2 slotPos;
    bool slotCaptured;
    bool hovered;
    Canvas sortCanvas;
    GraphicRaycaster raycaster;

    public void Init(float hoverScale, float pullStrength, float duration)
    {
        this.hoverScale = hoverScale;
        this.pullStrength = pullStrength;
        this.duration = duration;
    }

    void Awake()
    {
        rt = (RectTransform)transform;
        scroll = GetComponentInParent<ScrollRect>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hovered) return;
        hovered = true;
        // Capture the grid slot ONCE. A card returning from hover can slide back
        // under the pointer and re-fire enter mid-tween; capturing on every enter
        // recorded that in-flight position as the "slot", ratcheting the card off
        // its grid position a little more per hover (worst at the edges, where
        // the pull is largest). The slot never changes while the list is open —
        // the clone set is fixed and scrolling moves the content, not the cards.
        if (!slotCaptured)
        {
            slotPos = rt.anchoredPosition;
            slotCaptured = true;
        }

        // Delta to the viewport centre as a world vector converted into the
        // content's local space — a vector, not a point, so the card's anchor
        // offset cancels and the result applies directly to anchoredPosition.
        Vector2 pull = Vector2.zero;
        if (scroll != null && scroll.viewport != null)
        {
            Vector3 centerWorld = scroll.viewport.TransformPoint(scroll.viewport.rect.center);
            Vector3 cardWorld = rt.TransformPoint(rt.rect.center);
            Vector3 delta = rt.parent.InverseTransformVector(centerWorld - cardWorld);
            float ox, oy;
            CardListHoverMath.PullOffset(0f, 0f, delta.x, delta.y, pullStrength, out ox, out oy);
            pull = new Vector2(ox, oy);
        }

        LiftAboveNeighbours();
        rt.DOKill();
        rt.DOScale(hoverScale, duration).SetEase(Ease.OutCubic);
        rt.DOAnchorPos(slotPos + pull, duration).SetEase(Ease.OutCubic);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hovered) return;
        hovered = false;
        rt.DOKill();
        rt.DOScale(1f, duration).SetEase(Ease.OutCubic);
        rt.DOAnchorPos(slotPos, duration).SetEase(Ease.OutCubic)
            .OnComplete(DropBehindNeighbours);
    }

    // A sub-canvas re-registers this card's graphics under itself; it needs its
    // own GraphicRaycaster or the card stops receiving pointer events the moment
    // the canvas is added (instant exit flicker).
    void LiftAboveNeighbours()
    {
        // Parent canvas read from the parent, not self: once the sub-canvas is
        // added, GetComponentInParent on this object would find the sub-canvas.
        var parentCanvas = transform.parent.GetComponentInParent<Canvas>();
        if (sortCanvas == null) sortCanvas = gameObject.AddComponent<Canvas>();
        sortCanvas.overrideSorting = true;
        sortCanvas.sortingOrder = parentCanvas != null ? parentCanvas.sortingOrder + 1 : 1;
        if (raycaster == null) raycaster = gameObject.AddComponent<GraphicRaycaster>();
    }

    void DropBehindNeighbours()
    {
        // Raycaster depends on the canvas, so it must be destroyed first.
        if (raycaster != null) { Destroy(raycaster); raycaster = null; }
        if (sortCanvas != null) { Destroy(sortCanvas); sortCanvas = null; }
    }

    void OnDestroy()
    {
        rt.DOKill();
    }
}
