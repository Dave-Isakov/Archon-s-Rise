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
    // Passive gate (Charismatic): no activatable effect; queried by
    // Player.HasCharismatic to allow recruiting influenced enemies. Appended.
    RecruitEnemies,
}
