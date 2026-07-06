# Input System Migration & Controller Support — Design (Hand & Card-Play Rework, Phase 4)

**Date:** 2026-07-05
**Status:** Draft, awaiting user review
**Parent spec:** `2026-06-29-hand-and-card-play-rework-design.md` (Phase 4)
**Scope:** Replace Unity's legacy input with the new Input System (exclusive) and make the
hand fan + card pop-out fully playable on a gamepad, including Undo, End Turn/Round, and the
pause menu. The hand is only one of the player's control surfaces — the hex-grid map (tokens,
dungeons, movement), town menus, and combat/reward card selection are later phases, but the
foundation built here (actions asset, input contexts, focus-controller pattern) is designed
to be the same one those phases plug into.

## Goal

At the end of this phase a player with only a gamepad (or only a keyboard) can play a full
turn: move focus through the fan, open the inspector, pick Choice/Improvise/Empower, play the
card, undo it, end the turn, and open the menu — while a mouse player notices no change at all.

## Decisions locked during brainstorming

- **Scope:** foundation + hand/inspector. Map/board, town, and combat-card navigation are
  deferred, but nothing in this phase may assume the hand is the only controllable surface —
  the actions asset and context model are shared infrastructure for those later phases.
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
| **Gameplay** | Navigate, Submit, Cancel, Undo, EndTurn, Menu, Empower, SectionChoice, SectionImprovise | The context controllers (`HandFocusController`, `InspectorNavController` now; board/town/combat controllers later) and `DataManager` |

The Gameplay map is deliberately **context-agnostic**: the actions are semantic
(Navigate/Submit/Cancel), and what they *mean* is decided by whichever input context is
active (see "Input contexts" below). Later phases add controllers, not action maps — hex-grid
navigation, town menu selection, and combat-card picking all read these same actions. The
pop-out adds three inspector-specific actions (Empower, SectionChoice, SectionImprovise) to
the same map, consumed only by `InspectorNavController`.

Gamepad bindings (one set — Xbox, DualSense, and Switch Pro all normalize to the Input
System's `Gamepad` layout; Switch mapping is positional, so "south" is always the bottom
button regardless of the printed letter):

| Control | Action |
|---|---|
| D-pad / left stick | Navigate — fan focus movement and within-section option cycling in the pop-out |
| South (A / Cross) | Submit — open inspector, select segment, press Play |
| East (B / Circle) | Cancel — close the pop-out back to the fan |
| West (X / Square) | Undo (board/fan) / **toggle Empower** (inside the pop-out; Undo is context-suppressed there so both actions on the same button never collide) |
| North (Y / Triangle) | End Turn / End Round (whichever button is currently active, respecting its `interactable` gate) |
| L1 (LB / Left bumper) | Pop-out: enter **Improvise** section |
| R1 (RB / Right bumper) | Pop-out: enter **Choice** section |
| Start / Options | Menu (same as Escape) |

Keyboard bindings on the same actions: arrows = navigate, Enter = Submit, Backspace = Cancel,
Escape = Menu, `Space` = Empower, `Q` = Improvise section, `E` = Choice section. Keyboard-only
card play becomes possible as a side effect.

**Device coexistence — last input wins.** The mouse claims hand focus only when the pointer
*moves* (delta ≠ 0 since last frame); gamepad/keyboard claims it on a navigate press. This
prevents the per-frame mouse hit-test from stomping a d-pad focus change. No cursor hiding,
no device-switch UI — only this arbitration rule.

**Input contexts — who owns Navigate right now.** A minimal `InputContext` model (a small
static class or enum-valued property, not a framework): exactly one context is active at a
time, and only that context's controller consumes Navigate/Submit/Cancel. This phase defines
three:

- **Board** — the default. No gamepad navigation yet (mouse-only this phase); this is the
  slot where the future hex-grid/map controller, town-menu controller, and combat-card
  controller will live. Undo/EndTurn/Menu shortcuts are live here.
- **Fan** — entered when gamepad/keyboard navigation targets the hand (see coexistence rule);
  `HandFocusController` consumes Navigate/Submit. Undo/EndTurn/Menu remain live.
- **Inspector** — entered by `CardInspector.Open()`, exited by `Close()`;
  `InspectorNavController` consumes everything. Undo/EndTurn are **suppressed**
  (mid-selection undo would yank state out from under the inspector; Cancel out first).

The validation-message popup is a **modal overlay**, not a context: `MessageController` owns
input while `messageCanvas.enabled` (Submit or Cancel → `ReturnButton()`), and every gameplay
controller early-returns while it is up (swallowing the frame it closes on so the dismiss press
does not double-act). It is gated by the canvas flag like `mainMenuCanvas`/`cardListCanvas`
rather than by saving/restoring the underlying context.

Making the context explicit now — instead of scattered `if (cardCanvas.enabled)` checks —
is the seam the later map/town/combat phases extend by adding a context, not by rewiring
this one. Mouse users can click any on-screen button anytime regardless of context, exactly
as today; the shortcuts invoke the same handlers the on-screen buttons use, so all existing
validation and `interactable` gating applies.

## Part 3 — Hand fan navigation

**`HandFanLayout` refactor — geometry only.** It exposes `SetFocus(Card)` / `ClearFocus()`
and keeps the slot/lift/dim/relayout job. The mouse hit-test moves out of `LateUpdate` into
the focus controller. The existing "inspector open → clear focus" guard stays.

**`HandFocusController` (new MonoBehaviour) — the single writer of focus.**
- **Mouse path:** the same slot-based hit-test as today (front-to-back, slot position not
  lifted position), reading `Mouse.current.position`, evaluated only when the pointer moved.
- **Pad/keyboard path:** the first Navigate press from the Board context enters the Fan
  context and focuses the middle card. Left/right then steps focus through playable cards —
  skipping wounds (same rule the mouse applies) and **wrapping** at the ends.
- **Submit** opens the inspector for the focused card through the same path a mouse click
  takes. **Cancel** clears the hand focus and drops back to the Board context.
- On relayout (draw/discard/heal/play), focus clamps to the nearest surviving card instead of
  vanishing.

**`HandNavRules` (new, pure).** Index stepping with wound-skip + wrap, middle-card pick, and
clamp-to-nearest-on-removal. EditMode-tested like `PreviewRules`.

## Part 4 — Inspector navigation

**`InspectorNavController` (new MonoBehaviour).** Active only while the pop-out is open;
reads the same Gameplay actions and drives the **existing** inspector API (`ChooseStat`,
`ImproviseStat`, `SetEmpowered`, `Play`, `Close`). No new state — the section views keep
rendering from `CardPlaySelection` via `inspector.Changed`, so mouse and pad stay in sync by
construction.

**Dedicated buttons over free directional navigation.** Free "focus through every option"
felt unnatural, so section entry and Empower move to dedicated buttons; the moving outline is
kept but only travels within one section at a time.

- **Section entry is by shoulder button, not direction:** **R1** (rightShoulder / kbd `E`) →
  Choice; **L1** (leftShoulder / kbd `Q`) → Improvise. Unreachable target → focus stays put.
- **Empower is a global button, not a section:** **X** (`buttonWest` / kbd `Space`) toggles
  the reservation from anywhere it is currently allowed (`CanEmpower()`), never moving focus.
  It is no longer a directional stop (the `Empower` enum value is retained, but the rules
  never produce it, so wired scene references stay valid).
- **Direction cycles options *within* the focused section only:** Choice (horizontal) —
  Left/Right cycle segments (wrap), Down → Play; Improvise (vertical) — Up/Down through the
  four stats, Down past the last → Play; Play — directions do nothing (leave via L1/R1).
- **Submit** activates the focused option (choice/improvise stat) or Play; **Cancel** calls
  `Close()`. Back is no longer a pad focus target and is **hidden while the controller is the
  active device** (Cancel covers it), reappearing on the next mouse/keyboard use — last-input-wins.
- **Auto-default to Play:** each frame, if the focused section is no longer reachable — e.g.
  Improvise focused then X empowers (locking Improvise), or Choice locks while Improvise is
  active — focus snaps to Play. Generalizes "improvise unavailable → default to Play."
- **`InspectorNavRules` (new, pure):** the reduced graph above plus `EnterChoice` /
  `EnterImprovise` section-entry helpers and `ClampReachable`. EditMode-tested, including the
  auto-default and "unreachable section entry stays put" cases.

**Focus visuals.** A single moving outline marks the focused Choice/Improvise segment or the
Play button (Empower and Back are never focus targets). Mouse hover over a focusable element
relays into the same outline so both devices feel like one system.

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

## Looking ahead — the rest of the control surface (design constraint, not deliverables)

The player's other control surfaces arrive in later phases, but this phase's foundation must
not paint them into a corner. Known future contexts and what they already have waiting:

- **Hex-grid map:** focusing and activating map hexes, enemy/town tokens, and dungeons, plus
  moving the player. Whether that's snap-to-object navigation or a free virtual cursor is a
  later design decision — both consume the same Vector2 `Navigate` action, so nothing here
  pre-commits. `PreviewTrigger.Focus()/Unfocus()` was already built input-agnostic so a
  controller focus can drive the enemy preview exactly like mouse hover does.
- **Town menus:** recruit/crystal/reward buttons — conventional button grids; a town context
  controller reading Navigate/Submit/Cancel.
- **Combat and reward card rows:** picking a combat card or reward — likely a simplified
  cousin of the fan's focus-stepping, reusing `HandNavRules`-style pure logic.

Each becomes a new `InputContext` plus a controller reading the existing Gameplay map. If a
later phase finds it needs a *new action* (e.g. a map-zoom axis), it adds it to the Gameplay
map without touching the hand/inspector controllers.

## Out of scope / non-goals

- Board navigation on controller (map movement, hex/token/dungeon/town focus, town menus,
  rewards, crystal inventory browsing) — later phases, anticipated above but not built here.
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
