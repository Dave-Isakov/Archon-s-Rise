using UnityEngine;

// Drives one dungeon delve (M2.9, spec 2026-07-13): a single tiered fight per
// Explore spend. Mirrors GuardianAssault's spawn/track pattern, but a delve is
// one fight, not a chain — win records depth (and maybe completes the
// dungeon); flee is field rules (1 wound), routed here by GameManager.
public class DungeonDelve : MonoBehaviour
{
    private DungeonToken dungeon;
    private EnemyCard activeCard;
    private bool defeatRecorded;

    private static DungeonDelve instance;
    public static DungeonDelve Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("DungeonDelve").AddComponent<DungeonDelve>();
            return instance;
        }
    }

    public bool InProgress => dungeon != null;
    // Read by Rewards (exp-only routing) and GameManager.FleeCombat without
    // lazily creating the singleton.
    public static bool AnyInProgress => instance != null && instance.InProgress;

    public void Begin(DungeonToken token)
    {
        dungeon = token;
        GameManager.Instance.CombatCanvasActive();
        SpawnDelveEnemy();
    }

    private void Update()
    {
        if (dungeon == null || activeCard == null) return;
        if (!activeCard.IsDefeated || defeatRecorded) return;

        defeatRecorded = true;
        DungeonTracker.Instance.RecordDefeat(dungeon.gridPos);
        var done = dungeon;
        dungeon = null;   // one fight per delve: the verdict ends the delve
        activeCard = null;
        done.RefreshVisual();

        if (DungeonTracker.Instance.IsComplete(done.gridPos))
            DungeonTracker.Instance.CompleteDungeon(done);
        // The combat canvas closes via the usual defeated-card click
        // (GameManager.CheckCombatants), same as guardian fights.
    }

    // Field-rules flee (1 wound, applied by the caller): clear the delve so
    // the undefeated slot enemy can be retried with a fresh Explore spend.
    public void Flee()
    {
        if (!InProgress) return;

        GameManager.Instance.playerHand.GetComponent<PlayerHand>().AddWound();

        foreach (var card in GameManager.Instance.enemyCardCombatPosition
                     .GetComponentsInChildren<EnemyCard>())
            Destroy(card.gameObject);

        dungeon = null;
        activeCard = null;
        GameManager.Instance.CloseCombatCanvas();
        GameManager.Instance.ValidationMessage("You withdraw from the dungeon and suffer a wound! Your progress is kept.");
    }

    private void SpawnDelveEnemy()
    {
        int slot = DungeonTracker.Instance.DefeatedCount(dungeon.gridPos); // 0..2 → tier 1..3
        var next = dungeon.dungeonSO.enemies[slot];

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
