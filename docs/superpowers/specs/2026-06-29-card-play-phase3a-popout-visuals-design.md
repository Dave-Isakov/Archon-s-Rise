# Card-Play Rework — Phase 3a: Pop-out Float + Crystalline Dark Styling — Design

**Date:** 2026-06-29
**Status:** Approved design, ready for implementation planning
**Parent spec:** [2026-06-29-hand-and-card-play-rework-design.md](2026-06-29-hand-and-card-play-rework-design.md)
**Scope:** The presentation half of the parent design's Phase 3 ("Pop-out visuals + juice"). Phase 3
was split into **3a (this doc) = center float + wrap-panel styling** and **3b (later) = juice**
(echo-flight, stat pop, crystal-spend flourish, commit sweep).

## Goal

Turn the functional-but-plain Phase-1 card menu into the approved **Crystalline Dark** pop-out:
1. The focused card **floats from its fan slot to center screen** with a board dim behind it,
   replacing the current instant scale-to-`enlargeCardPosition` snap.
2. The four section views (**Choice / Improvise / Empower / Play**) are **restyled** to the
   Crystalline Dark direction — glassy panels, neon accents, clear selected/available/locked states.
3. A single **stat-accent palette** is introduced so 3a's segments and 3b's echo-flight share one
   source of truth for color.

This is presentation only. It does **not** touch `CardPlaySelection`, `CardInspector`'s routing to
the existing `CardEvent`/`PlayCommand`/undo path, the crystal consume/restore path, the fan layout
math, or any `Player` stat method.

## Locked decisions (from brainstorming + visual companion)

- **Visual direction:** Crystalline Dark — dark glassmorphic panels (`rgba` fill + 1px accent
  border + soft outer glow), neon accents, blurred backdrop feel.
- **Layout (already locked in the parent spec):** card centered; **Choice banner top** (choice
  cards only); **Improvise panel left**; **Empower panel right**; **Play bar bottom** with **Back**.
- **Stat-accent palette (locked):**
  - Attack = **red** · Defend = **purple** · Influence = **yellow** · Explore = **green**
  - Empower = **cyan** (its own "currency" accent, deliberately distinct from the four stats)
  - These align with the existing crystal/empower color language (red/yellow/green/purple).
- **Selected vs available vs locked** must read by **color + glow**, not by greying out the chosen
  button. (Today selection is shown via `interactable = false`, which is wrong for this direction.)
- **Center-float feel:** on open, the card lifts out of its fan slot and arcs to center while scaling
  up (~**0.25s**, ease-out-back); the board behind dims to ~**40%**; the panels fade/slide in from
  their edges. Back/Cancel/play-commit reverses it back into the fan slot.
- **Play bar** keeps its live label (`PLAY · +N Stat`) and uses an accent-blended gradient.
- **Empower panel** shows the **cyan reserved-crystal indicator** + the **`+2 → +4` preview**.
- **Improvise panel** shows a one-line **"Locked while empowered"** reason in its locked state
  (no stored state is destroyed — that rule is already enforced in Phase-1 logic).

## Approach (chosen: animate in place, minimal new surface)

Consistent with the parent spec's "incremental layer-swap, minimize regression surface" ethos right
before milestones M2/M3:

- **Keep the existing maximize/minimize event wiring and the open/close control flow.** Upgrade the
  card movement from an instant snap to a DOTween animation **in the existing methods**, and add a
  scrim + panel fade driven by `CardInspector.Open/Close`. Do **not** reroute the `CardEvent` graph
  through a brand-new controller.
- **Rejected alternative:** a fresh `CardPopout` controller that owns Show/Hide and replaces the
  `onOpenCardMenu_MaximizeCard`/`onCloseCardMenu_MinimizeCard` events. Cleaner boundaries, but it
  rewires the click-to-open, click-to-close, and `MinimizeAfterPlay` paths — more regression surface
  for no functional gain this phase.
- **Rejected alternative:** a `StatPalette` ScriptableObject asset. The colors are locked and shared
  by only a handful of MonoBehaviours; a plain static class is zero-wiring and equally reusable by 3b.

## Architecture

### StatPalette (new, plain static C# class)

The single source of truth for stat-accent colors. Pure data; no scene dependency.

```
static class StatPalette
    Color For(StatType stat)   // Attack→red, Defend→purple, Influence→yellow, Explore→green
    Color Empower              // cyan
    Color Muted                // unselected-segment base
    Color Locked               // dimmed/locked tint
```

- Consumed by `StatSegment` and `PlayBar` now; by the Phase-3b echo-flight later.
- Hex values are the locked palette; editing the file is the way to retune (no editor wiring).
- Lives in Assembly-CSharp alongside the section views (only MonoBehaviours read it). May use
  `UnityEngine.Color`.

### Center float + board dim (upgraded existing path)

- `Card.SetCardObjectToMax` / `SetCardObjectToNormal` change from an instant
  `SetParent` + `localScale = (4,4,0)` snap to a **DOTween** move + scale (arc to
  `enlargeCardPosition`, ~0.25s, ease-out-back; reverse on minimize). The reparent-to-hand +
  `PlayerHand.Relayout()` behavior on minimize is **preserved** exactly (it returns the card to the
  fan). `MinimizeAfterPlay` continues to route through the same minimize path.
- A **board scrim** — a full-screen `Image` + `CanvasGroup` child of the card-menu canvas, ordered
  behind the pop-out card/panels but above the board — fades to ~40% on `CardInspector.Open` and
  back out on `Close`. Driven from the inspector's open/close (it already flips the menu canvas
  sorting via `CardMenuCanvas.OnCanvas/OffCanvas`).
- The four section panels fade/slide in from their edges on open and out on close (DOTween on each
  panel's `CanvasGroup`/anchored position), gated so a rapid re-open doesn't stack tweens.

### StatSegment (new, small reusable MonoBehaviour)

One per Choice/Improvise option button. Renders the three visual states from `StatPalette`:

```
enum SegmentState { Selected, Available, Locked }

class StatSegment
    StatType stat
    void SetState(SegmentState state)   // fill + glow + text + interactable, from StatPalette
```

- `ChoiceBanner.Render` and `ImprovisePanel.Render` call `segment.SetState(...)` in place of the
  current `b.interactable = !(…)` toggle. The chosen segment → `Selected` (accent fill + glow); other
  available stats → `Available` (muted); a segment in a locked panel → `Locked`.
- Centralizes the "how a segment looks" logic so both panels stay consistent and 3b can reuse the
  accent lookups.

### EmpowerPanel / PlayBar (restyle + tiny render additions)

- **EmpowerPanel:** cyan panel chrome; a reserved-crystal indicator (drives off the existing
  `Crystal.SetReserved` reservation already wired in Phase 1); a `+2 → +4` preview line computed from
  the selection's base vs empowered resolve. Still toggles via the existing `inspector.SetEmpowered`.
- **PlayBar:** accent-blended gradient background; keeps the existing live label
  (`sel.IsPlayable() ? "PLAY · " + sel.Describe() : "Cannot play"`) and the Play/Back wiring
  unchanged.

### Editor work (prefab + scene)

Most of the visual change is data, not code:
- `CardMenu` prefab: glassy panel backgrounds (`rgba` fill + accent border + glow), the wrap-around
  anchoring around `enlargeCardPosition`, the new board-scrim object, the `StatSegment` component on
  each option button, TMP styling for labels/preview/reason text.
- `GameBoard.unity`: wire the scrim's `CanvasGroup` to the inspector; assign any new serialized refs.

## Out of scope / non-goals

- **No juice (Phase 3b):** no echo-flight to the stat, no stat pop/count-up, no crystal-spend
  flourish, no commit-to-discard sweep. `StatsDisplay` is **not** changed in 3a.
- No gamepad / Input System (Phase 4).
- No change to selection logic, command/undo, crystal consume/restore, fan math, or `Player` stats.
- No new card content or balance.

## Testing

- `StatPalette` is trivial pure data; an optional EditMode sanity test (each `StatType` maps to a
  distinct color) is the only automatable check.
- The float animation, scrim dim, panel fades, and the three segment states are verified **manually
  in Play mode** (consistent with Phase 2): open a normal card, a choice card, and an empowerable
  card; toggle Empower to confirm Improvise shows the locked state and the `+2 → +4` preview; Back
  reverses cleanly; Play commits and the card returns to the fan marked played.

## Dependencies & risks

- **DOTween** already vendored at `Assets/Plugins/Demigiant` — no new dependency.
- **Regression risk** concentrates in the maximize/minimize path (shared by click-to-open,
  click-to-close, and `MinimizeAfterPlay`). Mitigated by keeping the event wiring and only swapping
  the snap for a tween; verify all three entry/exit paths in Play mode.
- **Tween stacking** on rapid open/close/Back — kill in-flight tweens on the card/panels/scrim before
  starting a new one.
- The dim is an alpha-based scrim (a practical stand-in for "brightness"), matching the Phase-2
  caveat; a true tint can come later if needed.
