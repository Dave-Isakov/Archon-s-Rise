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

    // §4.2 Warlord grants real Attack to every OTHER survivor. Additive, so two
    // Warlords stack — unlike granting auras, which are idempotent by OR.
    public static int WarlordAura(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        int count = 0;
        for (int i = 0; i < roster.Count; i++)
            if (i != index && roster[i].Traits.HasFlag(EnemyTrait.Warlord)) count++;
        return count * t.warlordBonus;
    }

    public static int BaseAttack(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
        => roster[index].Attack + WarlordAura(index, roster, t);

    // The bar Defend must clear. Swift raises it WITHOUT raising the punishment.
    public static int Threat(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        int b = BaseAttack(index, roster, t);
        return EffectiveTraits(roster[index], roster).HasFlag(EnemyTrait.Swift) ? b * t.swiftThreatMult : b;
    }

    // What can become wounds. Never scaled by Swift — that is the cap's job.
    public static int Basis(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
        => BaseAttack(index, roster, t);

    // Elusive returns int.MaxValue rather than needing a separate bool, so the
    // Siege phase keeps exactly one comparison.
    public static int SiegeCost(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        var traits = EffectiveTraits(roster[index], roster);
        if (traits.HasFlag(EnemyTrait.Elusive)) return int.MaxValue;
        int hp = roster[index].HP;
        return traits.HasFlag(EnemyTrait.Armored) ? hp * t.armorSiegeMult : hp;
    }

    public static int AttackCost(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        int hp = roster[index].HP;
        return EffectiveTraits(roster[index], roster).HasFlag(EnemyTrait.Hulking) ? hp * t.hulkAttackMult : hp;
    }
}
