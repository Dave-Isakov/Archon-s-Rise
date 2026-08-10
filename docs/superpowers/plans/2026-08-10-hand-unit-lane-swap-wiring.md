# Lane swap — editor wiring

## 1. Unit prefab (`Assets/Prefabs/Unit.prefab`)
- Set the root `RectTransform` width/height to match the card prefab's.
- Re-lay the portrait, name and description children onto the card-shaped face.
- Add a `CanvasGroup` component to the root (FanLane dims items through it).
- Set the new `Exhausted Grey` colour field on the `Unit` component (default 0.55 grey is fine).

## 2. Units container (scene `GameBoard`, object `Units`)
- Delete the `Grid Layout Group` component — `FanLane` writes positions directly.
- Delete the `UnitsLane` component (its script is gone) and the focus-outline child object.
- Add `FanLane`. Set: Spread 66, Card Spacing 120, Arc Drop 40, Max Width 900,
  Focus Lift 40, Focus Scale 1.3, Dim Brightness 0.86.
- Add a `CanvasGroup` to `Units` and drag it into FanLane's `Lane Group` field.
- Pose: Focused Pos `(-430, -300)`, Parked Pos `(-760, -330)`, Parked Scale 0.55,
  Parked Alpha 0.5, Pose Tween 0.18.

## 3. Hand container (the object carrying the old `HandFanLayout`, under `Hand.prefab`)
- The component is now called `FanLane` and keeps its serialized values (the GUID was preserved).
- Add a `CanvasGroup` to the same object and drag it into `Lane Group`.
- Set Max Width 900. Pose: Focused Pos `(0, -300)`, Parked Pos `(520, -330)`,
  Parked Scale 0.55, Parked Alpha 0.5, Pose Tween 0.18.

## 4. Bar controller
- On the object that used to carry `HandFocusController` (it now has a missing script), remove the
  missing-script entry and add `BarFocusController`.
- Drag the `Units` object into `Unit Lane` and the hand fan container into `Card Lane`.

## 5. Check for missing references
- Open the scene and the Hand/Unit prefabs; confirm no component shows "Missing (Mono Script)".
- Confirm `PlayerHand`'s `Hand Layout` field still points at the hand `FanLane`.
