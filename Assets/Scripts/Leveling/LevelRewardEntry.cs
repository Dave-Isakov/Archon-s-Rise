// One row of the level reward table. Plain serializable data — no UnityEngine —
// so LevelRules stays testable from the CLI mcs harness. All fields are counts,
// never booleans: every reward knob is tunable per level in the inspector.
[System.Serializable]
public class LevelRewardEntry
{
    public int level;
    public int hpBonus;
    public int handSizeBonus;
    public int armySizeBonus;
    public int skillPicks;
    public int cardPicks;
}
