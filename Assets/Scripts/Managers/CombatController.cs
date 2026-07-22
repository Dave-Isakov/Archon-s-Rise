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

    readonly List<EnemyCard> live = new();   // logical set; resolution keys off THIS, not childCount
    CombatContext context;
    TownToken guardianPlace;

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

    void Awake() { Instance = this; }

    public void OpenFight(List<EnemySpawn> spawns, CombatContext context, TownToken guardianPlace)
    {
        this.context = context;
        this.guardianPlace = guardianPlace;
        live.Clear();

        var prefab = FindAnyObjectByType<EnemyDeck>().PrefabEnemyCard;
        var parent = GameManager.Instance.enemyCardCombatPosition.transform;
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
        SetPhase(CombatPhase.Siege);
    }

    void SetPhase(CombatPhase phase)
    {
        Phase = phase;
        foreach (var card in live) card.ApplyPhase(phase);
        if (multiButtonLabel != null) multiButtonLabel.text = CombatPhaseRules.ButtonLabel(phase);
        if (onCombatPhaseChanged != null) onCombatPhaseChanged.Raise();
    }

    // The Defend resolution (spec 2026-07-21, Spec 2): summed survivor Attack vs
    // Defend in one HP-bite comparison; unspent Siege is consumed by committing.
    public void Engage()
    {
        if (Phase != CombatPhase.Siege) return;
        var player = FindAnyObjectByType<Player>();

        int total = 0;
        foreach (var card in live) total += card.EffectiveAttack;

        int wounds = CombatRules.GroupWoundCount(player.PlayerDefend, total, player.PlayerHP);
        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        for (int i = 0; i < wounds; i++) hand.AddWound();

        player.PlayerDefend = Mathf.Max(0, player.PlayerDefend - total);
        player.PlayerSiege = 0;                       // Siege is a Siege-phase-only currency
        GameManager.Instance.commands.ClearStack();   // Engage is a commit point

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

        if (context == CombatContext.Guardian && guardianPlace != null)
            ConquestTracker.Instance.RecordDefeat(guardianPlace.gridPos);

        // Exp/crystal bank now; the name + card pick are paid at fight-end.
        pendingRewards.Add((card.enemySO.cardName, GameManager.Instance.CaptureReward(card)));

        var fx = card.GetComponent<EnemyCardDefeatFx>();
        if (wasInfluence) fx.PlayFade(null); else fx.PlayDestroy(null);

        if (!HasLiveEnemies) WinFight();
    }

    void WinFight() { EndFight(paidFlee: false); }

    // Fight-end payout + close. Every captured reward is paid through the queue in
    // kill order (deferred so mid-fight kills never interrupt decisions). On a
    // cleared guardian roster we fire the conquest message + victory check.
    void EndFight(bool paidFlee)
    {
        Phase = CombatPhase.Resolved;

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

        GameManager.Instance.CloseCombatCanvas();
        guardianPlace = null;
    }
}
