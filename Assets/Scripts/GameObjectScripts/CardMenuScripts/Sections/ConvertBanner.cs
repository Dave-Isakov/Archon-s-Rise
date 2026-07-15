using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Opt-in conversion section (spec 2026-07-14). Shows only for converter cards;
// the toggle arms "convert all X → Y" on the in-progress play. Locks (dim +
// reason) while Improvise is active or while an empower-gated conversion isn't
// empowered. Same lifetime-subscription pattern as ChoiceBanner: Render hides
// root (self), so Awake/OnDestroy survive self-deactivation.
public class ConvertBanner : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;              // banner container to show/hide
    [SerializeField] Toggle convertToggle;
    [SerializeField] TextMeshProUGUI label;        // "Convert all Defend → Attack"
    [SerializeField] GameObject lockedReason;
    [SerializeField] TextMeshProUGUI lockedReasonText;

    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        convertToggle.onValueChanged.AddListener(v => inspector.SetConvert(v));
    }

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool show = sel != null && card != null && sel.HasConversion;
        root.SetActive(show);
        if (!show) return;

        label.text = ConvertRules.Describe(card.cardSO.convertFrom, card.cardSO.convertTo);

        bool can = sel.CanConvert();
        convertToggle.SetIsOnWithoutNotify(sel.ConvertOn && can);
        convertToggle.interactable = can;
        if (lockedReason != null) lockedReason.SetActive(!can);
        if (!can && lockedReasonText != null)
            lockedReasonText.text = sel.Mode == PlayMode.Improvise
                ? "Locked while improvising"
                : "Empower to unlock";
    }
}
