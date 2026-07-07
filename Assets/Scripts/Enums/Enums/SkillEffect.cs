// What activating a skill does. Stat gains feed the same per-turn pools as
// cards/units; GainCrystal and HealWound reuse the existing crystal / heal paths.
public enum SkillEffect
{
    GainAttack,
    GainDefend,
    GainInfluence,
    GainExplore,
    GainCrystal,
    HealWound,
}
