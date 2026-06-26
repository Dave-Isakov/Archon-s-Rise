using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardButton : TownButtons
{
    private void Update() {
        if (_town is not null)
            if(currentPlayerInfluence < _town.townSO.cardLevel)
                thisButton.interactable = false;
    }
    public override void UpdateButtonText()
    {
        if (_town is not null)
        {
            buttonText.text = "Card " + _town.townSO.cardLevel.ToString();
            if (_town.townSO.activity.HasFlag(TownsSO.TownActivity.Cards))
            {
                thisButton.gameObject.SetActive(true);
                if(currentPlayerInfluence < _town.townSO.cardLevel)
                {
                    thisButton.interactable = false;
                }
                else
                    thisButton.interactable = true;
                    thisButton.onClick.RemoveAllListeners();
                    thisButton.onClick.AddListener(() => townEvent.Raise(_town));
                    thisButton.onClick.AddListener(() => influenceCostEvent.Raise(_town.townSO.cardLevel));
            }
            else
            {
                thisButton.gameObject.SetActive(false);
            }
        }
    }
}
