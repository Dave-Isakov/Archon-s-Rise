using UnityEngine;
using UnityEngine.UI;

// The ? button dropped into a panel's corner (M2.12). Holds its panel's
// HelpEntrySO, opens the shared popup, and pulses until the entry has been
// read once — and only while tips are enabled. Prefab wiring: the Button's
// OnClick calls OpenEntry.
public class HelpIcon : MonoBehaviour
{
    [SerializeField] HelpEntrySO entry;
    [SerializeField] Graphic pulseTarget; // the ? glyph

    public void OpenEntry()
    {
        if (HelpPopup.Instance != null) HelpPopup.Instance.Open(entry);
    }

    void Update()
    {
        if (pulseTarget == null || entry == null) return;
        bool pulse = TutorialManager.Instance != null
            && TutorialManager.Instance.ShouldPulseHelp(entry.panelId);
        var c = pulseTarget.color;
        c.a = pulse ? GlowPulse.Alpha(Time.time, 0.35f, 1f, 4f) : 1f;
        pulseTarget.color = c;
    }
}
