using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Toggles the empower flag. Locked (non-interactable) when CanEmpower() is false
// (Improvise active, or a non-empowerable card). Shows the cyan reserved indicator
// and a "+base -> +empowered" preview of the card's total output.
public class EmpowerPanel : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;
    [SerializeField] Toggle empowerToggle;
    [SerializeField] GameObject reservedIndicator; // cyan crystal mark, on when empower active
    [SerializeField] TextMeshProUGUI previewLabel; // "+2 -> +4"
    [SerializeField] GameObject lockedReason;      // "Locked while improvising"

    // Lifetime subscription, not per-enable (root == self). See ChoiceBanner for detail.
    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        empowerToggle.onValueChanged.AddListener(OnToggle);
    }

    void OnToggle(bool value)
    {
        if (_suppress) return;
        inspector.SetEmpowered(value);
    }

    bool _suppress;

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool show = sel != null && card != null;
        root.SetActive(show);
        if (!show) return;

        bool can = sel.CanEmpower();
        empowerToggle.interactable = can;

        if (lockedReason != null)
            lockedReason.SetActive(!can && card.cardSO.empowerType != EmpowerType.None);

        _suppress = true;
        empowerToggle.isOn = sel.EffectiveEmpowered();
        _suppress = false;

        if (reservedIndicator != null)
            reservedIndicator.SetActive(sel.EffectiveEmpowered());

        if (previewLabel != null)
        {
            int baseTotal = Sum(sel.PreviewStats(false));
            int empTotal  = Sum(sel.PreviewStats(true));
            previewLabel.text = $"+{baseTotal} → +{empTotal}";
            previewLabel.color = StatPalette.Empower;
        }
    }

    static int Sum(int[] stats)
    {
        int total = 0;
        foreach (var v in stats) total += v;
        return total;
    }
}
