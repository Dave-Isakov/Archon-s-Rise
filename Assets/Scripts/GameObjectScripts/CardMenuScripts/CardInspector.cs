using System;
using UnityEngine;
using DG.Tweening;

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

    [Header("Phase 3a presentation")]
    [SerializeField] CanvasGroup boardScrim;   // full-screen dim behind the pop-out
    [SerializeField] CanvasGroup popoutGroup;  // CanvasGroup wrapping the four panels
    [SerializeField] float scrimAlpha = 0.6f;
    [SerializeField] float fadeTime = 0.2f;

    [Header("Phase 3b juice")]
    [SerializeField] StatEchoes echoes;

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
        FadeIn();
        Raise();
        InputContextState.Current = InputContext.Inspector;
    }

    public void Close()
    {
        var closing = Card; // capture before clearing so we can return it to the hand
        ReleaseReservation();
        GameManager.Instance.cardCanvas.enabled = false;
        Menu?.OffCanvas();
        SnapClosed();
        Card = null;
        Selection = null;
        // Every close path (Back, click-off, Play) returns the focused card to the hand
        // and clears its maximized flag. Without this, Back left the card stranded and
        // invisible in the centre until it was clicked again.
        closing?.ReturnToHand();
        // Default back to Board; HandFocusController promotes to Fan on its next
        // Update if gamepad focus resumes in the hand.
        InputContextState.Current = InputContext.Board;
    }

    void FadeIn()
    {
        if (boardScrim != null)
        {
            boardScrim.DOKill();
            boardScrim.alpha = 0f;
            boardScrim.DOFade(scrimAlpha, fadeTime);
        }
        if (popoutGroup != null)
        {
            popoutGroup.DOKill();
            popoutGroup.alpha = 0f;
            popoutGroup.DOFade(1f, fadeTime);
        }
    }

    // Close is synchronous (Play()/undo rely on it), so scrim + panels clear instantly;
    // the card's return tween carries the closing motion.
    void SnapClosed()
    {
        if (boardScrim != null) { boardScrim.DOKill(); boardScrim.alpha = 0f; }
        if (popoutGroup != null) { popoutGroup.DOKill(); popoutGroup.alpha = 0f; }
    }

    Crystal _reserved;

    public void SetMode(PlayMode mode)      { Selection?.SetMode(mode); Raise(); }
    public void ChooseStat(StatType stat)   { Selection?.SetChoiceStat(stat); Raise(); }
    public void ImproviseStat(StatType s)   { Selection?.SetImproviseStat(s); Raise(); }
    public void SetConvert(bool value)      { Selection?.SetConvert(value); Raise(); }

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
                string need = Card.cardSO.empowerType.IsAllColors()
                    ? $"any crystal {IconMarkup.CrystalTag(EmpowerType.None)}"
                    : $"a {Card.cardSO.empowerType} {IconMarkup.CrystalTag(Card.cardSO.empowerType)} or wild crystal";
                GameManager.Instance.ValidationMessage($"You need {need} to empower this card.");
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
        // Always assigned — true or false — so a card replayed later can never
        // carry a stale opt-in (spec 2026-07-14).
        Card.ConvertOn = Selection.EffectiveConvert();
        var evt = EventFor(Selection);
        if (evt == null) return;

        // Capture before the command/Close: the card is at centre and Selection is live.
        Vector3 origin = Card.transform.position;
        var applied = Selection.PreviewStats(Selection.EffectiveEmpowered());

        GameManager.Instance.commands.AddCommand(new PlayCommand(evt, Card));
        _reserved = null; // ownership passes to the real consume/undo path

        // Fire one "+N" per boosted stat (after Execute() so the crystal-spend flourish
        // leads the stat echo). Echoes are play-only; undo shows the stat count-down.
        if (echoes != null)
            foreach (var e in StatEchoPlan.NonZero(applied))
                echoes.Emit(origin, e.Stat, e.Amount);

        // Dismiss the menu so PLAY can't be clicked again; Close() returns the card to the hand.
        Close();
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
            so.empowerAttack, so.empowerDefend, so.empowerInfluence, so.empowerExplore,
            so.convertTo, so.convertFrom, so.convertRequiresEmpower);

    void Raise() => Changed?.Invoke();
}
