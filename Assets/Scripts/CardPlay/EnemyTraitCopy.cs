using System.Collections.Generic;

// Trait rule lines, GENERATED from the tuning values (spec §8.2). Authored
// strings would go stale the first time a number moved, and EnemyTraitTuning
// exists precisely so numbers can move freely in playtest.
//
// Lives in CardPlay, not UiLanguage, because UiLanguage cannot reference
// CardPlay and this needs EnemyTraitTuning.
public static class EnemyTraitCopy
{
    public static List<EnemyTrait> Split(EnemyTrait mask)
    {
        var list = new List<EnemyTrait>();
        foreach (var t in IconMarkup.AllTraits)
            if (mask.HasFlag(t)) list.Add(t);
        return list;
    }

    public static string Rule(EnemyTrait t, EnemyTraitTuning tuning)
    {
        string hp     = IconMarkup.Tag(IconConcept.Hp);
        string siege  = IconMarkup.Tag(IconConcept.Siege);
        string attack = IconMarkup.Tag(IconConcept.Attack);
        string defend = IconMarkup.Tag(IconConcept.Defend);
        string wound  = IconMarkup.Tag(IconConcept.Wound);
        string cryst  = IconMarkup.Tag(IconConcept.Crystal);

        switch (t)
        {
            case EnemyTrait.Armored:
                return siege + " must cover " + tuning.armorSiegeMult + "x its " + hp;
            case EnemyTrait.Hulking:
                return attack + " must cover " + tuning.hulkAttackMult + "x its " + hp;
            case EnemyTrait.Elusive:
                return siege + " cannot remove it";
            case EnemyTrait.Swift:
                return "Needs " + tuning.swiftThreatMult + "x " + defend + " to block";
            case EnemyTrait.Brutal:
                return "Unblocked, it strikes for " + (1 + tuning.brutalSurchargeMult) + "x";
            case EnemyTrait.Toxic:
                return "Its " + wound + " are doubled into your discard";
            case EnemyTrait.Leech:
                return "Steals " + tuning.leechCrystals + " " + cryst + " per " + wound;
            case EnemyTrait.Vengeful:
                return "Killing it with " + attack + " costs " + tuning.vengefulWounds + " " + wound;
            case EnemyTrait.Harrying:
                return "Fleeing costs " + tuning.harryHandPenalty + " hand size next turn";
            case EnemyTrait.Warlord:
                return "Every other enemy gains +" + tuning.warlordBonus + " " + attack;
            case EnemyTrait.Miasma:
                return "Every enemy becomes Toxic";
            case EnemyTrait.Ironclad:
                return "Every enemy becomes Armored";
            case EnemyTrait.Outrider:
                return "Every enemy becomes Swift";
            default: return "";
        }
    }
}
