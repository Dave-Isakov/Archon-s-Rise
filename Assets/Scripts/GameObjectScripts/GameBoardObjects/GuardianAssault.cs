using UnityEngine;

// Drives a resumable assault on one guarded place. Modeled on
// Dungeon.SpawnDungeonEnemy's sequential spawn but with conquest semantics;
// kept separate so dungeon behavior is untouched. Guardians are fought in
// order (guardians[defeatedCount]); defeated guardians never respawn. The
// next guardian auto-spawns when the previous one falls; the assault ends
// when the roster is exhausted (conquered) or the player retreats (3 wounds,
// progress kept — GameManager.FleeCombat delegates here).
public class GuardianAssault : MonoBehaviour
{
    private TownToken place;
    private EnemyCard activeCard;
    private bool defeatRecorded;

    private static GuardianAssault instance;
    public static GuardianAssault Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("GuardianAssault").AddComponent<GuardianAssault>();
            return instance;
        }
    }

    public bool InProgress => place != null;

    // Read by turn/round gating and FleeCombat without lazily creating the singleton.
    public static bool AnyInProgress => instance != null && instance.InProgress;

    public void Begin(TownToken town)
    {
        place = town;
        // Tear down the place menu the button click came from.
        foreach (var card in FindObjectsByType<TownCard>(FindObjectsSortMode.None))
            Destroy(card.gameObject);
        GameManager.Instance.townCanvas.enabled = false;
        GameManager.Instance.CombatCanvasActive();
        SpawnNextGuardian();
    }

    private void Update()
    {
        if (place == null || activeCard == null) return;

        if (activeCard.IsDefeated && !defeatRecorded)
        {
            defeatRecorded = true;
            ConquestTracker.Instance.RecordDefeat(place.gridPos);

            if (ConquestTracker.Instance.IsConquered(place.gridPos))
            {
                GameManager.Instance.ValidationMessage(
                    $"{place.townSO.cardName} is conquered! Its services are now open to you.");
                // M2.5 win check: territory is the sole win axis.
                if (RunEndRules.IsVictory(ConquestTracker.Instance.ConqueredCastleCount()))
                    RunEndController.RequestEnd(RunOutcome.Victory);
                place = null; // combat canvas closes via CheckCombatants on card click
                activeCard = null;
            }
            else
            {
                // Chain the next guardian now so the canvas-close check
                // (CheckCombatants childCount == 1) keeps the fight open.
                SpawnNextGuardian();
            }
        }
    }

    public void Retreat()
    {
        if (!InProgress) return;

        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        for (int i = 0; i < PlaceRules.RetreatWoundCount; i++)
            hand.AddWound();

        foreach (var card in GameManager.Instance.enemyCardCombatPosition
                     .GetComponentsInChildren<EnemyCard>())
            Destroy(card.gameObject);

        place = null;
        activeCard = null;
        GameManager.Instance.CloseCombatCanvas();
        GameManager.Instance.ValidationMessage(
            $"You retreat from the assault and suffer {PlaceRules.RetreatWoundCount} wounds! Your progress is not lost.");
    }

    private void SpawnNextGuardian()
    {
        var roster = place.townSO.guardians;
        var next = roster[ConquestTracker.Instance.DefeatedCount(place.gridPos)];

        var prefab = FindAnyObjectByType<EnemyDeck>().PrefabEnemyCard;
        var cardObject = Instantiate(prefab,
            GameManager.Instance.enemyCardCombatPosition.transform);
        cardObject.transform.localPosition = Vector3.zero;
        cardObject.transform.localScale = new Vector3(1.75f, 1.75f);
        activeCard = cardObject.GetComponent<EnemyCard>();
        activeCard.enemySO = next;
        defeatRecorded = false;
    }
}
