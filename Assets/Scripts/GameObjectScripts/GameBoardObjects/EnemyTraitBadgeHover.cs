using UnityEngine;
using UnityEngine.EventSystems;

// Hover trigger for one EnemyCard's trait badge row (spec 2026-07-30 §5.5).
// Lives on the traitBadges GameObject itself (already a raycast target), not
// on the whole card — trait info is reachable by hovering the badges
// specifically, not by hovering anywhere on the card.
public class EnemyTraitBadgeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] EnemyCard card;

    // Unity raycasts against the RectTransform's rect, NOT the rendered glyphs, so
    // a zero-area rect can never receive a pointer event — the text still draws
    // (TMP overflows a zero rect happily), which makes it look like the hover code
    // is broken when the row is simply unhoverable. This bit the badge row for real
    // (2026-07-30): it shipped at the placeholder Size Delta (0,0), and every hover
    // silently did nothing. Warn loudly instead of failing silently.
    void Start()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;
        if (rt.rect.width > 0f && rt.rect.height > 0f) return;

        Debug.LogWarning(
            $"[EnemyTraitBadgeHover] '{name}' has a {rt.rect.width}x{rt.rect.height} rect, so it can " +
            "never receive hover events and the trait tooltip will never open. Give the badge row a " +
            "real width and height.", this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EnemyTraitTooltip.Instance == null || card == null) return;
        var tuning = CombatController.Instance != null ? CombatController.Instance.Tuning : new EnemyTraitTuning();

        // transform.position on a Screen Space - Camera canvas is world space,
        // not screen pixels — convert via this GameObject's own canvas/camera,
        // the same pattern EnemyCard's other screen-position math already uses.
        var canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null ? canvas.rootCanvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, transform.position);

        EnemyTraitTooltip.Instance.Show(card.Traits, tuning, screenPoint);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (EnemyTraitTooltip.Instance != null) EnemyTraitTooltip.Instance.Hide();
    }

    // Unity never delivers OnPointerExit to an object that is deactivated or
    // destroyed under the cursor (a card being killed, or Decline() tearing
    // down the fight), so without this the tooltip would hang on screen with
    // nothing left to close it.
    void OnDisable()
    {
        if (EnemyTraitTooltip.Instance != null) EnemyTraitTooltip.Instance.Hide();
    }
}
