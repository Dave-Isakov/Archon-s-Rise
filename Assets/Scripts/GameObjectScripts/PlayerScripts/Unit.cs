using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Unit : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IFanItem
{
    [SerializeField] Image image;
    [SerializeField] public UnitsSO unitSO;
    [SerializeField] TextMeshProUGUI unitLetter;
    [SerializeField] TextMeshProUGUI unitText;
    private bool isPlayed = false;
    public bool IsPlayed { get => isPlayed; set => isPlayed = value; }

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
        if (isPlayed)
        {
            GameLog.Instance.Post($"{unitSO.cardName} has already been played, undo to revert action.");
            return;
        }
        FindAnyObjectByType<UnitInspector>().Open(this);
    }

    void Start()
    {
        image.color = unitSO.color;
        unitLetter.text = unitSO.cardName.ToString();
        unitText.text = unitSO.cardDescription;
    }

    // Mouse hover shows the same moving outline the controller lane uses (the
    // token no longer scales). The lane owns the outline; while it's driving
    // focus with the pad, the mouse leaves it alone.
    public void OnPointerEnter(PointerEventData eventData)
    {
        var lane = FindAnyObjectByType<UnitsLane>();
        if (lane != null && !lane.IsActive)
            lane.FocusOutlineOver((RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var lane = FindAnyObjectByType<UnitsLane>();
        if (lane != null && !lane.IsActive)
            lane.HideOutline();
    }

}
