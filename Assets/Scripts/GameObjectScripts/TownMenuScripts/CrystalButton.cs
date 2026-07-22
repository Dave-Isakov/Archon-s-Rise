using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrystalButton : TownButtons
{
    private void Update() {
        if (_town is not null)
        {
            if(currentPlayerInfluence < _town.townSO.resourceLevel || !CanActThisVisit)
                thisButton.interactable = false;
            SyncLock();
        }
    }
    public override void UpdateButtonText()
    {
        if (_town is null) return;

        buttonText.text =
            $"{IconMarkup.Tag(IconConcept.Crystal)} Crystal — {IconMarkup.Cost(IconConcept.Influence, _town.townSO.resourceLevel)}";
        bool allowed = PlaceRules.AllowedServices(_town.townSO.placeType).HasFlag(PlaceService.Crystal);
        bool open = ConquestTracker.Instance.IsConquered(_town.gridPos);
        if (allowed && open)
        {
            thisButton.gameObject.SetActive(true);
            thisButton.interactable = currentPlayerInfluence >= _town.townSO.resourceLevel;

            // Clicking only reveals the crystal options; influence is not spent until the
            // player actually picks a crystal (OnCrystalPurchased).
            thisButton.onClick.RemoveAllListeners();
            thisButton.onClick.AddListener(() => townEvent.Raise(_town));
        }
        else
        {
            thisButton.gameObject.SetActive(false);
        }
        SyncLock();
    }

    // Fired when the player selects one of the pop-out crystals. Deducting here (rather than
    // on the Crystal button press) means opening the pop-out and then closing it or clicking
    // away costs nothing, and each purchase spends exactly one crystal's worth of influence.
    public void OnCrystalPurchased()
    {
        if (_town is not null)
            influenceCostEvent.Raise(_town.townSO.resourceLevel);
        // Buying a crystal is the visit's committed action (spec 2026-07-22). The
        // Crystal button is gated on the visit still owning the action, so the
        // pop-out only ever opens when this commit is allowed.
        if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
    }
}
