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

    // Blocked for this Defend phase. A blocked enemy is ALIVE — its auras still
    // apply (spec §7.4), which is what keeps Siege strictly better than blocking.
    public bool Blocked { get; set; }

    public EnemyTrait Traits => enemySO != null ? enemySO.traits : EnemyTrait.None;

    // The pure projection every rule consumes, so EnemyTraitRules never sees a
    // MonoBehaviour and stays CLI-testable.
    public EnemyCombatant ToCombatant() => new EnemyCombatant
    {
        Attack = EffectiveAttack,
        HP = EffectiveHP,
        Traits = Traits,
        Blocked = Blocked,
    };

    [SerializeField] private TextMeshProUGUI enemyName;
    [SerializeField] private TextMeshProUGUI enemyHP;
    [SerializeField] private TextMeshProUGUI enemyAttack;
    [SerializeField] private TextMeshProUGUI enemyInfluence;
    [SerializeField] EnemyCardEvent onClick_ValidatePlayerAttackToEnemyHP;
    [SerializeField] EnemyCardEvent onClick_SiegeEnemy;
    [SerializeField] public Button fightButton;
    [SerializeField] public Button siegeButton;
    [SerializeField] public Button influenceButton;
    [SerializeField] public Button defendButton;          // Defend phase: block this enemy
    [SerializeField] TextMeshProUGUI traitBadges;         // compact badge row, no words
    [SerializeField] TextMeshProUGUI defendButtonText;    // shows the block cost
    [SerializeField] GameObject siegeGlow;     // lit while staged Siege can spend here (spec 2026-07-24)
    [SerializeField] GameObject influenceGlow; // lit while staged Influence can spend here
    [SerializeField] TextMeshProUGUI fightButtonText;
    [SerializeField] TextMeshProUGUI influenceButtonText;
    [SerializeField] Image artwork; // per-enemy portrait; disabled when the SO has none
    public bool isDefeated = false;
    private bool isMaximized;

    public bool IsDefeated { get => isDefeated;}

    void Start() 
    {
        enemyName.text = enemySO.cardName;
        if (artwork != null)
        {
            if (enemySO.cardArt != null) { artwork.sprite = enemySO.cardArt; artwork.enabled = true; }
            else artwork.enabled = false;
        }
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
        RefreshTraitBadges();
    }

    // Badges only — no words. The hover tooltip (EnemyTraitTooltip, spec §8.3)
    // is the legend now, so a bare badge is never a dead end.
    public void RefreshTraitBadges()
    {
        if (traitBadges == null) return;
        var parts = new System.Text.StringBuilder();
        foreach (var t in EnemyTraitCopy.Split(Traits))
        {
            if (parts.Length > 0) parts.Append(' ');
            parts.Append(IconMarkup.TraitBadge(t));
        }
        traitBadges.text = parts.ToString();
        traitBadges.gameObject.SetActive(parts.Length > 0);
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
        // Out-of-range preview card: a click dismisses the peek (its fight buttons
        // are disabled). Guarded by !InCombat because a LIVE card in the Siege
        // phase also has its Fight button disabled (spec 2026-07-21, Spec 2) —
        // without the guard, clicking a real enemy's body would desync the fight.
        bool inCombat = CombatController.Instance != null && CombatController.Instance.InCombat;
        if (!isDefeated && !fightButton.interactable && !inCombat)
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

    // Lights this card's Siege/Influence buttons while the matching pool is
    // staged in the Siege phase (spec 2026-07-24 phase controls), inviting the
    // player to spend it here. Null-safe: an unwired glow stays dark. The
    // controller passes false/false in every other phase.
    public void SetActionGlow(bool siegeLit, bool influenceLit)
    {
        if (siegeGlow != null) siegeGlow.SetActive(siegeLit);
        if (influenceGlow != null) influenceGlow.SetActive(influenceLit);
    }

    // Phase-gates this card's buttons (spec 2026-07-21, Spec 2). Siege/Influence
    // are live only in the Siege phase; Fight only in the Attack phase.
    public void ApplyPhase(CombatPhase phase)
    {
        if (siegeButton != null)     siegeButton.interactable     = CombatPhaseRules.CanSiege(phase);
        if (fightButton != null)     fightButton.interactable     = CombatPhaseRules.CanNormalAttack(phase);
        if (influenceButton != null) influenceButton.interactable = CombatPhaseRules.CanInfluence(phase) && enemySO.canInfluence;
    }

    // All four per-enemy buttons route through CombatPhaseRules so no button
    // manages its own phase state (spec §7.5).
    //
    // canCommit false is a preview-only fight (2026-07-31): Siege and Influence
    // are the two spends reachable in the Siege phase, so both are withheld.
    // Defend and Fight need no gate — their phases are unreachable without an
    // Engage, which preview-only already refuses.
    public void RefreshPhaseButtons(CombatPhase phase, int defendLeft, int blockCost, bool canCommit = true)
    {
        if (siegeButton != null)     siegeButton.gameObject.SetActive(CombatPhaseRules.CanSiege(phase) && canCommit);
        if (influenceButton != null) influenceButton.gameObject.SetActive(
            CombatPhaseRules.CanInfluence(phase) && enemySO.canInfluence && canCommit);
        if (fightButton != null)     fightButton.gameObject.SetActive(CombatPhaseRules.CanNormalAttack(phase));

        if (defendButton == null) return;
        bool live = CombatPhaseRules.CanBlock(phase) && !Blocked;
        defendButton.gameObject.SetActive(CombatPhaseRules.CanBlock(phase));

        bool affordable = live && defendLeft >= blockCost;
        defendButton.interactable = affordable;
        UiLock.Apply(defendButton.GetComponent<CanvasGroup>(), !affordable);

        if (defendButtonText != null)
            defendButtonText.text = Blocked
                ? IconMarkup.Tag(IconConcept.Defend) + " Blocked"
                : IconMarkup.Cost(IconConcept.Defend, blockCost);
    }

    // Wired to defendButton.onClick in the prefab.
    public void OnDefendClicked()
    {
        var controller = FindAnyObjectByType<CombatController>();
        var player = FindAnyObjectByType<Player>();
        if (controller == null || player == null) return;
        if (!CombatPhaseRules.CanBlock(controller.Phase) || Blocked) return;

        int cost = controller.BlockCostFor(this);
        if (player.PlayerDefend < cost) return;

        GameManager.Instance.commands.AddCommand(new BlockCommand(this, player, cost));
    }
}
