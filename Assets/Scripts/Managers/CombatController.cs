using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum CombatContext { Field, Guardian, Dungeon }

// Owns one phased fight (spec 2026-07-21, Spec 2): the CombatPhase machine, the
// logical live-enemy set, the per-fight context, and the single multi-purpose
// button. Engage/kill/withdraw are added in Tasks 6-8.
public class CombatController : MonoBehaviour
{
    public static CombatController Instance { get; private set; }

    [SerializeField] Button multiButton;            // the repurposed Flee button
    [SerializeField] TMPro.TextMeshProUGUI multiButtonLabel;
    [SerializeField] VoidEvent onCombatPhaseChanged; // HUD phase label listens

    public CombatPhase Phase { get; private set; }
    public bool CanSiege        => CombatPhaseRules.CanSiege(Phase);
    public bool CanInfluence    => CombatPhaseRules.CanInfluence(Phase);
    public bool CanNormalAttack => CombatPhaseRules.CanNormalAttack(Phase);
    // True from the final kill until the canvas actually closes: input is gated
    // (Phase == Resolved) but the death FX is still playing and the canvas is open.
    bool resolving;

    // A fight is live in any non-Resolved phase, or while the closing FX plays.
    // Phase is initialized to Resolved in Awake so this reads false before the
    // first fight (the enum's default is Siege, which would otherwise report
    // combat when none is running).
    public bool InCombat => Phase != CombatPhase.Resolved || resolving;

    readonly List<EnemyCard> live = new();   // logical set; resolution keys off THIS, not childCount
    CombatContext context;
    TownToken guardianPlace;   // Guardian: the assaulted place
    EnemyToken fieldToken;     // Field: the map token, destroyed + save-recorded on defeat
    DungeonToken dungeonToken; // Dungeon: depth/completion tracked on defeat

    // Captured (enemy name, reward) pairs for killed enemies; the exp/crystal is
    // banked immediately by CaptureReward, but the naming message + card pick are
    // paid at fight-end so a kill mid-fight never pops a modal that interrupts
    // Siege/Attack decisions. RewardSummary carries no name, so we pair it here.
    readonly List<(string name, RewardSummary summary)> pendingRewards = new();

    public bool HasLiveEnemies => live.Count > 0;

    public struct EnemySpawn
    {
        public EnemiesSO so; public int bonusHP; public int bonusAttack;
        public EnemySpawn(EnemiesSO so, int bonusHP, int bonusAttack)
        { this.so = so; this.bonusHP = bonusHP; this.bonusAttack = bonusAttack; }
    }

    void Awake() { Instance = this; Phase = CombatPhase.Resolved; }

    // Opens a phased fight. The source varies by context — guardianPlace for a
    // Guardian assault, fieldToken for a Field encounter, dungeonToken for a
    // Dungeon delve — and drives that context's win bookkeeping (Task 9).
    public void OpenFight(List<EnemySpawn> spawns, CombatContext context,
        TownToken guardianPlace = null, EnemyToken fieldToken = null, DungeonToken dungeonToken = null)
    {
        this.context = context;
        this.guardianPlace = guardianPlace;
        this.fieldToken = fieldToken;
        this.dungeonToken = dungeonToken;
        live.Clear();

        var parent = GameManager.Instance.enemyCardCombatPosition.transform;
        // Clear any stragglers (a fled fight's survivors, or an out-of-range peek
        // card) so a new fight never inherits stale cards.
        foreach (var stale in parent.GetComponentsInChildren<EnemyCard>())
            Destroy(stale.gameObject);

        var prefab = FindAnyObjectByType<EnemyDeck>().PrefabEnemyCard;
        foreach (var s in spawns)
        {
            var go = Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(1.75f, 1.75f, 1f);
            var card = go.GetComponent<EnemyCard>();
            card.enemySO = s.so;
            card.bonusHP = s.bonusHP;
            card.bonusAttack = s.bonusAttack;
            live.Add(card);
        }

        GameManager.Instance.combatCanvas.enabled = true;
        if (multiButton != null) multiButton.gameObject.SetActive(true); // the multi-purpose (ex-Flee) button
        SetPhase(CombatPhase.Siege);
    }

    void SetPhase(CombatPhase phase)
    {
        Phase = phase;
        foreach (var card in live) card.ApplyPhase(phase);
        if (multiButtonLabel != null) multiButtonLabel.text = CombatPhaseRules.ButtonLabel(phase);
        if (onCombatPhaseChanged != null) onCombatPhaseChanged.Raise();
    }

    // Engage (Siege -> Defend, spec 2026-07-22): commit the Siege-phase removals
    // and open the Defend window. Siege is a Siege-phase-only currency, cleared
    // here. NO counterattack yet — that waits for the Defend press so the player
    // can play defense first.
    public void Engage()
    {
        if (Phase != CombatPhase.Siege) return;

        var player = FindAnyObjectByType<Player>();
        player.PlayerSiege = 0;                       // Siege doesn't carry past Engage
        GameManager.Instance.commands.ClearStack();   // Engage is a commit point

        SetPhase(CombatPhase.Defend);
    }

    // Defend (Defend -> Attack, spec 2026-07-22): resolve the summed survivor
    // counterattack against whatever Defend the player built during the window —
    // one HP-bite comparison, wounds for the shortfall — then open the Attack phase.
    public void ResolveDefend()
    {
        if (Phase != CombatPhase.Defend) return;
        var player = FindAnyObjectByType<Player>();

        int total = 0;
        foreach (var card in live) total += card.EffectiveAttack;

        int wounds = CombatRules.GroupWoundCount(player.PlayerDefend, total, player.PlayerHP);
        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        for (int i = 0; i < wounds; i++) hand.AddWound();

        player.PlayerDefend = Mathf.Max(0, player.PlayerDefend - total);
        GameManager.Instance.commands.ClearStack();   // taking the hit is a commit point

        if (wounds > 0)
            GameManager.Instance.ValidationMessage($"The enemies strike back! You are wounded {wounds} times.");

        SetPhase(CombatPhase.Attack);
    }

    // Called when a specific enemy is removed (Siege/Attack kill, or Influence).
    // Banks the kill immediately; the FX plays out and self-destroys the card.
    public void NotifyDefeated(EnemyCard card, bool wasInfluence)
    {
        if (!live.Remove(card)) return;

        GameManager.Instance.commands.ClearStack();   // a kill is irreversible

        // Per-context win bookkeeping, banked at kill time (parallels how the
        // guardian ConquestTracker record used to fire from GuardianAssault.Update).
        if (context == CombatContext.Guardian && guardianPlace != null)
            ConquestTracker.Instance.RecordDefeat(guardianPlace.gridPos);
        else if (context == CombatContext.Field && fieldToken != null)
            RecordFieldDefeat(fieldToken);
        else if (context == CombatContext.Dungeon && dungeonToken != null)
            RecordDungeonDefeat(dungeonToken);

        // Exp/crystal bank now; the name + card pick are paid at fight-end. Dungeon
        // fights are exp-only (spec 2026-07-13), driven by context, not a flag.
        pendingRewards.Add((card.enemySO.cardName,
            GameManager.Instance.CaptureReward(card, expOnly: context == CombatContext.Dungeon)));

        // On the final kill, gate further input (Resolved) but keep the canvas
        // open; the fight only closes once the death FX finishes (spec 2026-07-22),
        // so the player actually sees the dissolve/fade.
        bool wasLast = !HasLiveEnemies;
        if (wasLast) { Phase = CombatPhase.Resolved; resolving = true; }
        System.Action onFxDone = wasLast ? (System.Action)(() => EndFight(paidFlee: false)) : null;

        var fx = card.GetComponent<EnemyCardDefeatFx>();
        if (wasInfluence) fx.PlayFade(onFxDone); else fx.PlayDestroy(onFxDone);
    }

    // A field enemy's map token is removed and its cell recorded so a map-gen
    // enemy never respawns on reload (mid-run spawns aren't cell-tracked).
    void RecordFieldDefeat(EnemyToken token)
    {
        if (!token.isMidRunSpawn && DataManager.Instance != null)
            DataManager.Instance.DefeatedEnemies.Add(
                new ArchonsRise.SaveData.Cell(token.gridPos.x, token.gridPos.y));
        Destroy(token.gameObject);
    }

    // A delve win records depth, refreshes the token's marker, and completes the
    // dungeon when the last slot falls (mirrors the old DungeonDelve.Update).
    void RecordDungeonDefeat(DungeonToken token)
    {
        DungeonTracker.Instance.RecordDefeat(token.gridPos);
        token.RefreshVisual();
        if (DungeonTracker.Instance.IsComplete(token.gridPos))
            DungeonTracker.Instance.CompleteDungeon(token);
    }

    // The multi-purpose button in the Attack phase. Survivors alive => this IS
    // the flee (field/dungeon 1 wound, guardian 3-wound retreat). Kills banked.
    public void Withdraw()
    {
        if (Phase != CombatPhase.Attack) return;

        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        int cost = context == CombatContext.Guardian ? PlaceRules.RetreatWoundCount : 1;
        for (int i = 0; i < cost; i++) hand.AddWound();

        GameManager.Instance.ValidationMessage(context == CombatContext.Guardian
            ? $"You retreat from the assault and suffer {cost} wounds. Your progress is kept."
            : "You flee the battle and suffer a wound!");

        EndFight(paidFlee: true);
    }

    // The one button's click, dispatched by current phase.
    public void OnMultiButton()
    {
        if (Phase == CombatPhase.Siege) Engage();
        else if (Phase == CombatPhase.Defend) ResolveDefend();
        else if (Phase == CombatPhase.Attack) Withdraw();
    }

    // Fight-end payout + close. Every captured reward is paid through the queue in
    // kill order (deferred so mid-fight kills never interrupt decisions). On a
    // cleared guardian roster we fire the conquest message + victory check.
    void EndFight(bool paidFlee)
    {
        Phase = CombatPhase.Resolved;
        resolving = false;

        // A flee leaves survivors in the logical set — destroy their cards now so
        // the next fight starts clean (killed cards are already self-destroying).
        foreach (var card in live)
            if (card != null) Destroy(card.gameObject);
        live.Clear();

        foreach (var (name, summary) in pendingRewards)
        {
            var n = name; var s = summary;   // capture per-iteration for the closure
            RewardQueue.Instance.Enqueue(done => { GameManager.Instance.PayReward(n, s); done(); });
        }
        pendingRewards.Clear();

        if (!paidFlee && context == CombatContext.Guardian && guardianPlace != null
            && ConquestTracker.Instance.IsConquered(guardianPlace.gridPos))
        {
            GameManager.Instance.ValidationMessage(
                $"{guardianPlace.townSO.cardName} is conquered! Its services are now open to you.");
            if (RunEndRules.IsVictory(ConquestTracker.Instance.ConqueredCastleCount()))
                RunEndController.RequestEnd(RunOutcome.Victory);
        }

        // Fleeing a field fight leaves the token on the map; de-aggro it so the
        // player must step away and back to re-engage (parity with the old Flee).
        if (paidFlee && context == CombatContext.Field && fieldToken != null)
        {
            fieldToken.isAggro = false;
            if (fieldToken.player != null) fieldToken.player.inCombat = false;
        }

        GameManager.Instance.CloseCombatCanvas();
        guardianPlace = null;
        fieldToken = null;
        dungeonToken = null;

        // Resolved: the shared phase label falls back to the turn phase (Action,
        // since a fight is the turn's action) — see PhaseHud.OnCombatPhaseChanged.
        if (onCombatPhaseChanged != null) onCombatPhaseChanged.Raise();
    }
}
