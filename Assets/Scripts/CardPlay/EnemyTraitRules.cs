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

    // The one source of truth for Elusive. SiegeCost's int.MaxValue keeps the
    // arithmetic to a single comparison, but the UI must not quote that number at
    // the player — it branches on this instead and says "cannot be Sieged".
    public static bool CanBeSieged(int index, IReadOnlyList<EnemyCombatant> roster)
        => !EffectiveTraits(roster[index], roster).HasFlag(EnemyTrait.Elusive);

    public static int SiegeCost(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        if (!CanBeSieged(index, roster)) return int.MaxValue;
        int hp = roster[index].HP;
        return EffectiveTraits(roster[index], roster).HasFlag(EnemyTrait.Armored) ? hp * t.armorSiegeMult : hp;
    }

    public static int AttackCost(int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        int hp = roster[index].HP;
        return EffectiveTraits(roster[index], roster).HasFlag(EnemyTrait.Hulking) ? hp * t.hulkAttackMult : hp;
    }

    // What a removal actually costs, by kind. The kill path used to price every
    // enemy at raw HP, which made Elusive/Armored/Hulking inert at the only place
    // they matter; routing through here means a cost can never be read raw again.
    public static int CostFor(AttackKind kind, int index, IReadOnlyList<EnemyCombatant> roster,
        EnemyTraitTuning t)
        => kind == AttackKind.Siege ? SiegeCost(index, roster, t) : AttackCost(index, roster, t);

    // §4.3 step 0-2 + 6, plus the §4.4 attribution weights, in one pass.
    // BLOCKED ENEMIES ARE SKIPPED — but they were already counted for auras,
    // because blocking is not killing (§7.4).
    public static CounterattackPreview BuildPreview(IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        var p = new CounterattackPreview();
        for (int i = 0; i < roster.Count; i++)
        {
            if (roster[i].Blocked) continue;

            var traits = EffectiveTraits(roster[i], roster);
            int basis = Basis(i, roster, t);
            int surcharge = traits.HasFlag(EnemyTrait.Brutal)
                ? BaseAttack(i, roster, t) * t.brutalSurchargeMult : 0;

            p.UnblockedThreat += Threat(i, roster, t);
            p.UnblockedBasis  += basis;
            p.BrutalSurcharge += surcharge;

            int contribution = basis + surcharge;
            p.TotalContribution += contribution;
            if (traits.HasFlag(EnemyTrait.Toxic)) p.ToxicContribution += contribution;
            if (traits.HasFlag(EnemyTrait.Leech)) p.LeechContribution += contribution;
        }
        return p;
    }

    // §4.3 steps 3-7. The CAP (Swift's guarantee) must be applied BEFORE the
    // SURCHARGE (Brutal's punishment), or the cap swallows the surcharge and
    // Brutal does nothing.
    public static int Effective(CounterattackPreview p, int defendLeft)
    {
        int shortfall = p.UnblockedThreat - defendLeft;
        if (shortfall <= 0) return 0;
        int capped = shortfall < p.UnblockedBasis ? shortfall : p.UnblockedBasis;
        return capped + p.BrutalSurcharge;
    }

    // §4.3 step 8. Reuses CombatRules' bite loop so there is exactly one
    // implementation of "divide a shortfall into Toughness-sized bites".
    public static int HandWounds(CounterattackPreview p, int defendLeft, int toughness)
        => Bite(Effective(p, defendLeft), toughness);

    static int Bite(int amount, int toughness)
        => amount <= 0 ? 0 : CombatRules.WoundCount(AttackKind.Normal, 0, amount, toughness);

    // §4.4 The summed counterattack has no notion of whose wound is whose, so
    // payout traits claim a share of the total contribution. One function
    // serves both Toxic and Leech.
    static int Share(CounterattackPreview p, int defendLeft, int traitContribution)
    {
        if (p.TotalContribution <= 0 || traitContribution <= 0) return 0;
        return Effective(p, defendLeft) * traitContribution / p.TotalContribution; // floor
    }

    public static int DiscardWounds(CounterattackPreview p, int defendLeft, int toughness, EnemyTraitTuning t)
        => Bite(Share(p, defendLeft, p.ToxicContribution), toughness) * t.toxicCopies;

    public static int CrystalsStolen(CounterattackPreview p, int defendLeft, int toughness, EnemyTraitTuning t)
        => Bite(Share(p, defendLeft, p.LeechContribution), toughness) * t.leechCrystals;

    // Vengeful punishes the ATTACK phase only, so the Siege wound-free promise
    // in mechanics.md is preserved and rewarded rather than broken. This is the
    // trait that makes Siege matter in a solo fight.
    public static int VengefulWounds(EnemyCombatant e, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
        => EffectiveTraits(e, roster).HasFlag(EnemyTrait.Vengeful) ? t.vengefulWounds : 0;

    // The whole counterattack in one call. Unit armor IS Defend, so `soak` is
    // simply added to defendLeft and every existing trait behaviour follows for
    // free. Exactly two traits are exceptions: Leech reads the PRE-soak number,
    // and Toxic transfers — its discard copies become 0 and each committed unit
    // takes 2 wounds instead of 1.
    public static CounterattackOutcome Resolve(CounterattackPreview p, int defendLeft, int soak,
        int toughness, EnemyTraitTuning t, int committedUnits)
    {
        bool anyToxic = p.ToxicContribution > 0;
        bool sheltered = committedUnits > 0;

        return new CounterattackOutcome
        {
            HandWounds     = HandWounds(p, defendLeft + soak, toughness),
            DiscardWounds  = sheltered && anyToxic ? 0 : DiscardWounds(p, defendLeft, toughness, t),
            CrystalsStolen = CrystalsStolen(p, defendLeft, toughness, t),
            WoundsPerCommittedUnit = anyToxic ? 2 : 1,
        };
    }

    // Harrying costs hand size on the turn AFTER a flee. Flat, not per-enemy:
    // it is a property of the fight you fled, so two harriers do not cost two
    // cards. Blocked enemies still count — blocking is not killing.
    public static int HarryPenalty(IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning t)
    {
        for (int i = 0; i < roster.Count; i++)
            if (EffectiveTraits(roster[i], roster).HasFlag(EnemyTrait.Harrying))
                return t.harryHandPenalty;
        return 0;
    }
}

// One pass over the unblocked survivors, so CombatPhaseRules.Advance takes a
// single argument instead of a growing parameter list. Contribution is the
// attribution weight for share traits (§4.4).
public struct CounterattackPreview
{
    public int UnblockedThreat;
    public int UnblockedBasis;
    public int BrutalSurcharge;
    public int TotalContribution;
    public int ToxicContribution;
    public int LeechContribution;
}

// Everything one counterattack does, so CombatController makes a single call
// instead of four and can never apply three of the four.
public struct CounterattackOutcome
{
    public int HandWounds;
    public int DiscardWounds;
    public int CrystalsStolen;
    public int WoundsPerCommittedUnit;
}
