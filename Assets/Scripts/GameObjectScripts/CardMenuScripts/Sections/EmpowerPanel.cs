using UnityEngine;
using UnityEngine.UI;

// Toggles the empower flag. Locked (non-interactable) when CanEmpower() is false
// (Improvise active, or a non-empowerable card). Crystal reservation is added in Task 6.
public class EmpowerPanel : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;
    [SerializeField] Toggle empowerToggle;

    void OnEnable()  { inspector.Changed += Render; }
    void OnDisable() { inspector.Changed -= Render; }

    void Start()
    {
        empowerToggle.onValueChanged.AddListener(OnToggle);
    }

    void OnToggle(bool value)
    {
        // Ignore programmatic changes during Render (guarded by _suppress).
        if (_suppress) return;
        inspector.SetEmpowered(value);
    }

    bool _suppress;

    void Render()
    {
        var sel = inspector.Selection;
        bool show = sel != null;
        root.SetActive(show);
        if (!show) return;

        empowerToggle.interactable = sel.CanEmpower();

        _suppress = true;
        empowerToggle.isOn = sel.EffectiveEmpowered();
        _suppress = false;
    }
}
