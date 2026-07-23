using UnityEngine;
using TMPro;

// Small screen-space label that follows the pointed cell. HexInteractor sets its text
// and world anchor each frame; empty text hides it. Uses TMP so IconMarkup sprite tags
// (the explore/scroll glyph) render inline, matching card text.
public class HexTooltip : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] RectTransform panel;      // the panel to move; anchored in screen space
    [SerializeField] Vector2 screenOffset = new Vector2(0f, 40f);

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
        var c = cam != null ? cam : Camera.main;
        if (c != null)
            panel.position = c.WorldToScreenPoint(worldAnchor) + (Vector3)screenOffset;
    }
}
