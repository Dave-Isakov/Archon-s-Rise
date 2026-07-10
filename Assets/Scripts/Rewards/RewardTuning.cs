// The combat-reward balance knobs. A plain serializable class (not a
// ScriptableObject) so RewardRules stays Unity-free and mcs-testable;
// RewardTuningSO wraps one instance for the inspector and adds the per-tier
// card pools (Unity object refs, which cannot live here). All values are
// balance.md starting values (spec 2026-07-10).
[System.Serializable]
public class RewardTierTuning
{
    public int expMin;
    public int expMax;
    // Independent bonus-roll odds on defeat (0..1). Crystals common, cards rare.
    public float crystalChance;
    public float cardChance;
}

[System.Serializable]
public class RewardTuning
{
    // How many uniform draws are averaged for the exp bell curve. Higher =
    // tighter bell around the range's centre. 1 = flat/uniform.
    public int expBellSamples = 3;

    // Level-up card picks draw from a tier chosen by player level: tier 2 at
    // level >= levelTier2, tier 3 at level >= levelTier3 (mirrors enemy drops
    // scaling with doom tier, so card strength tracks run progress).
    public int levelTier2 = 4;
    public int levelTier3 = 7;

    // Tier 1 = Beginner, Tier 2 = Intermediate, Tier 3 = Advanced.
    public RewardTierTuning tier1 = new RewardTierTuning { expMin = 1, expMax = 5, crystalChance = 0.50f, cardChance = 0.08f };
    public RewardTierTuning tier2 = new RewardTierTuning { expMin = 3, expMax = 7, crystalChance = 0.60f, cardChance = 0.12f };
    public RewardTierTuning tier3 = new RewardTierTuning { expMin = 6, expMax = 10, crystalChance = 0.70f, cardChance = 0.18f };

    // Clamp any tier value to the active 1..3 set and return its config.
    public RewardTierTuning Tier(int tier)
        => tier <= 1 ? tier1 : tier == 2 ? tier2 : tier3;

    public float CrystalChance(int tier) => Tier(tier).crystalChance;
    public float CardChance(int tier) => Tier(tier).cardChance;
}
