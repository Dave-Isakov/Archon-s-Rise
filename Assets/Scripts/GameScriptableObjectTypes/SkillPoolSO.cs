using System.Collections.Generic;
using UnityEngine;

// The skills a character can be offered on a skill-pick level (spec 2026-07-23,
// A2). Split out of LevelRewardsSO so characters that share a progression curve
// can reuse one level table while drawing from different pools — retuning the
// exp curve stays a single-asset edit.
[CreateAssetMenu(fileName = "SkillPool", menuName = "ScriptableObjects/SkillPool")]
public class SkillPoolSO : ScriptableObject
{
    [SerializeField] List<SkillsSO> skills = new();

    public IReadOnlyList<SkillsSO> Skills => skills;
}
