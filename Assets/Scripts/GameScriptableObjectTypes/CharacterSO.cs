using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// A playable character: the content bundle that makes one hero different from
// another (spec 2026-07-23, Part A). Characters differ ONLY in starting cards,
// skill pool, level table, and these scalars — never in rules. Rule-bending
// belongs in SkillEffect (the Charismatic/RecruitEnemies precedent), and every
// character starts with zero skills.
//
// Renamed from PlayerSO 2026-07-23; the file+class were renamed together so the
// .meta guid — and therefore the authored asset binding — survived. The three
// FormerlySerializedAs names are PlayerSO's, so the shipped Player1.asset keeps
// its authored name/hand size/deck instead of resetting to defaults.
[CreateAssetMenu(fileName = "Character", menuName = "ScriptableObjects/CharacterSO")]
public class CharacterSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] string id;
    [FormerlySerializedAs("playerName")]
    [SerializeField] string characterName;

    [Header("Starting stats")]
    // Toughness is a DIVISOR of the Defend shortfall, not a health pool: higher
    // = fewer wounds per bad fight. Seeds Player at run start only; it then
    // grows via level-up toughnessBonus and is restored from the save on load.
    [SerializeField] int startingToughness = 2;
    [FormerlySerializedAs("playerHandSize")]
    [SerializeField] int handSize = 5;
    [SerializeField] int improvAttack = 1;
    [SerializeField] int improvDefend = 1;
    [SerializeField] int improvExplore = 1;
    [SerializeField] int improvInfluence = 1;

    [Header("Content")]
    [FormerlySerializedAs("startingHand")]
    [SerializeField] List<CardsSO> startingDeck = new();
    [SerializeField] LevelRewardsSO levelTable;
    [SerializeField] SkillPoolSO skillPool;

    [Header("Presentation")]
    [SerializeField] RuntimeAnimatorController animatorController;

    public string Id => id;
    public string CharacterName => characterName;
    public int StartingToughness => startingToughness;
    public int HandSize => handSize;
    public int ImprovAttack => improvAttack;
    public int ImprovDefend => improvDefend;
    public int ImprovExplore => improvExplore;
    public int ImprovInfluence => improvInfluence;
    public List<CardsSO> StartingDeck => startingDeck;
    public LevelRewardsSO LevelTable => levelTable;
    public SkillPoolSO SkillPool => skillPool;
    public RuntimeAnimatorController AnimatorController => animatorController;

    void OnValidate()
    {
        // A 0 toughness would be a divide-by-zero-shaped hang in
        // CombatRules.WoundCount. The rule clamps too, but refuse to author it.
        if (startingToughness < 1) startingToughness = 1;

        if (string.IsNullOrEmpty(id))
            Debug.LogWarning($"{name}: CharacterSO needs a stable id (used by the save file).", this);
        if (startingDeck == null || startingDeck.Count == 0)
            Debug.LogWarning($"{name}: CharacterSO has an empty startingDeck.", this);
        if (levelTable == null)
            Debug.LogWarning($"{name}: CharacterSO has no levelTable.", this);
        if (skillPool == null)
            Debug.LogWarning($"{name}: CharacterSO has no skillPool.", this);
    }
}
