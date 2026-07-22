using System.Collections.Generic;
using UnityEngine;

// Opens one dungeon delve (M2.9, spec 2026-07-13) as a single phased
// Dungeon-context fight (spec 2026-07-21, Spec 2). Depth tracking, completion,
// exp-only rewards, and the 1-wound withdraw now all live in CombatController;
// this is just the entry point that picks the slot enemy and opens the fight.
public class DungeonDelve : MonoBehaviour
{
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

    public void Begin(DungeonToken token)
    {
        GameManager.Instance.CombatCanvasActive();

        int slot = DungeonTracker.Instance.DefeatedCount(token.gridPos); // 0..2 → tier 1..3
        var spawns = new List<CombatController.EnemySpawn>
        {
            new CombatController.EnemySpawn(token.dungeonSO.enemies[slot], 0, 0)
        };
        CombatController.Instance.OpenFight(spawns, CombatContext.Dungeon, dungeonToken: token);
    }
}
