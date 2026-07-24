using TMPro;
using UnityEngine;

// HUD toughness readout, modelled on DoomMeter: driven by an IntEvent via an
// IntListener, never per-frame polling.
//
// Renders the WORD plus the number, deliberately not the `hp` glyph — that icon
// means enemy HP, a depleting pool, and borrowing it would re-assert exactly the
// equivalence the Toughness rename exists to break (spec Part T).
public class ToughnessLabel : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;

    public void OnToughnessChanged(int toughness)
    {
        label.text = $"Toughness {toughness}";
    }
}
