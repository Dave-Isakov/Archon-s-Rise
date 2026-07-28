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

    public void OnPointerClick(PointerEventData eventData)
    {
        HideAll();
    }

    // Hide the crystal options (and the click-off catcher) instead of destroying them, so the
    // pop-out can be reopened for another purchase. Hidden = non-interactable; these buttons'
    // disabled color has alpha 0, so a hidden crystal is invisible and unclickable.
    public static void HideAll()
    {
        foreach (var crystal in FindObjectsByType<CreateCrystalButtons>(FindObjectsInactive.Include))
            crystal.thisButton.interactable = false;
        foreach (var catcher in FindObjectsByType<CrystalDismissCatcher>(FindObjectsInactive.Include))
            catcher.Hide();
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
        if (PlaceFan.Instance != null && PlaceFan.Instance.IsOpen) return;
        thisButton.interactable = false;
    }
}
