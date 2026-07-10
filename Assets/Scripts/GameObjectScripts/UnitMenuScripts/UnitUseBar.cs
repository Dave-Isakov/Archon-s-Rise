using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The Use button (label = live preview of the selected option) plus Back.
public class UnitUseBar : MonoBehaviour
{
    [SerializeField] UnitInspector inspector;
    [SerializeField] public Button useButton;
    [SerializeField] TextMeshProUGUI useLabel;
    [SerializeField] Button backButton;

    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        useButton.onClick.AddListener(() => inspector.Use());
        backButton.onClick.AddListener(() => inspector.Close());
    }

    void Render()
    {
        var sel = inspector.Selection;
        if (sel == null) return;
        useButton.interactable = sel.CanUse;
        useLabel.text = sel.CanUse
            ? $"USE · {sel.Describe(sel.SelectedIndex)}"
            : (sel.SelectedIndex >= 0 ? "Needs a crystal" : "No options");
    }
}
