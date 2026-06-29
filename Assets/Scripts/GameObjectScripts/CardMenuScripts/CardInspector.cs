using System;
using UnityEngine;

// Owns the in-progress play for the focused card. Single source of truth that the
// section views render from; replaces the old toggle-event web. Routes Play to the
// existing CardEvent assets so PlayCommand/undo and Player stat math stay unchanged.
public class CardInspector : MonoBehaviour
{
    [Header("Normal / Choice routing (existing assets)")]
    [SerializeField] CardEvent onPlay_Normal;     // OnPlay_SetCardDataToPlayer
    [SerializeField] CardEvent onPlay_Attack;      // OnPlay_SetAttackDataToPlayer
    [SerializeField] CardEvent onPlay_Defend;      // OnPlay_SetDefendDataToPlayer
    [SerializeField] CardEvent onPlay_Influence;   // OnPlay_SetInfluenceDataToPlayer
    [SerializeField] CardEvent onPlay_Explore;     // OnPlay_SetExploreDataToPlayer

    [Header("Improvise routing (existing assets)")]
    [SerializeField] CardEvent onImprov_Attack;    // OnImprovAttack_SetPlayerStats
    [SerializeField] CardEvent onImprov_Defend;    // OnImprovDefend_SetPlayerStats
    [SerializeField] CardEvent onImprov_Influence; // OnImprovInfluence_SetPlayerStats
    [SerializeField] CardEvent onImprov_Explore;   // OnImprovExplore_SetPlayerStats

    public CardPlaySelection Selection { get; private set; }
    public Card Card { get; private set; }
    public event Action Changed;

    public void Open(Card card)
    {
        Card = card;
        Selection = new CardPlaySelection(Snapshot(card.cardSO));
        GameManager.Instance.cardCanvas.enabled = true;
        Raise();
    }

    public void Close()
    {
        GameManager.Instance.cardCanvas.enabled = false;
        Card = null;
        Selection = null;
    }

    public void SetMode(PlayMode mode)      { Selection?.SetMode(mode); Raise(); }
    public void ChooseStat(StatType stat)   { Selection?.SetChoiceStat(stat); Raise(); }
    public void ImproviseStat(StatType s)   { Selection?.SetImproviseStat(s); Raise(); }
    public void SetEmpowered(bool value)    { Selection?.SetEmpowered(value); Raise(); }

    public void Play()
    {
        if (Selection == null || !Selection.IsPlayable()) return;
        Card.IsEmpowered = Selection.EffectiveEmpowered();
        var evt = EventFor(Selection);
        if (evt == null) return;
        GameManager.Instance.commands.AddCommand(new PlayCommand(evt, Card));
        Raise();
    }

    CardEvent EventFor(CardPlaySelection s)
    {
        switch (s.Mode)
        {
            case PlayMode.Improvise: return ImprovEventFor(s.ImproviseStat);
            case PlayMode.Choice:    return ChoiceEventFor(s.ChoiceStat);
            default:                 return onPlay_Normal;
        }
    }

    CardEvent ChoiceEventFor(StatType stat)
    {
        if (stat == StatType.Attack)    return onPlay_Attack;
        if (stat == StatType.Defend)    return onPlay_Defend;
        if (stat == StatType.Influence) return onPlay_Influence;
        if (stat == StatType.Explore)   return onPlay_Explore;
        return onPlay_Normal;
    }

    CardEvent ImprovEventFor(StatType stat)
    {
        if (stat == StatType.Attack)    return onImprov_Attack;
        if (stat == StatType.Defend)    return onImprov_Defend;
        if (stat == StatType.Influence) return onImprov_Influence;
        if (stat == StatType.Explore)   return onImprov_Explore;
        return onImprov_Attack;
    }

    static CardSnapshot Snapshot(CardsSO so) =>
        new CardSnapshot(so.cardType, so.empowerType, so.isChoice,
            so.attack, so.defend, so.influence, so.explore,
            so.empowerAttack, so.empowerDefend, so.empowerInfluence, so.empowerExplore);

    void Raise() => Changed?.Invoke();
}
