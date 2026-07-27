// Shrine reward types (spec 2026-07-24, §2). Append-only — new members go at
// the end so saved ints stay stable. NO skills (skills are a level-up channel).
public enum ShrineReward
{
    CardPick = 0,
    Unit = 1,
    LargeExp = 2
}
