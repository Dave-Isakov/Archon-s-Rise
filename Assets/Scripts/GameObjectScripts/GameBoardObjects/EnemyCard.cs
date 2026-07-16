using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EnemyCard : MonoBehaviour, IPointerClickHandler
{   
    public EnemiesSO enemySO;
    // Doom-scaling bonuses copied from the spawning token (zero for guardians
    // and map-gen enemies). Display and combat both use Effective values.
    public int bonusHP;
    public int bonusAttack;
    public int EffectiveHP => enemySO.enemyHP + bonusHP;
    public int EffectiveAttack => enemySO.enemyAttack + bonusAttack;
    [SerializeField] private TextMeshProUGUI enemyName;
    [SerializeField] private TextMeshProUGUI enemyHP;
    [SerializeField] private TextMeshProUGUI enemyAttack;
    [SerializeField] private TextMeshProUGUI enemyInfluence;
    [SerializeField] EnemyCardEvent onClick_ValidatePlayerAttackToEnemyHP;
    [SerializeField] EnemyCardEvent onClick_SiegeEnemy;
    [SerializeField] public Button fightButton;
    [SerializeField] public Button siegeButton;
    [SerializeField] public Button influenceButton;
    [SerializeField] TextMeshProUGUI fightButtonText;
    [SerializeField] TextMeshProUGUI influenceButtonText;
    public bool isDefeated = false;
    private bool isMaximized;

    public bool IsDefeated { get => isDefeated;}

    void Start() 
    {
        enemyName.text = enemySO.cardName;
        enemyAttack.text = IconMarkup.Tag(IconConcept.Attack) + " \n" + EffectiveAttack.ToString();
        enemyHP.text = IconMarkup.Tag(IconConcept.Hp) + " \n" + EffectiveHP.ToString();
        var player = FindAnyObjectByType<Player>();
        if (enemySO.canInfluence)
        {
            bool recruit = enemySO.recruitedUnit != null && player != null && player.HasCharismatic;
            enemyInfluence.gameObject.SetActive(true);
            enemyInfluence.text = IconMarkup.Tag(IconConcept.Influence) + " \n" + enemySO.influenceCost.ToString();
            influenceButtonText.text = (recruit ? "Recruit " : "Pay ")
                + IconMarkup.Cost(IconConcept.Influence, enemySO.influenceCost);
            influenceButton.interactable = true;
            influenceButton.onClick.AddListener(() => player.InfluenceEnemy(this));
        }
        else
        {
            influenceButtonText.text = "Impossible";
            influenceButton.interactable = false;
        }
        Debug.Log($"{enemySO.name} ({this.gameObject.name}) has entered the battlefield.");
        fightButton.onClick.AddListener(() => DefeatMonster());
        siegeButton.onClick.AddListener(() => SiegeMonster());
    }
    public void DefeatMonster()
    {
        onClick_ValidatePlayerAttackToEnemyHP.Raise(this);
    //         //ChooseReward();
    //         //AssignReward();
    //  isDefeated = true;
    //         GameManager.Instance.commands.ClearStack();
    }

    public void SiegeMonster()
    {
        onClick_SiegeEnemy.Raise(this);
    }

    // public void CheckWounds(Player player)
    // {
    //     if (player.PlayerDefend < enemySO.enemyAttack)
    //     {
    //         int woundCount = 0;
    //         for (var i = 0; i < enemySO.enemyAttack-player.PlayerDefend; i += player.PlayerHP)
    //         {
    //             onDefeat_WoundPlayer.Raise(this);
    //             woundCount++;
    //         }
    //         GameManager.Instance.ValidationMessage($"{enemySO.name} has been destroyed! You are wounded {woundCount} times!");
    //     }
    // }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(isDefeated == true)
        {
            Destroy(this.gameObject, 1f * Time.deltaTime);
            GameManager.Instance.ValidationMessage($"{enemySO.name} has been destroyed!");
            GameManager.Instance.CheckCombatants();
        }
        if(isDefeated == false && !fightButton.interactable)
        {
            GameManager.Instance.combatCanvas.enabled = false;
            Destroy(this.gameObject);
        }
    }

    // public void OnPointerEnter(PointerEventData eventData)
    // {
    //     this.transform.localScale = new Vector3(2,2,2);
    //     GetComponent<Canvas>().overrideSorting = true;
    //     GetComponent<Canvas>().sortingOrder = 50;
    // }

    // public void OnPointerExit(PointerEventData eventData)
    // {
    //     this.transform.localScale = new Vector3(1,1,1);
    //     GetComponent<Canvas>().overrideSorting = false;
    //     GetComponent<Canvas>().sortingOrder = 0;
    // }

    public void DestroyEnemyObject(EnemyCard card)
    {
        card.isDefeated = true;
        GameManager.Instance.commands.ClearStack();
    }

    public void EnableCombat(EnemyToken token)
    {
        fightButton.interactable = token.isAggro;
        siegeButton.interactable = token.isAggro;
        influenceButton.interactable = token.isAggro && enemySO.canInfluence;
    }
}
