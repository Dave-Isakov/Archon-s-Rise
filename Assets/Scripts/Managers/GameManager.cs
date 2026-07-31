using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance { get { return instance; } }

    public Canvas mainMenuCanvas;
    public GameObject enlargeCardPosition;
    public GameObject enlargeTownCardPosition;
    public Canvas cardCanvas;
    public Canvas unitCanvas;
    public Canvas combatCanvas;
    public GameObject enemyCardCombatPosition;
    [SerializeField] Rewards rewards;
    [SerializeField] CombatBackdrop combatBackdrop; // fades the battlefield after the intro
    [SerializeField] TextMeshProUGUI combatBanner; // the "Combat!" intro text
    [SerializeField] float combatIntroDuration = 1.5f; // how long the banner holds
    // The enemy token whose combat is currently open. Set when combat starts,
    // cleared on teardown by EndCombat().
    [HideInInspector] public EnemyToken activeCombatant;
    // Flee control. Shown whenever a fight canvas is open; the actual flee
    // (CombatController.Withdraw) is gated on the Attack phase, so pressing it
    // during an uncommitted free look (spec 2026-07-30) is a no-op.
    public Button fleeButton;
    // M2.12 tutorial triggers, raised at the real sites so the rail and
    // one-shots key off actual play. Null-safe until wired.
    [SerializeField] VoidEvent onCombatStartedTutorial;
    [SerializeField] VoidEvent onEnemyResolvedTutorial;
    public Canvas cardRewardCanvas;
    public Canvas cardListCanvas;
    public Canvas townCanvas;
    public Canvas dungeonCanvas;
    public GameObject playerHand;
    public PlayManager commands;
    private int roundNum;
    private int turnNum;
    public int Round { get => roundNum; set => roundNum = value; }
    public int Turn  { get => turnNum;  set => turnNum  = value; }
    public TextMeshProUGUI roundTurnText;

    private void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }

        cardCanvas.gameObject.SetActive(true);
        cardCanvas.enabled = false;
        unitCanvas.gameObject.SetActive(true);
        unitCanvas.enabled = false;
        cardListCanvas.gameObject.SetActive(true);
        cardListCanvas.enabled = false;
        cardRewardCanvas.gameObject.SetActive(true);
        cardRewardCanvas.enabled = false;
        mainMenuCanvas.gameObject.SetActive(true);
        mainMenuCanvas.enabled = false;
        townCanvas.gameObject.SetActive(true);
        townCanvas.enabled = false;
        dungeonCanvas.gameObject.SetActive(true);
        dungeonCanvas.enabled = false;
        combatCanvas.gameObject.SetActive(true);
        combatCanvas.enabled = false;
        if (fleeButton != null) fleeButton.gameObject.SetActive(false);
        roundNum = 1;
        turnNum = 1;
    }

    private void Start() 
    {
        commands = new PlayManager();
    }

    // The old per-frame "Round/Turn" label is now the event-driven day countdown +
    // phase label driven by PhaseHud off the controller's events (spec 2026-07-21).

    public void TurnPlus()
    {
        turnNum++;
    }

    public void RoundPlus()
    {
        roundNum++;
        // By design, units exhausted during the round all refresh when a new round starts.
        var player = FindAnyObjectByType<Player>();
        if (player != null) player.RefreshUnits();
        if (player != null) player.RefreshSkills(true);

        // Doom rises on the same cadence that refreshes units/skills, plus +1
        // per flagged, uncleared dungeon (M2.9).
        if (DoomClock.Instance != null)
            DoomClock.Instance.Add(DungeonRules.RoundTick(DungeonTracker.Instance.FlaggedCount));

        // Spawner reads the doom value the tick above just produced.
        if (EnemySpawner.Instance != null) EnemySpawner.Instance.OnRoundEnd();
    }

    public void CombatCanvasActive()
    {
        if (onCombatStartedTutorial != null) onCombatStartedTutorial.Raise();
        combatCanvas.enabled = true;
        if (combatBanner != null) combatBanner.enabled = false; // no intro flash for guardian/dungeon
        if (fleeButton != null) fleeButton.gameObject.SetActive(true);
        if (combatBackdrop != null) combatBackdrop.FadeToBattle();
    }

    // Field-combat intro: enable the canvas, show the "Combat!" banner for
    // combatIntroDuration, then hide it and fade the backdrop into the fight.
    // The banner used to be an authored Animator clip; the combat canvas has no
    // Animator any more (2026-07-30), so the hold is the coroutine's own wait and
    // the banner is toggled explicitly — including OFF at the end, which the clip
    // used to do. Without that it would stay on screen for the whole fight.
    public IEnumerator PlayCombatIntro()
    {
        if (onCombatStartedTutorial != null) onCombatStartedTutorial.Raise();
        combatCanvas.enabled = true;
        if (combatBanner != null) combatBanner.enabled = true;
        if (fleeButton != null) fleeButton.gameObject.SetActive(true);
        yield return new WaitForSeconds(combatIntroDuration);
        if (combatBanner != null) combatBanner.enabled = false;
        if (combatBackdrop != null) combatBackdrop.FadeToBattle();
    }

    // Clears combat state shared by every way combat can end (win or flee).
    private void EndCombat()
    {
        activeCombatant = null;
        if (fleeButton != null) fleeButton.gameObject.SetActive(false);
    }

    // Shared canvas teardown for every non-victory combat exit (token flee,
    // assault retreat).
    public void CloseCombatCanvas()
    {
        if (combatBackdrop != null) combatBackdrop.Restore();
        combatCanvas.enabled = false;
        if (combatBanner != null) combatBanner.enabled = false;
        EndCombat();
    }

    // Bank a killed enemy's reward at defeat time (spec 2026-07-21, Spec 2). The
    // exp/crystal apply instantly inside GetReward; CombatController holds the
    // returned summary and pays its message + card pick at fight-end via
    // PayReward. Keeps the private rewards service encapsulated in GameManager.
    public RewardSummary CaptureReward(EnemyCard enemy, bool expOnly) => rewards.GetReward(enemy, expOnly);

    // Pay one captured reward summary (spec 2026-07-21, Spec 2 deferred payout).
    // Mirrors the old ResolveDefeat body minus the exp grant (banked at capture)
    // and teardown (the FX owns teardown).
    public void PayReward(string enemyName, RewardSummary summary)
    {
        if (onEnemyResolvedTutorial != null) onEnemyResolvedTutorial.Raise();
        GameLog.Instance.Post(DefeatMessage.Compose(enemyName, summary.exp, summary.crystal, summary.cardPick));
        if (summary.cardPick) rewards.OfferCardChoice(summary.tier);
    }

}
