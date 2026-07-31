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
//
// Scene layout is deliberately flexible: `root` may be this GameObject or a
// child, and the GameObject may be authored active or inactive. See EnsureInit.
public class EnemyTraitTooltip : MonoBehaviour
{
    // Resolved lazily, INCLUDING inactive objects. A hidden tooltip is the normal
    // state, so authoring its GameObject inactive in the scene is the natural
    // instinct — and that used to mean Awake never ran, Instance stayed null, and
    // every hover silently returned at EnemyTraitBadgeHover's first guard with no
    // error to explain it (2026-07-30). Finding it lazily removes that trap.
    static EnemyTraitTooltip instance;
    public static EnemyTraitTooltip Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<EnemyTraitTooltip>(FindObjectsInactive.Include);
            return instance;
        }
    }

    [SerializeField] GameObject root;          // the panel shown/hidden; may be this GameObject
    [SerializeField] RectTransform panelRect;  // moved to the screen position
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Vector2 offset = new Vector2(0f, 30f); // nudge off the badge row (screen px)
    [SerializeField] float screenMargin = 12f;

    Canvas canvas;
    RectTransform canvasRect;
    CanvasGroup group;
    bool initialised;

    void Awake()
    {
        instance = this;
        EnsureInit();
    }

    // Idempotent, and safe on an inactive GameObject — Show() calls it too, so the
    // tooltip works whether or not Awake ever fired, and whether `root` is this
    // GameObject or a child.
    //
    // Visibility is CanvasGroup alpha rather than SetActive on purpose: when root
    // IS this GameObject, toggling active would make Unity run Awake in the middle
    // of Show() and immediately re-hide the tooltip we were opening.
    void EnsureInit()
    {
        if (initialised) return;
        initialised = true; // set first: activating below can re-enter via Awake

        var parentCanvas = GetComponentInParent<Canvas>(true); // true: we may be inactive
        if (parentCanvas != null) canvas = parentCanvas.rootCanvas;
        if (canvas != null) canvasRect = canvas.transform as RectTransform;

        var host = root != null ? root : gameObject;
        group = host.GetComponent<CanvasGroup>();
        if (group == null) group = host.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; // never steal the hover it's drawn over
        group.interactable = false;
        if (!host.activeSelf) host.SetActive(true); // once — alpha owns visibility now
        group.alpha = 0f;
    }

    public void Show(EnemyTrait traits, EnemyTraitTuning tuning, Vector3 screenPosition)
    {
        EnsureInit();

        string text = EnemyTraitCopy.Legend(traits, tuning);
        if (string.IsNullOrEmpty(text)) { Hide(); return; }

        if (label != null) label.text = text;
        if (group != null) group.alpha = 1f;

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
        if (group != null) group.alpha = 0f;
    }

    void PlaceAtScreenPoint(Vector2 screenPoint)
    {
        if (canvasRect == null) { panelRect.position = screenPoint; return; }
        Camera cam = canvas != null ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPoint, cam, out Vector3 world))
            panelRect.position = world;
    }
}
