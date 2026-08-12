using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Unit : MonoBehaviour, IPointerClickHandler, IFanItem
{
    [SerializeField] Image image;
    [SerializeField] public UnitsSO unitSO;
    [SerializeField] TextMeshProUGUI unitLetter;
    [SerializeField] TextMeshProUGUI unitText;
    [SerializeField] Color exhaustedGrey = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] Color woundedRed = new Color(0.65f, 0.18f, 0.18f, 1f);
    [SerializeField] GameObject woundDecal;
    [SerializeField] TextMeshProUGUI woundCountLabel;
    private bool isPlayed = false;
    private int woundCount = 0;

    // Exhaustion used to be a -90 rotation, which FanLane would overwrite with the
    // slot tilt on the next relayout. It is now a grey tint — the same language
    // wounds use — applied here so no caller can drift from it.
    public bool IsPlayed
    {
        get => isPlayed;
        set { isPlayed = value; ApplyStateTint(); }
    }

    public int WoundCount
    {
        get => woundCount;
        set { woundCount = Mathf.Clamp(value, 0, 2); ApplyStateTint(); }
    }

    public bool IsWounded => woundCount > 0;
    public int ArmorClass => unitSO != null ? unitSO.armorClass : 0;

    CanvasGroup _group;
    public RectTransform Rect => (RectTransform)transform;
    public CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();
    public bool Selectable => !isPlayed && !IsWounded;

    public void Activate()
    {
        var inspector = FindAnyObjectByType<UnitInspector>();
        if (inspector != null) inspector.Open(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InputContextState.MapOpen) return; // map mode: look, don't touch
        if (BarFocusController.Instance != null &&
            BarFocusController.Instance.TryClaimClick(this)) return;
        if (IsWounded)
        {
            GameLog.Instance.Post($"{unitSO.cardName} is wounded and cannot act until healed.");
            return;
        }
        if (isPlayed)
        {
            GameLog.Instance.Post($"{unitSO.cardName} has already been played, undo to revert action.");
            return;
        }
        FindAnyObjectByType<UnitInspector>().Open(this);
    }

    void Start()
    {
        unitLetter.text = unitSO.cardName.ToString();
        unitText.text = unitSO.cardDescription;
        ApplyStateTint();
    }

    // Wounded outranks exhausted: a wounded unit is unusable for the rest of the
    // run until healed, while exhaustion clears at round start.
    void ApplyStateTint()
    {
        if (image == null || unitSO == null) return;
        if (IsWounded)      image.color = woundedRed;
        else if (isPlayed)  image.color = exhaustedGrey;
        else                image.color = unitSO.color;

        if (woundDecal != null) woundDecal.SetActive(IsWounded);
        if (woundCountLabel != null)
        {
            woundCountLabel.gameObject.SetActive(woundCount > 1);
            woundCountLabel.text = woundCount.ToString();
        }
    }
}
