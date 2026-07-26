using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Mouse implementation of IHexPointerSource. Converts the cursor to a grid cell each
// frame and reports left-click as confirm. Suppresses everything while the pointer is
// over UI (so clicking the hand never moves the board).
public class MouseHexPointerSource : IHexPointerSource
{
    readonly Grid grid;
    readonly Camera cam;
    static readonly List<RaycastResult> uiHits = new();

    public MouseHexPointerSource(Grid grid, Camera cam)
    {
        this.grid = grid;
        this.cam = cam;
    }

    // Over ACTUAL UI only (the hand/cards canvas — a GraphicRaycaster hit), NOT a
    // world-space token collider. IsPointerOverGameObject() conflates the two: with
    // a Physics2DRaycaster in the scene it returns true over town/dungeon/hotspot
    // sprites, which would suppress the hex pointer (no tooltip, dead click zone) on
    // every occupied cell. Place tokens still self-handle clicks via their own
    // IPointerClickHandler; the interactor won't double-act because Blocks()/PlaceOccupies
    // gate place cells.
    bool OverUI
    {
        get
        {
            var es = EventSystem.current;
            if (es == null || Mouse.current == null) return false;
            var data = new PointerEventData(es) { position = Mouse.current.position.ReadValue() };
            uiHits.Clear();
            es.RaycastAll(data, uiHits);
            foreach (var r in uiHits)
                if (r.module is GraphicRaycaster) return true;
            return false;
        }
    }

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
