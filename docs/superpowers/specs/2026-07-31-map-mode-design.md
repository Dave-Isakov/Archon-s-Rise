# Map Mode — free camera over the hex board

Date: 2026-07-31

## 1. Goal

Pressing **M** on the board opens a map view: the camera detaches from the player,
zooms out, and pans freely under keyboard control, clamped to the generated map's
edges. **M** closes it again from any state. No movement, no board actions, and no
turn flow happen while the map is open — it is a pure viewing mode.

The design leaves a deliberate seat for a later "click a hex on the map to teleport
there" feature, without building any of it now.

## 2. Constraints discovered

- The Main Camera is a **child of the PlayerPosition prefab** at local `(0,0,-10)`,
  orthographic size 4. The player is therefore always screen centre today.
- Roughly twenty canvases in `GameBoard.unity` reference that exact camera as their
  `m_Camera` (Screen Space – Camera). Swapping in a different camera would misrender
  the whole UI, so map mode must reuse this one.
- `HexInteractor` runs every frame off the mouse and dispatches Move / ScoutFog on
  click. `EnemyToken` and `PlaceTokenBase` handle their own `OnPointerClick`.
  All three must be gated or the player acts on the board while panning.
- `HandFocusController` reads `Gameplay/Navigate` every frame to step card-fan focus,
  so pan input must not reuse that action.
- Map size is hardcoded 20×20 in `GridGeneration`, but the terrain Tilemaps carry a
  real `cellBounds` — that is the dynamic edge source.

## 3. Decisions

| Question | Decision |
|---|---|
| Zoom | Zoom out to a fixed wider orthographic size on open; no runtime zoom control. |
| Pan input | Keyboard/stick only (WASD, arrows, left stick). No drag, no edge scroll. |
| Mouse over hexes | Hover tooltips stay live; **all clicks are inert**. |
| Edge behaviour | The camera **centre** is clamped to the map's cell bounds. |
| When M opens | Only from a clear board (`InputContext.Board`, no modal). M always closes. |
| Camera decoupling | Camera stays parented; map mode drives `localPosition` + `orthographicSize`. |

### 3.1 Why the camera stays parented

Rejected: reparenting to a scene-root rig (adds a restore step that can desync on
scene reload or save load, and buys nothing while the player cannot move). Rejected:
a second dedicated map camera (every canvas points at the player's camera by fileID).

Keeping the camera parented means "restore" is writing back two literals rather than
bookkeeping a previous parent. Because the player cannot move while the map is open,
the parent transform is static, so a world-space pan is a straight subtraction into
local space.

## 4. Components

### 4.1 `MapCameraRules` — pure

`Assets/Scripts/MapMode/MapCameraRules.cs`, with its own asmdef
(`ArchonsRise.MapMode`) and a tests asmdef alongside it, mirroring
`Assets/Scripts/Exploration/`.

```
NormalizeInput(float ix, float iy, out float nx, out float ny)
StepAxis(float current, float input, float speed, float dt, float min, float max) -> float
ClampAxis(float v, float min, float max) -> float
```

The API is **floats only, no `UnityEngine` types**, so the EditMode tests compile and
run under the standalone mcs harness without referencing `UnityEngine.dll` — necessary
because an open editor blocks batch-mode `runTests`.

`NormalizeInput` scales any input vector with magnitude > 1 down to 1, so diagonal
panning is not faster than cardinal. `ClampAxis` returns the midpoint when `min > max`,
so a degenerate or empty map locks that axis to centre instead of producing NaN.

### 4.2 `MapModeController` — MonoBehaviour

`Assets/Scripts/GameObjectScripts/PlayerScripts/MapModeController.cs`. A
`static Instance`, matching `HexInteractor` and `ExplorationController`.

Serialized fields: `boardCamera`, `gameboard` (Grid), the ground / water / mountains
Tilemaps, `mapOrthoSize` (default 8), `panSpeed` (default 12 world units/sec),
`transitionSeconds` (default 0.15). Those three defaults are tuning values to be
adjusted in play; none are load-bearing.

State: `Vector2 panWorld`, `bool IsOpen`.

**Open.** `panWorld` initialises to the player's current world position, so the camera
does not jump — only `orthographicSize` eases 4 → `mapOrthoSize`. Sets
`InputContextState.Current = InputContext.Map`. Recomputes the edge limits (§5), and
calls a new `HexInteractor.DisarmFogScout()` — `armedFogCell` is private, so the reset
is exposed as a method rather than reached into.

**Per frame while open.** Read `MapPan`, `NormalizeInput`, `StepAxis` per axis into
`panWorld`, then write:

```
boardCamera.transform.localPosition =
    new Vector3(panWorld.x - player.position.x, panWorld.y - player.position.y, -10f);
```

**Close.** Eases `localPosition` back to `(0,0,-10)` and `orthographicSize` back to 4,
sets `InputContextState.Current = InputContext.Board`. Both values are written
unconditionally at the end of the ease, so a close during an in-progress transition
cannot strand the camera at a partial offset.

**Public surface for the future teleport:** `PanWorld`, `CenterCell` (the grid cell at
the camera centre), and `Close()`, which is idempotent.

## 5. Dynamic map edges

Recomputed on **every open**, never cached across opens, so changing the generated map
size requires no edit here.

1. Union the `cellBounds` of the ground, water and mountains Tilemaps. Fog is excluded
   — it is not terrain, and it covers cells that terrain does not.
2. Take the **four corner cells** of that union, world-project each with
   `grid.GetCellCenterWorld`, and min/max each axis independently. Four corners rather
   than two because hex offset-rows shift odd rows by +0.5 on x; two corners would
   produce a lopsided rect.
3. The resulting rect is the clamp limit for the camera centre. For the current 20×20
   map that is approximately x ∈ [0, 19.5], y ∈ [0, 14.25].

`cellBounds` over-reports if tiles are ever cleared, which would allow panning slightly
past the outermost hex. That is harmless; it can never under-report and lock the player
out of real map.

## 6. Input

Two new actions in the `Gameplay` map of `Assets/Input/Controls.inputactions`. Unity
regenerates `Assets/Scripts/Input/Controls.cs` on import — the generated file is never
hand-edited.

- **`ToggleMap`** — Button — `<Keyboard>/m` and `<Gamepad>/select`.
- **`MapPan`** — Value / Vector2 — a WASD 2DVector composite, an arrow-key 2DVector
  composite, and `<Gamepad>/leftStick`.

`MapPan` is deliberately a new action rather than a reuse of `Navigate`:
`HandFocusController` reads `Navigate` every frame to step card-fan focus, so binding
WASD onto `Navigate` would make WASD walk the hand during normal play.

A gamepad binding is included from the start per the standing rule that controller
support must never be retrofitted onto a keyboard-only design.

## 7. Gating

`InputContext` gains a fourth value, `Map`. That enum value **is** the flag — there is
no separate `MapMode.IsOpen` static, so there is one source of truth, exactly as
`Inspector` works today.

Each touch point is a one-line early return on
`InputContextState.Current == InputContext.Map`:

| File | Edit | Prevents |
|---|---|---|
| `Input/InputContext.cs` | add `Map` to the enum | — |
| `HexInteractor.cs` | gate `Dispatch` only; Classify/Render still run | clicking pans → moving the player off-screen |
| `HexInteractor.cs` | add `DisarmFogScout()`, called on open | a half-armed fog scout surviving into map mode |
| `PlaceTokenBase.cs` | early return in `OnPointerClick` | clicking a town opening its menu |
| `EnemyToken.cs` | early return in `OnPointerClick` | clicking an enemy starting a fight |
| `HandFocusController.cs` | early return in `Update` | the mouse hit-test focusing cards under the map |
| `TurnFlowShortcuts.cs` | add `Map` to the existing guard | Undo / End Turn hotkeys firing |
| `DataManager.cs` | `Map` branch calls `MapModeController.Close()` | Escape stacking the pause menu over the map |

Both token handlers already open with `if (MapFog.IsHidden(gridPos)) return;`, so the
new guard sits directly beneath as a parallel line.

The `DataManager` edit mirrors the existing `Inspector` branch in the same method,
where Escape closes the pop-out instead of opening the main menu.

## 8. Open condition and the `IsSettledState` split

M opens the map only when both hold:

- `InputContextState.Current == InputContext.Board`
- the board is clear of modals

`DataManager.IsSettledState()` already enumerates every modal, but it **also** requires
an empty command stack. `PlayManager` keeps every undoable play on that stack until a
commit, so after one card play or one move it is non-empty for the rest of the turn —
gating on it directly would lock the map out of most of every turn.

The modal half is therefore extracted:

```
// True when the board is clear: no modal sub-screen, nothing queued, run not over,
// not mid-load. IsSettledState is this plus an empty command stack.
public bool IsBoardClear()
```

covering: null `GameManager`, `IsLoading`, `RunEndController.HasEnded`, the combat /
town / dungeon / card-reward / card-list canvases, `PlaceFan.IsOpen`, and
`RewardQueue.Busy`. `IsSettledState()` becomes `IsBoardClear() && commands.IsEmpty`.

Save-gate behaviour is unchanged, there is no second modal list to drift, and any modal
added later gates the map for free.

**M always closes the map**, with no gate at all.

## 9. Future teleport

The dispatch gate is verdict-aware rather than a blanket skip:

```
bool DispatchAllowed(HexActionKind k)
    => InputContextState.Current != InputContext.Map || k == HexActionKind.TeleportTarget;
```

`teleportMode` is false today, so `HexActionRules.Resolve` never produces
`TeleportTarget` and this branch is inert. When map-teleport is built, it needs no new
gating code — only `teleportMode = true` while the map is open, plus a `Close()` call
on completion.

Explicitly **not** built now: any teleport UI, cost, targeting rules, or unlock.

## 10. Persistence

Map mode is transient view state. Nothing enters the save model, so there is no schema
version bump and no migrator change. A scene reload or save load lands with the map
closed by construction.

## 11. Testing

**EditMode / mcs harness — `MapCameraRules`:**

- `ClampAxis` at, inside, below and above each limit
- `ClampAxis` with `min > max` returns the midpoint
- `NormalizeInput` leaves sub-unit input untouched and scales a diagonal to magnitude 1
- `StepAxis` integrates `input * speed * dt` and clamps the result in one call

**Manual scene checklist** (the camera and gating are scene-wired):

- M opens and closes; M closes from every state the map can be in
- Panning clamps at all four edges: the outermost hex on each side can be brought to
  screen centre, and no further
- A degenerate map (a single row or column of terrain) locks that axis to its midpoint
  instead of producing NaN
- Hex tooltips still appear on hover; clicking a hex does nothing
- Clicking a town, dungeon or enemy token does nothing
- Escape closes the map rather than opening the pause menu
- Undo / End Turn hotkeys do nothing while the map is open
- M is ignored during combat, a reward, a town visit, and the run-end screen
- On close the camera returns exactly to local `(0,0,-10)` and orthographic size 4
- Closing mid-transition still lands on those exact values

## 12. Wiring (manual, in the editor)

`MapModeController` goes on the `ExplorationController` GameObject, which already
carries the Grid and all three terrain Tilemaps; the same references are dragged onto
the new component, plus the Main Camera from inside the PlayerPosition prefab.
Step-by-step instructions accompany the implementation plan.
