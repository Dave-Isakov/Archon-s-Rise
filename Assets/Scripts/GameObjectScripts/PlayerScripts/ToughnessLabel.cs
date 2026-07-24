using TMPro;
using UnityEngine;

// HUD toughness readout, modelled on DoomMeter: driven by an IntEvent via an
// IntListener, never per-frame polling.
//
// Renders just the number.
public class ToughnessLabel : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;

    public void OnToughnessChanged(int toughness)
    {
        label.text = $"{toughness}";
    }
}
