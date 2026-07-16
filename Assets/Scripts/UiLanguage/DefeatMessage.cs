// Post-combat defeat + reward line in the icon language (spec 2026-07-16).
// Pure/mcs-testable: no UnityEngine dependency. GameManager.ResolveDefeat feeds
// the RewardSummary fields in and routes the result through ValidationMessage.
public static class DefeatMessage
{
    public static string Compose(string enemyName, int exp, EmpowerType? crystal, bool cardPick)
    {
        string msg = $"{enemyName} has been defeated. You receive "
                     + IconMarkup.Cost(IconConcept.Experience, exp);
        if (crystal.HasValue)
            msg += " " + IconMarkup.CrystalTag(crystal.Value);
        if (cardPick)
            msg += " and a new card to choose";
        return msg + ".";
    }
}
