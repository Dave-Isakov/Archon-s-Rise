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

    // Same GameObject as the Canvas; flips its sorting order so the menu renders in
    // front of the board (OnCanvas) instead of behind it (OffCanvas, the default).
    CardMenuCanvas _menu;
    CardMenuCanvas Menu => _menu != null ? _menu : (_menu = GetComponent<CardMenuCanvas>());

    public void Open(Card card)
    {
        Card = card;
        Selection = new CardPlaySelection(Snapshot(card.cardSO));
        GameManager.Instance.cardCanvas.enabled = true;
        Menu?.OnCanvas();
        Raise();
    }

    public void Close()
    {
        ReleaseReservation();
        GameManager.Instance.cardCanvas.enabled = false;
        Menu?.OffCanvas();
        Card = null;
        Selection = null;
    }

    Crystal _reserved;

    public void SetMode(PlayMode mode)      { Selection?.SetMode(mode); Raise(); }
    public void ChooseStat(StatType stat)   { Selection?.SetChoiceStat(stat); Raise(); }
    public void ImproviseStat(StatType s)   { Selection?.SetImproviseStat(s); Raise(); }

    public void SetEmpowered(bool value)
    {
        if (Selection == null) return;

        if (value && Selection.CanEmpower())
        {
            var inv = FindAnyObjectByType<CrystalInventory>();
            inv?.SetCard(Card);
            var crystal = inv != null ? inv.SelectEmpowerCrystal() : null;
            if (crystal == null)
            {
                GameManager.Instance.ValidationMessage(
                    $"You cannot empower without {Card.cardSO.empowerType} crystals or an Allcrystal!");
                Raise();
                return;
            }
            _reserved = crystal;
            _reserved.SetReserved(true);
            Selection.SetEmpowered(true);
        }
        else
        {
            ReleaseReservation();
            Selection.SetEmpowered(false);
        }
        Raise();
    }

    void ReleaseReservation()
    {
        if (_reserved != null) { _reserved.SetReserved(false); _reserved = null; }
    }

    public void Play()
    {
        if (Selection == null || !Selection.IsPlayable()) return;
        Card.IsEmpowered = Selection.EffectiveEmpowered();
        var evt = EventFor(Selection);
        if (evt == null) return;
        GameManager.Instance.commands.AddCommand(new PlayCommand(evt, Card));
        _reserved = null; // ownership passes to the real consume/undo path

        // Dismiss the menu so the PLAY button can't be clicked again (each extra click
        // would push another PlayCommand and toggle the card's stats back off/on). The
        // card is now IsPlayed, so reopening it shows the "already played" message.
        var played = Card;
        Close();
        played.MinimizeAfterPlay();
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
