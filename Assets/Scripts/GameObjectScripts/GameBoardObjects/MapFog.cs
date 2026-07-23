using UnityEngine;
using UnityEngine.Tilemaps;

// Single source of truth for "is this map cell still under fog of war?". Map
// tokens and preview triggers gate their interaction through here so nothing
// hidden by fog can be hovered, previewed, or clicked. The fog Tilemap is owned
// by the ExplorationController; it is looked up once and cached (the cache self-heals
// after a scene reload because a destroyed Unity object compares == null).
public static class MapFog
{
    static Tilemap fog;

    static Tilemap Fog()
    {
        if (fog == null)
        {
            var ctrl = Object.FindAnyObjectByType<ExplorationController>();
            if (ctrl != null) fog = ctrl.Fog;
        }
        return fog;
    }

    // True while the fog still covers this cell. If the fog cannot be found we
    // treat the cell as visible, so a missing reference never wrongly blocks play.
    public static bool IsHidden(Vector3Int cell)
    {
        var f = Fog();
        return f != null && f.HasTile(cell);
    }
}
