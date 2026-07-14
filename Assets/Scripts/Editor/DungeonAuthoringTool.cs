#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Idempotently authors the M2.9 dungeon set (spec 2026-07-13): 6 dungeons,
// 3 enemy slots each (tier 1/2/3), completion tier 3, rewardCount 1 with one
// showpiece at 2. Re-running overwrites the same assets in place.
public static class DungeonAuthoringTool
{
    private const string DungeonDir = "Assets/Scripts/ScriptableObjectData/Non-Player/Dungeons";
    private const string EnemyDir = "Assets/Scripts/ScriptableObjectData/Non-Player/Enemies";

    [MenuItem("Tools/Archon's Rise/Author M2.9 Dungeons")]
    public static void Author()
    {
        Create("Derelict Tower", "dungeon-derelict-tower",
            "A leaning spire whose upper floors still hum with residual sorcery.",
            2, 1, "GoblinPack", "WanderingMystic", "DungeonEnemies/Sorceror");
        Create("Sunken Crypt", "dungeon-sunken-crypt",
            "Flooded burial halls. Something below has learned to swim.",
            2, 1, "DireWolves", "CorruptTreekin", "DungeonEnemies/CorruptedTroll");
        Create("Bandit Warrens", "dungeon-bandit-warrens",
            "A smugglers' maze dug beneath the hills, ruled by a brute of a chief.",
            2, 1, "BanditFootsoldier", "WanderingMystic", "OgreBruteEnemy");
        Create("Howling Caverns", "dungeon-howling-caverns",
            "Wind screams through these caves — and it is not alone.",
            3, 1, "DireWolves", "GryphonRiderEnemy", "DungeonEnemies/CorruptedTroll");
        Create("Forgotten Vault", "dungeon-forgotten-vault",
            "Sealed by the old kingdom; its wards are failing at last.",
            3, 1, "MercenaryBand", "CorruptTreekin", "DungeonEnemies/Sorceror");
        Create("Wyrm's Hollow", "dungeon-wyrms-hollow",
            "The deepest delve in the realm. Whatever sleeps here, it hoards well.",
            3, 2, "GoblinPack", "GryphonRiderEnemy", "OgreBruteEnemy");

        AssetDatabase.SaveAssets();
        Debug.Log("Author M2.9 Dungeons: 6 dungeons written to " + DungeonDir);
    }

    private static void Create(string name, string id, string description,
        int exploreCost, int rewardCount, params string[] enemyAssets)
    {
        var path = $"{DungeonDir}/{name}.asset";
        var so = AssetDatabase.LoadAssetAtPath<DungeonsSO>(path);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<DungeonsSO>();
            AssetDatabase.CreateAsset(so, path);
        }
        so.id = id;
        so.cardName = name;
        so.cardDescription = description;
        so.exploreCost = exploreCost;
        so.tier = 3;          // the finale is a tier-3 fight; the bundle pays tier 3
        so.rewardCount = rewardCount;
        so.enemies = new List<EnemiesSO>();
        foreach (var e in enemyAssets)
        {
            var enemy = AssetDatabase.LoadAssetAtPath<EnemiesSO>($"{EnemyDir}/{e}.asset");
            if (enemy == null) { Debug.LogError($"Author Dungeons: missing enemy asset '{e}.asset'"); return; }
            so.enemies.Add(enemy);
        }
        EditorUtility.SetDirty(so);
    }
}
#endif
