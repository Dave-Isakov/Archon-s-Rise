using UnityEngine;

public class CardButton : TownButtons
{
    private void Update()
    {
        if (_town is not null)
        {
            if (currentPlayerInfluence < _town.townSO.cardLevel || !CanActThisVisit)
                thisButton.interactable = false;
            SyncLock();
        }
    }

    public override void UpdateButtonText()
    {
        if (_town is null) return;

        buttonText.text =
            $"{IconMarkup.Tag(IconConcept.Card)} Cards — {IconMarkup.Cost(IconConcept.Influence, _town.townSO.cardLevel)}";
        bool sells = _town.townSO.purchasableCards != null && _town.townSO.purchasableCards.Count > 0;
        bool open = ConquestTracker.Instance.IsConquered(_town.gridPos);
        if (sells && open)
        {
            thisButton.gameObject.SetActive(true);
            thisButton.interactable = currentPlayerInfluence >= _town.townSO.cardLevel;
            thisButton.onClick.RemoveAllListeners();
            thisButton.onClick.AddListener(() => _town.BuyCards());
        }
        else
        {
            thisButton.gameObject.SetActive(false);
        }
        SyncLock();
    }
}
