using UnityEngine;
using UnityEngine.InputSystem;

// Gamepad/keyboard navigation for the unit pop-out: one vertical lane of
// option rows ending at Use (UnitNavRules). Moving focus onto a row selects it
// (focus == selection, per spec); Submit on Use fires it; Cancel closes.
public class UnitInspectorNavController : MonoBehaviour
{
    [SerializeField] UnitInspector inspector;
    [SerializeField] UnitOptionList optionList;
    [SerializeField] UnitUseBar useBar;
    [SerializeField] RectTransform focusOutline; // Image, raycastTarget off

    int _pos;
    bool _latched;
    bool _wasOpen;
    bool _messageWasUp;

    void Update()
    {
        bool open = GameManager.Instance != null && GameManager.Instance.unitCanvas.enabled
                    && inspector.Selection != null;
        if (!open)
        {
            _wasOpen = false;
            if (focusOutline != null) focusOutline.gameObject.SetActive(false);
            return;
        }

        if (GameManager.Instance.messageCanvas.enabled) { _messageWasUp = true; return; }
        if (_messageWasUp) { _messageWasUp = false; return; }

        if (!_wasOpen)
        {
            // First open frame: focus the selected row (or Use) and swallow the
            // Submit that opened the pop-out.
            _wasOpen = true;
            _pos = inspector.Selection.SelectedIndex >= 0
                ? inspector.Selection.SelectedIndex
                : UnitNavRules.UseSlot(inspector.Selection.Count);
            RenderOutline();
            return;
        }

        HandleNavigate();

        if (GameControls.Gameplay.Cancel.WasPressedThisFrame())
        {
            inspector.Close();
            return;
        }

        if (GameControls.Gameplay.Submit.WasPressedThisFrame())
        {
            if (_pos == UnitNavRules.UseSlot(inspector.Selection.Count)) inspector.Use();
            // Submit on a row is a no-op: focusing it already selected it.
            return;
        }

        RenderOutline();
    }

    void HandleNavigate()
    {
        Vector2 nav = GameControls.Gameplay.Navigate.ReadValue<Vector2>();
        if (Mathf.Abs(nav.y) < 0.5f) { _latched = false; return; }
        if (_latched) return;
        _latched = true;

        int next = UnitNavRules.Move(_pos, nav.y > 0 ? +1 : -1, inspector.Selection.Count);
        if (next == _pos) return;
        _pos = next;
        if (_pos < inspector.Selection.Count) inspector.SelectOption(_pos);
        RenderOutline();
    }

    void RenderOutline()
    {
        if (focusOutline == null) return;
        RectTransform target = _pos == UnitNavRules.UseSlot(inspector.Selection.Count)
            ? (RectTransform)useBar.useButton.transform
            : (RectTransform)optionList.Rows[_pos].transform;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            focusOutline.gameObject.SetActive(false);
            return;
        }
        focusOutline.SetParent(target, false);
        focusOutline.anchorMin = Vector2.zero;
        focusOutline.anchorMax = Vector2.one;
        focusOutline.offsetMin = new Vector2(-4f, -4f);
        focusOutline.offsetMax = new Vector2(4f, 4f);
        focusOutline.SetAsLastSibling();
        focusOutline.gameObject.SetActive(true);
    }
}
