using UnityEngine;
using UnityEngine.UI;

// The ? button dropped into a panel's corner (M2.12). Holds its panel's
// HelpEntrySO, opens the shared popup, and pulses until the entry has been
// read once. Tips OFF hides the icon entirely (invisible + non-interactive)
// via a CanvasGroup — never by deactivating this GameObject, so Update keeps
// polling and the icon reappears the instant tips are turned back on. Prefab
// wiring: the Button's OnClick calls OpenEntry.
public class HelpIcon : MonoBehaviour
{
    [SerializeField] HelpEntrySO entry;
    [SerializeField] Graphic pulseTarget; // the ? glyph

    CanvasGroup group; // hides the whole icon while tips are off

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    public void OpenEntry()
    {
        if (HelpPopup.Instance != null) HelpPopup.Instance.Open(entry);
    }

    void Update()
    {
        // Tips off → the icon vanishes and stops catching clicks. TutorialPrefs
        // is the fallback before the manager's Awake (or in scenes without one).
        bool tipsOn = TutorialManager.Instance != null
            ? TutorialManager.Instance.TipsEnabled
            : TutorialPrefs.Enabled;

        group.interactable = tipsOn;
        group.blocksRaycasts = tipsOn;
        if (!tipsOn)
        {
            group.alpha = 0f;
            return;
        }
        group.alpha = 1f;

        if (pulseTarget == null || entry == null) return;
        bool pulse = TutorialManager.Instance != null
            && TutorialManager.Instance.ShouldPulseHelp(entry.panelId);
        var c = pulseTarget.color;
        c.a = pulse ? GlowPulse.Alpha(Time.time, 0.35f, 1f, 4f) : 1f;
        pulseTarget.color = c;
    }
}
