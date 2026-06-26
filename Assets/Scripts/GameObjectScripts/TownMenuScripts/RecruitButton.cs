using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecruitButton : TownButtons
{
    private void Update() {
        if (_town is not null)
            if(currentPlayerInfluence < _town.townSO.recruitLevel)
                thisButton.interactable = false;
    }
    public override void UpdateButtonText()
    {
        if (_town is not null)
        {
            buttonText.text = "Recruit " + _town.townSO.recruitLevel.ToString();
            if (_town.townSO.activity.HasFlag(TownsSO.TownActivity.Recruit))
            {
                thisButton.gameObject.SetActive(true);
                if(currentPlayerInfluence < _town.townSO.recruitLevel)
                {
                    thisButton.interactable = false;
                }
                else
                    thisButton.interactable = true;
                    thisButton.onClick.RemoveAllListeners();
                    thisButton.onClick.AddListener(() => townEvent.Raise(_town));
                    thisButton.onClick.AddListener(() => influenceCostEvent.Raise(_town.townSO.recruitLevel));
            }
            else
            {
                thisButton.gameObject.SetActive(false);
            }
        }
    }
}
