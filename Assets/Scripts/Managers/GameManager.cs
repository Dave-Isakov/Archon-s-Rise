using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance { get { return instance; } }

    public Canvas messageCanvas;
    public Canvas mainMenuCanvas;
    public GameObject enlargeCardPosition;
    public GameObject enlargeTownCardPosition;
    public Canvas cardCanvas;
    public Canvas unitCanvas;
    public Canvas combatCanvas;
    public GameObject enemyCardCombatPosition;
    [SerializeField] Rewards rewards;
    [SerializeField] TextMeshProUGUI combatBanner; // the "Combat!" intro text
    [SerializeField] string combatIntroState = "CombatIntro";
    [SerializeField] float combatIntroDuration = 1.5f;
    // The enemy token whose combat is currently open. Set when combat starts,
    // read by FleeCombat() to de-aggro the right token, cleared on teardown.
    // Non-null ONLY during a real fight, never while merely previewing a token.
    [HideInInspector] public EnemyToken activeCombatant;
    // Flee control. The combat canvas is reused to preview enemy tokens out of
    // range, so the Flee button is shown only during a real fight.
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
    public Button returnButton;
    public TextMeshProUGUI messageText;
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
        messageCanvas.gameObject.SetActive(true);
        messageCanvas.enabled = false;
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

    // Dismiss callback for the queued message currently on screen; set by the
    // ValidationMessage job, consumed exactly once by ReturnButton.
    private System.Action messageDone;

    public void ReturnButton()
    {
        messageCanvas.enabled = false;
        var done = messageDone;
        messageDone = null;
        done?.Invoke();
    }

    public void ValidationMessage(string message)
    {
        // The run-end screen is terminal: no popup may appear over it.
        if (RunEndController.HasEnded) return;
        RewardQueue.Instance.Enqueue(done =>
        {
            if (RunEndController.HasEnded) { done(); return; } // run ended while queued
            if (messageCanvas.enabled)
                Debug.LogError("ValidationMessage: message canvas already open — modal routing bug.");
            messageDone = done;
            messageCanvas.enabled = true;
            messageText.text = message;
        });
    }

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
        combatCanvas.GetComponentInChildren<Animator>().enabled = true;
        if (combatBanner != null) combatBanner.enabled = false; // no intro flash for guardian/dungeon
        if (fleeButton != null) fleeButton.gameObject.SetActive(true);
    }

    // Field-combat intro: enable the canvas, replay the authored banner clip from
    // frame 0 (deterministic — no longer keyed off the banner TMP's enabled flag,
    // which never reset and made the intro play only once), wait its duration.
    public IEnumerator PlayCombatIntro()
    {
        if (onCombatStartedTutorial != null) onCombatStartedTutorial.Raise();
        combatCanvas.enabled = true;
        var animator = combatCanvas.GetComponentInChildren<Animator>(true);
        if (combatBanner != null) combatBanner.enabled = true;
        if (animator != null)
        {
            animator.enabled = true;
            animator.Play(combatIntroState, 0, 0f);
        }
        if (fleeButton != null) fleeButton.gameObject.SetActive(true);
        yield return new WaitForSeconds(combatIntroDuration);
    }

    public void CheckCombatants()
    {
        if(enemyCardCombatPosition.transform.childCount == 1)
        {
            combatCanvas.enabled = false;
            combatCanvas.GetComponentInChildren<Animator>().enabled = false;
            EndCombat();
        }
    }

    // Single defeat orchestrator (spec 2026-07-16). Applies rewards, shows a
    // reward-naming message, opens the card pick after it, then tears the fight
    // down — all serialized through RewardQueue so ordering is correct by
    // construction. Replaces the OnEnemyDefeat_GetRewards fan-out and the old
    // click-the-defeated-card teardown.
    public void ResolveDefeat(EnemyCard enemy)
    {
        if (onEnemyResolvedTutorial != null) onEnemyResolvedTutorial.Raise();
        RewardSummary summary = rewards.GetReward(enemy);

        ValidationMessage(DefeatMessage.Compose(
            enemy.enemySO.cardName, summary.exp, summary.crystal, summary.cardPick));

        if (summary.cardPick)
            rewards.OfferCardChoice(summary.tier); // self-enqueues, lands after the message

        RewardQueue.Instance.Enqueue(done => StartCoroutine(TeardownDefeat(enemy, done)));
    }

    // Runs as a coroutine so watcher Updates (GuardianAssault/DungeonDelve/
    // EnemyToken) react to isDefeated before the close check. CheckCombatants
    // must see the defeated card STILL present: childCount == 1 means it was the
    // last enemy (close); a spawned next-guardian makes it 2 (stay open).
    private IEnumerator TeardownDefeat(EnemyCard enemy, System.Action done)
    {
        enemy.isDefeated = true;
        yield return null; // let watcher Updates spawn any next-guardian card
        CheckCombatants(); // closes + EndCombat only if the defeated card is the last
        Destroy(enemy.gameObject);
        commands.ClearStack();
        done();
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
        combatCanvas.enabled = false;
        combatCanvas.GetComponentInChildren<Animator>().enabled = false;
        EndCombat();
    }

    // Bank a killed enemy's reward at defeat time (spec 2026-07-21, Spec 2). The
    // exp/crystal apply instantly inside GetReward; CombatController holds the
    // returned summary and pays its message + card pick at fight-end via
    // PayReward. Keeps the private rewards service encapsulated in GameManager.
    public RewardSummary CaptureReward(EnemyCard enemy) => rewards.GetReward(enemy);

    // Pay one captured reward summary (spec 2026-07-21, Spec 2 deferred payout).
    // Mirrors the old ResolveDefeat body minus the exp grant (banked at capture)
    // and teardown (the FX owns teardown).
    public void PayReward(string enemyName, RewardSummary summary)
    {
        ValidationMessage(DefeatMessage.Compose(enemyName, summary.exp, summary.crystal, summary.cardPick));
        if (summary.cardPick) rewards.OfferCardChoice(summary.tier);
    }

    // Player gives up the current fight. During a guardian assault the Flee
    // button acts as Retreat (3 wounds, conquest progress kept); in field
    // combat it takes one wound and de-aggros the engaged token.
    public void FleeCombat()
    {
        if (GuardianAssault.AnyInProgress)
        {
            GuardianAssault.Instance.Retreat();
            return;
        }

        // A delve flee is field rules (1 wound); there is no map token to
        // de-aggro, so DungeonDelve owns the whole teardown.
        if (DungeonDelve.AnyInProgress)
        {
            DungeonDelve.Instance.Flee();
            return;
        }

        // Guard: activeCombatant is set only by a real fight, never while the
        // combat canvas is merely previewing an out-of-range enemy token.
        if (activeCombatant == null) return;

        playerHand.GetComponent<PlayerHand>().AddWound();

        foreach (var card in enemyCardCombatPosition.GetComponentsInChildren<EnemyCard>())
            Destroy(card.gameObject);

        activeCombatant.isAggro = false;
        if (activeCombatant.player != null)
            activeCombatant.player.inCombat = false;

        CloseCombatCanvas();

        ValidationMessage("You flee the battle and suffer a wound!");
    }


}
