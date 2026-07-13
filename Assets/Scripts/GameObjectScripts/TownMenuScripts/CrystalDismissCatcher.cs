using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Invisible full-panel catcher rendered behind the crystal pop-out. While the pop-out is
// open its raycast is enabled; clicking anywhere that isn't a crystal (i.e. "off" the
// pop-out) dismisses the crystals via CreateCrystalButtons.HideAll without spending influence.
// The GameObject stays active while hidden (only the raycast is toggled) so its show/hide
// event listeners keep receiving events.
public class CrystalDismissCatcher : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Graphic raycastArea; // transparent Image; raycastTarget toggled to show/hide

    // Wire to the same event that reveals the crystals (OnCrystalButtonClick_CreateCrystalButtons).
    public void Show()
    {
        if (raycastArea != null) raycastArea.raycastTarget = true;
    }

    // Wire to onClick_CloseTownMenu; also called by CreateCrystalButtons.HideAll on purchase.
    public void Hide()
    {
        if (raycastArea != null) raycastArea.raycastTarget = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        CreateCrystalButtons.HideAll();
    }
}
