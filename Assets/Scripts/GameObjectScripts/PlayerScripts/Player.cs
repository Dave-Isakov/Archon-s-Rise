using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    [SerializeField] PlayerSO player;
    [SerializeField] GameObject unitPrefab;
    [SerializeField] PlayerPosition playerPosition;
    private int playerAttack;
    private int playerDefend;
    public int playerInfluence;
    public int playerExplore;
    private int playerSiege;
    private int improvAttackValue = 1;
    private int improvDefendValue = 1;
    private int improvInfluenceValue = 1;
    private int improvExploreValue = 1;
    private int playerHP = 2;
    [SerializeField] private int playerExp;
    [SerializeField] private int expToNextLevel;
    [SerializeField] private int playerLevel;
    private bool inDungeon;
    private bool inCombat;
    private bool inTown;
    [SerializeField] private List<UnitsSO> units = new();
    [SerializeField] LevelRewardsSO levelRewards;
    [SerializeField] List<SkillsSO> skills = new();
    public int PlayerHP { get => playerHP; set => playerHP = value;}
    // Derived, never stored: base size from PlayerSO plus every table bonus at
    // or below the current level. Same derivation on load, so saves can't drift.
    public int PlayerHandSize => LevelRules.DerivedHandSize(player.PlayerHandSize, playerLevel, levelRewards.Entries);
    public int PlayerAttack { get => playerAttack; set => playerAttack = value; }
    public int PlayerDefend { get => playerDefend; set => playerDefend = value;}
    public int PlayerInfluence { get => playerInfluence; set => playerInfluence = value;}
    public int PlayerExplore { get => playerExplore; set => playerExplore = value; }
    public int PlayerSiege { get => playerSiege; set => playerSiege = value; }
    public int PlayerExp { get => playerExp; set => playerExp = value; }
    public bool InTown { get => inTown; set => inTown = value; }
    public bool InDungeon { get => inDungeon; set => inDungeon = value; }
    public bool InCombat { get => inCombat; set => inCombat = value; }
    public int ExpToNextLevel { get => expToNextLevel; set => expToNextLevel = value; }
    public int PlayerLevel { get => playerLevel; set => playerLevel = value; }
    public IReadOnlyList<UnitsSO> Units => units;
    public int ArmyCap => LevelRules.DerivedArmyCap(playerLevel, levelRewards.Entries);
    public LevelRewardsSO LevelRewards => levelRewards;
    public IReadOnlyList<SkillsSO> Skills => skills;
    // Charismatic passive: influenced enemies with a recruitedUnit join the army.
    public bool HasCharismatic => skills.Exists(s => s.effect == SkillEffect.RecruitEnemies);

    [Header("Events")]
    [SerializeField] VoidEvent onSuccessfulExploration_ExploreNextCard;
    [SerializeField] CardEvent onEmpower_DestroyCrystalGameObject;
    [SerializeField] CardEvent onUndo_RegenerateCrystalGameObject;
    [SerializeField] CardEvent onPlay_TriggerAdditionalEffects;
    [SerializeField] PlayerEvent onEnemyDefeat_CheckPlayerDefendForWound;
    [SerializeField] EnemyCardEvent OnEnemyDefeat_GetRewards;
    [SerializeField] IntEvent onInfluenceEvent_GetCurrentInfluence;
    [SerializeField] IntEvent OnExploreEvent_GetCurrentExplore;
    [SerializeField] EnemyCardEvent onDefeat_WoundPlayer;

    void Start()
    {
        OnExploreEvent_GetCurrentExplore.Raise(playerExplore);
    }
    
    void Update()
    {
        if (playerDefend < 0) playerDefend = 0;
        if(playerExp >= expToNextLevel) PlayerLevelUp();
    }

    public void AssignPlayerStats(int[] stats)
    {
        playerAttack += stats[0];
        playerDefend += stats[1];
        playerInfluence += stats[2];
        playerExplore += stats[3];
        playerSiege += stats[4];
        // Push the new explore total to the map's arrow buttons; they cache it
        // via OnExploreEvent and won't see a unit/card gain otherwise.
        GetCurrentExplore();
    }
    public void UnAssignPlayerStats(int[] stats)
    {
        playerAttack -= stats[0];
        playerDefend -= stats[1];
        playerInfluence -= stats[2];
        playerExplore -= stats[3];
        playerSiege -= stats[4];
        GetCurrentExplore();
    }

    public void Exploration(int newExplore)
    {
        playerExplore = newExplore;
        GameManager.Instance.commands.ClearStack();
    }
    
    public void Influence(int influenceCost)
    {
        playerInfluence -= influenceCost;
        GetCurrentInfluence();
        GameManager.Instance.commands.ClearStack();
    }

    public void ImprovAttack(Card card)
    {
        if(!card.IsPlayed)
        {
            playerAttack += improvAttackValue;
            card.IsPlayed = true;
        }
        else if(card.IsPlayed)
        {
            playerAttack -= improvAttackValue;
            card.IsPlayed = false;
        }
    }
    public void ImprovDefend(Card card)
    {
        if(!card.IsPlayed)
        {
            playerDefend += improvDefendValue;
            card.IsPlayed = true;
        }
        else if(card.IsPlayed)
        {
            playerDefend -= improvDefendValue;
            card.IsPlayed = false;
        }
    }
    public void ImprovInfluence(Card card)
    {
        if(!card.IsPlayed)
        {
            playerInfluence += improvInfluenceValue;
            card.IsPlayed = true;
        }
        else if(card.IsPlayed)
        {
            playerInfluence -= improvInfluenceValue;
            card.IsPlayed = false;
        }
    }
    public void ImprovExplore(Card card)
    {
        if(!card.IsPlayed)
        {
            playerExplore += improvExploreValue;
            card.IsPlayed = true;
        }
        else if(card.IsPlayed)
        {
            playerExplore -= improvExploreValue;
            card.IsPlayed = false;
        }
        GetCurrentExplore();
    }
    public void PlayCard(Card card)
    {
        if(!card.IsPlayed)
        {
            AssignPlayerStats(card.cardSO.GetCardStats(card.IsEmpowered));
            EmpowerCrystalCheck(card);
            onPlay_TriggerAdditionalEffects.Raise(card);
        }
        else if(card.IsPlayed)
        {
            UnAssignPlayerStats(card.cardSO.GetCardStats(card.IsEmpowered));
            UndoEmpower(card);
            onPlay_TriggerAdditionalEffects.Raise(card);
        }
    }
    public void AttackChoice(Card card)
    {
        if(!card.IsPlayed)
        {
            playerAttack += card.cardSO.ReturnAttack(card.IsEmpowered);
            EmpowerCrystalCheck(card);
        }
        else if(card.IsPlayed)
        {
            playerAttack -= card.cardSO.ReturnAttack(card.IsEmpowered);
            UndoEmpower(card);
        }
    }
    public void DefendChoice(Card card)
    {
        if(!card.IsPlayed)
        {
            playerDefend += card.cardSO.ReturnDefend(card.IsEmpowered);
            EmpowerCrystalCheck(card);
        }
        else if(card.IsPlayed)
        {
            playerDefend -= card.cardSO.ReturnDefend(card.IsEmpowered);
            UndoEmpower(card);
        }
    }
    public void InfluenceChoice(Card card)
    {
        if(!card.IsPlayed)
        {
            playerInfluence += card.cardSO.ReturnInfluence(card.IsEmpowered);
            EmpowerCrystalCheck(card);
        }
        else if(card.IsPlayed)
        {
            playerInfluence -= card.cardSO.ReturnInfluence(card.IsEmpowered);
            UndoEmpower(card);
        }
    }
    public void ExploreChoice(Card card)
    {
        if(!card.IsPlayed)
        {
            playerExplore += card.cardSO.ReturnExplore(card.IsEmpowered);
            EmpowerCrystalCheck(card);
        }
        else if(card.IsPlayed)
        {
            playerExplore -= card.cardSO.ReturnExplore(card.IsEmpowered);
            UndoEmpower(card);
        }
        GetCurrentExplore();
    }

    private void UndoEmpower(Card card)
    {
        if (card.IsEmpowered)
            onUndo_RegenerateCrystalGameObject.Raise(card);
        card.IsPlayed = false;
    }

    private void EmpowerCrystalCheck(Card card)
    {
        if (card.IsEmpowered)
            onEmpower_DestroyCrystalGameObject.Raise(card);
        card.IsPlayed = true;
    }
    // Normal attack: spends the Attack pool, and the enemy's counterattack can wound
    // the player when Defend falls short. Entry point wired to the Fight button.
    public void ValidatePlayerAttackToEnemyHP(EnemyCard enemy)
    {
        ResolveAttack(enemy, AttackKind.Normal);
    }

    // Siege attack: spends the Siege pool and is always wound-free. Entry point
    // wired to the Siege button.
    public void SiegeEnemy(EnemyCard enemy)
    {
        ResolveAttack(enemy, AttackKind.Siege);
    }

    void ResolveAttack(EnemyCard enemy, AttackKind kind)
    {
        int hp = enemy.EffectiveHP;
        if (!CombatRules.CanDefeat(kind, playerAttack, playerSiege, hp))
        {
            string need = kind == AttackKind.Siege ? "Siege" : "Attack (Siege counts)";
            GameManager.Instance.ValidationMessage($"You need {hp} {need} in order to defeat this monster.");
            return;
        }

        if (kind == AttackKind.Siege)
        {
            playerSiege -= hp;
        }
        else
        {
            // Attack drains first; Siege covers only the shortfall.
            int fromSiege = CombatRules.SiegeSpentOnNormal(playerAttack, hp);
            playerAttack -= hp - fromSiege;
            playerSiege  -= fromSiege;
        }

        int wounds = CombatRules.WoundCount(kind, playerDefend, enemy.EffectiveAttack, playerHP);
        for (int i = 0; i < wounds; i++)
            onDefeat_WoundPlayer.Raise(enemy);

        // Only a Normal attack takes the counterattack against Defend.
        if (kind == AttackKind.Normal) playerDefend -= enemy.EffectiveAttack;

        if (wounds > 0)
            GameManager.Instance.ValidationMessage($"{enemy.enemySO.name} has been destroyed! You are wounded {wounds} times!");

        OnEnemyDefeat_GetRewards.Raise(enemy);
    }

    // Influence resolution (spec 2026-07-09): pay the cost to end the fight
    // wound-free WITH defeat rewards; with Charismatic and a recruitedUnit the
    // same payment also adds the unit (rewards + unit). At the army cap the
    // disband picker runs first; cancelling it spends nothing.
    public void InfluenceEnemy(EnemyCard enemy)
    {
        if (!enemy.enemySO.canInfluence) return;
        int cost = enemy.enemySO.influenceCost;
        if (playerInfluence < cost)
        {
            GameManager.Instance.ValidationMessage($"You need {cost} Influence to sway {enemy.enemySO.cardName}.");
            return;
        }

        bool recruit = enemy.enemySO.recruitedUnit != null && HasCharismatic;
        if (recruit && ArmyRules.NeedsDisband(units.Count, ArmyCap))
        {
            FindAnyObjectByType<DisbandPanel>().OpenForHire(() => CompleteInfluence(enemy, true));
            return;
        }
        CompleteInfluence(enemy, recruit);
    }

    void CompleteInfluence(EnemyCard enemy, bool recruit)
    {
        if (recruit) AddUnit(enemy.enemySO.recruitedUnit);
        GameManager.Instance.ValidationMessage(recruit
            ? $"{enemy.enemySO.cardName} joins your army!"
            : $"{enemy.enemySO.cardName} departs peacefully.");
        Influence(enemy.enemySO.influenceCost); // spend + clear undo stack (standard for influence spends)
        OnEnemyDefeat_GetRewards.Raise(enemy);  // rewards + the defeat/cleanup chain; no counterattack ran = wound-free
    }

    public void AddUnit(UnitsSO so)
    {
        units.Add(so);
        var newUnit = Instantiate(unitPrefab, new Vector3(0, 0, 0), Quaternion.identity,
            GameObject.Find("Units").transform);
        newUnit.GetComponent<Unit>().unitSO = so;
    }

    // Disband-to-hire: removes one unit to make room at the army cap. A played
    // unit keeps its stat contribution for this turn (it fought its last
    // battle); pools reset at turn end anyway. The town flow clears the undo
    // stack right after hiring, so no stale UnitCommand can reference it.
    public void DisbandUnit(Unit unit)
    {
        units.Remove(unit.unitSO);
        Destroy(unit.gameObject);
    }

    public void RebuildUnits(List<UnitsSO> unitSOs, bool[] exhausted = null)
    {
        // Clear any existing Unit GameObjects (including the placeholder created in Awake) and the list.
        foreach (var existing in FindObjectsByType<Unit>())
            Destroy(existing.gameObject);
        units.Clear();

        var unitsParent = GameObject.Find("Units");
        for (int i = 0; i < unitSOs.Count; i++)
        {
            var so = unitSOs[i];
            if (so == null) continue;
            units.Add(so);
            var newUnit = Instantiate(unitPrefab, new Vector3(0, 0, 0), Quaternion.identity,
                unitsParent?.transform);
            var unit = newUnit.GetComponent<Unit>();
            unit.unitSO = so;
            if (exhausted != null && i < exhausted.Length && exhausted[i])
            {
                unit.transform.Rotate(0, 0, -90);
                unit.IsPlayed = true;
            }
        }
    }
    // Round end: used units stand back up for the new round. Only the exhausted
    // state resets — their stat contribution was already cleared by TurnEnd, so
    // this must not go through PlayUnit (which would subtract stats again).
    public void RefreshUnits()
    {
        foreach (var unit in FindObjectsByType<Unit>())
        {
            if (unit.IsPlayed)
            {
                unit.transform.Rotate(0, 0, 90);
                unit.IsPlayed = false;
            }
        }
    }

    // Applies ONE authored option (spec 2026-07-09). Crystal cost consumption
    // lives in UnitCommand (which owns the reserved crystal); this method only
    // applies the option's effect and the exhaust state.
    public void ApplyUnitOption(Unit unit, UnitOption option)
    {
        switch (option.effect)
        {
            case UnitEffect.Attack:    playerAttack    += option.amount; break;
            case UnitEffect.Defend:    playerDefend    += option.amount; break;
            case UnitEffect.Siege:     playerSiege     += option.amount; break;
            case UnitEffect.Explore:   playerExplore   += option.amount; GetCurrentExplore();   break;
            case UnitEffect.Influence: playerInfluence += option.amount; GetCurrentInfluence(); break;
            case UnitEffect.Heal:
            {
                var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
                for (int i = 0; i < option.amount; i++) hand.HealWound();
                break;
            }
            case UnitEffect.Crystallize:
            {
                var crystals = FindAnyObjectByType<CrystalInventory>();
                for (int i = 0; i < option.amount; i++) crystals.UnitCrystallize(option.grantColor);
                break;
            }
        }
        PulseStatIcon(option.effect);
        unit.transform.Rotate(0, 0, -90);
        unit.IsPlayed = true;
    }

    // Unit options apply a single effect through the pop-out (no Card), so pulse
    // the matching HUD stat icon directly — the card flow does the same via its
    // play event. Fires on apply only; undo doesn't re-pulse. Effects with no HUD
    // icon (e.g. Heal/Crystallize/Siege if none is wired) simply match nothing.
    void PulseStatIcon(UnitEffect effect)
    {
        StatType stat;
        switch (effect)
        {
            case UnitEffect.Attack:      stat = StatType.Attack;    break;
            case UnitEffect.Defend:      stat = StatType.Defend;    break;
            case UnitEffect.Explore:     stat = StatType.Explore;   break;
            case UnitEffect.Influence:   stat = StatType.Influence; break;
            case UnitEffect.Siege:       stat = StatType.Siege;     break;
            case UnitEffect.Heal:        stat = StatType.Heal;      break;
            case UnitEffect.Crystallize: stat = StatType.Crystal;   break;
            default:                     return;
        }
        foreach (var icon in FindObjectsByType<PlayerIcon>())
            icon.AnimateStat(stat);
    }

    public void RevertUnitOption(Unit unit, UnitOption option)
    {
        switch (option.effect)
        {
            case UnitEffect.Attack:    playerAttack    -= option.amount; break;
            case UnitEffect.Defend:    playerDefend    -= option.amount; break;
            case UnitEffect.Siege:     playerSiege     -= option.amount; break;
            case UnitEffect.Explore:   playerExplore   -= option.amount; GetCurrentExplore();   break;
            case UnitEffect.Influence: playerInfluence -= option.amount; GetCurrentInfluence(); break;
            case UnitEffect.Heal:
            {
                var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
                for (int i = 0; i < option.amount; i++) hand.RestoreHealedWound();
                break;
            }
            case UnitEffect.Crystallize:
            {
                var crystals = FindAnyObjectByType<CrystalInventory>();
                for (int i = 0; i < option.amount; i++) crystals.UndoUnitCrystallize();
                break;
            }
        }
        unit.transform.Rotate(0, 0, 90);
        unit.IsPlayed = false;
    }

    // SkillEvent listener target. Toggles like PlayUnit: the same event fires on
    // command Execute and Undo, so IsUsed decides apply vs revert.
    public void PerformSkillAction(SkillToken token)
    {
        if (!token.IsUsed)
        {
            ApplySkillEffect(token.skillSO, +1);
            token.SetUsed(true);
        }
        else
        {
            ApplySkillEffect(token.skillSO, -1);
            token.SetUsed(false);
        }
    }

    private void ApplySkillEffect(SkillsSO skill, int sign)
    {
        switch (skill.effect)
        {
            case SkillEffect.GainAttack:    playerAttack    += sign * skill.magnitude; break;
            case SkillEffect.GainDefend:    playerDefend    += sign * skill.magnitude; break;
            case SkillEffect.GainInfluence: playerInfluence += sign * skill.magnitude; GetCurrentInfluence(); break;
            case SkillEffect.GainExplore:   playerExplore   += sign * skill.magnitude; GetCurrentExplore(); break;
            case SkillEffect.GainCrystal:
            {
                var crystals = FindAnyObjectByType<CrystalInventory>();
                for (int i = 0; i < skill.magnitude; i++)
                {
                    if (sign > 0) crystals.SkillCrystallize(skill.crystalColor);
                    else          crystals.UndoSkillCrystallize();
                }
                break;
            }
            case SkillEffect.HealWound:
            {
                var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
                for (int i = 0; i < skill.magnitude; i++)
                {
                    if (sign > 0) hand.HealWound();
                    else          hand.RestoreHealedWound();
                }
                break;
            }
            case SkillEffect.RecruitEnemies: break; // passive — no activatable effect
        }
    }

    public void AddSkill(SkillsSO skill)
    {
        skills.Add(skill);
        var bar = FindAnyObjectByType<SkillBar>();
        if (bar != null) bar.AddToken(skill);
    }

    // Save/load path (mirrors RebuildUnits): wipe tokens + list, re-add each
    // owned skill, restore exhausted state by id.
    public void RebuildSkills(List<SkillsSO> skillSOs, HashSet<string> exhaustedIds)
    {
        var bar = FindAnyObjectByType<SkillBar>();
        if (bar != null) bar.Clear();
        skills.Clear();

        foreach (var so in skillSOs)
        {
            if (so == null) continue;
            skills.Add(so);
            var token = bar != null ? bar.AddToken(so) : null;
            if (token != null && exhaustedIds.Contains(so.id))
                token.SetUsed(true);
        }
    }

    // Cadence refresh. Turn end refreshes per-turn skills; round end refreshes
    // everything. Safe against the undo stack: End Turn / End Round clear the
    // command stack before their events fire, so no skill command is undoable
    // by the time this runs.
    public void RefreshSkills(bool includePerRound)
    {
        foreach (var token in FindObjectsByType<SkillToken>())
        {
            if (!token.IsUsed) continue;
            if (token.skillSO.cadence == SkillCadence.PerTurn || includePerRound)
                token.SetUsed(false);
        }
    }

    public void GetCurrentInfluence()
    {
        onInfluenceEvent_GetCurrentInfluence.Raise(playerInfluence);
    }

    public void GetCurrentExplore()
    {
        OnExploreEvent_GetCurrentExplore.Raise(playerExplore);
    }
    
    public void TurnEnd()
    {
        playerAttack = 0;
        playerDefend = 0;
        playerInfluence = 0;
        playerExplore = 0;
        playerSiege = 0;
        GetCurrentExplore(); // clear the arrow buttons' cached explore too
        RefreshSkills(false);
    }

    public void PlayerLevelUp()
    {
        playerLevel++;
        // Overflow exp carries into the next level (the old reset-to-0 discarded
        // it). Update() keeps polling, so back-to-back level-ups fire one per
        // frame and their reward queues chain in order.
        playerExp = LevelRules.CarriedExp(playerExp, expToNextLevel);
        expToNextLevel = expToNextLevel + playerLevel + 12;

        var entry = LevelRules.RewardsFor(playerLevel, levelRewards.Entries);
        if (entry != null) playerHP += entry.hpBonus;

        var controller = FindAnyObjectByType<LevelUpController>();
        if (controller != null) controller.EnqueueLevelRewards(playerLevel, entry);
    }

    // Autosave on quit only. Saving from OnDisable fired on every scene
    // transition/destroy, which could overwrite a good save with default
    // stats while a load was in progress.
    private void OnApplicationQuit()
    {
        if (DataManager.Instance == null || DataManager.Instance.IsLoading) return;
        DataManager.Instance.SaveGame();
    }
}
