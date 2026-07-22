using TMPro;
using UnityEngine;

// Event-driven HUD (spec 2026-07-21): a "Turns left" day countdown (repurposed
// from the old Round/Turn label) plus a phase label. Updated off the controller's
// events, never per-frame.
public class PhaseHud : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI turnsLeftText; // the repurposed Round/Turn TMP
    [SerializeField] TextMeshProUGUI phaseText;     // new TMP beside it

    // Wired to onTurnsRemainingChanged (IntListener, dynamic int arg).
    public void OnTurnsRemainingChanged(int turnsLeft)
    {
        if (turnsLeftText != null) turnsLeftText.text = "Turns left: " + turnsLeft;
    }

    // Wired to onPhaseChanged (VoidListener).
    public void OnPhaseChanged()
    {
        if (phaseText == null || TurnPhaseController.Instance == null) return;
        phaseText.text = "Phase: " + TurnPhaseController.Instance.CurrentPhase;
    }

    // Wired to onCombatPhaseChanged (VoidListener, spec 2026-07-22). The one phase
    // label doubles as the combat sub-phase readout so the HUD never shows two
    // competing phase labels. On Resolved we fall back to the turn phase, which is
    // Action after a fight — so leaving combat reads "Phase: Action".
    public void OnCombatPhaseChanged()
    {
        if (phaseText == null || CombatController.Instance == null) return;
        CombatPhase cp = CombatController.Instance.Phase;
        if (cp == CombatPhase.Resolved) { OnPhaseChanged(); return; }
        phaseText.text = "Phase: " + cp; // Siege / Defend / Attack
    }
}
