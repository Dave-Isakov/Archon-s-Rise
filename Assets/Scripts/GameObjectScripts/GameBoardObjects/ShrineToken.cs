using System.Collections.Generic;
using UnityEngine;
using ArchonsRise.HexTooltipInfo;
using ArchonsRise.Shrines;

// Map-side shrine identity (spec 2026-07-24). Entry lives in PlaceTokenBase
// (spec 2026-07-28). A spent or guarded shrine now shows a LOCKED Engage slot
// rather than firing a message — the same way towns and dungeons show state.
public class ShrineToken : PlaceTokenBase
{
    public ShrineSO shrineSO;
    [SerializeField] GameObject liveMarker;
    [SerializeField] GameObject dormantMarker;
    [SerializeField] GameObject guardingMarker;

    protected override string PlaceName => shrineSO.cardName;

    protected override void OnStart()
    {
        ShrineTracker.Instance.Register(gridPos, shrineSO.id);
        RefreshVisual();
    }

    public override HexDescriptor Describe()
        => new HexDescriptor(
            TileDescriptor.Shrine(ShrineTracker.Instance.State(gridPos), shrineSO.crystalCost),
            TileDescriptor.PlacePriority);

    public override List<PlaceAction> BuildActions()
        => PlaceActionRules.ForShrine(new ShrineActionSnapshot(
            isLive: ShrineTracker.Instance.State(gridPos) == ShrineVisualState.Live,
            crystalCost: shrineSO.crystalCost,
            visitCanAct: CanActThisVisit));

    public override void Dispatch(PlaceActionId id)
    {
        if (id != PlaceActionId.Engage) return;
        FindAnyObjectByType<ShrinePanel>(FindObjectsInactive.Include).Open(this);
    }

    public void RefreshVisual()
    {
        var s = ShrineTracker.Instance.State(gridPos);
        if (liveMarker != null) liveMarker.SetActive(s == ShrineVisualState.Live);
        if (dormantMarker != null) dormantMarker.SetActive(s == ShrineVisualState.ConsumedDormant);
        if (guardingMarker != null) guardingMarker.SetActive(s == ShrineVisualState.Guarding);
    }
}
