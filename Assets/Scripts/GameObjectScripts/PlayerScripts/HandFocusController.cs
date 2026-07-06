using UnityEngine;
using UnityEngine.InputSystem;

// The single writer of hand-fan focus. Mouse claims focus only when the pointer
// actually MOVES (delta != 0); gamepad/keyboard claims it on a Navigate press —
// last input wins, so the per-frame hit-test can never stomp a d-pad selection.
// Owns the Board <-> Fan context transitions; CardInspector owns Inspector.
public class HandFocusController : MonoBehaviour
{
    [SerializeField] HandFanLayout layout;

    enum FocusOwner { None, Mouse, Pad }
    FocusOwner _owner = FocusOwner.None;
    Vector2 _lastMousePos;
    bool _navLatched;
    int _lastPadIndex = -1;
    bool _inspectorWasOpen;
    bool _messageWasUp;

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.messageCanvas.enabled)
        {
            // A validation message is a modal: MessageController owns input, we do
            // nothing. Keep focus as-is so it resumes when the message clears.
            _messageWasUp = true;
            return;
        }
        if (_messageWasUp)
        {
            // Swallow the frame the message closed on so the A/B that dismissed it
            // can't also open a card here (independent of Update ordering).
            _messageWasUp = false;
            return;
        }

        if (gm.cardCanvas.enabled)
        {
            // Pop-out open: the fan shows no focus and this controller consumes no
            // input. _owner is kept so pad focus can be restored on close.
            if (!_inspectorWasOpen) { _inspectorWasOpen = true; layout.ClearFocus(); }
            return;
        }

        if (_inspectorWasOpen)
        {
            // First frame after the pop-out closed. Restore pad focus (the fan is
            // where Cancel/Play lands you) and swallow this frame's input so the
            // Cancel/Submit press that closed the pop-out can't also act here.
            _inspectorWasOpen = false;
            if (_owner == FocusOwner.Pad) RestorePadFocus();
            return;
        }

        if (gm.mainMenuCanvas.enabled || gm.cardListCanvas.enabled) return;

        if (_owner == FocusOwner.Pad) KeepPadFocusValid();
        HandleMouse();
        HandleNavigate();
        HandleCancel();
        HandleSubmit(); // last: opening the inspector must be this frame's final act
    }

    void HandleMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 pos = mouse.position.ReadValue();
        if (pos == _lastMousePos) return;
        _lastMousePos = pos;

        var hit = layout.HitTest(pos);
        if (hit != null)
        {
            _owner = FocusOwner.Mouse;
            layout.SetFocus(hit);
        }
        else if (_owner == FocusOwner.Mouse)
        {
            // Only a mouse-claimed focus is cleared by the mouse leaving the fan;
            // drifting the mouse must not clear a pad-claimed focus.
            _owner = FocusOwner.None;
            layout.ClearFocus();
        }
    }

    void HandleNavigate()
    {
        Vector2 nav = GameControls.Gameplay.Navigate.ReadValue<Vector2>();
        // Latch so one press = one step (sticks and held d-pads report every frame).
        if (Mathf.Abs(nav.x) < 0.5f) { _navLatched = false; return; }
        if (_navLatched) return;
        _navLatched = true;

        var cards = layout.InHand();
        var wounds = new bool[cards.Count];
        for (int i = 0; i < cards.Count; i++)
            wounds[i] = cards[i].cardSO.cardType == StatType.Wound;

        int current = layout.Focused != null ? cards.IndexOf(layout.Focused) : -1;
        int next = HandNavRules.Step(current, nav.x > 0 ? +1 : -1, wounds);
        if (next < 0) return;

        _owner = FocusOwner.Pad;
        _lastPadIndex = next;
        InputContextState.Current = InputContext.Fan;
        layout.SetFocus(cards[next]);
    }

    void HandleCancel()
    {
        if (_owner != FocusOwner.Pad) return;
        if (!GameControls.Gameplay.Cancel.WasPressedThisFrame()) return;
        _owner = FocusOwner.None;
        _lastPadIndex = -1;
        layout.ClearFocus();
        InputContextState.Current = InputContext.Board;
    }

    void HandleSubmit()
    {
        if (!GameControls.Gameplay.Submit.WasPressedThisFrame()) return;
        if (layout.Focused == null) return;
        layout.Focused.ToggleInspect();
    }

    // After draw/discard/heal/play the focused card may have left the fan; keep pad
    // focus on the nearest surviving card instead of letting it vanish.
    void KeepPadFocusValid()
    {
        var cards = layout.InHand();
        if (layout.Focused != null && cards.Contains(layout.Focused))
        {
            _lastPadIndex = cards.IndexOf(layout.Focused);
            return;
        }
        RestorePadFocus();
    }

    void RestorePadFocus()
    {
        var cards = layout.InHand();
        var wounds = new bool[cards.Count];
        for (int i = 0; i < cards.Count; i++)
            wounds[i] = cards[i].cardSO.cardType == StatType.Wound;

        int next = HandNavRules.ClampAfterChange(_lastPadIndex, wounds);
        if (next < 0)
        {
            _owner = FocusOwner.None;
            _lastPadIndex = -1;
            layout.ClearFocus();
            InputContextState.Current = InputContext.Board;
            return;
        }
        _lastPadIndex = next;
        layout.SetFocus(cards[next]);
        InputContextState.Current = InputContext.Fan;
    }
}
