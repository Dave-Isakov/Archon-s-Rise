// The single owner of TMP sprite-tag names and cost strings (spec 2026-07-15).
// Each icon is a single-glyph TMP Sprite Asset in
// "Assets/TextMesh Pro/Resources/Sprite Assets/" referenced by asset name, so a
// tag is <sprite="name" index=0>. UI-framework-free: mcs/EditMode testable.
public static class IconMarkup
{
    // Canonical listing order for the four action stats, everywhere they appear.
    public static readonly IconConcept[] ActionStatOrder =
    {
        IconConcept.Attack, IconConcept.Defend, IconConcept.Explore, IconConcept.Influence,
    };

    public static string TmpName(IconConcept concept)
    {
        switch (concept)
        {
            case IconConcept.Attack:     return "Sword";
            case IconConcept.Defend:     return "shield";
            case IconConcept.Explore:    return "scroll";
            case IconConcept.Influence:  return "gem";
            case IconConcept.Heal:       return "Heal";
            case IconConcept.Wound:      return "wound";
            case IconConcept.Crystal:    return "crystal";
            case IconConcept.Siege:      return "siege";
            case IconConcept.Hp:         return "hp";
            case IconConcept.Doom:       return "doom";
            case IconConcept.Experience: return "xp";
            case IconConcept.Army:       return "army";
            case IconConcept.Town:       return "town";
            case IconConcept.Keep:       return "keep";
            case IconConcept.Castle:     return "castle";
            case IconConcept.Dungeon:    return "dungeon";
            case IconConcept.Empower:    return "empower";
            case IconConcept.Refresh:    return "refresh";
            case IconConcept.Card:       return "card";
            case IconConcept.Menu:       return "menu";
            default:                     return "";
        }
    }

    public static string Tag(IconConcept concept)
        => $"<sprite=\"{TmpName(concept)}\" index=0>";

    // The canonical cost form: icon immediately followed by the number.
    public static string Cost(IconConcept concept, int amount)
        => $"{Tag(concept)}{amount}";

    // Canonical crystal-color hexes (house pattern: tint the one crystal glyph).
    public static string CrystalHex(EmpowerType color)
    {
        if (color == EmpowerType.Red)    return "E5484D";
        if (color == EmpowerType.Yellow) return "F5D90A";
        if (color == EmpowerType.Green)  return "46A758";
        if (color == EmpowerType.Purple) return "8E4EC6";
        return "";
    }

    // A crystal glyph tinted by color; None or all-colors renders untinted.
    public static string CrystalTag(EmpowerType color)
    {
        string hex = color == EmpowerType.None || color.IsAllColors() ? "" : CrystalHex(color);
        return hex.Length == 0
            ? Tag(IconConcept.Crystal)
            : $"<sprite=\"{TmpName(IconConcept.Crystal)}\" index=0 color=#{hex}>";
    }

    // Maps a single StatType flag to its concept. False for None and
    // combined flags.
    public static bool TryForStat(StatType stat, out IconConcept concept)
    {
        concept = IconConcept.Attack;
        if (stat == StatType.Attack)    { concept = IconConcept.Attack;    return true; }
        if (stat == StatType.Defend)    { concept = IconConcept.Defend;    return true; }
        if (stat == StatType.Explore)   { concept = IconConcept.Explore;   return true; }
        if (stat == StatType.Influence) { concept = IconConcept.Influence; return true; }
        if (stat == StatType.Heal)      { concept = IconConcept.Heal;      return true; }
        if (stat == StatType.Wound)     { concept = IconConcept.Wound;     return true; }
        if (stat == StatType.Crystal)   { concept = IconConcept.Crystal;   return true; }
        if (stat == StatType.Siege)     { concept = IconConcept.Siege;     return true; }
        if (stat == StatType.Refresh)   { concept = IconConcept.Refresh;   return true; }
        return false;
    }

    // --- Enemy trait badges (spec 2026-07-29 §8.1) ---
    // Badges route through IconMarkup like every other glyph, so call sites
    // never touch a raw sprite name.

    public static readonly EnemyTrait[] AllTraits =
    {
        EnemyTrait.Armored, EnemyTrait.Elusive, EnemyTrait.Hulking, EnemyTrait.Swift,
        EnemyTrait.Brutal,  EnemyTrait.Toxic,   EnemyTrait.Leech,   EnemyTrait.Harrying,
        EnemyTrait.Vengeful,
        EnemyTrait.Warlord, EnemyTrait.Miasma,  EnemyTrait.Ironclad, EnemyTrait.Outrider,
    };

    public static string TraitBadge(EnemyTrait t)
    {
        string name = TraitSpriteName(t);
        return name.Length == 0 ? "" : $"<sprite=\"{name}\" index=0>";
    }

    static string TraitSpriteName(EnemyTrait t)
    {
        switch (t)
        {
            case EnemyTrait.Armored:  return "traitArmored";
            case EnemyTrait.Brutal:   return "traitBrutal";
            case EnemyTrait.Elusive:  return "traitElusive";
            case EnemyTrait.Harrying: return "traitHarrying";
            case EnemyTrait.Hulking:  return "traitHulking";
            case EnemyTrait.Ironclad: return "traitIronclad";
            case EnemyTrait.Leech:    return "traitLeech";
            case EnemyTrait.Miasma:   return "traitMiasma";
            case EnemyTrait.Outrider: return "traitOutrider";
            case EnemyTrait.Swift:    return "traitSwift";
            case EnemyTrait.Toxic:    return "traitToxic";
            case EnemyTrait.Vengeful: return "traitVengeful";
            case EnemyTrait.Warlord:  return "traitWarlord";
            default: return "";
        }
    }

    public static string TraitName(EnemyTrait t)
    {
        switch (t)
        {
            case EnemyTrait.Armored:  return "Armored";
            case EnemyTrait.Brutal:   return "Brutal";
            case EnemyTrait.Elusive:  return "Elusive";
            case EnemyTrait.Harrying: return "Harrying";
            case EnemyTrait.Hulking:  return "Hulking";
            case EnemyTrait.Ironclad: return "Ironclad";
            case EnemyTrait.Leech:    return "Leech";
            case EnemyTrait.Miasma:   return "Miasma";
            case EnemyTrait.Outrider: return "Outrider";
            case EnemyTrait.Swift:    return "Swift";
            case EnemyTrait.Toxic:    return "Toxic";
            case EnemyTrait.Vengeful: return "Vengeful";
            case EnemyTrait.Warlord:  return "Warlord";
            default: return "";
        }
    }

    // Aura traits (Warlord/Miasma/Ironclad/Outrider) once rendered amber-tinted
    // so "which of these is buffing the others" read without a hover. Dropped
    // 2026-07-30: TMP tints by MULTIPLYING the glyph, so a colour tint only
    // reads on white/near-white art, and the trait icons are full-colour
    // painted art (blue Warlord, green Miasma, already-gold Ironclad) that
    // turns muddy under it. Auras are now distinguished by their own icon plus
    // the hover legend. Restoring the tint needs monochrome badge art first —
    // then this is `<sprite="name" index=0 color=#F5D90A>`, colour as an
    // attribute ON the sprite tag, never a wrapping <color> tag.
    //
    // The aura COMBAT rules are unaffected and live in EnemyTraitRules
    // (GrantedByAuras/WarlordAura); nothing here ever fed them.
}
