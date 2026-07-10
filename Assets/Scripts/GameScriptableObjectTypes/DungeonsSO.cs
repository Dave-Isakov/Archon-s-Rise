using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Dungeons", menuName = "ScriptableObjects/Dungeons")]
public class DungeonsSO : AllCards
{
    public int exploreCost;
    public List<EnemiesSO> enemies;
    // Reward tier (1-3) every reward event in this dungeon pays out at, and how
    // many reward events the dungeon offers before its rewards are exhausted
    // (spec 2026-07-10 — replaces the old List<RewardsSO>).
    public int tier = 1;
    public int rewardCount = 1;
}
