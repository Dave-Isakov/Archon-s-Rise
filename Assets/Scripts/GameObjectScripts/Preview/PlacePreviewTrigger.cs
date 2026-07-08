using System.Collections.Generic;
using UnityEngine;

// Assault-button preview source: previews all guardians still standing at this
// place, anchored to the button. Sits on the same GameObject as the AssaultButton,
// which already holds the place reference and receives pointer events.
[RequireComponent(typeof(AssaultButton))]
public class PlacePreviewTrigger : PreviewTrigger
{
    AssaultButton assault;
    Camera uiCam;   // the button's canvas render camera (null under Overlay)

    void Awake()
    {
        assault = GetComponent<AssaultButton>();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) uiCam = canvas.rootCanvas.worldCamera;
    }

    protected override IReadOnlyList<EnemyPreviewData> ResolveEntries()
    {
        var town = assault.Town;
        if (town == null || town.townSO == null) return new List<EnemyPreviewData>();
        int defeated = ConquestTracker.Instance.DefeatedCount(town.gridPos);
        var remaining = PreviewRules.RemainingGuardians(town.townSO.guardians, defeated);
        var entries = new List<EnemyPreviewData>(remaining.Count);
        foreach (var g in remaining)
            entries.Add(new EnemyPreviewData(g, 0, 0)); // guardians never doom-scale
        return entries;
    }

    // The button is UI on a Screen Space - Camera canvas, so its transform.position
    // is world space; convert it to the screen pixels the panel expects.
    protected override Vector3 ScreenPosition()
        => RectTransformUtility.WorldToScreenPoint(uiCam, transform.position);
}
