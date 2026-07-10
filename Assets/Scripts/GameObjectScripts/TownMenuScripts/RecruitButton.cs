using UnityEngine;

public class RecruitButton : TownButtons
{
    [SerializeField] RecruitPanel recruitPanel;

    // Enabled when at least one listed unit is affordable (per-unit pricing —
    // the town's recruitLevel is retired as the price).
    bool AnyAffordable()
        => _town.townSO.recruitableUnits.Exists(u => u != null && u.influenceCost <= currentPlayerInfluence);

    private void Update()
    {
        if (_town is not null)
            thisButton.interactable = AnyAffordable();
    }

    public override void UpdateButtonText()
    {
        if (_town is not null)
        {
            buttonText.text = "Recruit";
            bool allowed = PlaceRules.AllowedServices(_town.townSO.placeType).HasFlag(PlaceService.Recruit);
            bool open = ConquestTracker.Instance.IsConquered(_town.gridPos);
            if (allowed && open)
            {
                thisButton.gameObject.SetActive(true);
                thisButton.interactable = AnyAffordable();
                thisButton.onClick.RemoveAllListeners();
                thisButton.onClick.AddListener(() => recruitPanel.Open(_town));
            }
            else
            {
                thisButton.gameObject.SetActive(false);
            }
        }
    }
}
