using System.Collections.Generic;
using System.Text;

// The shrine payout line (2026-07-31). Shrine rewards used to land in silence:
// exp and units left no trace at all, and a card pick opened a picker that never
// said where it came from. Composed here, posted through GameLog (toast + log)
// by Rewards.GrantShrineReward on BOTH payout paths — the safe roll's 1x and the
// guardian's 2x — so the two read identically.
//
// Pure/mcs-testable, same shape as DefeatMessage. Stat amounts use icon tags,
// never typed-out stat words.
public static class ShrineRewardMessage
{
    // Returns "" when the shrine granted nothing at all (an authored Unit pool
    // that turned out empty). GameLog.Post ignores empty, so the caller needs no
    // guard of its own.
    public static string Compose(int exp, IReadOnlyList<string> unitNames, int cardPicks)
    {
        var parts = new List<string>();

        if (exp > 0)
            parts.Add(IconMarkup.Cost(IconConcept.Experience, exp));

        int units = unitNames != null ? unitNames.Count : 0;
        if (units > 0)
            parts.Add(JoinNames(unitNames) + (units == 1 ? " joins your army" : " join your army"));

        if (cardPicks == 1) parts.Add("a new card to choose");
        else if (cardPicks > 1) parts.Add($"{cardPicks} new cards to choose");

        if (parts.Count == 0) return "";
        return "The shrine's bargain pays out: " + JoinParts(parts) + ".";
    }

    // "Knight", "Knight and Scout", "Knight, Scout and Merchant".
    static string JoinNames(IReadOnlyList<string> names)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < names.Count; i++)
        {
            if (i > 0) sb.Append(i == names.Count - 1 ? " and " : ", ");
            sb.Append(names[i]);
        }
        return sb.ToString();
    }

    // The reward types never mix today (one type is rolled, then granted `count`
    // times), but joining is written for the general case so a future mixed
    // payout reads correctly instead of silently dropping a clause.
    static string JoinParts(List<string> parts)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0) sb.Append(i == parts.Count - 1 ? " and " : ", ");
            sb.Append(parts[i]);
        }
        return sb.ToString();
    }
}
