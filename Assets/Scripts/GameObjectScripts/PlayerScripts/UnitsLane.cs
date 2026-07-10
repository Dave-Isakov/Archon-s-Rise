using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Gamepad focus lane over the unit tokens (the "Units" container). Entered
// from the hand fan (up past the hand); left/right cycles, down returns to
// the hand, Submit opens the unit pop-out, Cancel drops to Board. Focus is
// shown with a moving outline frame (the tokens no longer scale) — the same
// frame the tokens' mouse-hover drives, so mouse and pad look identical.
public class UnitsLane : MonoBehaviour
{
    [SerializeField] HandFocusController hand;
    [SerializeField] UnitInspector inspector;
    [SerializeField] RectTransform focusOutline; // Image, raycastTarget off; inactive by default

    int _index;
    bool _active;
    bool _latched;

    public bool HasUnits => CurrentUnits().Count > 0;
    public bool IsActive => _active;

    List<Unit> CurrentUnits()
    {
        var list = new List<Unit>(FindObjectsByType<Unit>());
        list.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        return list;
    }

    public void Enter()
    {
        var units = CurrentUnits();
        if (units.Count == 0) return;
        _active = true;
        _index = Mathf.Clamp(_index, 0, units.Count - 1);
        FocusOutlineOver((RectTransform)units[_index].transform);
        InputContextState.Current = InputContext.Fan;
    }

    public void Exit()
    {
        HideOutline();
        _active = false;
    }

    void Update()
    {
        if (!_active) return;
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.messageCanvas.enabled || gm.cardCanvas.enabled || gm.unitCanvas.enabled) return;

        var units = CurrentUnits();
        if (units.Count == 0) { ExitToHand(); return; }
        _index = Mathf.Clamp(_index, 0, units.Count - 1);

        Vector2 nav = GameControls.Gameplay.Navigate.ReadValue<Vector2>();
        if (nav.magnitude < 0.5f) _latched = false;
        else if (!_latched)
        {
            _latched = true;
            if (Mathf.Abs(nav.x) >= Mathf.Abs(nav.y))
            {
                int next = Mathf.Clamp(_index + (nav.x > 0 ? 1 : -1), 0, units.Count - 1);
                if (next != _index) { _index = next; FocusOutlineOver((RectTransform)units[_index].transform); }
            }
            else if (nav.y < 0) { ExitToHand(); return; }
        }

        if (GameControls.Gameplay.Cancel.WasPressedThisFrame())
        {
            Exit();
            InputContextState.Current = InputContext.Board;
            return;
        }
        if (GameControls.Gameplay.Submit.WasPressedThisFrame())
        {
            var unit = units[_index];
            if (unit.IsPlayed)
                GameManager.Instance.ValidationMessage($"{unit.unitSO.cardName} has already been played, undo to revert action.");
            else
                inspector.Open(unit);
        }
    }

    void ExitToHand()
    {
        Exit();
        hand.EnterFromUnits();
    }

    // Frames the target token by reparenting the outline into it (so it tracks
    // the token's position). Public so the tokens' mouse hover drives the same
    // frame. Reused verbatim by the pop-out's nav controller.
    public void FocusOutlineOver(RectTransform target)
    {
        if (focusOutline == null || target == null) return;
        focusOutline.SetParent(target, false);
        focusOutline.anchorMin = Vector2.zero;
        focusOutline.anchorMax = Vector2.one;
        focusOutline.offsetMin = new Vector2(-4f, -4f);
        focusOutline.offsetMax = new Vector2(4f, 4f);
        focusOutline.SetAsLastSibling();
        focusOutline.gameObject.SetActive(true);
    }

    public void HideOutline()
    {
        if (focusOutline == null) return;
        // Re-home under this lane before hiding so disbanding the framed unit
        // (which would Destroy its children) can't take the outline down with it.
        focusOutline.SetParent(transform, false);
        focusOutline.gameObject.SetActive(false);
    }
}
