using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The single writer of bar focus. Two lanes — units left, cards right — are
// walked as one rail, so crossing the lane boundary IS the swap; there is no
// separate swap input and no hand-off between controllers. Mouse claims focus
// only when the pointer actually MOVES (delta != 0); gamepad/keyboard claims it
// on a Navigate press — last input wins.
public class BarFocusController : MonoBehaviour
{
    public static BarFocusController Instance { get; private set; }

    [SerializeField] FanLane unitLane;
    [SerializeField] FanLane cardLane;

    enum FocusOwner { None, Mouse, Pad }
    FocusOwner _owner = FocusOwner.None;
    Vector2 _lastMousePos;
    bool _navLatched;
    bool _inspectorWasOpen;
    RailPos _lastPos = RailPos.None;
    BarLane _focusedLane = BarLane.Cards;

    void Awake()
    {
        Instance = this;
        cardLane.SetPose(parked: false, instant: true);
        unitLane.SetPose(parked: true, instant: true);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    FanLane Lane(BarLane lane) => lane == BarLane.Units ? unitLane : cardLane;
    List<IFanItem> Items(BarLane lane) => Lane(lane).InLane();

    static List<bool> Blocked(List<IFanItem> items)
    {
        var blocked = new List<bool>(items.Count);
        foreach (var item in items) blocked.Add(!item.Selectable);
        return blocked;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.cardCanvas.enabled || gm.unitCanvas.enabled)
        {
            // Pop-out open: the bar shows no item focus and consumes no input.
            // _owner and the lane pose are kept so both restore on close.
            if (!_inspectorWasOpen) { _inspectorWasOpen = true; ClearItemFocus(); }
            return;
        }

        if (_inspectorWasOpen)
        {
            // First frame after the pop-out closed. Restore pad focus and swallow
            // this frame's input so the Cancel/Submit press that closed the
            // pop-out cannot also act here.
            _inspectorWasOpen = false;
            if (_owner == FocusOwner.Pad) RestorePadFocus();
            return;
        }

        if (gm.mainMenuCanvas.enabled || gm.cardListCanvas.enabled) return;

        // Map mode: the per-frame hit-test would focus items the player is
        // panning over, and arrow keys are pan input there, not rail navigation.
        if (InputContextState.Current == InputContext.Map) return;

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

        // Hover acts only inside the focused lane; a parked lane responds to
        // clicks, never to the cursor passing over it.
        var lane = Lane(_focusedLane);
        var hit = lane.HitTest(pos);
        if (hit != null)
        {
            _owner = FocusOwner.Mouse;
            lane.SetFocus(hit);
            _lastPos = new RailPos(_focusedLane, Items(_focusedLane).IndexOf(hit));
        }
        else if (_owner == FocusOwner.Mouse)
        {
            // Only a mouse-claimed focus is cleared by the mouse leaving; drifting
            // the mouse must not clear a pad-claimed focus.
            _owner = FocusOwner.None;
            ClearItemFocus();
        }
    }

    void HandleNavigate()
    {
        Vector2 nav = GameControls.Gameplay.Navigate.ReadValue<Vector2>();
        // Latch so one press = one step (sticks and held d-pads report every frame).
        if (nav.magnitude < 0.5f) { _navLatched = false; return; }
        if (_navLatched) return;
        _navLatched = true;

        // Up/down no longer cross lanes — left/right is the whole rail.
        if (Mathf.Abs(nav.y) > Mathf.Abs(nav.x)) return;

        var units = Items(BarLane.Units);
        var cards = Items(BarLane.Cards);
        var next = BarRailRules.Step(_lastPos, nav.x > 0 ? +1 : -1, Blocked(units), Blocked(cards));
        if (next.IsNone) return;

        _owner = FocusOwner.Pad;
        ApplyPos(next);
    }

    void HandleCancel()
    {
        if (_owner != FocusOwner.Pad) return;
        if (!GameControls.Gameplay.Cancel.WasPressedThisFrame()) return;
        _owner = FocusOwner.None;
        _lastPos = RailPos.None;
        ClearItemFocus();
        InputContextState.Current = InputContext.Board;
    }

    void HandleSubmit()
    {
        if (!GameControls.Gameplay.Submit.WasPressedThisFrame()) return;
        var focused = Lane(_focusedLane).Focused;
        if (focused == null) return;
        focused.Activate();
    }

    // Card and Unit offer their click here first. A click on an item in a PARKED
    // lane is consumed as swap-and-select: the lanes swap, focus lands on (or
    // beside) the clicked item, and its pop-out does NOT open. A click in the
    // focused lane is not claimed, so the normal open path runs.
    public bool TryClaimClick(IFanItem item)
    {
        if (InputContextState.MapOpen) return false;
        var gm = GameManager.Instance;
        if (gm == null) return false;
        if (gm.cardCanvas.enabled || gm.unitCanvas.enabled ||
            gm.mainMenuCanvas.enabled || gm.cardListCanvas.enabled) return false;

        if (!Locate(item, out var lane, out int index)) return false; // not in a lane
        if (lane == _focusedLane) return false;

        _owner = FocusOwner.Mouse;

        // Clamp inside the CLICKED lane, not across the bar: the player aimed at
        // a lane, so a wound or exhausted unit still buys them that lane.
        int landing = HandNavRules.ClampAfterChange(index, Blocked(Items(lane)));
        if (landing < 0)
        {
            SetFocusedLane(lane);
            ClearItemFocus();
            return true;
        }
        ApplyPos(new RailPos(lane, landing));
        return true;
    }

    // Rebuilds the unit lane from its children in sibling order. Called by Player
    // whenever the army or its exhaust state changes, because IsPlayed feeds
    // IFanItem.Selectable and the rail's mask is stale until it runs.
    public void RelayoutUnits()
    {
        if (unitLane == null) return;
        var items = new List<IFanItem>();
        foreach (Transform child in unitLane.Container)
        {
            var unit = child.GetComponent<Unit>();
            if (unit != null) items.Add(unit);
        }
        unitLane.Relayout(items);
        if (_owner == FocusOwner.Pad) KeepPadFocusValid();
    }

    bool Locate(IFanItem item, out BarLane lane, out int index)
    {
        index = Items(BarLane.Units).IndexOf(item);
        if (index >= 0) { lane = BarLane.Units; return true; }
        index = Items(BarLane.Cards).IndexOf(item);
        if (index >= 0) { lane = BarLane.Cards; return true; }
        lane = BarLane.Cards;
        return false;
    }

    void ApplyPos(RailPos pos)
    {
        if (pos.IsNone) { ClearItemFocus(); return; }
        var items = Items(pos.Lane);
        if (pos.Index >= items.Count) return;

        SetFocusedLane(pos.Lane);
        Lane(pos.Lane).SetFocus(items[pos.Index]);
        Lane(pos.Lane == BarLane.Units ? BarLane.Cards : BarLane.Units).ClearFocus();
        _lastPos = pos;
        InputContextState.Current = InputContext.Fan;
    }

    void SetFocusedLane(BarLane lane)
    {
        if (_focusedLane == lane) return;
        _focusedLane = lane;
        cardLane.SetPose(parked: lane != BarLane.Cards);
        unitLane.SetPose(parked: lane != BarLane.Units);
    }

    void ClearItemFocus()
    {
        unitLane.ClearFocus();
        cardLane.ClearFocus();
    }

    // After draw/discard/heal/play/recruit/exhaust the focused item may have left
    // the bar; keep pad focus on the nearest survivor instead of letting it vanish.
    // The clamp is free to cross the lane boundary, and lane focus follows it.
    void KeepPadFocusValid()
    {
        var focused = Lane(_focusedLane).Focused;
        if (focused != null && Items(_focusedLane).Contains(focused))
        {
            _lastPos = new RailPos(_focusedLane, Items(_focusedLane).IndexOf(focused));
            return;
        }
        RestorePadFocus();
    }

    void RestorePadFocus()
    {
        var units = Items(BarLane.Units);
        var cards = Items(BarLane.Cards);
        var next = BarRailRules.ClampAfterChange(_lastPos, Blocked(units), Blocked(cards));
        if (next.IsNone)
        {
            _owner = FocusOwner.None;
            _lastPos = RailPos.None;
            ClearItemFocus();
            InputContextState.Current = InputContext.Board;
            return;
        }
        ApplyPos(next);
    }
}
