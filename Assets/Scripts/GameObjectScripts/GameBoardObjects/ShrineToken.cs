using System.Collections.Generic;
using UnityEngine;
using ArchonsRise.HexTooltipInfo;
using ArchonsRise.Shrines;

// Map-side shrine identity (spec 2026-07-24). Entry lives in PlaceTokenBase
// (spec 2026-07-28). A spent shrine shows a LOCKED Engage slot rather than
// firing a message — the same way towns and dungeons show state — and a GUARDED
// one swaps that slot for the guardian fight it now hosts (2026-07-31).
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
            isGuarded: ShrineTracker.Instance.IsGuarding(gridPos),
            crystalCost: shrineSO.crystalCost,
            visitCanAct: CanActThisVisit));

    public override void Dispatch(PlaceActionId id)
    {
        if (id == PlaceActionId.Engage)
        {
            FindAnyObjectByType<ShrinePanel>(FindObjectsInactive.Include).Open(this);
            return;
        }

        // Assault: the guardian a sour bargain left behind. Opening is a free
        // look either way — with the turn's action still in hand it opens as a
        // real fight that commits on the first Engage/Siege/Influence; with the
        // action already spent it opens preview-only (2026-07-31).
        if (id != PlaceActionId.Assault) return;

        if (!CanActThisVisit)
        {
            BeginGuardianFight(previewOnly: true, onCommit: null);
            return;
        }
        BeginGuardianFight(previewOnly: false,
            onCommit: () => { if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction(); });
    }

    // The shrine's guardian fight. Two callers: ShrinePanel the moment a bargain
    // sours (where the action and crystals are already paid, so onCommit is
    // null), and the Assault slot on any later visit.
    //
    // The guardian is authored on the shrine and fought FROM the shrine — it is
    // never placed on the map, so there is no token to find, no cell to pick,
    // and no way for it to drift off the shrine it belongs to.
    public void BeginGuardianFight(bool previewOnly, System.Action onCommit)
    {
        if (shrineSO == null || shrineSO.summonedEnemy == null)
        {
            // NOT PlaceName: it reads through shrineSO, which may be the null here.
            Debug.LogWarning($"Shrine guardian: '{name}' has no summonedEnemy authored — nothing to fight.");
            return;
        }

        if (previewOnly)
            GameLog.Instance.Post("You've already acted this turn — you can study the guardian, but not fight it.");

        // Fixed tier-3 from the authored asset: no doom stat bonus (spec §3).
        var spawns = new List<CombatController.EnemySpawn>
        {
            new CombatController.EnemySpawn(shrineSO.summonedEnemy, 0, 0)
        };

        GameManager.Instance.CombatCanvasActive();
        CombatController.Instance.OpenFight(spawns, CombatContext.Shrine,
            shrineToken: this, onCommit: onCommit, previewOnly: previewOnly);
    }

    public void RefreshVisual()
    {
        var s = ShrineTracker.Instance.State(gridPos);
        if (liveMarker != null) liveMarker.SetActive(s == ShrineVisualState.Live);
        if (dormantMarker != null) dormantMarker.SetActive(s == ShrineVisualState.ConsumedDormant);
        if (guardingMarker != null) guardingMarker.SetActive(s == ShrineVisualState.Guarding);
    }
}
