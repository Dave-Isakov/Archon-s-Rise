using TMPro;
using UnityEngine;

// The shared help reader every ? opens (M2.12). Player-initiated, read-only,
// one at a time; not a modal (never on RewardQueue). Lives on TutorialCanvas
// but works regardless of the tips toggle — ? help is always available.
public class HelpPopup : MonoBehaviour
{
    public static HelpPopup Instance { get; private set; }

    [SerializeField] GameObject root; // outside-click catcher + panel
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

    // Wired to the X button AND the full-screen outside-click catcher.
    public void Close() => root.SetActive(false);
}
