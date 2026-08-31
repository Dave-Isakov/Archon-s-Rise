using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The rail/one-shot instruction banner (M2.12). Dumb view: TutorialManager
// decides what shows; the buttons call back into the manager (wired in the
// editor). Not a modal — never enqueued on RewardQueue; the manager's
// CanvasGroup hides it while modals or pickers are open.
public class TutorialBanner : MonoBehaviour
{
    [SerializeField] GameObject root; // the visible panel
    [SerializeField] TextMeshProUGUI bodyText;
    [SerializeField] Button nextButton;
    [SerializeField] TextMeshProUGUI nextLabel;
    [SerializeField] Button skipButton;
    // The rect that actually moves between edges. Leave empty to move `root`,
    // which is the normal case; set it only when the panel that should slide is
    // not the same object that gets shown and hidden.
    [SerializeField] RectTransform sidePanel;

    // The authored (left-hand) pose, captured once on the first side change and
    // never re-read. Only the left pose is authored in the editor; the right one
    // is mirrored from it, so the two can never drift apart.
    RectTransform panel;
    Vector2 leftAnchorMin, leftAnchorMax, leftPivot, leftPosition;
    bool poseCaptured;

    public void ShowStep(string text, bool informational, BannerSide side)
    {
        root.SetActive(true);
        SetSide(side);
        bodyText.text = text;
        nextButton.gameObject.SetActive(informational);
        if (informational) nextLabel.text = "Next";
        skipButton.gameObject.SetActive(true);
    }

    public void ShowTip(string text, BannerSide side)
    {
        root.SetActive(true);
        SetSide(side);
        bodyText.text = text;
        nextButton.gameObject.SetActive(true);
        nextLabel.text = "Got it";
        skipButton.gameObject.SetActive(false);
    }

    public void HideAll() => root.SetActive(false);

    // Mirrors the authored pose horizontally about the parent's centre, which keeps
    // the same inset from whichever edge it lands against for any anchor setup.
    // Captured lazily rather than in Awake so it cannot read a pose this method has
    // already modified, and so it is safe however the panel's active state starts.
    void SetSide(BannerSide side)
    {
        if (!poseCaptured)
        {
            panel = sidePanel != null ? sidePanel : root.GetComponent<RectTransform>();
            if (panel == null) return; // non-RectTransform root: nothing to move
            leftAnchorMin = panel.anchorMin;
            leftAnchorMax = panel.anchorMax;
            leftPivot = panel.pivot;
            leftPosition = panel.anchoredPosition;
            poseCaptured = true;
        }
        if (panel == null) return;

        bool right = side == BannerSide.Right;
        panel.anchorMin = new Vector2(right ? 1f - leftAnchorMax.x : leftAnchorMin.x, leftAnchorMin.y);
        panel.anchorMax = new Vector2(right ? 1f - leftAnchorMin.x : leftAnchorMax.x, leftAnchorMax.y);
        panel.pivot = new Vector2(right ? 1f - leftPivot.x : leftPivot.x, leftPivot.y);
        panel.anchoredPosition = new Vector2(right ? -leftPosition.x : leftPosition.x, leftPosition.y);
    }
}
