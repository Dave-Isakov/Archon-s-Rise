#if UNITY_2017_1_OR_NEWER
using UnityEngine.Serialization;
#endif

// One row of the level reward table. Plain serializable data — no UnityEngine —
// so LevelRules stays testable from the CLI mcs harness. All fields are counts,
// never booleans: every reward knob is tunable per level in the inspector.
//
// toughnessBonus was named hpBonus before 2026-07-23 (spec Part T). The
// FormerlySerializedAs is Unity-only so the mcs harness, which compiles this
// file with no UnityEngine reference, still builds it.
[System.Serializable]
public class LevelRewardEntry
{
    public int level;
#if UNITY_2017_1_OR_NEWER
    [FormerlySerializedAs("hpBonus")]
#endif
    public int toughnessBonus;
    public int handSizeBonus;
    public int armySizeBonus;
    public int skillPicks;
    public int cardPicks;
}
