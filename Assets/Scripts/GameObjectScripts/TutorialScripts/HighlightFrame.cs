using UnityEngine;
using UnityEngine.UI;

// Pulsing outline that parks over the current step's target (M2.12). Resolves
// ids through the TutorialTarget registry every frame, so panels opening and
// closing just work. UI targets get their padded rect; world targets (the
// starter enemy token) get a fixed-size box projected through the camera
// (canvases are Screen Space – Camera and share the main camera). A missing
// target hides the frame and warns once — it never blocks progression.
public class HighlightFrame : MonoBehaviour
{
    [SerializeField] Image frame;              // outline sprite; Raycast Target OFF
    [SerializeField] RectTransform canvasRect; // the TutorialCanvas root rect
    [SerializeField] Vector2 padding = new Vector2(24f, 24f);
    [SerializeField] Vector2 worldBoxSize = new Vector2(150f, 170f);

    string targetId;
    bool warned;

    public void Show(string id)
    {
        targetId = id;
        warned = false;
        frame.enabled = false; // becomes visible once the target resolves
    }

    public void Hide()
    {
        targetId = null;
        frame.enabled = false;
    }

    void LateUpdate()
    {
        if (string.IsNullOrEmpty(targetId)) return;
        var target = TutorialTarget.Find(targetId);
        if (target == null)
        {
            if (!warned)
            {
                Debug.LogWarning($"Tutorial highlight target '{targetId}' not registered — frame hidden.");
                warned = true;
            }
            frame.enabled = false;
            return;
        }

        var cam = Camera.main;
        var rt = target.transform as RectTransform;
        Vector2 screenCenter;
        Vector2 size;
        if (rt != null)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            screenCenter = (min + max) * 0.5f;
            size = (max - min) + padding;
        }
        else
        {
            screenCenter = RectTransformUtility.WorldToScreenPoint(cam, target.transform.position);
            size = worldBoxSize;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenCenter, cam, out var local);
        var frameRect = (RectTransform)frame.transform;
        frameRect.anchoredPosition = local;
        frameRect.sizeDelta = size;

        frame.enabled = true;
        var c = frame.color;
        c.a = GlowPulse.Alpha(Time.time, 0.35f, 1f, 3f);
        frame.color = c;
    }
}
