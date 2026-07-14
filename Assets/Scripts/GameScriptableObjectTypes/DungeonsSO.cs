using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Dungeons", menuName = "ScriptableObjects/Dungeons")]
public class DungeonsSO : AllCards
{
    public int exploreCost;
    public List<EnemiesSO> enemies;
    // Completion bundle (spec 2026-07-13): tier the bundle pays at, and how
    // many crystals AND card picks it grants (all guaranteed, no rolls).
    public int tier = 1;
    public int rewardCount = 1;

#if UNITY_EDITOR
    // Authoring contract (spec 2026-07-13): exactly 3 enemies, slot i = tier i+1.
    private void OnValidate()
    {
        if (enemies == null) return;
        if (enemies.Count != 3)
            Debug.LogWarning($"{name}: dungeons need exactly 3 enemies (tier 1/2/3 slots) — has {enemies.Count}.", this);
        for (int i = 0; i < enemies.Count && i < 3; i++)
            if (enemies[i] != null && enemies[i].tier != i + 1)
                Debug.LogWarning($"{name}: enemy slot {i} should be tier {i + 1} but '{enemies[i].name}' is tier {enemies[i].tier}.", this);
    }
#endif
}
