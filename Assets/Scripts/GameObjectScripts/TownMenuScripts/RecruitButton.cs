using UnityEngine;

public class RecruitButton : TownButtons
{
    [SerializeField] DisbandPanel disbandPanel;

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
            bool allowed = PlaceRules.AllowedServices(_town.townSO.placeType).HasFlag(PlaceService.Recruit);
            bool open = ConquestTracker.Instance.IsConquered(_town.gridPos);
            if (allowed && open)
            {
                thisButton.gameObject.SetActive(true);
                if(currentPlayerInfluence < _town.townSO.recruitLevel)
                {
                    thisButton.interactable = false;
                }
                else
                    thisButton.interactable = true;
                    thisButton.onClick.RemoveAllListeners();
                    thisButton.onClick.AddListener(Recruit);
            }
            else
            {
                thisButton.gameObject.SetActive(false);
            }
        }
    }

    // At the army cap the hire needs a disband first; below it, the original
    // two-event flow runs unchanged.
    private void Recruit()
    {
        var player = FindAnyObjectByType<Player>();
        if (ArmyRules.NeedsDisband(player.Units.Count, player.ArmyCap))
        {
            disbandPanel.Open(_town);
            return;
        }
        townEvent.Raise(_town);
        influenceCostEvent.Raise(_town.townSO.recruitLevel);
    }
}
