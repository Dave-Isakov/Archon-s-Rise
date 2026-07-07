using System.Collections.Generic;
using UnityEngine;

// THE level reward table: one asset drives all level-up payouts, so every
// balance change during playtesting is an inspector edit on this asset.
[CreateAssetMenu(fileName = "LevelRewards", menuName = "ScriptableObjects/LevelRewards")]
public class LevelRewardsSO : ScriptableObject
{
    [SerializeField] List<SkillsSO> skillPool = new();
    [SerializeField] List<LevelRewardEntry> entries = new();

    public IReadOnlyList<SkillsSO> SkillPool => skillPool;
    public IReadOnlyList<LevelRewardEntry> Entries => entries;
}
