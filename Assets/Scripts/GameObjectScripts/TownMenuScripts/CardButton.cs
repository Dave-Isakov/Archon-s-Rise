using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardButton : TownButtons
{
    public override void UpdateButtonText()
    {
        if (_town is not null)
        {
            bool allowed = PlaceRules.AllowedServices(_town.townSO.placeType).HasFlag(PlaceService.Cards);
            bool open = ConquestTracker.Instance.IsConquered(_town.gridPos);
            if (allowed && open)
            {
                // M2 stub: the Castle card shop is a deferred follow-up. The
                // button is present so the service slot is visible, but buying
                // is disabled until the purchase economics land.
                thisButton.gameObject.SetActive(true);
                buttonText.text = "Cards (soon)";
                thisButton.interactable = false;
                thisButton.onClick.RemoveAllListeners();
            }
            else
            {
                thisButton.gameObject.SetActive(false);
            }
        }
    }
}
