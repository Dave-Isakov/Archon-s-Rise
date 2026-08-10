# Hand / unit lane swap — design (2026-08-10)

## 1. The problem

Units and cards are both first-class: each offers actions, each spends a turn resource, each is a
thing the player picks off the bottom of the screen. The UI does not treat them that way.

Cards live in a fan (`HandFanLayout` + the pure `FanMath`), centred on their container, growing
outward as the hand grows. Units live in a `GridLayoutGroup` on a `Units` container anchored at
`(-345, -280)` with a `690 x 90` box — a flat row of `80 x 80` squares whose **right edge is canvas
centre**. Neither container knows the other exists.

At five cards and one unit they happen to clear each other, which is why it looks fine today. They do
not stay clear. Hand size reaches 8 by level 10 (`balance.md`), and `PlayerHand.AddWound` appends past
hand size, so 10–12 items in the card lane is reachable. The fan grows left out of its centre straight
into the unit row. The army grows right along its row into the fan. There is no arbitration because
there is no component that owns both.

The input layer has the same split. `HandFocusController` owns card focus; `UnitsLane` owns unit
focus; they hand off to each other with up/down navigation and each re-implements its own hit-testing,
skip rules and focus visuals. Two owners that have to agree is the shape of the problem, not a
detail of it.

## 2. The interaction

The bottom of the screen is one **bar** holding two **lanes**: units on the left, cards on the right.
Exactly one lane is *focused* at a time.

The focused lane sits at full size and takes the middle of the bar. The other lane is *parked*: same
fan, same shape, scaled to 55% and dimmed to half alpha, tucked against its own outer edge. It stays
readable — the player can see what is in the lane they are not using, and can aim at a specific item
in it.

Swapping is deliberate, never incidental:

- **Mouse.** Clicking an item in the parked lane swaps the lanes and lands focus on the item clicked.
  It does **not** open that item's pop-out; a second click does that. Hovering a parked lane does
  nothing at all. Within the focused lane, hover moves item focus and a click opens the pop-out,
  exactly as cards behave today.
- **Pad / keyboard.** Left/right walks a single rail across both lanes as one continuum. Stepping
  left off the leftmost card lands on the rightmost unit; stepping right off the rightmost unit lands
  on the leftmost card. Crossing the boundary *is* the swap — there is no separate swap input.

Lane focus is **sticky**. Moving the cursor off the bar entirely, or pressing Cancel, clears the
per-item lift but leaves the lanes where they are. Nothing in the bar moves unless the player asked
for it.

Units become card-shaped: same `RectTransform` dimensions as cards, the same fan geometry, the same
focus treatment. The difference between the two lanes is their content, not their shape — which is
the point, since the two are meant to feel like a swappable medium.

## 3. Architecture

### 3.1 Pure rules (`ArchonsRise.Hand` asmdef)

**`FanMath` gains a width cap.** `FanSettings` grows a `MaxWidth`, and the solver uses
`spacing = count > 1 ? min(CardSpacing, MaxWidth / (count - 1)) : CardSpacing`. Arc drop and tilt are
already normalised on `t`, so they need no change: an oversized fan tightens rather than sprawls.

`MaxWidth` caps the **centre-to-centre span** of the fan, not its total pixel width — the solver knows
slot positions, not item dimensions. Total on-screen width is that span plus one item width.

**`BarRailRules` — new.** It flattens the bar into one virtual list — units first, then cards — plus a
parallel selectable mask, delegates the stepping to the existing `HandNavRules.Step`, and maps the
result back to a `(lane, index)` pair. Cross-lane movement is therefore not special-cased anywhere:
"leftmost card to rightmost unit" is index `U` to `U-1`. Wrapping happens once, around the whole bar,
so stepping left off the leftmost unit reaches the rightmost card.

`ClampAfterChange` works the same way over the combined mask, so a clamp is free to land in the other
lane.

Delegating to `HandNavRules` rather than reimplementing it is most of why this refactor is cheap, and
it keeps the existing `HandNavRules` tests meaningful.

### 3.2 `FanLane` (was `HandFanLayout`)

`HandFanLayout` is renamed `FanLane` and stops knowing about `Card`. It takes
`IReadOnlyList<IFanItem>`, where:

```
IFanItem { RectTransform Rect; bool Selectable; CanvasGroup Group; }
```

`Card` implements `Selectable => cardSO.cardType != StatType.Wound`. `Unit` implements
`Selectable => !IsPlayed`. Arc placement, focus lift / un-tilt / scale, dimming, sibling ordering and
the slot-rect `HitTest` are then shared by both lanes with no branching.

`FanLane` also owns its **presentation state** — `Focused` or `Parked` — driving its container's
`anchoredPosition`, `localScale` and lane-level alpha. Presentation is per-lane rather than global, so
a third lane later is a prefab rather than a new system.

### 3.3 `BarFocusController`

One input owner, replacing `HandFocusController` and the input half of `UnitsLane`. It owns:

- which lane is focused (sticky),
- the rail position,
- the per-frame mouse hit-test across **both** lanes,
- Submit / Cancel routing.

The pop-out-open, map-mode, main-menu and card-list guards move across unchanged. They are
load-bearing — particularly the `_inspectorWasOpen` sequence that swallows the frame after a pop-out
closes so the closing press cannot also act in the bar — and are not re-derived here.

`UnitsLane` is deleted, and its outline machinery goes with it. Its comment claims the pop-out's nav
controller reuses `FocusOutlineOver`; nothing does — `UnitsLane` and `Unit` are the only callers.

### 3.4 Clicks

`Card.OnPointerClick` and `Unit.OnPointerClick` both survive, but each offers the click to the bar
first. If the item sits in a **parked** lane, the bar claims it as swap-and-select and the click never
reaches `ToggleInspect()`.

This is deliberately not "delete the pointer handlers and let the bar own clicks outright".
`Card.OnPointerClick` is also what closes an already-maximized card, and a maximized card has been
reparented out of the lane, so it is never claimed. The claim path must also honour
`InputContextState.MapOpen`, or map mode silently gains a working lane swap.

## 4. Geometry

Canvas is `1920 x 1080` reference, so x runs `-960 … +960`.

**One shared `FanSettings` for both lanes**, not per-lane geometry: 66° spread, 120px spacing, 40px
arc drop, focus lift 40, focus scale 1.3, dim 0.86 — today's card numbers, now the bar's numbers.
`MaxWidth = 900` is shared too. At 8 cards the required spacing is 128 > 120, so nothing compresses;
compression begins at 9+, which is exactly the wound-inflated hand the cap exists for.

Lane positions, all serialized and tunable in the editor:

| lane | focused | parked (scale 0.55, alpha 0.5) |
| --- | --- | --- |
| Card lane | `x = 0` | `x = +520` |
| Unit lane | `x = -430` | `x = -760` |

Parked `y` is its own field rather than shared with the focused `y`: `localScale` shrinks about the
container's centre pivot, so a parked lane floats up off the bar baseline unless its `y` compensates.

Sanity check at real counts, both lanes being card-width (~170px):

- Cards focused at 8 span `-505 … +505`; parked units at 5 span `-939 … -581`. Clear.
- Units focused at 5 span `-755 … -105`; a parked 12-card hand spans `+226 … +814`. Clear, and inside
  the screen edge.

**Motion.** DOTween, 0.18s `OutCubic` on position, scale and alpha, with `DOKill()` on the lane before
each new tween so mashing between lanes cannot stack transforms.

## 5. Selectability and the exhausted unit

The rail lands only on things the player can act on. Wounds are already skipped by both the mouse
hit-test and pad nav; exhausted units join them. An all-exhausted army is visible but not landable,
and the rail wraps past it.

That forces a change to how exhaustion reads. It is shown today by `transform.Rotate(0, 0, -90)` in
`Player.ExhaustUnit` / `ReadyUnit` / `RebuildUnits`, and `FanLane` writes `localRotation` from the slot
tilt on every relayout — the tap rotation would be wiped by the next relayout, and a sideways card
inside an arc reads as a layout bug besides.

So exhaustion becomes a grey tint on the unit's serialized `image`, restored to `unitSO.color` when
the unit readies — the same move `CardVisuals.ApplyWoundStyle` makes for wounds. One "you cannot act
on this" language across the whole bar.

All three call sites must also notify the unit lane to relayout: `IsPlayed` feeds
`IFanItem.Selectable`, and the rail's mask is stale until they do.

## 6. Input detail

**Mouse.** Each frame the pointer moves, hit-test the focused lane and set item focus; if nothing is
hit and the focus was mouse-claimed, clear it. A click on a selectable item in the parked lane swaps
and lands on it. A click on a *wound or exhausted unit* in a parked lane still swaps, but the rail
lands on the nearest selectable neighbour in that lane — the player aimed at the lane, so they get the
lane.

**Pad / keyboard.** Left/right walks the rail; crossing the boundary swaps lane focus as a side
effect. Up/down no longer cross lanes and become no-ops in the bar, freed for later use. Submit opens
the focused item's pop-out; Cancel clears item focus and drops to `InputContext.Board` while leaving
lane focus alone.

Mouse claims focus on pointer movement, pad on a Navigate press, last input wins — unchanged from
today.

**Re-clamping.** After anything changes either lane's contents — draw, play, heal, wound, recruit,
disband, exhaust, save-load — the bar rebuilds the combined mask and runs
`BarRailRules.ClampAfterChange`. If the clamp lands across the boundary, lane focus follows it, so
playing your last playable card with units still ready slides focus into the units rather than
dropping to Board. As today this runs only while the **pad** owns focus; mouse-driven changes never
move the lane on the player.

## 7. Edge cases

- **Empty army** — unit lane renders nothing, rail is cards-only, wrapping behaves exactly as today.
- **All units exhausted** — lane still renders, greyed, so the player can see the army; nothing in it
  is landable.
- **Nothing selectable anywhere** — rail returns none, item focus clears, context drops to Board. The
  same fallback `RestorePadFocus` has now.
- **Pop-out open** — the bar consumes no input and clears item focus, but **preserves lane focus**. On
  close, pad focus restores and that frame's input is swallowed.
- **Map mode** — the bar idles, including the click-claim path (§3.4).
- **Main menu / card list open** — the bar idles, unchanged.
- **Save-load** — `RebuildHand` / `RebuildUnits` destroy and respawn; both lanes relayout and the rail
  re-clamps. Exhausted units return greyed rather than rotated.
- **Undo** — a `UnitCommand` undo readies a unit; relayout plus re-clamp returns it to the rail.
- **Wound arrives while units are focused** — the card lane relayouts in place, still parked. No swap.

## 8. Testing

EditMode, in the existing tests asmdef:

- `BarRailRules` — stepping across the boundary in both directions; wrapping at the outer ends;
  skipping unselectable entries; an empty lane on either side; nothing selectable anywhere returning
  none; `ClampAfterChange` landing across the boundary.
- `FanMath` — spacing untouched below the threshold; capped above it; the centre-to-centre span never
  exceeding `MaxWidth`; counts 0 and 1 safe; positions symmetric.
- Existing `HandNavRules` tests stay green, untouched.

No PlayMode tests. Acceptance is a manual play pass, as with the other milestones.

## 9. Manual editor work

Per memory `manual-unity-edits-for-risky-changes`, the scene and prefab changes are done by hand from
step-by-step instructions produced with the implementation plan:

1. `Unit.prefab` — `RectTransform` to card dimensions, children re-laid onto a card-shaped face, add a
   `CanvasGroup`.
2. `Units` container — delete the `GridLayoutGroup`, match the hand container's anchoring, add
   `FanLane`.
3. Hand container — `HandFanLayout` becomes `FanLane`. The rename must move
   `HandFanLayout.cs.meta` alongside the `.cs`, preserving the GUID; deleting the old file and adding
   a new one would leave `Hand.prefab` with a missing script reference and silently drop its serialized
   fan settings.
4. Delete the focus-outline object and the `UnitsLane` component.
5. Add `BarFocusController`, wired to both lanes and both inspectors.

## 10. Acceptance (USER)

- With cards focused, the unit lane sits small and dimmed on the left and the card fan holds the
  middle; moving the mouse over the map changes neither.
- Clicking a unit in the parked lane swaps the lanes, lifts that unit, and opens nothing; clicking it
  again opens its pop-out.
- With a pad, holding left from the middle of the hand walks off the leftmost card onto the rightmost
  unit and the lanes swap in one motion; continuing left past the leftmost unit wraps to the rightmost
  card.
- Wounds and exhausted units are stepped over by both mouse hover and pad nav, and exhausted units
  read as greyed rather than rotated.
- A 12-card hand (hand size plus wounds) stays on screen and does not touch the parked unit lane.
- Playing the last playable card with a ready unit left moves pad focus into the unit lane rather than
  dropping to Board.
- Opening and closing a card or unit pop-out leaves the same lane focused as before.

## 11. Out of scope

The pop-outs themselves; combat-time UI; new unit art (re-layout yes, new assets no); reward-modal
gating (the bar's behaviour behind reward canvases is unchanged and already imperfect); and any
controller remapping beyond the rail.

## 12. Noted, not changed

`LevelRules.DerivedArmyCap` tops out at 4 (base 1, plus one each at levels 4, 7 and 10 per
`balance.md`), not the 5 the lane was sized against. The width cap is tunable so the design holds
either way; flagged in case the army table is meant to grant one more.
