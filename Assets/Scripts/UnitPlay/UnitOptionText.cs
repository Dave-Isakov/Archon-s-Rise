// Row/label text for a unit option. UI-framework-free so it is mcs-testable
// and reusable by the pop-out rows, the Use bar, and the recruit panel.
// Icon language per spec 2026-07-15: icon + word for the effect, canonical
// [icon][number] for costs, tinted crystal glyph for colors.
public static class UnitOptionText
{
    static IconConcept ConceptFor(UnitEffect effect)
    {
        if (effect == UnitEffect.Attack)    return IconConcept.Attack;
        if (effect == UnitEffect.Defend)    return IconConcept.Defend;
        if (effect == UnitEffect.Explore)   return IconConcept.Explore;
        if (effect == UnitEffect.Influence) return IconConcept.Influence;
        if (effect == UnitEffect.Siege)     return IconConcept.Siege;
        if (effect == UnitEffect.Heal)      return IconConcept.Heal;
        return IconConcept.Crystal; // Crystallize
    }

    public static string Describe(UnitOption o)
    {
        string body = o.effect == UnitEffect.Crystallize
            ? $"{IconMarkup.CrystalTag(o.grantColor)} Crystallize {o.amount}"
            : $"{IconMarkup.Tag(ConceptFor(o.effect))} {o.effect} {o.amount}";

        if (o.influenceCost > 0)
            return $"{body} — {IconMarkup.Cost(IconConcept.Influence, o.influenceCost)}";
        if (o.crystalCost == EmpowerType.None) return body;
        string suffix = o.crystalCost.IsAllColors() ? " (any color)" : "";
        return $"{body} — {IconMarkup.CrystalTag(o.crystalCost)}1{suffix}";
    }
}
