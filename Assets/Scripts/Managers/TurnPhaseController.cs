using UnityEngine;

// Owns the turn/round phase state (spec 2026-07-21). Strict Explore->Action->End
// turns inside a Doom-band-scaled "day". The Explore->Action transition is
// implicit (taking the action); End Turn is the only turn-flow control; the
// round auto-ends when its turn budget is spent or the deck can't refill.
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
    // day, and auto-ends the round when the budget is spent or the deck is dry.
    public void EndTurnPressed()
    {
        GameManager.Instance.commands.ClearStack();

        bool deckCanRefill = RoundRules.DeckCanRefill(CurrentDrawVerdict());
        endTheTurn.Raise(); // pools reset, hand top-up, TurnPlus (existing chain)

        int next = RoundRules.NextTurnsRemaining(TurnsRemaining);
        if (RoundRules.IsRoundOver(next, deckCanRefill))
        {
            endTheRound.Raise(); // reshuffle + Doom tick + unit/skill refresh (existing chain)
            if (RunEndController.HasEnded) return; // Doom tick may have lost the run
            StartRound();        // budget from the post-tick band
        }
        else
        {
            TurnsRemaining = next;
            onTurnsRemainingChanged.Raise(TurnsRemaining);
            BeginTurn();
        }
    }

    // Load path: restore the remaining budget; phase always resets to Explore.
    public void LoadState(int turnsRemaining)
    {
        TurnsRemaining = turnsRemaining;
        onTurnsRemainingChanged.Raise(TurnsRemaining);
        BeginTurn();
    }

    void StartRound()
    {
        int doom = DoomClock.Instance != null ? DoomClock.Instance.Doom : 0;
        TurnsRemaining = DoomRules.TurnsForBand(doom, doomTuning.tuning);
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

    DrawVerdict CurrentDrawVerdict()
    {
        var deck = FindAnyObjectByType<PlayerDeck>();
        var hand = FindAnyObjectByType<PlayerHand>();
        var player = FindAnyObjectByType<Player>();
        if (deck == null || hand == null || player == null) return DrawVerdict.Draw;
        return DrawGate.Evaluate(deck.CardsInDeck.Count, hand.cardsInPlay.Count, player.PlayerHandSize);
    }
}
