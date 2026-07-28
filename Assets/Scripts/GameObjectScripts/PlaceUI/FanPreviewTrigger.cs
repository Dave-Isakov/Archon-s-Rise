using System.Collections.Generic;
using UnityEngine;

// Hover preview for fan slots (spec 2026-07-28). Extends the shipped
// PreviewTrigger, so the gamepad focus path added at the controller milestone
// drives this with no change here.
//
// Only Assault and Delve preview anything; every other slot returns empty, and
// PreviewTrigger.Focus already no-ops on an empty list.
[RequireComponent(typeof(PlaceFanSlot))]
public class FanPreviewTrigger : PreviewTrigger
{
    static readonly List<EnemyPreviewData> Empty = new List<EnemyPreviewData>();

    PlaceFanSlot slot;
    Camera uiCam;   // the slot's canvas render camera (null under Overlay)

    void Awake()
    {
        slot = GetComponent<PlaceFanSlot>();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) uiCam = canvas.rootCanvas.worldCamera;
    }

    // A slot can disappear while the pointer is still over it: clicking Assault
    // dismisses the fan (root.SetActive(false)), and a shrinking action list
    // deactivates pooled slots. Unity never delivers OnPointerExit to an object
    // that is deactivated under the cursor, so without this the preview would
    // hang on screen — over the combat canvas, with nothing left to close it.
    //
    // The town menu never hit this because it closed with Canvas.enabled = false,
    // which leaves the GameObject active and lets the exit through.
    void OnDisable() => Unfocus();

    protected override IReadOnlyList<EnemyPreviewData> ResolveEntries()
    {
        var host = PlaceFan.Instance != null ? PlaceFan.Instance.CurrentHost : null;
        if (host == null) return Empty;

        if (slot.Action == PlaceActionId.Assault && host is TownToken town)
        {
            if (town.townSO == null) return Empty;
            int defeated = ConquestTracker.Instance.DefeatedCount(town.gridPos);
            var remaining = PreviewRules.RemainingGuardians(town.townSO.guardians, defeated);
            var entries = new List<EnemyPreviewData>(remaining.Count);
            foreach (var g in remaining)
                entries.Add(new EnemyPreviewData(g, 0, 0)); // guardians never doom-scale
            return entries;
        }

        if (slot.Action == PlaceActionId.Delve && host is DungeonToken dungeon)
        {
            if (dungeon.dungeonSO == null || !PreviewRules.CanPreview()) return Empty;
            int cleared = DungeonTracker.Instance.DefeatedCount(dungeon.gridPos);
            if (cleared >= dungeon.dungeonSO.enemies.Count) return Empty;
            return new List<EnemyPreviewData>
            {
                new EnemyPreviewData(dungeon.dungeonSO.enemies[cleared], 0, 0),
            };
        }

        return Empty;
    }

    // The slot is UI on a Screen Space - Camera canvas, so its transform.position
    // is world space; convert it to the screen pixels the panel expects.
    protected override Vector3 ScreenPosition()
        => RectTransformUtility.WorldToScreenPoint(uiCam, transform.position);
}
