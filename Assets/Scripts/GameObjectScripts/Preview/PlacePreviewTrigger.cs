using System.Collections.Generic;
using UnityEngine;

// Assault-button preview source: previews all guardians still standing at this
// place, anchored to the button. Sits on the same GameObject as the AssaultButton,
// which already holds the place reference and receives pointer events.
[RequireComponent(typeof(AssaultButton))]
public class PlacePreviewTrigger : PreviewTrigger
{
    AssaultButton assault;

    void Awake() => assault = GetComponent<AssaultButton>();

    protected override IReadOnlyList<EnemiesSO> ResolveEnemies()
    {
        var town = assault.Town;
        if (town == null || town.townSO == null) return new List<EnemiesSO>();
        int defeated = ConquestTracker.Instance.DefeatedCount(town.gridPos);
        return PreviewRules.RemainingGuardians(town.townSO.guardians, defeated);
    }

    // The Assault button is UI on an overlay canvas, so its transform.position is
    // already in screen coordinates.
    protected override Vector3 ScreenPosition() => transform.position;
}
