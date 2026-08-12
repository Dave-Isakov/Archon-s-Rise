using TMPro;
using UnityEngine;

// Army size readout: "army 1/2".
//
// Update-polled rather than event-driven on purpose. An IntEvent + listener
// means more hand-wiring in the scene and re-exposes the Static-vs-Dynamic
// dropdown footgun that silently pins listeners to 0. HealButton, CombatButtons
// and CombatController all already refresh per frame.
public class ArmyHud : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;
    // Optional. "2/2" is ambiguous between "full army" and "full army, both
    // down", so this renders a wounded count when there is one. Leave
    // unassigned to hide it entirely.
    [SerializeField] TextMeshProUGUI woundedLabel;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color atCapColor = new Color(0.95f, 0.80f, 0.30f);

    Player player;

    void Update()
    {
        if (label == null) return;
        if (player == null) player = FindAnyObjectByType<Player>();
        if (player == null) return;

        int count = player.Units.Count;
        int cap = player.ArmyCap;

        label.text = $"{IconMarkup.Tag(IconConcept.Army)} {count}/{cap}";
        // Same predicate the recruit flow uses to decide whether hiring opens the
        // disband picker, so the HUD and that flow can never disagree.
        label.color = ArmyRules.NeedsDisband(count, cap) ? atCapColor : normalColor;

        if (woundedLabel == null) return;
        int wounded = 0;
        foreach (var unit in FindObjectsByType<Unit>())
            if (unit.IsWounded) wounded++;
        woundedLabel.gameObject.SetActive(wounded > 0);
        woundedLabel.text = $"{wounded} {IconMarkup.Tag(IconConcept.Wound)}";
    }
}
