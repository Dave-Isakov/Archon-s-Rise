using UnityEngine;
using UnityEngine.Tilemaps;

// Input + feedback layer for hex-selection exploration (spec 2026-07-23). Each frame:
// resolve the pointed cell from the active IHexPointerSource, gather facts from
// ExplorationController + MapFog, classify via HexActionRules, drive the tooltip +
// highlight, and on confirm dispatch Move/ScoutFog. Token cells (enemy/place) are left
// to the tokens; teleport targeting is added in Task 5.
public class HexInteractor : MonoBehaviour
{
    public static HexInteractor Instance { get; private set; }

    [SerializeField] Grid gameboard;
    [SerializeField] Camera boardCamera;
    [SerializeField] ExplorationController exploration;
    [SerializeField] HexTooltip tooltip;
    [SerializeField] Tilemap highlight;          // overlay tilemap, above terrain / below tokens
    [SerializeField] TileBase highlightTile;      // single hex tile stamped on the pointed cell
    [SerializeField] Color moveColor    = new Color(0.3f, 1f, 0.3f, 0.5f);
    [SerializeField] Color blockedColor = new Color(1f, 0.3f, 0.3f, 0.5f);
    [SerializeField] Color scoutColor   = new Color(0.3f, 0.6f, 1f, 0.5f);
    [SerializeField] Color teleportColor = new Color(0.7f, 0.3f, 1f, 0.5f);
    [SerializeField] Color infoColor    = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] Color ringColor    = new Color(0.3f, 1f, 0.3f, 0.18f); // faint affordable-ring

    IHexPointerSource pointer;
    readonly System.Collections.Generic.List<Vector3Int> painted = new(); // cells tinted this frame
    Vector3Int? armedFogCell;   // fog scout requires a confirming second click on the same cell

    // Teleport state (wired up in Task 5).
    protected bool teleportMode;
    public bool IsTeleporting => teleportMode;

    void Awake() { Instance = this; }

    void Start()
    {
        pointer = new MouseHexPointerSource(gameboard, boardCamera != null ? boardCamera : Camera.main);
    }

    void Update()
    {
        if (!pointer.TryGetCell(out var cell))
        {
            ClearPainted();
            tooltip.Hide();
            armedFogCell = null;
            return;
        }

        var verdict = Classify(cell, out bool placeOnCell);
        Render(cell, verdict);

        if (pointer.ConfirmPressed)
            Dispatch(cell, verdict, placeOnCell);
    }

    HexAction Classify(Vector3Int cell, out bool placeOnCell)
    {
        bool isSameCell = cell == exploration.PlayerCell;
        bool hasTerrain = exploration.TryTerrain(cell, out int entryCost);
        bool isAdjacent = exploration.IsAdjacent(cell);
        bool isFog = MapFog.IsHidden(cell);
        bool enemyOnCell = exploration.EnemyOccupies(cell);
        placeOnCell = PlaceOccupies(cell);
        return HexActionRules.Resolve(isSameCell, hasTerrain, entryCost, isAdjacent,
            isFog, enemyOnCell, exploration.PlayerExplore, exploration.FogCost, teleportMode);
    }

    void Dispatch(Vector3Int cell, HexAction verdict, bool placeOnCell)
    {
        switch (verdict.Kind)
        {
            case HexActionKind.Move:
                // Place cells are moved onto via the place token (Task 4); defer.
                if (!placeOnCell) { exploration.Move(cell); armedFogCell = null; }
                break;
            case HexActionKind.ScoutFog:
                if (armedFogCell == cell) { exploration.ScoutFog(cell); armedFogCell = null; }
                else armedFogCell = cell;   // first click arms; tooltip prompts to confirm
                break;
            // EnemyFight / DistantInfo / DistantFog / None / OffMap: no board dispatch.
            // TeleportTarget is handled by the Task 5 override.
            default:
                break;
        }
    }

    void Render(Vector3Int cell, HexAction verdict)
    {
        ClearPainted();
        PaintRing(cell);            // faint tint on affordable adjacent cells
        PaintHover(cell, verdict);  // stronger tint on the pointed cell (drawn on top)
        tooltip.Show(TooltipText(cell, verdict), gameboard.GetCellCenterWorld(cell));
    }

    // Persistent affordable-ring affordance (replaces the arrows' always-visible costs):
    // faintly tint each adjacent cell the player could currently step onto. Skips the
    // hovered cell so PaintHover's stronger tint wins there. Suppressed in teleport mode.
    void PaintRing(Vector3Int hoverCell)
    {
        if (teleportMode) return;
        foreach (var n in exploration.PlayerNeighbors())
        {
            if (n == hoverCell) continue;
            if (!exploration.TryTerrain(n, out int cost)) continue;
            if (MapFog.IsHidden(n)) continue;
            if (exploration.EnemyOccupies(n)) continue;
            if (exploration.PlayerExplore < cost) continue;
            Paint(n, ringColor);
        }
    }

    void PaintHover(Vector3Int cell, HexAction verdict)
    {
        Color? tint = verdict.Kind switch
        {
            HexActionKind.Move           => verdict.Affordable ? moveColor : blockedColor,
            HexActionKind.ScoutFog       => verdict.Affordable ? scoutColor : blockedColor,
            HexActionKind.TeleportTarget => teleportColor,
            HexActionKind.DistantInfo    => infoColor,
            _ => (Color?)null
        };
        if (tint.HasValue) Paint(cell, tint.Value);
    }

    string TooltipText(Vector3Int cell, HexAction verdict)
    {
        string exp = IconMarkup.Tag(IconConcept.Explore);
        switch (verdict.Kind)
        {
            case HexActionKind.Move:
                return verdict.Affordable
                    ? $"Move here — {exp} {verdict.Cost}"
                    : $"Need {exp} {verdict.Cost} to move here";
            case HexActionKind.ScoutFog:
                if (!verdict.Affordable) return $"Need {exp} {verdict.Cost} to scout this fog";
                return armedFogCell == cell
                    ? "Click again to scout"
                    : $"Scout this fog — {exp} {verdict.Cost}";
            case HexActionKind.DistantInfo:
                return $"{exp} {verdict.Cost}";
            case HexActionKind.DistantFog:
                return "Unexplored";
            case HexActionKind.TeleportTarget:
                return "Teleport here";
            default:
                return null; // None / OffMap / EnemyFight (enemy token shows its own preview)
        }
    }

    void Paint(Vector3Int cell, Color tint)
    {
        if (highlight == null || highlightTile == null) return;
        highlight.SetTile(cell, highlightTile);
        highlight.SetTileFlags(cell, TileFlags.None);
        highlight.SetColor(cell, tint);
        painted.Add(cell);
    }

    void ClearPainted()
    {
        if (highlight != null)
            foreach (var c in painted) highlight.SetTile(c, null);
        painted.Clear();
    }

    // A town or dungeon token stands on this cell (visible). These handle their own
    // clicks (Task 4), so HexInteractor never dispatches Move onto them.
    protected bool PlaceOccupies(Vector3Int cell)
    {
        if (MapFog.IsHidden(cell)) return false;
        foreach (var t in FindObjectsByType<TownToken>())
            if (t.gridPos == cell) return true;
        foreach (var d in FindObjectsByType<DungeonToken>())
            if (d.gridPos == cell) return true;
        return false;
    }
}
