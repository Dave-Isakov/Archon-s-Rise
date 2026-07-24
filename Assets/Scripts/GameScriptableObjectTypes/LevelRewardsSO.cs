using System.Collections.Generic;
using UnityEngine;

// THE level reward table: one asset drives all level-up payouts, so every
// balance change during playtesting is an inspector edit on this asset.
// The skill pool moved to SkillPoolSO on 2026-07-23 (spec A2) so characters
// can share one curve while drawing different skills.
[CreateAssetMenu(fileName = "LevelRewards", menuName = "ScriptableObjects/LevelRewards")]
public class LevelRewardsSO : ScriptableObject
{
    [SerializeField] List<LevelRewardEntry> entries = new();

    public IReadOnlyList<LevelRewardEntry> Entries => entries;
}
