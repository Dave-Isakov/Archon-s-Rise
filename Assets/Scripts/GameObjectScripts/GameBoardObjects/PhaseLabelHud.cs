using TMPro;
using UnityEngine;

// Combat HUD phase caption (spec 2026-07-21, Spec 2). Driven by the
// onCombatPhaseChanged VoidEvent: wire a GameEventListener's response to Refresh,
// which reads CombatController.Instance.Phase and captions the current sub-phase.
public class PhaseLabelHud : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;

    void Reset() { label = GetComponent<TextMeshProUGUI>(); }

    // VoidEvent listener response. Blanks the caption once combat resolves.
    public void Refresh()
    {
        if (label == null || CombatController.Instance == null) return;
        CombatPhase phase = CombatController.Instance.Phase;
        if (phase == CombatPhase.Siege)       label.text = "Siege Phase";
        else if (phase == CombatPhase.Attack) label.text = "Attack Phase";
        else                                  label.text = "";
    }
}
