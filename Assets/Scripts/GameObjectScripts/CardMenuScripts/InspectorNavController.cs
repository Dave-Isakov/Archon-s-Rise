using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Gamepad/keyboard navigation for the card pop-out (hybrid model). Reads the Gameplay
// actions and drives the existing CardInspector API; holds only the focus POSITION —
// all play state stays in CardPlaySelection, so the section views render pad changes
// exactly like mouse clicks. Active only while the pop-out is open.
//
// Section entry is by shoulder button (R1 -> Choice, L1 -> Improvise); Empower is a
// global X toggle, never a focus target; the d-pad only cycles options within the
// focused section. Focus snaps to Play whenever the focused section becomes unreachable.
public class InspectorNavController : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] StatSegment[] choiceSegments;    // same objects/order as ChoiceBanner's array
    [SerializeField] StatSegment[] improviseSegments; // same objects/order as ImprovisePanel's array (top to bottom)
    [SerializeField] RectTransform empowerTarget;     // unused in the hybrid model (Empower is not focusable); kept to preserve scene wiring
    [SerializeField] RectTransform playTarget;        // Play button rect
    [SerializeField] RectTransform backTarget;        // Back button rect — hidden while the controller is the active device (B/Cancel covers it)
    [SerializeField] RectTransform focusOutline;      // moving highlight frame (Image, raycastTarget off)

    InspectorNavPosition _pos;
    bool _latched;
    bool _wasOpen;
    bool _messageWasUp;
    bool _lastWasPad;

    void Update()
    {
        bool open = GameManager.Instance != null && GameManager.Instance.cardCanvas.enabled
                    && inspector.Selection != null;
        if (!open)
        {
            _wasOpen = false;
            if (focusOutline != null) focusOutline.gameObject.SetActive(false);
            return;
        }

        if (GameManager.Instance.messageCanvas.enabled)
        {
            // A validation message modally captures input (MessageController owns it);
            // don't drive the pop-out while it's up.
            _messageWasUp = true;
            return;
        }
        if (_messageWasUp)
        {
            // Swallow the frame the message closed on so its dismiss A/B can't also
            // act on the pop-out (independent of Update ordering).
            _messageWasUp = false;
            return;
        }

        UpdateBackVisibility();

        if (!_wasOpen)
        {
            // First open frame: reset focus and swallow this frame's input so the
            // Submit press that opened the pop-out can't also press Play.
            _wasOpen = true;
            _pos = InspectorNavRules.Open();
            RenderOutline();
            return;
        }

        HandleSectionEntry(); // R1 / L1
        HandleEmpower();      // X
        HandleNavigate();     // d-pad within section

        if (GameControls.Gameplay.Cancel.WasPressedThisFrame())
        {
            inspector.Close();
            return;
        }
        HandleSubmit();

        // Empower or a live selection change can lock the focused section; keep focus valid.
        _pos = InspectorNavRules.ClampReachable(_pos, ChoiceReachable, ImproviseReachable);
        RenderOutline();
    }

    bool ChoiceReachable    => inspector.Card.cardSO.isChoice && inspector.Selection.Mode != PlayMode.Improvise;
    bool ImproviseReachable => inspector.Card.cardSO.empowerType != EmpowerType.None && !inspector.Selection.EffectiveEmpowered();
    bool EmpowerReachable   => inspector.Selection.CanEmpower();

    // Choice options are only the segments whose stat flag is set on this card,
    // mirroring what ChoiceBanner actually shows.
    List<StatSegment> ActiveChoiceSegments()
    {
        var active = new List<StatSegment>();
        var card = inspector.Card;
        if (card == null || !card.cardSO.isChoice) return active;
        foreach (var seg in choiceSegments)
            if (card.cardSO.cardType.HasFlag(seg.Stat))
                active.Add(seg);
        return active;
    }

    void HandleSectionEntry()
    {
        if (GameControls.Gameplay.SectionChoice.WasPressedThisFrame())
            _pos = InspectorNavRules.EnterChoice(_pos, ChoiceReachable);
        if (GameControls.Gameplay.SectionImprovise.WasPressedThisFrame())
            _pos = InspectorNavRules.EnterImprovise(_pos, ImproviseReachable);
    }

    void HandleEmpower()
    {
        if (!GameControls.Gameplay.Empower.WasPressedThisFrame()) return;
        if (!EmpowerReachable) return;
        inspector.SetEmpowered(!inspector.Selection.EffectiveEmpowered());
    }

    void HandleNavigate()
    {
        Vector2 nav = GameControls.Gameplay.Navigate.ReadValue<Vector2>();
        // Latch so one press = one step (sticks and held d-pads report every frame).
        if (nav.magnitude < 0.5f) { _latched = false; return; }
        if (_latched) return;
        _latched = true;

        // Dominant axis only; diagonals resolve to the stronger direction.
        int dx = Mathf.Abs(nav.x) >= Mathf.Abs(nav.y) ? (nav.x > 0 ? 1 : -1) : 0;
        int dy = dx == 0 ? (nav.y > 0 ? 1 : -1) : 0;

        _pos = InspectorNavRules.Move(_pos, dx, dy,
            ActiveChoiceSegments().Count, improviseSegments.Length);
    }

    void HandleSubmit()
    {
        if (!GameControls.Gameplay.Submit.WasPressedThisFrame()) return;
        switch (_pos.Section)
        {
            case InspectorSection.Choice:
            {
                var segs = ActiveChoiceSegments();
                if (_pos.Option < segs.Count) inspector.ChooseStat(segs[_pos.Option].Stat);
                break;
            }
            case InspectorSection.Improvise:
                if (_pos.Option < improviseSegments.Length)
                    inspector.ImproviseStat(improviseSegments[_pos.Option].Stat);
                break;
            default: // Play
                inspector.Play();
                break;
        }
    }

    // Mouse hover moves the same focus the pad uses, so both devices share one
    // highlight. Ignores hovers onto non-focusable elements (Empower, Back) and
    // sections the pad couldn't reach (locked/hidden).
    public void FocusFromHover(InspectorSection section, StatSegment segment, bool isBack)
    {
        if (!_wasOpen) return;
        if (section == InspectorSection.Empower) return;             // not focusable
        if (section == InspectorSection.Play && isBack) return;      // Back not focusable
        if (section == InspectorSection.Choice && !ChoiceReachable) return;
        if (section == InspectorSection.Improvise && !ImproviseReachable) return;

        int option = 0;
        if (section == InspectorSection.Choice)
            option = Mathf.Max(0, ActiveChoiceSegments().IndexOf(segment));
        else if (section == InspectorSection.Improvise)
            option = Mathf.Max(0, System.Array.IndexOf(improviseSegments, segment));

        _pos = new InspectorNavPosition(section, option);
        RenderOutline();
    }

    void RenderOutline()
    {
        if (focusOutline == null) return;
        var target = TargetFor(_pos);
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

    RectTransform TargetFor(InspectorNavPosition pos)
    {
        switch (pos.Section)
        {
            case InspectorSection.Choice:
            {
                var segs = ActiveChoiceSegments();
                if (segs.Count == 0) return playTarget;
                return (RectTransform)segs[Mathf.Clamp(pos.Option, 0, segs.Count - 1)].transform;
            }
            case InspectorSection.Improvise:
                return (RectTransform)improviseSegments[Mathf.Clamp(pos.Option, 0, improviseSegments.Length - 1)].transform;
            default: // Play
                return playTarget;
        }
    }

    // Back is redundant with Cancel (B) on a controller, so hide it while the pad is
    // the active device and restore it the moment the mouse/keyboard is used. Same
    // last-input-wins arbitration the hand fan uses; the opening press sets the initial
    // state (A opened it -> pad; click opened it -> desktop).
    void UpdateBackVisibility()
    {
        if (backTarget == null) return;
        if (PadActuated()) _lastWasPad = true;
        else if (DesktopActuated()) _lastWasPad = false;

        if (backTarget.gameObject.activeSelf == _lastWasPad)
            backTarget.gameObject.SetActive(!_lastWasPad);
    }

    static bool PadActuated()
    {
        var pad = Gamepad.current;
        if (pad == null) return false;
        return pad.leftStick.ReadValue().sqrMagnitude > 0.25f
            || pad.dpad.ReadValue().sqrMagnitude > 0.25f
            || pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
            || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
            || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
            || pad.startButton.wasPressedThisFrame;
    }

    static bool DesktopActuated()
    {
        var mouse = Mouse.current;
        if (mouse != null && (mouse.delta.ReadValue() != Vector2.zero
            || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            return true;
        var kb = Keyboard.current;
        return kb != null && kb.anyKey.wasPressedThisFrame;
    }
}
