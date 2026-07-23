using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Mouse implementation of IHexPointerSource. Converts the cursor to a grid cell each
// frame and reports left-click as confirm. Suppresses everything while the pointer is
// over UI (so clicking the hand never moves the board).
public class MouseHexPointerSource : IHexPointerSource
{
    readonly Grid grid;
    readonly Camera cam;

    public MouseHexPointerSource(Grid grid, Camera cam)
    {
        this.grid = grid;
        this.cam = cam;
    }

    bool OverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    public bool TryGetCell(out Vector3Int cell)
    {
        cell = default;
        if (Mouse.current == null || cam == null || OverUI) return false;
        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
        cell = grid.WorldToCell(world);
        return true;
    }

    public bool ConfirmPressed =>
        !OverUI && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
}
