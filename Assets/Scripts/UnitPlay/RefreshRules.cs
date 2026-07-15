// Pure refresh-budget math (spec 2026-07-14). Refresh N is a budget spent
// across exhausted units; each pick deducts the unit's recruit influenceCost.
// Unity-free so it is mcs-CLI-testable.
public static class RefreshRules
{
    // A unit never refreshes for free: an authored cost below 1 counts as 1 so
    // every pick shrinks the budget and the picker always terminates.
    public static int PickCost(int influenceCost)
    {
        return influenceCost < 1 ? 1 : influenceCost;
    }

    public static bool CanPick(bool exhausted, int influenceCost, int remaining)
    {
        return exhausted && PickCost(influenceCost) <= remaining;
    }
}
