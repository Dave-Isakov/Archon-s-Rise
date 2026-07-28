using System.Collections.Generic;
using UnityEngine;
using ArchonsRise.HexTooltipInfo;

// Map-side dungeon identity (M2.9). Entry, registry and fan handling all live in
// PlaceTokenBase (spec 2026-07-28); this class carries only what is dungeon-specific:
// its SO, its visual state markers, and what its fan offers.
public class DungeonToken : PlaceTokenBase
{
    public DungeonsSO dungeonSO;
    [SerializeField] GameObject flagMarker;    // active while flagged, until cleared
    [SerializeField] GameObject clearedMarker; // active once complete
    [SerializeField] VoidEvent onDungeonOpenTutorial; // M2.12 one-shot, raised on fan open

    protected override string PlaceName => dungeonSO.cardName;

    protected override void OnStart()
    {
        DungeonTracker.Instance.Register(gridPos, dungeonSO.id);
        RefreshVisual();
    }

    public override HexDescriptor Describe()
        => new HexDescriptor(
            TileDescriptor.Dungeon(dungeonSO.cardName,
                DungeonTracker.Instance.DefeatedCount(gridPos), DungeonRules.DelveCount),
            TileDescriptor.PlacePriority);

    public override List<PlaceAction> BuildActions()
        => PlaceActionRules.ForDungeon(new DungeonActionSnapshot(
            complete: DungeonTracker.Instance.IsComplete(gridPos),
            explore: PlayerStats != null ? PlayerStats.PlayerExplore : 0,
            delveCost: dungeonSO.exploreCost,
            visitCanAct: CanActThisVisit,
            hasMenu: true));

    public override void Dispatch(PlaceActionId id)
    {
        switch (id)
        {
            case PlaceActionId.Delve:
                DungeonPanel.PerformDelve(this);
                break;

            case PlaceActionId.OpenMenu:
                FindAnyObjectByType<DungeonPanel>(FindObjectsInactive.Include).Open(this);
                break;
        }
    }

    // The M2.12 one-shot used to key off DungeonPanel.Open. The fan is the normal
    // path now, so it fires here instead — otherwise it would never fire again
    // once players stop opening the panel.
    protected override void OnFanOpening()
    {
        if (onDungeonOpenTutorial != null) onDungeonOpenTutorial.Raise();
    }

    public void RefreshVisual()
    {
        bool complete = DungeonTracker.Instance.IsComplete(gridPos);
        if (clearedMarker != null) clearedMarker.SetActive(complete);
        if (flagMarker != null) flagMarker.SetActive(!complete && DungeonTracker.Instance.IsFlagged(gridPos));
    }
}
