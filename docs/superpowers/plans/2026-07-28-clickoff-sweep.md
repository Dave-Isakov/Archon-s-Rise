# Click-Off Sweep — Implementation Plan (Plan 2 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make clicking off any dismissable surface close it, and delete every exit button in the game except the card-pick Skip and the terminal screens' controls.

**Architecture:** One `ClickOffCatcher` component (shipped in Plan 1) dropped into each surface's canvas at sibling index 0, wired to that surface's existing close method. Code changes are almost entirely deletions — the close methods already exist and are already correct; the buttons that called them go away.

**Tech Stack:** Unity 6000.5.1f1, C#, Unity UI (uGUI).

Spec: `docs/superpowers/specs/2026-07-28-minimal-place-ui-and-player-log-design.md` (Section 3)
Depends on: Plan 1 (`ClickOffCatcher` must exist).

## Global Constraints

- **Never hand-edit scene or prefab YAML.** Every wiring step produces editor instructions for the user and stops.
- **The catcher must sit at sibling index 0** within its surface. At any other index it renders in front of the content and swallows the clicks it is meant to sit under.
- **The catcher wires to the surface's existing close method**, never to a new one. Those methods already release reservations, reset input context, and clear spawned entries — a bypass would leak state.
- **Two surfaces deliberately get no catcher:** `cardRewardCanvas` (click-off would forfeit a card irrecoverably — it keeps its Skip button) and `LevelUpModal` (a forced choice with no close path by design). `runEndCanvas` and `mainMenuCanvas` are out of scope.
- Commit after every task.

---

## File Structure

No new files. Modified:
- `Assets/Scripts/GameObjectScripts/TownMenuScripts/RecruitPanel.cs` — drop `cancelButton`
- `Assets/Scripts/GameObjectScripts/TownMenuScripts/DisbandPanel.cs` — drop `cancelButton`, expose `Close`
- `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs` — drop `doneButton`, expose `Close`
- `Assets/Scripts/GameObjectScripts/TutorialScripts/HelpPopup.cs` — comment only
- `Assets/Scripts/GameObjectScripts/TownMenuScripts/CrystalDismissCatcher.cs` — **deleted**
- `Assets/Scripts/GameObjectScripts/TownMenuScripts/CreateCrystalButtons.cs` — drop the `CrystalDismissCatcher` reference

---

### Task 1: Panels with a cancel/done button

Three panels close themselves from a button. Each keeps its close method and loses the button.

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/RecruitPanel.cs:22,30-35,83-87`
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/DisbandPanel.cs:16,27-33,62-68`
- Modify: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs:17,30-36,83-89`

**Interfaces:**
- Consumes: `ClickOffCatcher` (Plan 1 Task 4).
- Produces: `RecruitPanel.Close()`, `DisbandPanel.Close()`, `UnitPickerPanel.Close()` all `public` so a catcher's `UnityEvent` can find them in the inspector dropdown.

- [ ] **Step 1: `RecruitPanel` — drop the cancel button**

Delete this field:

```csharp
    [SerializeField] Button cancelButton;
```

Replace `Start` with:

```csharp
    void Start()
    {
        Canvas.enabled = false; // start closed regardless of the authored state
    }
```

Change `Close` from `void Close()` to:

```csharp
    // Public so the ClickOffCatcher's UnityEvent can bind to it (spec 2026-07-28).
    // Cancelling a hire is free: nothing is spent until Hire runs.
    public void Close()
```

The `using UnityEngine.UI;` import is still needed (`Button` is used on the spawned entries), so leave it.

- [ ] **Step 2: `DisbandPanel` — drop the cancel button**

Delete:

```csharp
    [SerializeField] Button cancelButton;
```

Replace `Start` with:

```csharp
    void Start()
    {
        AnyOpen = false;
        Canvas.enabled = false; // start closed regardless of the authored state
    }
```

Change `void Close()` to:

```csharp
    // Public so the ClickOffCatcher can bind to it. Cancelling never runs the
    // continuation, so no influence is spent and no unit is lost.
    public void Close()
```

- [ ] **Step 3: `UnitPickerPanel` — drop the done button**

Delete:

```csharp
    [SerializeField] Button doneButton;
```

Replace `Start` with:

```csharp
    void Start()
    {
        AnyOpen = false;
        Canvas.enabled = false; // start closed regardless of the authored state
    }
```

Change `void Close()` to:

```csharp
    // Public so the ClickOffCatcher can bind to it. Closing early forfeits any
    // unspent refresh budget, which is the shipped rule (spec 2026-07-14) — the
    // old Done button did exactly the same thing.
    public void Close()
```

- [ ] **Step 4: Hand the user the editor authoring steps**

> For each of `RecruitPanel`, `DisbandPanel` and `UnitPickerPanel`:
> 1. Delete the Cancel (or Done) button GameObject from the panel.
> 2. Add a full-screen `Image` as the **first child** of the panel's canvas (sibling index 0 — drag it to the top of the hierarchy list under that canvas). Stretch its RectTransform to fill the screen.
> 3. Add the `ClickOffCatcher` component to it.
> 4. On its `onClickOff` event, add the panel object and pick its `Close` method.
> 5. Play and verify: open each panel, click off it, it closes. Then re-open it — clicking a real entry inside must still work (if entries stop responding, the catcher is not at sibling index 0).

- [ ] **Step 5: Commit after the user confirms**

```bash
git add Assets/Scripts/GameObjectScripts/TownMenuScripts/RecruitPanel.cs Assets/Scripts/GameObjectScripts/TownMenuScripts/DisbandPanel.cs Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitPickerPanel.cs
git commit -m "refactor: panels close by click-off instead of cancel/done buttons"
```

---

### Task 2: Retire `CrystalDismissCatcher`

The crystal pop-out has a bespoke catcher predating the shared one. Replace it.

**Files:**
- Delete: `Assets/Scripts/GameObjectScripts/TownMenuScripts/CrystalDismissCatcher.cs` (and its `.meta`)
- Modify: `Assets/Scripts/GameObjectScripts/TownMenuScripts/CreateCrystalButtons.cs`

**Interfaces:**
- Consumes: `ClickOffCatcher`.
- Produces: `CreateCrystalButtons.HideAll()` unchanged in behaviour, no longer referencing the deleted type.

- [ ] **Step 1: Drop the dead reference from `CreateCrystalButtons`**

Replace `HideAll` with:

```csharp
    // Hide the crystal options instead of destroying them, so the pop-out can be
    // reopened for another purchase. Hidden = non-interactable; these buttons'
    // disabled color has alpha 0, so a hidden crystal is invisible and unclickable.
    // The shared ClickOffCatcher now owns dismissal (spec 2026-07-28).
    public static void HideAll()
    {
        foreach (var crystal in FindObjectsByType<CreateCrystalButtons>(FindObjectsInactive.Include))
            crystal.thisButton.interactable = false;
    }
```

Then replace the `Update` method installed by Plan 1 Task 11 with its final form, now that the bespoke catcher is gone:

```csharp
    // The pop-out used to be reachable only from the town canvas, so this gate
    // keyed off that canvas. The place fan can now open it with the canvas shut
    // (spec 2026-07-28), so the buttons stay live while either route is open.
    private void Update()
    {
        if (GameManager.Instance.townCanvas.enabled) return;
        if (PlaceFan.Instance != null && PlaceFan.Instance.IsOpen) return;
        thisButton.interactable = false;
    }
```

- [ ] **Step 2: Delete the old catcher**

```bash
git rm "Assets/Scripts/GameObjectScripts/TownMenuScripts/CrystalDismissCatcher.cs" "Assets/Scripts/GameObjectScripts/TownMenuScripts/CrystalDismissCatcher.cs.meta"
```

- [ ] **Step 3: Hand the user the editor authoring steps**

> 1. Find the GameObject in the town canvas that carries `CrystalDismissCatcher` (its component will now show as "Missing Script"). Replace that component with `ClickOffCatcher`.
> 2. Wire its `onClickOff` to `CreateCrystalButtons.HideAll` — pick any crystal button object and select the static `HideAll` method.
> 3. The old catcher was armed/disarmed by events (`Show`/`Hide`). Re-wire those same two event hookups to the new component's `SetArmed` — the show event passes `true`, the close event passes `false`.
> 4. Play: open a town fan, click Crystal, then click off the pop-out. The crystals should vanish with no influence spent, and reopening should work.

- [ ] **Step 4: Commit after the user confirms**

```bash
git add -A Assets/Scripts/GameObjectScripts/TownMenuScripts/
git commit -m "refactor: crystal pop-out uses the shared ClickOffCatcher"
```

---

### Task 3: `HelpPopup` — drop the X button

`HelpPopup` already has an outside-click catcher wired to `Close`, so it only loses its X.

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/TutorialScripts/HelpPopup.cs:31-32`

- [ ] **Step 1: Update the comment to match reality**

Replace the comment above `Close`:

```csharp
    // Wired to the full-screen ClickOffCatcher. The X button was removed in the
    // 2026-07-28 sweep: click-off is the one dismiss gesture.
    public void Close() => root.SetActive(false);
```

- [ ] **Step 2: Hand the user the editor authoring steps**

> 1. Delete the X button GameObject from the help popup.
> 2. Replace the existing outside-click catcher component with `ClickOffCatcher`, wired to `HelpPopup.Close`, at sibling index 0 under `root`.
> 3. Play: open any `?`, click off, it closes.

- [ ] **Step 3: Commit after the user confirms**

```bash
git add Assets/Scripts/GameObjectScripts/TutorialScripts/HelpPopup.cs
git commit -m "refactor: help popup dismisses by click-off only"
```

---

### Task 4: Inspectors and list canvases

`CardInspector`, `UnitInspector` and the card list already expose close paths. These are pure editor tasks — no code changes.

**Files:** none modified.

**Interfaces:**
- Consumes: `UnitInspector.Close()` (public, resets `InputContextState` and releases the crystal reservation), `CardInspector`'s existing close method, the card list's existing close method, `TownCard.OnPointerClick` (the town menu's current dismiss).

- [ ] **Step 1: Hand the user the editor authoring steps**

> Add a `ClickOffCatcher` at sibling index 0 to each of these canvases, wired to the method named:
>
> | Canvas | Wire `onClickOff` to |
> |---|---|
> | `unitCanvas` | `UnitInspector.Close` |
> | `cardCanvas` | `CardInspector`'s existing close method |
> | `cardListCanvas` | the card list's existing close method |
> | `townCanvas` | the same method the town menu's close button calls |
> | `dungeonCanvas` | `DungeonPanel.Close` |
>
> Then delete the close/back button GameObject from each of those five canvases.
>
> **Critical:** wire to the existing `Close` methods, not to `Canvas.enabled = false`. `UnitInspector.Close` releases the reserved crystal and resets `InputContextState` — bypassing it strands a dimmed crystal and leaves input in Inspector context.
>
> Play-verify each one: open it, click off, it closes; reopen and confirm the contents still respond to clicks.

- [ ] **Step 2: Commit the editor work**

```bash
git add -A Assets/Scenes Assets/Prefabs
git commit -m "chore: click-off catchers on inspector and list canvases"
```

---

## Plan 2 Acceptance

1. Every one of these closes by clicking off, with nothing spent: place fan, town menu, dungeon panel, recruit panel, disband panel, unit picker, crystal pop-out, help popup, card inspector, unit inspector, card list.
2. Contents of each surface still respond to clicks after reopening (proves catchers are behind, not in front).
3. Closing the unit inspector by click-off releases the reserved crystal and returns input to Board context — same as the old close button.
4. The card-pick reward canvas still has its Skip button and does **not** close on click-off.
5. `LevelUpModal` still has no close path.
6. The only exit buttons remaining in the game are the card-pick Skip and the run-end/main-menu controls.
