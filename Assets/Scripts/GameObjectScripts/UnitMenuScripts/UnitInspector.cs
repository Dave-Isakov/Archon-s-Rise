using System;
using UnityEngine;
using DG.Tweening;

// Owns the in-progress unit use. Mirrors CardInspector: single source of truth
// the section views render from; Use routes through UnitCommand so undo and
// Player stat math stay symmetric. Affordability is computed once at Open —
// the pop-out is modal, so crystal counts can't change underneath it.
public class UnitInspector : MonoBehaviour
{
    [SerializeField] CanvasGroup boardScrim;
    [SerializeField] CanvasGroup popoutGroup;
    [SerializeField] float scrimAlpha = 0.6f;
    [SerializeField] float fadeTime = 0.2f;

    public UnitPlaySelection Selection { get; private set; }
    public Unit Unit { get; private set; }
    public event Action Changed;

    Crystal _reserved;

    public void Open(Unit unit)
    {
        Unit = unit;
        var inv = FindAnyObjectByType<CrystalInventory>();
        var options = unit.unitSO.options;
        var affordable = new bool[options.Count];
        for (int i = 0; i < options.Count; i++)
            affordable[i] = inv == null ? options[i].crystalCost == EmpowerType.None
                                        : inv.CanPay(options[i].crystalCost);

        Selection = new UnitPlaySelection(options, affordable);
        GameManager.Instance.unitCanvas.enabled = true;
        FadeIn();
        ReserveForSelected();
        InputContextState.Current = InputContext.Inspector;
        Raise();
    }

    public void Close()
    {
        ReleaseReservation();
        GameManager.Instance.unitCanvas.enabled = false;
        SnapClosed();
        Unit = null;
        Selection = null;
        InputContextState.Current = InputContext.Board;
    }

    public void SelectOption(int index)
    {
        if (Selection == null) return;
        ReleaseReservation();
        Selection.Select(index);
        ReserveForSelected();
        Raise();
    }

    public void Use()
    {
        if (Selection == null || !Selection.CanUse) return;
        var option = Selection.Selected;
        var inv = FindAnyObjectByType<CrystalInventory>();
        GameManager.Instance.commands.AddCommand(
            new UnitCommand(FindAnyObjectByType<Player>(), inv, Unit, option, _reserved));
        _reserved = null; // ownership passed to the command's consume/undo path
        Close();
    }

    // Reserve (dim) the crystal the selected option would spend, exactly like
    // CardInspector's empower reservation.
    void ReserveForSelected()
    {
        var option = Selection?.Selected;
        if (option == null || option.crystalCost == EmpowerType.None || !Selection.CanUse) return;
        var inv = FindAnyObjectByType<CrystalInventory>();
        _reserved = inv != null ? inv.SelectPayCrystal(option.crystalCost) : null;
        _reserved?.SetReserved(true);
    }

    void ReleaseReservation()
    {
        if (_reserved != null) { _reserved.SetReserved(false); _reserved = null; }
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

    void SnapClosed()
    {
        if (boardScrim != null) { boardScrim.DOKill(); boardScrim.alpha = 0f; }
        if (popoutGroup != null) { popoutGroup.DOKill(); popoutGroup.alpha = 0f; }
    }

    void Raise() => Changed?.Invoke();
}
