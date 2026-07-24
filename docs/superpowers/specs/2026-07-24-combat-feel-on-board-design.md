# Combat Feel — Fighting on the Board — Design

Date: 2026-07-24

## Summary

Combat currently plays out under an opaque canvas that hides the board: enemy cards
stack at a single spawn point and the world-space player avatar is covered up, so the
player never sees themselves fight. This pass makes combat visibly happen **on the
board**. Five presentation-only pieces:

1. **Transparent battlefield** — after the Begin Combat animation, fade the combat
   background so the board and the existing player avatar show through. No second avatar.
2. **Semicircle enemy layout** — enemy cards fan in an arc around the screen-centre
   player, clear of the hand and HUD, replacing the single stacked spawn point.
3. **Unique enemy art** — a `Sprite` field on `EnemiesSO` and an `Image` on the enemy
   card, so each enemy can carry its own portrait. Art authored later (M3 content);
   field + wiring land now.
4. **Idle card sway** — subtle out-of-sync motion on enemy cards for life, stopping on
   defeat so it never fights the dissolve FX.
5. **Enemy token ↔ card morph** — cards emerge from the source token (monster / keep /
   dungeon), fan out to their slots, and either dissolve on defeat or return to the
   board on flee. A field token hides its board icon while its card is up.

**Nothing here gates combat logic.** Explore spend, phase transitions, kill banking,
and reward payout all resolve exactly as they do today; the visuals catch up.

## Non-goals

- No change to combat rules, turn phases, spawning, or rewards.
- **Deeper "combat fully re-hosted on the board"** (removing the combat canvas entirely,
  hand cards rendered as board objects) stays deferred.
- **Per-enemy art authoring** is M3 content work — this pass ships the field and an
  empty-safe fallback, not the art.
- Character select / multi-character content remain deferred to M3 (meta progression).

## Context (current behavior)

- `CombatController.OpenFight` instantiates `EnemyCard` prefabs under
  `GameManager.enemyCardCombatPosition`, each at `localPosition = Vector3.zero`, scaled
  1.75× — they stack (a layout group on the parent currently spreads them).
- `OpenFight` receives the source token for every path: `fieldToken` (`EnemyToken`),
  `guardianPlace` (`TownToken`), or `dungeonToken` (`DungeonToken`).
- `GameManager.PlayCombatIntro()` (field) plays a banner + intro animation; guardian and
  dungeon assault call `CombatCanvasActive()` with no banner. Assault already closes the
  town menu before spawning.
- The combat canvas is **Screen Space – Camera**; the camera rides `PlayerPosition`, so
  the player sits at screen-centre.
- `EnemiesSO : AllCards` has no sprite field. `EnemyCard` shows TMP text only.
- `EnemyCardDefeatFx` owns the defeat dissolve/tween. `EnemyToken` renders a board sprite
  plus a pulsing `glow` child.

## Part 1 — Transparent battlefield (`CombatBackdrop`)

A MonoBehaviour holding a serialized ref to the combat canvas **background Image only** —
not a `CanvasGroup` over the whole canvas. HUD, hand, buttons, and enemy cards stay fully
opaque; only the backdrop fades.

- After the Begin Combat beat, lerp the background alpha opaque → `combatAlpha`
  (inspector, default ~0.35 — a light dark tint so cards keep contrast against the board
  rather than full transparency) over `fadeDuration`.
- Restore to opaque on combat close.
- **Hooks:** tail of `GameManager.PlayCombatIntro()` (field, after the banner) and inside
  `CombatCanvasActive()` (guardian/dungeon, no banner). Restore in `CloseCombatCanvas()`
  and the victory teardown path.
- Cosmetic only. The world-space player avatar simply becomes visible through the tint —
  no duplicate avatar is created.

## Part 2 — Semicircle layout (`CombatLayoutRules` + applier)

### `CombatLayoutRules` (pure)

Unity-free static class, compiled by the mcs harness like `CombatRules` / `DefeatFxMath`.
It must **not reference `UnityEngine`** — it returns a plain struct, not `Vector2`:

```
struct Slot { public float X, Y, Scale, TiltDeg; }
Slot SlotFor(int index, int count, float radius, float arcDegrees);
```

- Fans `count` cards evenly across `arcDegrees`, symmetric around **top-centre**
  (straight up from the screen-centre player). `count == 1` → dead centre-top.
- Returns `Scale`, stepping down past a threshold count so a large roster never overlaps.
- `TiltDeg` gives an optional slight outward tilt along the arc.

Gets its own folder asmdef plus a reference from the tests asmdef, per the project's
asmdef rule. Tested via the mcs harness (symmetry, even spacing, single-card centring,
scale step-down, monotonic X across the arc).

### Applier (`CombatController`)

The applier converts `Slot` to `anchoredPosition` / scale / rotation. In `OpenFight`,
after instantiating each card, set its arc slot from `SlotFor(i, n, radius, arc)` instead
of `localPosition = Vector3.zero`. `enemyCardCombatPosition` becomes the arc origin (its
layout group is removed). `radius` / `arcDegrees` are inspector fields tuned against the
real screen so the arc clears the hand (bottom) and HUD (corners).

On a **mid-fight defeat**, re-lay the remaining live set so gaps close.

## Part 3 — Unique enemy art

- `EnemiesSO.cardArt : Sprite` (nullable) — authored per enemy later (M3).
- Enemy card prefab gains an artwork `Image` child; `EnemyCard` gains
  `[SerializeField] Image artwork;`.
- In `EnemyCard.Start()`: if `enemySO.cardArt != null`, set the sprite and enable the
  Image; otherwise disable it so an unauthored enemy shows the plain card, never a broken
  frame. No layout dependency — one more field set alongside the name/HP/attack text.

## Part 4 — Idle card sway (`EnemyCardIdleSway`)

- MonoBehaviour on the enemy card prefab, driving a **child content pivot** — not the card
  root. The layout applier owns the root's `anchoredPosition`; the sway offsets only the
  visual child, so the two never fight.
- Slow sine: a few px of position drift plus ~1–2° tilt, with a **random per-instance
  phase offset** so cards don't sway in lockstep. Amplitude/period are inspector fields.
- **Stops on defeat:** `EnemyCard` disables the sway when `isDefeated` is set, so it never
  competes with `EnemyCardDefeatFx`.
- Presentation only.

## Part 5 — Enemy token ↔ card morph

### Origin per context

`OpenFight` already receives the source token for every path. That token's board position
is the morph origin, projected into the canvas. Cards spawn there and fan out to their
`CombatLayoutRules` slots — enemies visibly emerge from the monster / keep / dungeon. All
contexts fan out from their single origin (guardian/dungeon cards spill from the place
token; this reads well).

**World → canvas projection:** standard `RectTransformUtility` screen-point conversion
using the combat canvas camera. Because the camera rides `PlayerPosition`, the player is
screen-centre and the arc is symmetric around it.

### Morph animation

A coroutine using the same tween idiom as `PlayerAvatar` / `CombatBackdrop`. Slot targets
come from the pure layout rules; the origin is a runtime value.

- **In:** spawn at the origin's projected canvas point, small and faded, then tween
  position → arc slot, scale → `SlotScale`, alpha → 1. Runs right after the backdrop fade.
- **Out (survivors only):** on flee / assault retreat, tween the surviving cards back to
  the origin and restore the board, then destroy the cards. A **defeated** card keeps its
  existing `EnemyCardDefeatFx` dissolve — no return trip.

### Board visibility

Only a **field `EnemyToken`** hides while its card is up. Add
`EnemyToken.SetBoardVisible(bool)` toggling its sprite and `glow`; call it false at
morph-in, true on non-defeat exit. On defeat the token is destroyed as it already is
(`CombatController.RecordFieldDefeat`). Towns / keeps / dungeons **stay visible** — the
place persists; its guardians merely pour out of it, so no place token is hidden.

## Coordinate spaces & sequencing

Per fight, in order:
1. Intro/banner plays (existing).
2. `CombatBackdrop` fades the background to `combatAlpha`.
3. Cards instantiate at the origin's projected point; morph-in tweens them to arc slots
   (targets from `CombatLayoutRules`); a field token hides its board icon.
4. Combat runs unchanged.
5. Exit: defeated cards dissolve (existing FX); on flee/retreat survivors morph back and
   the field token reappears; `CombatBackdrop` restores opaque.

## Editor work (USER)

Per the standing rule, all scene/prefab/asset authoring is USER editor work from
step-by-step instructions — no hand-edited YAML.

- Add and wire the background Image ref on `CombatBackdrop`; set `combatAlpha` /
  `fadeDuration`.
- Add the artwork `Image` to the enemy card prefab; wire `EnemyCard.artwork`.
- Add `EnemyCardIdleSway` and its content-pivot child to the enemy card prefab.
- Remove the layout group on `enemyCardCombatPosition`.
- Add the `CombatLayoutRules` folder asmdef + tests-asmdef reference.

## Acceptance

- Field fight: intro/banner → background fades to the tint → the enemy card emerges from
  the token and settles at centre-top; the board icon is hidden while the card is up.
- Multi-enemy (guardian/dungeon): cards fan out from the place token into an even arc, no
  overlap with hand or HUD; scale steps down for large rosters.
- Cards sway gently and out of sync; a defeated card stops swaying and dissolves normally;
  survivors on flee morph back to the board and the field token reappears.
- Combat end restores the opaque backdrop; the board returns to normal.
- `CombatLayoutRules` tests pass via the mcs harness.
- An enemy with no `cardArt` shows the plain card, not a broken image.

## Deferred follow-ups

- Deeper "combat fully re-hosted on the board" restaging (no combat canvas; hand as board
  objects).
- Per-enemy art authoring (M3 content).
