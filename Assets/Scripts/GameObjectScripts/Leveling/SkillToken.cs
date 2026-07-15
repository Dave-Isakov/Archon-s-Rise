using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillToken : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Image icon;
    // Semi-transparent cover enabled while exhausted.
    [SerializeField] Image dimOverlay;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] SkillEvent onClick_PerformSkillAction;
    public SkillsSO skillSO;
    // Per-activation conversion snapshot (spec 2026-07-14): the sign-flip undo
    // pattern can't reverse a conversion, so the applied amounts live here.
    [System.NonSerialized] public int[] ConvertMoved;
    // Units this activation readied (spec 2026-07-14) so undo re-exhausts exactly them.
    public readonly List<Unit> RefreshedUnits = new();
    public bool IsUsed { get; private set; }

    public void Bind(SkillsSO so)
    {
        skillSO = so;
        gameObject.name = so.cardName;
        if (icon != null && so.icon != null) icon.sprite = so.icon;
        if (label != null) label.text = so.cardName;
        SetUsed(false);
    }

    public void SetUsed(bool used)
    {
        IsUsed = used;
        if (dimOverlay != null) dimOverlay.enabled = used;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (skillSO.cadence == SkillCadence.Passive)
        {
            GameManager.Instance.ValidationMessage($"{skillSO.cardName} is always active.");
            return;
        }
        if (!IsUsed)
        {
            GameManager.Instance.commands.AddCommand(new SkillCommand(onClick_PerformSkillAction, this));
        }
        else
        {
            string refresh = skillSO.cadence == SkillCadence.PerTurn ? "next turn" : "next round";
            GameManager.Instance.ValidationMessage($"{skillSO.cardName} is exhausted until {refresh}. Undo to revert if it was just used.");
        }
    }
}
