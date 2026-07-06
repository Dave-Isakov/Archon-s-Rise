// Which surface currently owns gamepad/keyboard navigation. Exactly one context is
// active at a time; controllers check this before consuming Gameplay actions.
// Board is the default and the slot where the future hex-map/town/combat
// controllers will live (mouse-only this phase).
public enum InputContext { Board, Fan, Inspector }

public static class InputContextState
{
    public static InputContext Current { get; set; } = InputContext.Board;
}
