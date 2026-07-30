using UnityEngine;
using UnityEngine.EventSystems;

// Hover trigger for one EnemyCard's trait badge row (spec 2026-07-30 §5.5).
// Lives on the traitBadges GameObject itself (already a raycast target), not
// on the whole card — trait info is reachable by hovering the badges
// specifically, not by hovering anywhere on the card.
public class EnemyTraitBadgeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] EnemyCard card;

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
}
