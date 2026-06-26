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
    private int playerHandSize = 5;
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
    public int PlayerHP { get => playerHP; set => playerHP = value;}
    public int PlayerHandSize { get => playerHandSize; set => playerHandSize = value;}
    public int PlayerAttack { get => playerAttack; set => playerAttack = value; }
    public int PlayerDefend { get => playerDefend; set => playerDefend = value;}
    public int PlayerInfluence { get => playerInfluence; set => playerInfluence = value;}
    public int PlayerExplore { get => playerExplore; set => playerExplore = value; }
    public int PlayerExp { get => playerExp; set => playerExp = value; }
    public bool InTown { get => inTown; set => inTown = value; }
    public bool InDungeon { get => inDungeon; set => inDungeon = value; }
    public bool InCombat { get => inCombat; set => inCombat = value; }
    public int ExpToNextLevel { get => expToNextLevel; }
    public int PlayerLevel { get => playerLevel; set => playerLevel = value; }

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

    void Awake()
    {
        playerHandSize = player.PlayerHandSize;
        var newUnit = Instantiate(unitPrefab, new Vector3(0,0,0), Quaternion.identity, GameObject.Find("Units").transform);
    }

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
    }
    public void UnAssignPlayerStats(int[] stats)
    {
        playerAttack -= stats[0];
        playerDefend -= stats[1];
        playerInfluence -= stats[2];
        playerExplore -= stats[3];
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
    public void ValidatePlayerAttackToEnemyHP(EnemyCard enemy)
    {
        if(playerAttack >= enemy.enemySO.enemyHP)
        {
            playerAttack -= enemy.enemySO.enemyHP;
            CheckWounds(enemy);
            playerDefend -= enemy.enemySO.enemyAttack;
            OnEnemyDefeat_GetRewards.Raise(enemy);
        }
        else 
            GameManager.Instance.ValidationMessage($"You need {enemy.enemySO.enemyHP} Attack in order to defeat this monster.");
    }

    public void RecruitUnit(TownToken town)
    {
        units.Add(town.townSO.recruitableUnits[0]);
        var newUnit = Instantiate(unitPrefab, new Vector3(0,0,0), Quaternion.identity, GameObject.Find("Units").transform);
        newUnit.GetComponent<Unit>().unitSO = town.townSO.recruitableUnits[0];
    }
    public void PlayUnit(Unit unit)
    {
        if(!unit.IsPlayed)
        {
            AssignPlayerStats(unit.unitSO.GetUnitStats());
            unit.transform.Rotate(0 ,0 , -90);
            unit.IsPlayed = true;
        }
        else if(unit.IsPlayed)
        {
            UnAssignPlayerStats(unit.unitSO.GetUnitStats());
            unit.transform.Rotate(0 ,0 , 90);
            unit.IsPlayed = false;
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
    }

    public void PlayerLevelUp()
    {
        playerLevel++;
        expToNextLevel = expToNextLevel + playerLevel + 12;
        playerExp = 0;
        //Levelupscreen (even number levels +1 to a stat)(odd number levels HP+)(every 3 levels handsize+)(every level new skill)
    }

    public void CheckWounds(EnemyCard enemy)
    {
        if (playerDefend < enemy.enemySO.enemyAttack)
        {
            int woundCount = 0;
            for (var i = 0; i < enemy.enemySO.enemyAttack-playerDefend; i += playerHP)
            {
                onDefeat_WoundPlayer.Raise(enemy);
                woundCount++;
            }
            GameManager.Instance.ValidationMessage($"{enemy.enemySO.name} has been destroyed! You are wounded {woundCount} times!");
        }
    }

    // Autosave on quit only. Saving from OnDisable fired on every scene
    // transition/destroy, which could overwrite a good save with default
    // stats while a load was in progress.
    private void OnApplicationQuit()
    {
        if (DataManager.Instance == null || DataManager.Instance.IsLoading) return;
        DataManager.Instance.playerData = new PlayerData(this, playerPosition);
        DataManager.Instance.SaveGame();
    }
}
