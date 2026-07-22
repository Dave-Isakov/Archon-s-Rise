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
    }

    public void SiegeMonster()
    {
        onClick_SiegeEnemy.Raise(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Out-of-range preview card: a click dismisses the peek (fight buttons are
        // disabled here). A real defeat now tears itself down via ResolveDefeat.
        if (!isDefeated && !fightButton.interactable)
        {
            GameManager.Instance.combatCanvas.enabled = false;
            Destroy(this.gameObject);
        }
    }

    public void EnableCombat(EnemyToken token)
    {
        fightButton.interactable = token.isAggro;
        siegeButton.interactable = token.isAggro;
        influenceButton.interactable = token.isAggro && enemySO.canInfluence;
    }

    // Phase-gates this card's buttons (spec 2026-07-21, Spec 2). Siege/Influence
    // are live only in the Siege phase; Fight only in the Attack phase.
    public void ApplyPhase(CombatPhase phase)
    {
        if (siegeButton != null)     siegeButton.interactable     = CombatPhaseRules.CanSiege(phase);
        if (fightButton != null)     fightButton.interactable     = CombatPhaseRules.CanNormalAttack(phase);
        if (influenceButton != null) influenceButton.interactable = CombatPhaseRules.CanInfluence(phase) && enemySO.canInfluence;
    }
}
