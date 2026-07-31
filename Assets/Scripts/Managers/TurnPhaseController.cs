using UnityEngine;

// Owns the turn/round phase state (spec 2026-07-21). Strict Explore->Action->End
// turns inside a Doom-band-scaled "day". The Explore->Action transition is
// implicit (taking the action); End Turn is the only turn-flow control; the
// round auto-ends when its turn budget is spent, or one turn after the deck
// first fails to fully refill the hand (spec 2026-07-30's one-turn buffer).
public class TurnPhaseController : MonoBehaviour
{
    public static TurnPhaseController Instance { get; private set; }

    [SerializeField] DoomTuningSO doomTuning;      // per-band turn budgets
    [SerializeField] VoidEvent onPhaseChanged;
    [SerializeField] IntEvent onTurnsRemainingChanged;
    [SerializeField] VoidEvent endTheTurn;         // existing turn-end listener chain
    [SerializeField] VoidEvent endTheRound;        // existing round-end listener chain

    public TurnPhase CurrentPhase { get; private set; }
    public int TurnsRemaining { get; private set; }
    // True entering a turn means last turn's end-of-turn hand top-up came up
    // short (deck couldn't fully refill the hand) — this turn's End Turn ends
    // the round no matter how much of the hand gets played (spec 2026-07-30).
    public bool DeckShortfallPending { get; private set; }
    bool actionTaken;
    bool visitCanAct;

    public bool CanMove     => TurnPhaseRules.CanMove(CurrentPhase);
    public bool CanInteract => TurnPhaseRules.CanInteract(CurrentPhase, actionTaken);

    // True while an open place/dungeon menu is allowed to spend the turn's action —
    // snapshotted at menu open by BeginVisit (spec 2026-07-22).
    public bool VisitCanAct => visitCanAct;

    void Awake() { Instance = this; }

    void Start()
    {
        // A load path calls LoadState before the first Start-driven round; guard so
        // we don't stomp a restored budget.
        if (TurnsRemaining <= 0) StartRound();
        else BeginTurn();
    }

    // Implicit Explore->Action: committing the movement stack (can't undo the path
    // once you commit to the encounter/visit), spend the action, enter Action.
    public void BeginAction()
    {
        GameManager.Instance.commands.ClearStack();
        actionTaken = true;
        SetPhase(TurnPhase.Action);
    }

    // A place/dungeon menu opened (spec 2026-07-22). Opening is a free peek — it no
    // longer spends the action. Snapshot whether this visit may act (only if the
    // turn's action is still unspent); the first service committed inside does the
    // real BeginAction via CommitVisitAction.
    public void BeginVisit() => visitCanAct = CanInteract;

    // A service inside the open menu was committed. The first one spends the turn's
    // action; every later service in the SAME visit rides on it (a whole visit is
    // one action), so this is a no-op once the action is spent.
    public void CommitVisitAction()
    {
        if (visitCanAct && !actionTaken) BeginAction();
    }

    // The only turn-flow control. Commits, runs the turn-end chain, decrements the
    // day, and auto-ends the round when the budget is spent or last turn's hand
    // refill already fell short (spec 2026-07-30).
    public void EndTurnPressed()
    {
        GameManager.Instance.commands.ClearStack();

        // Read after the commit above (a just-played card has already left the
        // hand by here), so "needed draws" reflects what this turn's top-up will
        // actually attempt — but decide THIS press's round-over using LAST turn's
        // outcome, not this one (that's the one-turn buffer).
        var hand = FindAnyObjectByType<PlayerHand>();
        bool refillFallsShortThisTurn = hand != null && !hand.CanFullyRefillAtTurnEnd();
        bool forceRoundOver = DeckShortfallPending;

        HarvestHotspotIfParked();
        endTheTurn.Raise(); // pools reset, hand top-up, TurnPlus (existing chain)

        int next = RoundRules.NextTurnsRemaining(TurnsRemaining);
        if (RoundRules.IsRoundOver(next, forceRoundOver))
        {
            DeckShortfallPending = false; // fresh round, fresh deck
            endTheRound.Raise(); // reshuffle + Doom tick + unit/skill refresh (existing chain)
            if (RunEndController.HasEnded) return; // Doom tick may have lost the run
            StartRound();        // budget from the post-tick band
        }
        else
        {
            TurnsRemaining = next;
            onTurnsRemainingChanged.Raise(TurnsRemaining);
            DeckShortfallPending = refillFallsShortThisTurn; // carried into next turn
            BeginTurn();
        }
    }

    // Free passive (spec 2026-07-24): if the player ends a turn parked on a live
    // crystal hotspot, grant 1 crystal of its color and spend a charge. No modal
    // and no RewardQueue — the HUD crystal count + the token pip are the feedback
    // (memory map-feedback-tooltip-and-log).
    private void HarvestHotspotIfParked()
    {
        var pp = FindAnyObjectByType<PlayerPosition>();
        var grid = FindAnyObjectByType<Grid>();
        if (pp == null || grid == null) return;
        var cell = grid.LocalToCell(pp.transform.position);
        if (!HotspotTracker.Instance.CanHarvest(cell)) return;

        var color = HotspotTracker.Instance.ColorAt(cell);
        FindAnyObjectByType<CrystalInventory>().CreateCrystal(color);
        HotspotTracker.Instance.Harvest(cell);
        HotspotTracker.Instance.RefreshTokenVisuals();
    }

    // Load path: restore the remaining budget and the deck-shortfall buffer state;
    // phase always resets to Explore.
    public void LoadState(int turnsRemaining, bool deckShortfallPending)
    {
        TurnsRemaining = turnsRemaining;
        DeckShortfallPending = deckShortfallPending;
        onTurnsRemainingChanged.Raise(TurnsRemaining);
        BeginTurn();
    }

    void StartRound()
    {
        int doom = DoomClock.Instance != null ? DoomClock.Instance.Doom : 0;
        TurnsRemaining = DoomRules.TurnsForBand(doom, doomTuning.tuning);
        DeckShortfallPending = false; // fresh round always starts with a full deck
        onTurnsRemainingChanged.Raise(TurnsRemaining);
        BeginTurn();
    }

    void BeginTurn()
    {
        actionTaken = false;
        SetPhase(TurnPhase.Explore);
    }

    void SetPhase(TurnPhase phase)
    {
        CurrentPhase = phase;
        if (onPhaseChanged != null) onPhaseChanged.Raise();
    }

    // Whether ending the CURRENT turn (right now, mid-turn) would end the round —
    // used by EndTurnButton's label. Mirrors EndTurnPressed's own decision.
    public bool NextPressEndsRound =>
        RoundRules.IsRoundOver(RoundRules.NextTurnsRemaining(TurnsRemaining), DeckShortfallPending);
}
