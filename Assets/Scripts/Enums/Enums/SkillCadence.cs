// How often a used skill refreshes. PerTurn = weak effects, refresh at turn
// end. PerRound = strong effects (crystals, healing), refresh at round end.
public enum SkillCadence
{
    PerTurn,
    PerRound,
    // Passive skills are never activated/exhausted; their effect is queried
    // (e.g. Charismatic gates enemy recruiting). Appended for serialized ints.
    Passive,
}
