using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One slot on a place fan (spec 2026-07-28). Owns its own visuals; PlaceFan owns
// what the slot means. ONE prefab serves every action — the glyph is swapped at
// runtime from IconRegistry, so a new action needs a registry entry, not a prefab.
//
// Icon + amount only, never words (the shipped Play/Convert convention).
public class PlaceFanSlot : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI costLabel;  // hidden when the action is free
    [SerializeField] Button button;
    [SerializeField] CanvasGroup lockGroup;      // gets the UiLock dim

    // The action this slot currently shows, so hover previews can ask.
    public PlaceActionId Action { get; private set; }

    void Reset()
    {
        button = GetComponent<Button>();
        lockGroup = GetComponent<CanvasGroup>();
    }

    // Re-pointed on every Open so a pooled slot never reports a stale action.
    public void Bind(PlaceAction action, Action<PlaceActionId> onClick)
    {
        Action = action.Id;

        if (iconImage != null && IconRegistrySO.Instance != null)
            iconImage.sprite = IconRegistrySO.Instance.SpriteFor(action.Icon);

        if (costLabel != null)
        {
            bool showBadge = action.CostAmount != 0;
            costLabel.gameObject.SetActive(showBadge);
            if (showBadge)
                costLabel.text = action.CostIcon.HasValue
                    ? IconMarkup.Cost(action.CostIcon.Value, action.CostAmount)
                    : action.CostAmount.ToString();
        }

        if (button != null)
        {
            button.interactable = action.Enabled;
            button.onClick.RemoveAllListeners();
            var id = action.Id;
            button.onClick.AddListener(() => onClick(id));
        }

        UiLock.Apply(lockGroup, !action.Enabled);
    }
}
