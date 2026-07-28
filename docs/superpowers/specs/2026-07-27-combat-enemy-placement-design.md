# Combat enemy placement — design (2026-07-27)

Supersedes the arc-placement parts of `2026-07-24-combat-feel-on-board-design.md` and the
button-anchoring part of `2026-07-24-combat-phase-controls-design.md`.

## Problem

Enemy cards land in the wrong place and the wrong place moves.

1. **Mixed coordinate systems.** The `EnemyCard` prefab root is anchored and pivoted at
   `(0, 0)` — its `anchoredPosition` is measured from CombatPosition's *bottom-left corner*.
   `CombatController.OriginLocalPoint` computes the arc origin with
   `ScreenPointToLocalPointInRectangle`, which returns a point relative to the rect's
   *centre*. The two disagree by half the parent's size, so resizing CombatPosition moves
   every card.
2. **Wrong centre for place fights.** A guardian assault fans the cards around the assaulted
   town's projected position, not around the player, so a keep below the player throws the
   whole fight into the hand.
3. **Moving furniture.** The Advance/Withdraw buttons re-anchor every frame opposite the live
   enemy centroid, and survivors re-fanned after each kill. Both read as jitter, and a moving
   button is impossible to design enemy slots around.

## Coordinate model

At spawn `CombatController` forces each card's `anchorMin = anchorMax = pivot = (0.5, 0.5)`.
From then on `anchoredPosition` means **pixels from the player**: CombatPosition's centre is
the canvas centre, and the camera is parented under PlayerPosition, so screen centre is the
player.

CombatPosition's width and height are never read. Its *position* is also neutralised:
`PlayerLocal()` expresses the canvas centre in the parent's local space, and the ring centre and
safe area are both built from that point, so parking CombatPosition off-centre no longer drags
the layout with it. Authoring it at Pos `(0, 0, 0)`, anchors centre, pivot `(0.5, 0.5)` is still
the clearest setup. The old `(0, 50)` nudge becomes the `ringNudge` tunable.

## Placement

One layout entry point — `LayoutLive(centre, radius)`. Context chooses only those two values.

| Fight | Centre | Radius |
| --- | --- | --- |
| Guardian, or any fight with 2+ enemies | player `(0,0)` + `ringNudge (0, 40)` | `ringRadius 200` |
| Single enemy from a map token (field, dungeon) | token spot, see below | `tokenFanRadius 110` |

Dungeon delves spawn exactly one enemy, so they take the token path.

### Ring geometry (guardian / multi)

Neighbouring slots sit `slotSpacing = 55°` apart, the fan is centred on straight up, and its
total width is capped at `MaxArcDegrees = 170` so no slot ever drops to or below the centre's
horizontal.

| Count | Angles (deg, 0 = due right, 90 = up) |
| --- | --- |
| 1 | 90 |
| 2 | 62.5, 117.5 |
| 3 | 35, 90, 145 |
| 4 | 7.5, 62.5, 117.5, 172.5 |
| 5+ | capped at 170 total; gaps tighten |

Index 0 stays leftmost. Scale keeps the existing crowd step-down: full up to 3 cards, −12% per
card after, floored at 0.6.

At radius 200 around a centre 40px above the player, the lowest slots sit ~66px above the
player's line — clear of the hand and outside a statically parked Engage button.

### Token spot (field / dungeon)

Reuses `EnemyPreviewPanel`'s placement feel, computed in screen pixels:

```
anchor  = Lerp(tokenScreen, screenCentre, centreBias 0.5)
desired = anchor + offset (0, +40)
```

Then a **cluster-level** safety pass — the arc translates rigidly, so cards never move relative
to one another:

1. Clamp the cluster's bounds into the safe area: the screen inset by
   `(left 12, right 12, top 12, bottom handKeepOut ≈ 200)`. The bottom inset *is* the hand.
2. Push out of `buttonKeepOut`, a rect in player-relative pixels covering the parked
   Advance/Withdraw buttons. Vertical push is preferred; up wins ties.
3. Re-clamp into the safe area.

Cluster bounds need a card footprint. The prefab root rect is zero-sized, so `cardSize` is
authored as `(125, 170)` — the art's real size — and multiplied by the applied scale.

The ring path runs step 1 only. The safe-area clamp is free insurance on a short screen, but the
buttons are parked *inside* the ring by design, so pushing off them would be wrong there.

This replaces `ClampAboveHorizon`, which was a proxy for "not in the hand". An explicit keep-out
rect says it directly, so the angle clamp and its tests are deleted.

## Buttons

`CombatButtons` no longer positions anything. Both buttons are authored in the scene; the
script captures each `anchoredPosition` at Start and sways around it. Show/hide, label, colour
and glow are unchanged. `CombatLayoutRules.OppositeAnchor`, `CombatButtons.Clamp`, the
`advanceDistance` / `withdrawDistance` / `clampHalfExtents` fields, and
`CombatController.EnemyCentroidLocal` all lose their only callers and are deleted.

## Kills

Layout runs once, at fight-open. `NotifyDefeated` does not re-layout: survivors hold the slots
they opened the fight in and a defeated card leaves a gap.

## Testing

Pure additions to `CombatLayoutRules`, verified with the mcs harness:

- `FanArc(count, spacingDeg)` — the width table above, capped at `MaxArcDegrees`.
- `SlotFor` over that arc — angles and monotonic left-to-right X.
- `ShiftIntoSafeArea(cluster, safe)` — minimal delta, no-op when already inside.
- `ShiftOutOfKeepOut(cluster, keepOut)` — minimal delta, prefers vertical, up on ties, no-op
  when disjoint.
- End to end: a token-anchored cluster near the screen bottom ends up fully inside the safe
  area and clear of the button rect.

## Manual Unity steps

1. CombatPosition: Pos `(0, 0, 0)`, anchors centre, pivot `(0.5, 0.5)`. Size is irrelevant.
2. Park Advance and Withdraw where you want them; set `buttonKeepOut` to cover both.
3. Tune `ringRadius`, `ringNudge`, `slotSpacing`, `handKeepOut` in the CombatController
   inspector.

`OppositeAnchorTests.cs` is deleted along with the rule it covered.
