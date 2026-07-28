using System.Collections.Generic;
using UnityEngine;
using ArchonsRise.HexTooltipInfo;

// Map-side town/keep/castle identity. Entry, registry and fan handling live in
// PlaceTokenBase (spec 2026-07-28); this class carries only what is town-specific.
public class TownToken : PlaceTokenBase
{
    public TownsSO townSO;
    [SerializeField] TownDeck deck;
    [SerializeField] TownEvent onClick_OpenTownMenu;
    [SerializeField] TownEvent onClick_GetTownData;
    [SerializeField] IntEvent healInfluenceCostEvent; // the SPEND event, not GetCurrentInfluence
    [SerializeField] TownEvent healTownEvent;
    [SerializeField] TownEvent onCrystalButtonClick;  // reveals the crystal pop-out

    protected override string PlaceName => townSO.cardName;

    protected override void OnStart()
    {
        ConquestTracker.Instance.Register(gridPos, townSO.placeType, townSO.guardians.Count);
    }

    public override HexDescriptor Describe()
        => new HexDescriptor(
            TileDescriptor.Town(townSO.cardName, PlaceTypeIcon(townSO.placeType),
                ConquestTracker.Instance.IsConquered(gridPos)),
            TileDescriptor.PlacePriority);

    private static IconConcept PlaceTypeIcon(PlaceType type)
    {
        switch (type)
        {
            case PlaceType.Keep:   return IconConcept.Keep;
            case PlaceType.Castle: return IconConcept.Castle;
            default:               return IconConcept.Town;
        }
    }

    public override List<PlaceAction> BuildActions()
    {
        int influence = PlayerStats != null ? PlayerStats.playerInfluence : 0;

        bool anyUnitAffordable = townSO.recruitableUnits.Exists(
            u => u != null && u.influenceCost <= influence);

        return PlaceActionRules.ForTown(new TownActionSnapshot(
            placeType: townSO.placeType,
            conquered: ConquestTracker.Instance.IsConquered(gridPos),
            guardiansRemaining: townSO.guardians.Count
                                - ConquestTracker.Instance.DefeatedCount(gridPos),
            influence: influence,
            healCost: townSO.healLevel,
            crystalCost: townSO.resourceLevel,
            anyUnitAffordable: anyUnitAffordable,
            visitCanAct: CanActThisVisit,
            hasMenu: true));
    }

    public override void Dispatch(PlaceActionId id)
    {
        switch (id)
        {
            case PlaceActionId.Assault:
                GuardianAssault.Instance.Begin(this);
                break;

            case PlaceActionId.Heal:
                // Same three effects the old HealButton wired, in the same order.
                if (healTownEvent != null) healTownEvent.Raise(this);
                if (healInfluenceCostEvent != null) healInfluenceCostEvent.Raise(townSO.healLevel);
                if (TurnPhaseController.Instance != null)
                    TurnPhaseController.Instance.CommitVisitAction();
                break;

            case PlaceActionId.Recruit:
                // Resolved at click time, not serialized: this token is spawned from
                // a prefab asset (GridGeneration) while RecruitPanel lives only in
                // the scene, so the reference is unauthorable. Same resolution
                // DungeonToken and ShrineToken use for their panels. Once per
                // click, so the scene scan costs nothing.
                FindAnyObjectByType<RecruitPanel>(FindObjectsInactive.Include).Open(this);
                break;

            case PlaceActionId.Crystal:
                OpenCrystalPopout();
                break;

            case PlaceActionId.Cards:
                break; // M2 stub: the slot renders locked and does nothing

            case PlaceActionId.OpenMenu:
                GameManager.Instance.townCanvas.enabled = true;
                deck.CreateTown(this);
                // Revive any button that hid itself on a previous open so its
                // listener re-registers before the events below drive
                // UpdateButtonText.
                TownMenu.Instance.PrepareButtons();
                onClick_GetTownData.Raise(this);
                onClick_OpenTownMenu.Raise(this);
                break;
        }
    }

    // The influence and the turn's action are spent by CrystalButton.OnCrystalPurchased
    // when a colour is picked — not here, so opening the pop-out and clicking away
    // stays free. That handler reads CrystalButton._town, which ONLY
    // onClick_GetTownData sets, so the fan route has to raise it too: without this
    // the purchase silently skips both the deduction and the action commit.
    // PrepareButtons first, because a button that hid itself is not listening.
    private void OpenCrystalPopout()
    {
        TownMenu.Instance.PrepareButtons();
        if (onClick_GetTownData != null) onClick_GetTownData.Raise(this);
        if (onCrystalButtonClick != null) onCrystalButtonClick.Raise(this);
    }
}
