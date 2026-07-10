using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One option row: label, selected outline, locked dim. A locked row remains
// clickable/focusable (so its cost reads), but the Use bar refuses it.
public class UnitOptionRow : MonoBehaviour
{
    [SerializeField] public Button button;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Image selectedOutline;
    [SerializeField] CanvasGroup group;

    public void Bind(string text, bool selected, bool affordable)
    {
        label.text = text;
        if (selectedOutline != null) selectedOutline.enabled = selected;
        if (group != null) group.alpha = affordable ? 1f : 0.4f;
    }
}
