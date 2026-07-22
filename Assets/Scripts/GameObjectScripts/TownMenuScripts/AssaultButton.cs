using UnityEngine;

// Shown only for a guarded, not-yet-conquered place (a Town's empty roster is
// conquered immediately, so it never shows one). Assaulting is free; the cost
// is fighting the roster — or 3 wounds to retreat mid-assault. Closing the
// menu without assaulting costs nothing.
public class AssaultButton : TownButtons
{
    public override void UpdateButtonText()
    {
        if (_town is null) return;

        bool show = !ConquestTracker.Instance.IsConquered(_town.gridPos);
        thisButton.gameObject.SetActive(show);
        if (!show) return;

        int remaining = _town.townSO.guardians.Count
                        - ConquestTracker.Instance.DefeatedCount(_town.gridPos);
        buttonText.text =
            $"{IconMarkup.Tag(IconConcept.Attack)} Assault ({remaining} <sprite=\"guardian\" index=0>)";
        thisButton.interactable = CanActThisVisit;
        SyncLock();
        thisButton.onClick.RemoveAllListeners();
        thisButton.onClick.AddListener(() => GuardianAssault.Instance.Begin(_town));
    }
}
