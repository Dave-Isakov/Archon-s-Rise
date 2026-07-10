using System.Collections.Generic;
using UnityEngine;

// Inspector wrapper for the combat-reward balance knobs (DoomTuningSO pattern:
// one asset, every value tunable with no code change) plus the per-tier card
// reward pools. Card refs are Unity objects, so the pools live here rather than
// in the pure RewardTuning. Pool membership IS a card's rarity — a card may
// appear in several tiers (spec 2026-07-10).
[CreateAssetMenu(fileName = "RewardTuning", menuName = "ScriptableObjects/RewardTuning")]
public class RewardTuningSO : ScriptableObject
{
    public RewardTuning tuning = new RewardTuning();

    // Tier 1 = Beginner (the starting set), Tiers 2-3 add stronger cards.
    public List<CardsSO> tier1Cards = new List<CardsSO>();
    public List<CardsSO> tier2Cards = new List<CardsSO>();
    public List<CardsSO> tier3Cards = new List<CardsSO>();

    public RewardTuning Data => tuning;
    public float CrystalChance(int tier) => tuning.CrystalChance(tier);
    public float CardChance(int tier) => tuning.CardChance(tier);

    public List<CardsSO> CardPool(int tier)
        => tier <= 1 ? tier1Cards : tier == 2 ? tier2Cards : tier3Cards;
}
