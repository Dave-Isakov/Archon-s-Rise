using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

// Free-camera map mode (spec 2026-07-31). The Main Camera stays PARENTED to the
// PlayerPosition prefab — roughly twenty canvases reference it as their Screen
// Space - Camera target, so it can be neither swapped nor reparented safely.
// Map mode instead drives its localPosition + orthographicSize and writes back
// two literals on close, so "restore" is exact by construction.
//
// Movement is impossible while the map is open (see the gates in HexInteractor,
// the tokens, HandFocusController and TurnFlowShortcuts), so the parent transform
// is static and a world-space pan is a straight subtraction into local space.
public class MapModeController : MonoBehaviour
{
    public static MapModeController Instance { get; private set; }

    [SerializeField] Camera boardCamera;
    [SerializeField] Grid gameboard;
    [SerializeField] Transform player;
    [SerializeField] Tilemap ground;
    [SerializeField] Tilemap water;
    [SerializeField] Tilemap mountains;

    // Tuning; adjust in play. None of these are load-bearing.
    [SerializeField] float mapOrthoSize = 8f;
    [SerializeField] float panSpeed = 12f;          // world units per second
    [SerializeField] float transitionSeconds = 0.15f;

    // The values map mode restores on close. The camera's authored local offset.
    const float PlayOrthoSize = 4f;
    static readonly Vector3 PlayLocalPosition = new Vector3(0f, 0f, -10f);

    public bool IsOpen { get; private set; }
    public Vector2 PanWorld => panWorld;
    public Vector3Int CenterCell => gameboard.WorldToCell(new Vector3(panWorld.x, panWorld.y, 0f));

    Vector2 panWorld;
    float minX, maxX, minY, maxY;   // clamp limits for the camera centre
    float transition;               // 0 = fully closed, 1 = fully open

    void Awake() { Instance = this; }

    public void Open()
    {
        if (IsOpen || player == null) return;
        IsOpen = true;
        // Start exactly where the camera already is, so opening never jumps.
        // Set this BEFORE RecomputeLimits — its unwired fallback locks the limits
        // to the current pan position.
        panWorld = new Vector2(player.position.x, player.position.y);
        RecomputeLimits();
        panWorld.x = MapCameraRules.ClampAxis(panWorld.x, minX, maxX);
        panWorld.y = MapCameraRules.ClampAxis(panWorld.y, minY, maxY);
        InputContextState.Current = InputContext.Map;
        // A fog scout armed by a first click must not survive into map mode,
        // where the confirming second click can never be delivered.
        if (HexInteractor.Instance != null) HexInteractor.Instance.DisarmFogScout();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (InputContextState.Current == InputContext.Map)
            InputContextState.Current = InputContext.Board;
    }

    void Update()
    {
        if (IsOpen) Pan();

        // Fully closed and staying closed: touch nothing. Map mode must be inert
        // during normal play, not a component that rewrites the camera transform
        // every frame and stomps anything else that wants to move it.
        if (!IsOpen && transition <= 0f) return;

        // Ease toward the current mode, then apply. The frame the ease reaches 0
        // still runs Apply(), which writes the authored literals exactly — so a
        // close during an in-progress ease always lands on the right values.
        float target = IsOpen ? 1f : 0f;
        float rate = transitionSeconds > 0f ? Time.unscaledDeltaTime / transitionSeconds : 1f;
        transition = Mathf.MoveTowards(transition, target, rate);
        Apply();
    }

    void Pan()
    {
        Vector2 raw = GameControls.Gameplay.MapPan.ReadValue<Vector2>();
        MapCameraRules.NormalizeInput(raw.x, raw.y, out float ix, out float iy);
        float dt = Time.unscaledDeltaTime;
        panWorld.x = MapCameraRules.StepAxis(panWorld.x, ix, panSpeed, dt, minX, maxX);
        panWorld.y = MapCameraRules.StepAxis(panWorld.y, iy, panSpeed, dt, minY, maxY);
    }

    void Apply()
    {
        if (boardCamera == null || player == null) return;

        if (transition <= 0f)
        {
            boardCamera.transform.localPosition = PlayLocalPosition;
            boardCamera.orthographicSize = PlayOrthoSize;
            return;
        }

        var open = new Vector3(panWorld.x - player.position.x,
                               panWorld.y - player.position.y,
                               PlayLocalPosition.z);
        boardCamera.transform.localPosition = Vector3.Lerp(PlayLocalPosition, open, transition);
        boardCamera.orthographicSize = Mathf.Lerp(PlayOrthoSize, mapOrthoSize, transition);
    }

    // Edge limits for the camera CENTRE, recomputed on every open so the generated
    // map size can change freely. Fog is excluded — it is not terrain, and it covers
    // cells terrain does not.
    void RecomputeLimits()
    {
        if (ground == null || gameboard == null)
        {
            // Unwired: lock panning to where the camera already is rather than
            // throwing every frame. Loud, because it means the scene is wrong.
            Debug.LogWarning("MapModeController: ground tilemap or grid not assigned; map panning is disabled.", this);
            minX = maxX = panWorld.x;
            minY = maxY = panWorld.y;
            return;
        }

        var b = ground.cellBounds;
        if (water != null) { var w = water.cellBounds; b.SetMinMax(Vector3Int.Min(b.min, w.min), Vector3Int.Max(b.max, w.max)); }
        if (mountains != null) { var m = mountains.cellBounds; b.SetMinMax(Vector3Int.Min(b.min, m.min), Vector3Int.Max(b.max, m.max)); }

        // cellBounds.max is exclusive, so the last real cell is max - 1.
        int loX = b.min.x, loY = b.min.y;
        int hiX = b.max.x - 1, hiY = b.max.y - 1;

        // Project all FOUR corners, not two: hex offset-rows shift odd rows by
        // +0.5 on x, so two corners would give a lopsided rect.
        var c0 = gameboard.GetCellCenterWorld(new Vector3Int(loX, loY, 0));
        var c1 = gameboard.GetCellCenterWorld(new Vector3Int(hiX, loY, 0));
        var c2 = gameboard.GetCellCenterWorld(new Vector3Int(loX, hiY, 0));
        var c3 = gameboard.GetCellCenterWorld(new Vector3Int(hiX, hiY, 0));

        minX = Mathf.Min(Mathf.Min(c0.x, c1.x), Mathf.Min(c2.x, c3.x));
        maxX = Mathf.Max(Mathf.Max(c0.x, c1.x), Mathf.Max(c2.x, c3.x));
        minY = Mathf.Min(Mathf.Min(c0.y, c1.y), Mathf.Min(c2.y, c3.y));
        maxY = Mathf.Max(Mathf.Max(c0.y, c1.y), Mathf.Max(c2.y, c3.y));
    }
}
