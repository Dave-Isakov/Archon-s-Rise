using System.Collections.Generic;

// The trait pipeline (spec 2026-07-29 §4). Pure and Unity-free so it runs in
// the CLI harness. CombatRules is NOT modified — this calls into it for the
// one and only Toughness bite loop.
public static class EnemyTraitRules
{
    // §4.1 Auras resolve to a granted mask BEFORE anything else runs, so every
    // downstream step reads effective traits and needs no aura awareness.
    // Blocked enemies still grant: blocking is not killing (§7.4).
    public static EnemyTrait GrantedByAuras(IReadOnlyList<EnemyCombatant> roster)
    {
        EnemyTrait granted = EnemyTrait.None;
        for (int i = 0; i < roster.Count; i++)
        {
            var t = roster[i].Traits;
            if (t.HasFlag(EnemyTrait.Miasma))   granted |= EnemyTrait.Toxic;
            if (t.HasFlag(EnemyTrait.Ironclad)) granted |= EnemyTrait.Armored;
            if (t.HasFlag(EnemyTrait.Outrider)) granted |= EnemyTrait.Swift;
        }
        return granted;
    }

    public static EnemyTrait EffectiveTraits(EnemyCombatant e, IReadOnlyList<EnemyCombatant> roster)
        => e.Traits | GrantedByAuras(roster);
}
