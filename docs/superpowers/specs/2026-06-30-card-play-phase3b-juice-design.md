# Card-Play Rework — Phase 3b: Play-Commit Juice — Design

**Date:** 2026-06-30
**Status:** Approved design, ready for implementation planning
**Parent spec:** [2026-06-29-hand-and-card-play-rework-design.md](2026-06-29-hand-and-card-play-rework-design.md)
**Sibling:** [2026-06-29-card-play-phase3a-popout-visuals-design.md](2026-06-29-card-play-phase3a-popout-visuals-design.md)
**Scope:** The juice half of the parent design's Phase 3. Phase 3a delivered the center float +
Crystalline Dark panel styling; 3b adds the **play-commit feedback** that makes a play feel like it
*landed*.

## Goal

Make playing a card feel impactful without touching any game logic:

1. A colour-coded **floating "+N"** flies from the played card to the matching stat in `StatsDisplay`.
2. `StatsDisplay` **pops and counts** the changed stat from its old value to its new one (a
   punch-scale + colour flash), and does the same **in reverse (count-down) on undo**.
3. When an **empowered** card is played, the reserved crystal performs a **spend flourish** (drains
   toward the played card) just before the "+N" flies; **undo reverses it** (the crystal returns).

This is presentation only. It does **not** touch `CardPlaySelection`, `CardInspector`'s routing to
`CardEvent`/`PlayCommand`/undo, `PlayCommand`/`PlayManager`, the crystal consume/restore path
(`EmpowerCrystal`/`RegenCrystal`), the fan layout, or any `Player` stat method. It adds visual hooks
*alongside* the existing logic.

## Locked decisions (from brainstorming)

- **Echo fidelity = floating "+N" only.** No card-ghost/clone. A colour-coded number arcs from the
  played card to the matching stat, then fades. (A full card-ghost was rejected as far more build/tune
  cost for little extra feel.)
- **One label per boosted stat.** A multi-stat Normal card (e.g. `cardType` with Attack|Defend)
  emits one "+N" per non-zero action stat, each in that stat's accent colour, each flying to its own
  stat target. Single-stat cards emit one.
- **Undo feedback = stat count-down only.** No reverse "-N" flight. The stat-pop observer animates
  the number *down* on undo for free (it only watches the value), which satisfies the undo feedback
  with zero command hooks. The crystal flourish still reverses (it's hooked on the regen event).
- **Layers in this pass:** (1) floating "+N" echo, (2) stat pop/count-up, (3) crystal-spend flourish.
- **Deferred to a later pass:** the **commit sweep** (played cards sliding together to the discard
  pile on commit) is explicitly *out* of 3b. Today `DiscardPile.AddCardToDiscard` deactivates the
  card instantly; that behavior is unchanged here.
- **Stat-accent colour** comes from the existing `StatPalette` (3a) — single source of truth shared
  by the echo labels and the stat flash.

## Approach (chosen: presentation observers + a thin echo emitter)

Consistent with 3a's "presentation only, minimize regression surface" ethos and the parent spec's
frozen command/stat core:

- **`StatsDisplay` becomes a self-observing animator.** It stops rewriting all four texts every
  frame and instead caches each stat's last value, detects a change, and animates old→new. Because it
  only *watches* `Player`'s value, it animates **up on play and down on undo automatically** — no
  command hook, no awareness of *why* the value changed. This is what makes "count-down only on undo"
  free.
- **A thin `StatEchoes` emitter** spawns the floating "+N" labels. `CardInspector.Play()` drives it
  from the current selection (play-only, matching the undo decision). It does not route through the
  command layer.
- **The crystal flourish hangs off the existing consume/regen events** (`EmpowerCrystal` /
  `RegenCrystal`), not new commands.

### Rejected alternatives

- **Command-driven feedback** (`PlayCommand.Execute/Undo` raise new feedback events for a symmetric
  play/undo echo). Rejected: undo is count-down only, so symmetry buys nothing, and it touches the
  command layer the whole rework has deliberately kept frozen.
- **A central `PlayFeedbackController`** that subscribes to everything and orchestrates all three
  layers. Rejected: `StatsDisplay` self-observation is simpler and more robust than routing every
  stat delta through a controller; centralizing widens the blast radius for no gain.

## Architecture

### StatsDisplay (rework in place) — Layer 2: stat pop / count-up

```
class StatsDisplay
    // per stat: cached last value + the TMP label + an anchor transform
    Transform AnchorFor(StatType stat)   // where StatEchoes should fly a "+N" to
    // Update(): for each stat, if Player value != cached -> animate(old, new)
    //   - count the number old->new (DOTween value tween or virtual lerp)
    //   - punch-scale the label, flash it to StatPalette.For(stat), settle to default
    //   - cache the new value
```

- Same `Player` read it does today; no external dependency added beyond `StatPalette`.
- Up and down share one animate path (count direction follows old vs new), so undo is free.
- Exposes a per-stat **anchor** `Transform` (the stat number's UI position) for the echo target.
- Rapid changes (spam play/undo) must kill the in-flight tween for that stat before starting a new
  one and snap the cached value, so the number can't desync from `Player`.

### StatEchoes (new) — Layer 1: floating "+N" echo (play-only)

```
class StatEchoes : MonoBehaviour
    [SerializeField] StatsDisplay stats;     // for AnchorFor(stat)
    [SerializeField] GameObject labelPrefab; // a TMP "+N" under the menu/overlay canvas
    void Emit(Vector3 originWorld, StatType stat, int amount)
        // spawn label at origin, colour = StatPalette.For(stat), text = "+amount",
        // arc/move to stats.AnchorFor(stat) over ~0.4s, fade out, destroy.
```

- `CardInspector.Play()` (after pushing the `PlayCommand`, before/as it calls `Close()`) computes
  `Selection.PreviewStats(Selection.EffectiveEmpowered())` and, for each non-zero `[atk,def,inf,exp]`
  entry, calls `echoes.Emit(cardCenter, stat, amount)`. `cardCenter` is the focused card's current
  world position (it's at `enlargeCardPosition` the instant Play is pressed).
- Improvise resolves to a single stat (+1), so it emits one label; Choice emits one; Normal emits one
  per set action flag.
- Play-only by design; undo relies on Layer 2's count-down.

### Crystal-spend flourish (Crystal / CrystalInventory) — Layer 3

- **On play (consume):** the existing `onEmpower_DestroyCrystalGameObject → CrystalInventory.EmpowerCrystal()`
  removes the matching crystal. Add a flourish so the crystal **drains toward the played card** (a
  short move + scale-down + fade) immediately before removal, timed just ahead of the "+N" echo.
- **On undo (regen):** the existing `onUndo_RegenerateCrystalGameObject → CrystalInventory.RegenCrystal()`
  restores the crystal. The flourish reverses — the crystal fades/scales back in at its inventory
  slot.
- Reuses the reserved-crystal visual path introduced in 3a (`Crystal.SetReserved`); the real
  consume/restore (`RemoveCrystal`/`SetActive`) is untouched — the flourish only animates around it.
- The "toward the card" target is the played card's centre (same position the echo flies *from*).

### StatPalette (existing, unchanged)

The 3a `StatPalette.For(StatType)` is the single source of truth for the echo label colour and the
stat flash colour. No change.

## Data flow (one empowered single-stat play)

1. Player presses **PLAY** → `CardInspector.Play()`:
   - pushes `PlayCommand` (unchanged) → `Player.*` mutates the stat and sets `IsPlayed`;
     `EmpowerCrystalCheck` raises the consume event.
   - **Crystal flourish** plays (crystal drains toward the card centre), then the crystal is removed.
   - **`StatEchoes.Emit`** fires a "+N" from the card centre to the stat anchor.
   - `Close()` tweens the card back to its fan slot (3a behavior, unchanged).
2. `StatsDisplay.Update` sees the stat value changed → **counts up + pops + flashes**.
3. Player presses **Undo** → `PlayManager.UndoCommand` → `Player.*` reverses the stat;
   `UndoEmpower` raises the regen event.
   - **Crystal flourish reverses** (crystal returns to inventory).
   - `StatsDisplay.Update` sees the value drop → **counts down + pops** (no "-N" flight).

## Out of scope / non-goals

- **Commit sweep** (played cards sliding together to discard on commit) — deferred to a later pass.
  `DiscardPile.AddCardToDiscard` keeps its instant-deactivate behavior.
- Reverse "-N" undo flight; card-ghost/clone echo.
- Gamepad / Input System (Phase 4).
- No change to selection logic, `PlayCommand`/`PlayManager`, the crystal consume/restore path, fan
  math, or any `Player` stat method. No new card content or balance.

## Testing

- Consistent with 3a: the motion (echo arc, stat pop, crystal flourish) is verified **manually in
  Play mode** —
  1. Single-stat card: "+N" flies to the right stat in its colour; the stat counts up + pops.
  2. Multi-stat / choice card: one coloured "+N" per boosted stat to the correct anchors.
  3. Empowered card: crystal drains toward the card, then "+N" reflects the empowered value.
  4. Undo each: the stat counts **down** + pops; the crystal returns; no "-N" flight; console clean.
  5. Spam play/undo: numbers never desync from `Player` (per-stat tween kill + value snap).
- Any pure helper worth isolating (e.g. the "which `[atk,def,inf,exp]` entries are non-zero" delta
  feeding the echo, or a count-interpolation helper) gets a small EditMode test in the existing
  `ArchonsRise.Tests.EditMode` assembly; the visuals themselves are eyeballed.

## Dependencies & risks

- **DOTween** already vendored at `Assets/Plugins/Demigiant` — no new dependency.
- **Regression risk** is low: `StatsDisplay`'s value source is unchanged (still reads `Player`); the
  rework only changes *how* it renders. The echo and flourish are additive overlays.
- **Tween desync** on rapid play/undo — mitigated by killing the per-stat tween and snapping the
  cached value before starting a new one.
- The crystal flourish must fire *around* the existing remove/regen, not replace it — keep the real
  consume/restore call exactly where it is.
