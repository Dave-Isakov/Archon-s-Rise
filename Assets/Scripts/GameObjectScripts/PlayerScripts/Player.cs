using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    private int playerAttack;
    private int playerDefend;
    private int playerInfluence;
    private int playerExplore;
    [SerializeField] PlayerSO player;
    private int playerHandSize = 5;
    private int improvAttackValue = 1;
    private int improvDefendValue = 1;
    private int improvInfluenceValue = 1;
    private int improvExploreValue = 1;
    private int playerHP = 2;
    private int playerExp;
    private bool inDungeon;
    private bool inTown;
    [SerializeField] private List<UnitsSO> units = new();
    public int PlayerHP { get => playerHP; }
    public int PlayerHandSize { get => playerHandSize; }
    public int PlayerAttack { get => playerAttack; }
    public int PlayerDefend { get => playerDefend; }
    public int PlayerInfluence { get => playerInfluence; }
    public int PlayerExplore { get => playerExplore; }
    public int PlayerExp { get => playerExp; set => playerExp = value; }
    public bool InTown { get => inTown; set => inTown = value; }
    public bool InDungeon { get => inDungeon; set => inDungeon = value; }

    [SerializeField] VoidEvent onSuccessfulExploration_ExploreNextCard;
    [SerializeField] CardEvent onEmpower_DestroyCrystalGameObject;
    [SerializeField] CardEvent onUndo_RegenerateCrystalGameObject;
    [SerializeField] CardEvent onPlay_TriggerAdditionalEffects;
    [SerializeField] PlayerEvent onEnemyDefeat_CheckPlayerDefendForWound;
    [SerializeField] EnemyCardEvent OnEnemyDefeat_GetRewards;

    private void Awake()
    {
        playerHandSize = player.PlayerHandSize;
    }
    private void Update()
    {
        if (playerDefend < 0) playerDefend = 0;
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

    public void Exploration(int exploreCost)
    {
        if(playerExplore >= exploreCost)
        {
            playerExplore -= exploreCost;
            GameManager.Instance.commands.ClearStack();
            onSuccessfulExploration_ExploreNextCard.Raise();
        }
        else
            GameManager.Instance.ValidationMessage($"You need {exploreCost} Explore to reveal the next event.");
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
            onEnemyDefeat_CheckPlayerDefendForWound.Raise(this);
            playerDefend -= enemy.enemySO.enemyAttack;
            OnEnemyDefeat_GetRewards.Raise(enemy);
        }
        else 
            GameManager.Instance.ValidationMessage($"You need {enemy.enemySO.enemyHP} Attack in order to defeat this monster.");
    }

    public void RecruitUnit(TownCard town)
    {
        units.Add(town.townSO.recruitableUnits[0]);
    }
}
