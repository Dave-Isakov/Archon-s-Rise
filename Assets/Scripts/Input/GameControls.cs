// Single shared instance of the generated Controls wrapper. Controllers read
// GameControls.Gameplay.* directly instead of each owning InputActionReferences,
// so actions need no scene wiring.
public static class GameControls
{
    static Controls _controls;

    public static Controls.GameplayActions Gameplay
    {
        get
        {
            if (_controls == null)
            {
                _controls = new Controls();
                _controls.Gameplay.Enable();
            }
            return _controls.Gameplay;
        }
    }
}
