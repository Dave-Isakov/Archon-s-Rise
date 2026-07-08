using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The preview view. Input-agnostic: it shows a list of enemies at a screen
// position, or one whole-panel "blind" message when any enemy is hidden. Mouse
// hover drives it today; a gamepad focus will drive the same Show/Hide later.
// Scene-placed singleton (needs its prefab/container refs), reached via Instance.
public class EnemyPreviewPanel : MonoBehaviour
{
    public static EnemyPreviewPanel Instance { get; private set; }

    [SerializeField] GameObject root;              // toggled on Show/Hide
    [SerializeField] RectTransform panelRect;      // moved to the screen position
    [SerializeField] Transform entryContainer;     // parent for spawned entries
    [SerializeField] EnemyPreviewEntry entryPrefab;
    [SerializeField] GameObject blindState;        // the "You cannot see..." object
    [SerializeField] TextMeshProUGUI blindText;

    [Header("Placement")]
    [SerializeField] Vector2 offset = new Vector2(0f, 40f); // nudge off the icon (screen px); +Y floats above
    [SerializeField] float screenMargin = 12f;             // min gap kept from every screen edge
    [SerializeField, Range(0f, 1f)] float centerBias = 0.5f; // 0 = at the icon, 1 = at screen centre (the player);
                                                             // pulls the card into the space between token and player

    Canvas canvas;             // this panel's root canvas (any render mode)
    RectTransform canvasRect;  // the plane we project screen points onto

    void Awake()
    {
        Instance = this;
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas != null) canvasRect = canvas.transform as RectTransform;

        if (root != null)
        {
            // The preview must never intercept the pointer: it is drawn over the
            // hovered token, so a raycast-blocking graphic would steal the hover
            // and flip the token's enter/exit every frame (flicker). One
            // CanvasGroup makes the whole subtree transparent to raycasts.
            var cg = root.GetComponent<CanvasGroup>();
            if (cg == null) cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            root.SetActive(false);
        }
    }

    public void Show(IReadOnlyList<EnemyPreviewData> entries, Vector3 screenPosition)
    {
        Clear();

        var visible = new List<bool>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
            visible.Add(PreviewRules.CanPreview()); // no blind source today → all visible

        if (PreviewRules.EncounterVisible(visible))
        {
            blindState.SetActive(false);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = Instantiate(entryPrefab, entryContainer);
                entry.Populate(entries[i]);
            }
        }
        else
        {
            blindState.SetActive(true);
            blindText.text = entries.Count == 1
                ? "You cannot see the enemy you are about to confront."
                : "You cannot see the enemies you are about to confront.";
        }

        root.SetActive(true);
        PositionPanel(screenPosition);
    }

    // Anchors the panel near the hovered icon (nudged off it by `offset`) and
    // then slides it back on-screen so it is never clipped by a screen edge.
    // Runs after root is active and entries are spawned so the layout has its
    // final size. All anchoring/clamping is done in screen pixels (common to
    // every canvas render mode) and projected onto the canvas plane, so this is
    // correct for Screen Space - Camera and Overlay alike. `screenPoint` is a
    // pixel point (both triggers hand over WorldToScreenPoint results).
    void PositionPanel(Vector3 screenPoint)
    {
        // Pull the anchor from the icon toward screen centre (where the player is,
        // since the camera is parented under the player). This seats the card in
        // the band between the token and the player for guaranteed visibility,
        // rather than hard against a screen edge.
        Vector2 screenCentre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 anchor = Vector2.Lerp((Vector2)screenPoint, screenCentre, centerBias);
        Vector2 desired = anchor + offset;
        Camera cam = canvas != null ? canvas.worldCamera : null;

        PlaceAtScreenPoint(desired);

        // Measure the actual visible content (the entry stack or the blind text),
        // not the movable root — the root may be a zero-size anchor. The content
        // is a descendant of panelRect, so translating panelRect moves it rigidly.
        RectTransform content = ContentRect();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content); // size known before we measure

        var corners = new Vector3[4];
        content.GetWorldCorners(corners);              // [0]=bottom-left, [2]=top-right
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        float width = topRight.x - bottomLeft.x;
        float height = topRight.y - bottomLeft.y;

        // Clamp the content's bottom-left onto the screen, then re-place by the delta.
        float clampedX = PreviewRules.ClampAxis(bottomLeft.x, width, Screen.width, screenMargin);
        float clampedY = PreviewRules.ClampAxis(bottomLeft.y, height, Screen.height, screenMargin);
        PlaceAtScreenPoint(desired + new Vector2(clampedX - bottomLeft.x, clampedY - bottomLeft.y));
    }

    // The rect whose bounds must stay on-screen: the spawned entry stack, or the
    // blind message when blind, or the root as a last resort. Always a descendant
    // of panelRect so translating panelRect keeps it aligned.
    RectTransform ContentRect()
    {
        if (entryContainer != null && entryContainer.childCount > 0)
            return entryContainer as RectTransform;
        if (blindState != null && blindState.activeSelf)
            return blindState.transform as RectTransform;
        return panelRect;
    }

    // Positions the panel so its pivot sits at `screenPoint`, projected onto the
    // canvas plane. `cam` is null for Overlay, the render camera for SS-Camera —
    // RectTransformUtility handles both.
    void PlaceAtScreenPoint(Vector2 screenPoint)
    {
        if (canvasRect == null) { panelRect.position = screenPoint; return; }
        Camera cam = canvas != null ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect, screenPoint, cam, out Vector3 world))
            panelRect.position = world;
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        Clear();
    }

    void Clear()
    {
        for (int i = entryContainer.childCount - 1; i >= 0; i--)
            Destroy(entryContainer.GetChild(i).gameObject);
        if (blindState != null) blindState.SetActive(false);
    }
}
