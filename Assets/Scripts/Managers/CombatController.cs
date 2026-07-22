using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum CombatContext { Field, Guardian, Dungeon }

// Owns one phased fight (spec 2026-07-21, Spec 2): the CombatPhase machine, the
// logical live-enemy set, the per-fight context, and the single multi-purpose
// button. Engage/kill/withdraw are added in Tasks 6-8.
public class CombatController : MonoBehaviour
{
    public static CombatController Instance { get; private set; }

    [SerializeField] Button multiButton;            // the repurposed Flee button
    [SerializeField] TMPro.TextMeshProUGUI multiButtonLabel;
    [SerializeField] VoidEvent onCombatPhaseChanged; // HUD phase label listens

    public CombatPhase Phase { get; private set; }
    public bool CanSiege        => CombatPhaseRules.CanSiege(Phase);
    public bool CanInfluence    => CombatPhaseRules.CanInfluence(Phase);
    public bool CanNormalAttack => CombatPhaseRules.CanNormalAttack(Phase);

    readonly List<EnemyCard> live = new();   // logical set; resolution keys off THIS, not childCount
    CombatContext context;
    TownToken guardianPlace;

    public struct EnemySpawn
    {
        public EnemiesSO so; public int bonusHP; public int bonusAttack;
        public EnemySpawn(EnemiesSO so, int bonusHP, int bonusAttack)
        { this.so = so; this.bonusHP = bonusHP; this.bonusAttack = bonusAttack; }
    }

    void Awake() { Instance = this; }

    public void OpenFight(List<EnemySpawn> spawns, CombatContext context, TownToken guardianPlace)
    {
        this.context = context;
        this.guardianPlace = guardianPlace;
        live.Clear();

        var prefab = FindAnyObjectByType<EnemyDeck>().PrefabEnemyCard;
        var parent = GameManager.Instance.enemyCardCombatPosition.transform;
        foreach (var s in spawns)
        {
            var go = Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(1.75f, 1.75f, 1f);
            var card = go.GetComponent<EnemyCard>();
            card.enemySO = s.so;
            card.bonusHP = s.bonusHP;
            card.bonusAttack = s.bonusAttack;
            live.Add(card);
        }

        GameManager.Instance.combatCanvas.enabled = true;
        SetPhase(CombatPhase.Siege);
    }

    void SetPhase(CombatPhase phase)
    {
        Phase = phase;
        foreach (var card in live) card.ApplyPhase(phase);
        if (multiButtonLabel != null) multiButtonLabel.text = CombatPhaseRules.ButtonLabel(phase);
        if (onCombatPhaseChanged != null) onCombatPhaseChanged.Raise();
    }
}
