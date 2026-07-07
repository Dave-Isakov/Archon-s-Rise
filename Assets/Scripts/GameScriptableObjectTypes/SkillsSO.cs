using UnityEngine;

// A level-up skill: an activatable, exhaustible ability on the skill bar.
// id (from AllCards) is the stable save identity — never rename ids.
[CreateAssetMenu(fileName = "Skill", menuName = "ScriptableObjects/Skill")]
public class SkillsSO : AllCards
{
    public Sprite icon;
    public SkillEffect effect;
    public int magnitude = 1;
    // Only meaningful for SkillEffect.GainCrystal.
    public EmpowerType crystalColor;
    public SkillCadence cadence;
}
