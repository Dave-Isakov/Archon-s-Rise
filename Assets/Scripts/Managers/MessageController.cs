using UnityEngine;
using UnityEngine.InputSystem;

// Modal capture for the validation-message popup. While messageCanvas is up this is
// the ONLY surface that reads input: Submit (A/Enter) or Cancel (B/Backspace) both
// dismiss it via ReturnButton(). The gameplay controllers guard on messageCanvas.enabled
// (and swallow the frame it closes on) so nothing underneath reacts while it blocks.
public class MessageController : MonoBehaviour
{
    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.messageCanvas.enabled) return;

        if (GameControls.Gameplay.Submit.WasPressedThisFrame()
            || GameControls.Gameplay.Cancel.WasPressedThisFrame())
            gm.ReturnButton();
    }
}
