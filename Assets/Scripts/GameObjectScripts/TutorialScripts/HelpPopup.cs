using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The shared help reader every ? opens (M2.12). Player-initiated, read-only,
// one at a time; not a modal (never on RewardQueue). Lives on TutorialCanvas
// but works regardless of the tips toggle — ? help is always available.
public class HelpPopup : MonoBehaviour
{
    public static HelpPopup Instance { get; private set; }

    [SerializeField] GameObject root; // ClickOffCatcher + panel
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI bodyText;

    // Above every panel that hosts a ? (pickers 800, reward 850, run-end 1000),
    // below the transient Log/Toast canvases at 2000.
    const int HelpSortingOrder = 1500;

    void Awake()
    {
        Instance = this;
        // Several ? icons live inside surfaces that suppress TutorialCanvas. Parented
        // under it, the popup inherited both that canvas's zeroed group alpha and its
        // sorting order, so it opened invisible and behind whatever had opened it. Its
        // own group and its own sorting root take it out of both.
        var group = root.GetComponent<CanvasGroup>();
        if (group == null) group = root.AddComponent<CanvasGroup>();
        group.ignoreParentGroups = true;
        group.alpha = 1f;
        group.blocksRaycasts = true;

        var canvas = root.GetComponent<Canvas>();
        if (canvas == null) canvas = root.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = HelpSortingOrder;
        // A nested canvas needs its own raycaster, or the ClickOffCatcher under it
        // stops receiving the dismiss click.
        if (root.GetComponent<GraphicRaycaster>() == null)
            root.AddComponent<GraphicRaycaster>();

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
