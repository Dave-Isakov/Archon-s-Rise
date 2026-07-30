using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One enemy's trait legend (badge + name + rule per trait), shown on hover over
// an EnemyCard's badge row (spec 2026-07-30 §5.5/§6). The only place trait
// info renders now that the separate preview screen is gone — used identically
// whether the fight is committed or not.
//
// Positioning mirrors the deleted EnemyPreviewPanel: project a screen point
// onto the canvas plane, then slide the box back on-screen via
// PreviewRules.ClampAxis if it would clip an edge. That math wasn't the
// problem being fixed; having two competing content sources was.
public class EnemyTraitTooltip : MonoBehaviour
{
    public static EnemyTraitTooltip Instance { get; private set; }

    [SerializeField] GameObject root;          // toggled on Show/Hide
    [SerializeField] RectTransform panelRect;  // moved to the screen position
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Vector2 offset = new Vector2(0f, 30f); // nudge off the badge row (screen px)
    [SerializeField] float screenMargin = 12f;

    Canvas canvas;
    RectTransform canvasRect;

    void Awake()
    {
        Instance = this;
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas != null) canvasRect = canvas.transform as RectTransform;

        if (root != null)
        {
            var cg = root.GetComponent<CanvasGroup>();
            if (cg == null) cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; // never steal the hover it's drawn over
            cg.interactable = false;
            root.SetActive(false);
        }
    }

    public void Show(EnemyTrait traits, EnemyTraitTuning tuning, Vector3 screenPosition)
    {
        string text = EnemyTraitCopy.Legend(traits, tuning);
        if (string.IsNullOrEmpty(text)) { Hide(); return; }

        if (root != null) root.SetActive(true);
        if (label != null) label.text = text;

        if (panelRect == null) return;

        PlaceAtScreenPoint((Vector2)screenPosition + offset);

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        var corners = new Vector3[4];
        panelRect.GetWorldCorners(corners);
        Camera cam = canvas != null ? canvas.worldCamera : null;
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        float width = topRight.x - bottomLeft.x;
        float height = topRight.y - bottomLeft.y;

        float clampedX = PreviewRules.ClampAxis(bottomLeft.x, width, Screen.width, screenMargin);
        float clampedY = PreviewRules.ClampAxis(bottomLeft.y, height, Screen.height, screenMargin);
        PlaceAtScreenPoint((Vector2)screenPosition + offset + new Vector2(clampedX - bottomLeft.x, clampedY - bottomLeft.y));
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    void PlaceAtScreenPoint(Vector2 screenPoint)
    {
        if (canvasRect == null) { panelRect.position = screenPoint; return; }
        Camera cam = canvas != null ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPoint, cam, out Vector3 world))
            panelRect.position = world;
    }
}
