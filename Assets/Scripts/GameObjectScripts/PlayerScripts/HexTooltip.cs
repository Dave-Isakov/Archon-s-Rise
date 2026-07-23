using UnityEngine;
using TMPro;

// Small screen-space label that follows the pointed cell. HexInteractor sets its text
// and world anchor each frame; empty text hides it. Uses TMP so IconMarkup sprite tags
// (the explore/scroll glyph) render inline, matching card text.
//
// Placement note: every canvas here is Screen Space - Camera, so a RectTransform's
// position is WORLD space on the canvas plane, not screen pixels. We convert the board
// world anchor to a screen point (board camera), then project that screen point back
// onto the canvas plane via RectTransformUtility — the same approach EnemyPreviewPanel
// uses. Setting panel.position to a raw screen-pixel vector (the old bug) parked the
// panel at screen centre, i.e. over the player (the camera is under PlayerPosition).
public class HexTooltip : MonoBehaviour
{
    [SerializeField] Camera cam;               // board camera: world anchor -> screen point
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] RectTransform panel;      // the panel to move; a child of the canvas
    [SerializeField] Vector2 screenOffset = new Vector2(0f, 40f); // nudge above the hex (screen px)

    Canvas canvas;             // the panel's root canvas (Screen Space - Camera)
    RectTransform canvasRect;  // the plane we project screen points onto

    void Awake()
    {
        if (panel != null)
        {
            canvas = panel.GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
            if (canvas != null) canvasRect = canvas.transform as RectTransform;
        }
    }

    public void Hide()
    {
        if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
    }

    public void Show(string text, Vector3 worldAnchor)
    {
        if (panel == null || label == null) return;
        if (string.IsNullOrEmpty(text)) { Hide(); return; }
        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
        label.text = text;

        var boardCam = cam != null ? cam : Camera.main;
        if (boardCam == null) return;
        Vector3 screenPoint = boardCam.WorldToScreenPoint(worldAnchor) + (Vector3)screenOffset;
        PlaceAtScreenPoint(screenPoint);
    }

    // Positions the panel so its pivot sits at `screenPoint`, projected onto the canvas
    // plane. `worldCamera` is the render camera for Screen Space - Camera (null for
    // Overlay); RectTransformUtility handles both.
    void PlaceAtScreenPoint(Vector2 screenPoint)
    {
        if (canvasRect == null) { panel.position = screenPoint; return; }
        Camera uiCam = canvas != null ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect, screenPoint, uiCam, out Vector3 world))
            panel.position = world;
    }
}
