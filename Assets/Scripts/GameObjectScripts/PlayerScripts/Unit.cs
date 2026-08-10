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
    private bool isPlayed = false;

    // Exhaustion used to be a -90 rotation, which FanLane would overwrite with the
    // slot tilt on the next relayout. It is now a grey tint — the same language
    // wounds use — applied here so no caller can drift from it.
    public bool IsPlayed
    {
        get => isPlayed;
        set { isPlayed = value; ApplyExhaustTint(); }
    }

    CanvasGroup _group;
    public RectTransform Rect => (RectTransform)transform;
    public CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();
    public bool Selectable => !isPlayed;

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
        ApplyExhaustTint();
    }

    void ApplyExhaustTint()
    {
        if (image == null || unitSO == null) return;
        image.color = isPlayed ? exhaustedGrey : unitSO.color;
    }
}
