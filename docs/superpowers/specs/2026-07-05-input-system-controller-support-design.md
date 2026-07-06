# Input System Migration & Controller Support — Design (Hand & Card-Play Rework, Phase 4)

**Date:** 2026-07-05
**Status:** Draft, awaiting user review
**Parent spec:** `2026-06-29-hand-and-card-play-rework-design.md` (Phase 4)
**Scope:** Replace Unity's legacy input with the new Input System (exclusive) and make the
hand fan + card pop-out fully playable on a gamepad, including Undo, End Turn/Round, and the
pause menu. Board navigation (map tokens, dungeons, towns, town menus, rewards) is a later,
separate effort.

## Goal

At the end of this phase a player with only a gamepad (or only a keyboard) can play a full
turn: move focus through the fan, open the inspector, pick Choice/Improvise/Empower, play the
card, undo it, end the turn, and open the menu — while a mouse player notices no change at all.

## Decisions locked during brainstorming

- **Scope:** foundation + hand/inspector. Board navigation deferred.
- **Migration:** **full exclusive** — Active Input Handling = Input System (Approach C).
  Missed legacy call sites throw at runtime, failing loudly instead of silently.
- **Architecture:** custom focus/nav controllers reading input actions directly; uGUI's
  EventSystem is used only for pointer (mouse) input via `InputSystemUIInputModule`. Gamepad
  never goes through the EventSystem's selected-object machinery — focus has one owner in our
  code, following the `PreviewTrigger` Focus/Unfocus pattern.
- **Turn flow on pad:** dedicated bindings for Undo and End Turn/Round (no navigation UI for
  those buttons this phase).
- **No button-prompt glyphs** this phase; the wrap-around pop-out layout telegraphs the
  directions and selection highlights show position. Prompts come with the later board pass.
- **Test hardware:** Xbox pad (primary), DualSense and Switch Pro (mapping sanity checks).

## Current state (inputs to this design)

- Unity 6000.5.1f1; `activeInputHandler: 0` (legacy only); no `com.unity.inputsystem` package.
- Live legacy-`Input` call sites (the only ones):
  - `DataManager.Update()` — `Input.GetKeyDown(KeyCode.Escape)` toggles the main menu.
  - `HandFanLayout.LateUpdate()` — `Input.mousePosition` drives the fan focus hit-test.
  - Commented-out blocks in `PlayerPosition` and `Card` — dead code, delete.
- ~20 `IPointerClickHandler` / `IPointerEnter/ExitHandler` scripts (board tokens, decks,
  crystals, dungeons, buttons, `PreviewTrigger`) — these go through the EventSystem input
  module, not `UnityEngine.Input`, so they survive the exclusive flip once the module is swapped.
- Phases 1–3 delivered `CardPlaySelection` (pure), `CardInspector` (owns the selection, exposes
  `SetMode/ChooseStat/ImproviseStat/SetEmpowered/Play/Close`), section views driven by
  `inspector.Changed`, `HandFanLayout` (fan geometry + mouse focus poll), and play juice.
- An EditMode test assembly exists (`Assets/Tests/EditMode`), plus the mcs-based CLI harness
  for pure classes when the editor is holding the compile lock.

## Part 1 — Foundation: package, exclusive flip, EventSystem swap

1. Add `com.unity.inputsystem` to `Packages/manifest.json` (verified version for Unity 6.5).
2. Project Settings → Player → **Active Input Handling = Input System Package**. Requires an
   editor restart. From this point any surviving `UnityEngine.Input` call throws
   `InvalidOperationException` at runtime — intentional loud failure.
3. Scene `EventSystem`: replace `StandaloneInputModule` with `InputSystemUIInputModule`
   (manual editor step), assigned the **UI map** of the new actions asset. Default
   Point/Click behavior keeps every existing `IPointerClickHandler` working unchanged.
4. Migrate the two live legacy call sites:
   - `DataManager` Escape → the `Menu` action (Escape + gamepad Start). Pause/menu comes to
     the controller for free.
   - `HandFanLayout` mouse poll → moves into the new `HandFocusController` reading
     `Mouse.current.position` (Part 3).
5. Delete the commented legacy blocks in `PlayerPosition` and `Card`.

**Verification (this layer alone):** full mouse-only turn behaves exactly as today; Escape
still opens the menu; clicking every board object type still works.

## Part 2 — Actions asset and device coexistence

One asset, `Assets/Input/Controls.inputactions`, with two maps:

| Map | Actions | Consumer |
|---|---|---|
| **UI** | Point, Click, ScrollWheel, Navigate, Submit, Cancel | `InputSystemUIInputModule` (mouse compatibility) |
| **CardPlay** | Navigate, Submit, Cancel, Undo, EndTurn, Menu | `HandFocusController`, `InspectorNavController`, `DataManager` |

Gamepad bindings (one set — Xbox, DualSense, and Switch Pro all normalize to the Input
System's `Gamepad` layout; Switch mapping is positional, so "south" is always the bottom
button regardless of the printed letter):

| Control | Action |
|---|---|
| D-pad / left stick | Navigate — fan focus movement and pop-out section navigation |
| South (A / Cross) | Submit — open inspector, select segment, press Play |
| East (B / Circle) | Cancel — close the pop-out back to the fan |
| West (X / Square) | Undo |
| North (Y / Triangle) | End Turn / End Round (whichever button is currently active, respecting its `interactable` gate) |
| Start / Options | Menu (same as Escape) |

Keyboard bindings on the same actions: arrows = navigate, Enter = Submit,
Backspace = Cancel, Escape = Menu. Keyboard-only card play becomes possible as a side effect.

**Device coexistence — last input wins.** The mouse claims hand focus only when the pointer
*moves* (delta ≠ 0 since last frame); gamepad/keyboard claims it on a navigate press. This
prevents the per-frame mouse hit-test from stomping a d-pad focus change. No cursor hiding,
no device-switch UI — only this arbitration rule.

**Undo/EndTurn gating.** The pad shortcuts are live on the fan and **suppressed while the
pop-out is open** (mid-selection undo would yank state out from under the inspector; Cancel
out first). Mouse users can click the on-screen buttons anytime, exactly as today. The
shortcuts invoke the same handlers the on-screen buttons use, so all existing validation and
`interactable` gating applies.

**Menu gating.** While the pop-out is open, Escape acts as Cancel (closes the pop-out) rather
than opening the main menu; Menu (Escape / Start) opens the menu only from the fan/board.
This replaces today's behavior where Escape would open the main menu over the card menu.

## Part 3 — Hand fan navigation

**`HandFanLayout` refactor — geometry only.** It exposes `SetFocus(Card)` / `ClearFocus()`
and keeps the slot/lift/dim/relayout job. The mouse hit-test moves out of `LateUpdate` into
the focus controller. The existing "inspector open → clear focus" guard stays.

**`HandFocusController` (new MonoBehaviour) — the single writer of focus.**
- **Mouse path:** the same slot-based hit-test as today (front-to-back, slot position not
  lifted position), reading `Mouse.current.position`, evaluated only when the pointer moved.
- **Pad/keyboard path:** Navigate left/right steps focus through playable cards —
  skipping wounds (same rule the mouse applies) and **wrapping** at the ends. First navigate
  press with nothing focused focuses the middle card.
- **Submit** opens the inspector for the focused card through the same path a mouse click
  takes.
- On relayout (draw/discard/heal/play), focus clamps to the nearest surviving card instead of
  vanishing.

**`HandNavRules` (new, pure).** Index stepping with wound-skip + wrap, middle-card pick, and
clamp-to-nearest-on-removal. EditMode-tested like `PreviewRules`.

## Part 4 — Inspector navigation

**`InspectorNavController` (new MonoBehaviour).** Active only while the pop-out is open;
reads the same CardPlay actions and drives the **existing** inspector API (`ChooseStat`,
`ImproviseStat`, `SetEmpowered`, `Play`, `Close`). No new state — the section views keep
rendering from `CardPlaySelection` via `inspector.Changed`, so mouse and pad stay in sync by
construction.

- **Directional section jumps matching the physical layout:** Up = Choice banner (choice
  cards only), Left = Improvise panel, Right = Empower panel, Down = Play bar. The layout is
  its own tutorial.
- Within a section, navigate cycles the options; **Submit** activates the highlighted option
  (choice/improvise stat, Empower toggle, Play, Back); **Cancel** calls `Close()`.
- Hidden or locked sections are skipped (non-choice card → no Choice stop; Improvise active →
  Choice and Empower unreachable). You can never land on a locked segment.
- **`InspectorNavRules` (new, pure):** given the card shape (isChoice, empowerable, flags)
  and current mode, which sections exist and how focus cycles. EditMode-tested, including
  "Improvise active → Choice/Empower unreachable".

**Focus visuals.** Segments gain a *focused* state distinct from *selected* (outline /
brightness on `StatSegment`, plus equivalents for the Empower control and Play/Back). Mouse
hover reuses the same visual so both devices feel like one system.

## Testing & verification

- **EditMode:** `HandNavRules` and `InspectorNavRules` suites alongside the existing tests
  (mcs CLI harness fallback if the editor holds the compile lock).
- **Manual Play-mode checklist:**
  1. Mouse-only full turn — identical to today (regression for the exclusive flip).
  2. Gamepad-only full turn on Xbox: fan navigation → inspect → Choice/Improvise/Empower →
     Play → Undo → End Turn → Menu.
  3. Mapping sanity pass on DualSense and Switch Pro.
  4. Click every board object type (tokens, decks, crystals, dungeons, town) under the new
     input module; hover previews (`PreviewTrigger`) still fire.
- **Scene wiring** (EventSystem swap, new controller components, action-asset references) is
  done by the user in the editor from step-by-step instructions; no hand-edited scene YAML.

## Out of scope / non-goals

- Board navigation on controller (map movement, token/dungeon/town focus, town menus,
  rewards, crystal inventory browsing) — later effort; `PreviewTrigger.Focus()/Unfocus()` is
  already shaped for it.
- Button-prompt glyphs / last-device HUD hints.
- Input rebinding UI, cursor hiding, rumble.
- Any change to `CardPlaySelection`, the command/undo system, stat math, or play juice.

## Dependencies & risks

- **Exclusive flip risk:** any missed `UnityEngine.Input` call throws at runtime. Mitigation:
  the grep found exactly two live call sites, both migrated in Part 1, and the first
  mouse-only regression pass exercises the whole game surface.
- **Editor restart** required by the Active Input Handling change (user-driven step).
- **Third-party code:** DOTween does not read `UnityEngine.Input`; no other plugins in use.
- **`cardListCanvas` (“view whole hand”) and town/reward canvases** remain mouse-only this
  phase by design; they must still work with the mouse after the module swap (covered by the
  regression checklist).
