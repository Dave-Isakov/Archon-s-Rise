using TMPro;
using UnityEngine;

// The shared help reader every ? opens (M2.12). Player-initiated, read-only,
// one at a time; not a modal (never on RewardQueue). Lives on TutorialCanvas
// but works regardless of the tips toggle — ? help is always available.
public class HelpPopup : MonoBehaviour
{
    public static HelpPopup Instance { get; private set; }

    [SerializeField] GameObject root; // ClickOffCatcher + panel
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI bodyText;

    void Awake()
    {
        Instance = this;
        root.SetActive(false);
    }

    public void Open(HelpEntrySO entry)
    {
        if (entry == null) return;
        titleText.text = entry.title;
        bodyText.text = entry.body;
        root.SetActive(true);
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.MarkHelpSeen(entry.panelId);
    }

    // Wired to the full-screen ClickOffCatcher. The X button was removed in the
    // 2026-07-28 sweep: click-off is the one dismiss gesture.
    public void Close() => root.SetActive(false);
}
