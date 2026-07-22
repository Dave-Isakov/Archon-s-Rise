using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealButton : TownButtons
{
    private void Update() {
        if (_town is not null)
        {
            if(currentPlayerInfluence < _town.townSO.healLevel || !CanActThisVisit)
                thisButton.interactable = false;
            SyncLock();
        }
    }
    public override void UpdateButtonText()
    {
        if (_town is not null)
        {
            buttonText.text =
                $"{IconMarkup.Tag(IconConcept.Heal)} Heal — {IconMarkup.Cost(IconConcept.Influence, _town.townSO.healLevel)}";
            bool allowed = PlaceRules.AllowedServices(_town.townSO.placeType).HasFlag(PlaceService.Heal);
            bool open = ConquestTracker.Instance.IsConquered(_town.gridPos);
            if (allowed && open)
            {
                thisButton.gameObject.SetActive(true);
                if(currentPlayerInfluence < _town.townSO.healLevel)
                {
                    thisButton.interactable = false;
                }
                else
                    thisButton.interactable = true;
                    thisButton.onClick.RemoveAllListeners();
                    thisButton.onClick.AddListener(() => townEvent.Raise(_town));
                    thisButton.onClick.AddListener(() => influenceCostEvent.Raise(_town.townSO.healLevel));
                    // Healing is the visit's committed action (spec 2026-07-22).
                    thisButton.onClick.AddListener(() => {
                        if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
                    });
            }
            else
            {
                thisButton.gameObject.SetActive(false);
            }
            SyncLock();
        }
    }
}
