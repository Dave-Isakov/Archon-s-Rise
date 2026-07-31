// Which surface currently owns gamepad/keyboard navigation. Exactly one context is
// active at a time; controllers check this before consuming Gameplay actions.
// Board is the default and the slot where the future hex-map/town/combat
// controllers will live (mouse-only this phase).
public enum InputContext { Board, Fan, Inspector, Map }

public static class InputContextState
{
    static InputContext _current = InputContext.Board;

    // Map mode is MODAL, and it is the only context that is: while it is open every
    // board gate reads `Current == Map`, but CardInspector, UnitInspector, UnitsLane
    // and HandFocusController all write Current unconditionally. A single card click
    // in map mode ran CardInspector.Open() -> Inspector and Close() -> Board, which
    // silently killed every map gate while the camera was still zoomed out (you could
    // then move, fight and enter places from the map). So writes that would LEAVE Map
    // are ignored; only ReleaseMap() — i.e. MapModeController.Close() — gets out.
    public static InputContext Current
    {
        get => _current;
        set
        {
            if (_current == InputContext.Map && value != InputContext.Map) return;
            _current = value;
        }
    }

    // The single question every map gate asks. Reads better than repeating the
    // comparison, and keeps the "one flag" rule from the spec intact.
    public static bool MapOpen => _current == InputContext.Map;

    // The one sanctioned exit from Map. A no-op from any other context, so Escape
    // routing through MapModeController.Close() can never yank a live Inspector or
    // Fan back to Board.
    public static void ReleaseMap()
    {
        if (_current == InputContext.Map) _current = InputContext.Board;
    }
}
