using System;
using UnityEngine;
using UnityEngine.UI;

// One crystal slot in the shrine's fan (spec 2026-07-27). Owns its own visuals;
// ShrinePanel owns what the slot means. ONE prefab serves every color — the
// crystal art is swapped at runtime from the panel's bucket sprites, so a new
// crystal color needs a sprite, not a new prefab.
public class ShrineSlotButton : MonoBehaviour
{
    [SerializeField] Image crystalImage;   // hidden while the slot is empty
    [SerializeField] Button button;

    void Reset()
    {
        button = GetComponent<Button>();
    }

    // Re-pointed on every Open so a pooled slot never reports a stale index.
    public void Bind(int index, Action<int> onClick)
    {
        if (button == null) button = GetComponent<Button>();
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(index));
    }

    // null sprite = the slot is empty: the circle stays, the crystal vanishes.
    public void Show(Sprite sprite)
    {
        if (crystalImage == null) return;
        crystalImage.sprite = sprite;
        crystalImage.enabled = sprite != null;
    }
}
