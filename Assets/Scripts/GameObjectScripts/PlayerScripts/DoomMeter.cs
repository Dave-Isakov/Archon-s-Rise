using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD doom meter: fill bar + "12/20" label. Driven by the OnDoomChanged
// IntEvent via an IntListener (no per-frame polling).
public class DoomMeter : MonoBehaviour
{
    [SerializeField] Image fill;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] DoomTuningSO tuning;

    public void OnDoomChanged(int doom)
    {
        int max = tuning.tuning.doomMax;
        label.text = $"{IconMarkup.Tag(IconConcept.Doom)} {doom}/{max}";
        fill.fillAmount = max > 0 ? (float)doom / max : 0f;
    }
}
