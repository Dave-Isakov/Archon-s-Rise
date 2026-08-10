using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CreateCrystalButtons : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] EmpowerType color;
    [SerializeField] Button thisButton;
    [SerializeField] EmpowerColorEvent onCrystalButtonClick_CreateCrystalOfColor;
    // The pop-out's shared ClickOffCatcher. Assign on at least one crystal so a
    // purchase disarms it too — the catcher only hears about click-off, not about
    // the crystal that was bought, and an armed catcher over a hidden pop-out
    // eats the next click meant for the board. (Replaces CrystalDismissCatcher.Hide.)
    [SerializeField] ClickOffCatcher dismissCatcher;

    // The pop-out's own open state. PlaceFan.OnSlotClicked dismisses the fan BEFORE
    // it dispatches, so PlaceFan.IsOpen is already false by the time the fan route
    // opens the pop-out and can never gate it.
    static bool popoutOpen;

    // Wired as a second response on the crystal pop-out event, alongside the
    // per-crystal set_interactable calls.
    public void OnPopoutOpened() => popoutOpen = true;

    public void OnPointerClick(PointerEventData eventData)
    {
        HideAll();
    }

    // Hide the crystal options (and disarm the click-off catcher) instead of destroying them, so
    // the pop-out can be reopened for another purchase. Hidden = non-interactable; these buttons'
    // disabled color has alpha 0, so a hidden crystal is invisible and unclickable.
    // The shared ClickOffCatcher now owns dismissal (spec 2026-07-28).
    public static void HideAll()
    {
        popoutOpen = false;
        foreach (var crystal in FindObjectsByType<CreateCrystalButtons>(FindObjectsInactive.Include))
        {
            crystal.thisButton.interactable = false;
            if (crystal.dismissCatcher != null) crystal.dismissCatcher.SetArmed(false);
        }
    }

    private void Start() 
    {
        thisButton.onClick.RemoveAllListeners();
        thisButton.onClick.AddListener(() => onCrystalButtonClick_CreateCrystalOfColor.Raise(color));
    }

    // The pop-out used to be reachable only from the town canvas, so this gate
    // keyed off that canvas. The place fan can now open it with the canvas shut
    // (spec 2026-07-28), so the buttons stay live while EITHER route is open and
    // are disarmed by HideAll on purchase or click-off.
    private void Update()
    {
        if (GameManager.Instance.townCanvas.enabled) return;
        if (popoutOpen) return;
        thisButton.interactable = false;
    }
}
