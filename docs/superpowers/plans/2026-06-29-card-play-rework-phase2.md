# Hand Fan Presentation (Card-Play Rework Phase 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the flat `GridLayoutGroup` player hand with a static fan arc — cards spread along a fixed arc, the hovered card lifts and scales while the rest dim, wounds read as distinct grey cards, and the arc re-balances as the hand changes.

**Architecture:** A pure, EditMode-tested `FanMath` class computes each card's slot (anchored position + tilt) for a hand of N cards — no Unity scene dependency, mirroring how `CardPlaySelection` isolates Phase 1 logic. A thin `HandFanLayout` MonoBehaviour applies those slots to the live card RectTransforms, owns the focused-card lift/dim, and is re-run by `PlayerHand` whenever the hand changes. `PlayerHand` and `Card` stop reaching for `GridLayoutGroup` and route layout through `HandFanLayout`.

**Tech Stack:** Unity 6 (Assembly-CSharp + asmdefs), C#, DOTween (vendored at `Assets/Plugins/Demigiant`), NUnit EditMode tests (`ArchonsRise.Tests.EditMode`).

## Global Constraints

- **Presentation only.** Do not touch stat math, `PlayCommand`/`ICommands`/`commands.*`, the crystal consume/restore path, or `CardPlaySelection`/`CardInspector` play routing. (Spec: "This is a presentation + input rework.")
- **Phase 2 scope only.** No center pop-out float / wrap-panel restyle (Phase 3), no echo-flight/juice (Phase 3), no gamepad / Input System (Phase 4). Focus is driven by **mouse hover only** this phase.
- **Fan geometry (locked):** spread ≈ **±33°** across the hand; focused card lifts ≈ **40px**, scales **1.3×**, untilts (tilt → 0), z-sorts on top; non-focused cards dim to **~0.86** brightness. Wounds read as a distinct **grey** card and are non-interactive for play.
- **Preserve:** the save/load hand rebuild (`PlayerHand.RebuildHand`, the `DataManager.IsLoading` guard) and the `cardListCanvas` "view whole hand" path (`PlayerHand.HandToCardList`).
- **Pure logic stays UnityEngine-light:** `FanMath` may use `UnityEngine.Vector2`/`Mathf` (available to EditMode tests) but must not reference scene objects, MonoBehaviours, or `GameManager`.
- Each commit message ends with the project trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

---

## File Structure

- `Assets/Scripts/Hand/ArchonsRise.Hand.asmdef` — **new** assembly for pure hand-layout logic (auto-referenced, references nothing project-specific).
- `Assets/Scripts/Hand/FanSlot.cs` — **new** pure result struct (one card's computed position + tilt).
- `Assets/Scripts/Hand/FanMath.cs` — **new** pure slot solver. EditMode-tested.
- `Assets/Scripts/Hand/HandFanLayout.cs` — **new** MonoBehaviour (Assembly-CSharp) that applies `FanMath` to live cards and owns focus/dim. Manual Play-mode verification.
- `Assets/Tests/EditMode/FanMathTests.cs` — **new** EditMode tests for `FanMath`.
- `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` — **modify** to reference `ArchonsRise.Hand`.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs` — **modify** to route through `HandFanLayout` and re-layout on hand changes.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs` — **modify** to report hover focus and return-to-hand via `HandFanLayout`; wound styling.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/CardVisuals.cs` — **modify** to add a wound-grey helper.
- `Assets/Prefabs/Card.prefab` — **modify** (add a `CanvasGroup` for dimming). *(Unity Editor step, described in-task.)*
- `Assets/Scenes/GameBoard.unity` — **modify** (swap `GridLayoutGroup` → `HandFanLayout` on the hand container, wire `PlayerHand.handLayout`). *(Unity Editor step, described in-task.)*

> **Layout source-of-truth decision:** `HandFanLayout.Relayout(orderedCards)` is driven by `PlayerHand.cardsInPlay` order, **not** by live sibling order. This lets the focused card be `SetAsLastSibling()`'d for render-on-top without disturbing slot assignment. Cards not currently parented to the hand container (e.g. a card maximized into the inspector's `CardLocation`) are skipped and re-included when they return.

---

## Task 1: FanMath pure slot solver (TDD)

**Files:**
- Create: `Assets/Scripts/Hand/ArchonsRise.Hand.asmdef`
- Create: `Assets/Scripts/Hand/FanSlot.cs`
- Create: `Assets/Scripts/Hand/FanMath.cs`
- Modify: `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`
- Test: `Assets/Tests/EditMode/FanMathTests.cs`

**Interfaces:**
- Produces:
  - `struct FanSlot { Vector2 AnchoredPosition; float TiltZ; }` (degrees, +CCW).
  - `class FanSettings { float SpreadDegrees; float CardSpacing; float ArcDrop; }` (plain fields, defaults `66f`, `120f`, `40f`).
  - `static FanSlot[] FanMath.Solve(int count, FanSettings s)` — index 0 is leftmost.

- [ ] **Step 1: Create the Hand assembly definition**

Create `Assets/Scripts/Hand/ArchonsRise.Hand.asmdef`:

```json
{
    "name": "ArchonsRise.Hand",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": true
}
```

- [ ] **Step 2: Add the test assembly reference**

In `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`, add `"ArchonsRise.Hand"` to the `references` array (alongside `ArchonsRise.CardPlay`, `ArchonsRise.Enums`):

```json
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "ArchonsRise.CardPlay",
        "ArchonsRise.Enums",
        "ArchonsRise.Hand"
    ],
```

- [ ] **Step 3: Write the result struct and settings**

Create `Assets/Scripts/Hand/FanSlot.cs`:

```csharp
using UnityEngine;

// One card's computed place in the fan. Pure data — no scene dependency.
public readonly struct FanSlot
{
    public readonly Vector2 AnchoredPosition; // local, relative to hand-container center
    public readonly float TiltZ;              // degrees, +counter-clockwise

    public FanSlot(Vector2 anchoredPosition, float tiltZ)
    {
        AnchoredPosition = anchoredPosition;
        TiltZ = tiltZ;
    }
}

// Tunable fan geometry. Plain fields so HandFanLayout can serialize it.
[System.Serializable]
public class FanSettings
{
    public float SpreadDegrees = 66f; // total fan angle -> edges sit at ±33°
    public float CardSpacing = 120f;  // horizontal px between adjacent card centers
    public float ArcDrop = 40f;       // px the edge cards sit below the center card
}
```

- [ ] **Step 4: Write the failing tests**

Create `Assets/Tests/EditMode/FanMathTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class FanMathTests
{
    static FanSettings Settings() => new FanSettings
    {
        SpreadDegrees = 66f,
        CardSpacing = 120f,
        ArcDrop = 40f
    };

    [Test]
    public void Empty_ReturnsNoSlots()
    {
        Assert.AreEqual(0, FanMath.Solve(0, Settings()).Length);
    }

    [Test]
    public void SingleCard_IsCentredAndUntilted()
    {
        var slots = FanMath.Solve(1, Settings());
        Assert.AreEqual(1, slots.Length);
        Assert.AreEqual(0f, slots[0].AnchoredPosition.x, 0.001f);
        Assert.AreEqual(0f, slots[0].AnchoredPosition.y, 0.001f);
        Assert.AreEqual(0f, slots[0].TiltZ, 0.001f);
    }

    [Test]
    public void Edges_ReachFullSpreadAndMirror()
    {
        var slots = FanMath.Solve(5, Settings());
        // leftmost (index 0) and rightmost (index 4) tilt to ±33° and mirror.
        Assert.AreEqual(33f, slots[0].TiltZ, 0.001f);
        Assert.AreEqual(-33f, slots[4].TiltZ, 0.001f);
        Assert.AreEqual(-slots[0].AnchoredPosition.x, slots[4].AnchoredPosition.x, 0.001f);
        Assert.AreEqual(slots[0].AnchoredPosition.y, slots[4].AnchoredPosition.y, 0.001f);
    }

    [Test]
    public void Cards_AreEvenlySpacedAndCentred()
    {
        var slots = FanMath.Solve(4, Settings());
        // centred about x=0: spacing 120 -> x = {-180, -60, 60, 180}
        Assert.AreEqual(-180f, slots[0].AnchoredPosition.x, 0.001f);
        Assert.AreEqual(-60f, slots[1].AnchoredPosition.x, 0.001f);
        Assert.AreEqual(60f, slots[2].AnchoredPosition.x, 0.001f);
        Assert.AreEqual(180f, slots[3].AnchoredPosition.x, 0.001f);
    }

    [Test]
    public void EdgeCards_SitBelowCentre()
    {
        var slots = FanMath.Solve(3, Settings());
        Assert.AreEqual(0f, slots[1].AnchoredPosition.y, 0.001f);   // centre card at y=0
        Assert.AreEqual(-40f, slots[0].AnchoredPosition.y, 0.001f); // edges drop by ArcDrop
        Assert.AreEqual(-40f, slots[2].AnchoredPosition.y, 0.001f);
    }
}
```

- [ ] **Step 5: Run tests to verify they fail**

Run the EditMode suite (Unity → Window → General → Test Runner → EditMode → Run All), or headless:

```bash
Unity -batchmode -projectPath "." -runTests -testPlatform EditMode -testResults "TestResults.xml" -quit
```

Expected: FAIL — `FanMath` does not exist / `Solve` not defined.

- [ ] **Step 6: Implement FanMath**

Create `Assets/Scripts/Hand/FanMath.cs`:

```csharp
using UnityEngine;

// Pure fan-arc solver. Given a card count and geometry, returns each card's
// local position + tilt. Index 0 is the leftmost card. No scene dependency.
public static class FanMath
{
    public static FanSlot[] Solve(int count, FanSettings s)
    {
        var slots = new FanSlot[count < 0 ? 0 : count];
        if (count <= 0) return slots;

        for (int i = 0; i < count; i++)
        {
            // t in [-0.5, 0.5]; single card -> 0 (centred).
            float t = count == 1 ? 0f : (float)i / (count - 1) - 0.5f;

            float x = (i - (count - 1) * 0.5f) * s.CardSpacing;
            float y = -s.ArcDrop * (2f * t) * (2f * t); // parabolic dip, edges lowest
            float tilt = -t * s.SpreadDegrees;          // leftmost -> +half-spread

            slots[i] = new FanSlot(new Vector2(x, y), tilt);
        }
        return slots;
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run the EditMode suite again. Expected: all `FanMathTests` PASS, and the existing `CardPlaySelectionTests` / `CardSnapshotTests` remain green.

- [ ] **Step 8: Commit**

```bash
git add "Assets/Scripts/Hand" "Assets/Tests/EditMode/FanMathTests.cs" "Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef"
git commit -m "feat: FanMath pure hand-fan slot solver with EditMode tests

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

> After Unity regenerates `.meta` files for the new `Assets/Scripts/Hand/` folder and files, commit those too (`git add Assets/Scripts/Hand` picks them up once they exist).

---

## Task 2: HandFanLayout MonoBehaviour + scene swap

**Files:**
- Create: `Assets/Scripts/Hand/HandFanLayout.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs:155-161` (`SetCardObjectToNormal`)
- Modify: `Assets/Scenes/GameBoard.unity` (Unity Editor)
- Test: Manual Play-mode verification.

**Interfaces:**
- Consumes: `FanMath.Solve`, `FanSettings`, `FanSlot` from Task 1.
- Produces:
  - `HandFanLayout.Relayout(System.Collections.Generic.IReadOnlyList<Card> orderedCards)` — positions the cards that are children of this transform; safe to call every frame the hand changes.
  - `HandFanLayout.Container` (`Transform`) — the parent new/returning cards should be parented to (replaces `GetComponentInChildren<GridLayoutGroup>().transform`).

- [ ] **Step 1: Write HandFanLayout**

Create `Assets/Scripts/Hand/HandFanLayout.cs`. Note: this lives in Assembly-CSharp (no asmdef under `GameObjectScripts`), which auto-references `ArchonsRise.Hand`, so `FanMath`/`FanSlot`/`FanSettings` resolve.

```csharp
using System.Collections.Generic;
using UnityEngine;

// Applies FanMath slots to the live hand cards. Driven by PlayerHand (which owns
// card order); not a Unity LayoutGroup, so the GridLayoutGroup it replaces must be
// removed from the same GameObject. Focus lift/dim is added in Task 3.
public class HandFanLayout : MonoBehaviour
{
    [SerializeField] FanSettings fan = new FanSettings();

    public Transform Container => transform;

    public void Relayout(IReadOnlyList<Card> orderedCards)
    {
        // Only lay out cards currently parented here (a maximized card is elsewhere).
        var inHand = new List<Card>();
        foreach (var c in orderedCards)
            if (c != null && c.transform.parent == transform && c.gameObject.activeSelf)
                inHand.Add(c);

        var slots = FanMath.Solve(inHand.Count, fan);
        for (int i = 0; i < inHand.Count; i++)
            Apply(inHand[i], slots[i]);
    }

    void Apply(Card card, FanSlot slot)
    {
        var rt = (RectTransform)card.transform;
        rt.anchoredPosition = slot.AnchoredPosition;
        rt.localRotation = Quaternion.Euler(0f, 0f, slot.TiltZ);
        rt.localScale = Vector3.one;
    }
}
```

- [ ] **Step 2: Route PlayerHand through HandFanLayout**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs`:

Add a serialized field near the other layout fields (replacing reliance on `GridLayoutGroup`):

```csharp
    [SerializeField] HandFanLayout handLayout;
```

Replace every `GetComponentInChildren<GridLayoutGroup>().transform` parent target with `handLayout.Container`. There are two:
- in `DrawCard()` (`drawnCard.transform.SetParent(...)`)
- in `RebuildHand(...)` (`Instantiate(card, ...Container)`)
- in `HandToCardList()` (the `else` branch that returns cards to the hand)

Then add a re-layout call at the end of each method that changes the hand. Add this helper and call it from `DrawCard`, `RemovePlayedCardsFromHand`, `AddWound`, `HealWound`, `RestoreHealedWound`, `RebuildHand`, and the `else` branch of `HandToCardList`:

```csharp
    public void Relayout() => handLayout.Relayout(cardsInPlay);
```

Example for `DrawCard()` — after the `drawnCard.transform.SetParent(handLayout.Container);` line, the method already returns; add `Relayout();` as the last statement of the `if` block. For `DrawCards(int)` (the loop) a single `Relayout()` after the loop is enough, but calling per-card is harmless. Keep it simple: call `Relayout()` at the end of `DrawCard`, `RemovePlayedCardsFromHand`, `AddWound`, `HealWound`, `RestoreHealedWound`, and `RebuildHand`.

- [ ] **Step 3: Route the return-from-maximize through HandFanLayout**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs`, `SetCardObjectToNormal` (lines ~155-161) currently parents to `GridLayoutGroup` and sets a stored `siblingIndex`. Replace its body so the card returns to the fan container and triggers a re-layout:

```csharp
    //Returns the card to normal size in the player hand.
    public void SetCardObjectToNormal(Card card)
    {
        var hand = GameManager.Instance.playerHand.GetComponentInChildren<HandFanLayout>();
        card.gameObject.transform.SetParent(hand.Container, false);
        card.gameObject.transform.localScale = Vector3.one;
        card.isMaximized = false;
        GameManager.Instance.playerHand.GetComponent<PlayerHand>().Relayout();
    }
```

(`SetCardObjectToMax` is unchanged — the inspector pop-out still reparents to `CardLocation`; that's Phase 3's concern.)

- [ ] **Step 4: Swap the component in the scene (Unity Editor)**

In `GameBoard.unity`, on the hand-container GameObject that currently has the `GridLayoutGroup` (a child of the `PlayerHand` object, the transform returned by the old `GetComponentInChildren<GridLayoutGroup>()`):
1. **Remove** the `GridLayoutGroup` component (manual fan positioning fights any LayoutGroup).
2. **Add** the `HandFanLayout` component; leave `fan` at defaults.
3. On the `PlayerHand` component, assign the new **`handLayout`** field to this `HandFanLayout`.
4. Confirm the container's child `RectTransform`s have a consistent anchor/pivot (center) so `anchoredPosition` from `FanMath` reads as "relative to container center." If the hand was previously top-left anchored by the grid, set the container's children anchor/pivot to center via the Card prefab (Task 3 touches the prefab anyway).

- [ ] **Step 5: Manual Play-mode verification**

Enter Play mode. Expected: the starting hand fans out along an arc (edges tilted ~±33°, edges sitting lower), cards evenly spaced and centered. Drawing a card (end turn / round) re-balances the arc. No `GridLayoutGroup` snapping. Console clean.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Hand/HandFanLayout.cs" "Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs" "Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs" "Assets/Scenes/GameBoard.unity"
git commit -m "feat: fan the player hand via HandFanLayout (replaces GridLayoutGroup)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Hover focus — lift, scale, untilt, dim others

**Files:**
- Modify: `Assets/Scripts/Hand/HandFanLayout.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs` (add hover interfaces)
- Modify: `Assets/Prefabs/Card.prefab` (add a `CanvasGroup`) (Unity Editor)
- Test: Manual Play-mode verification.

**Interfaces:**
- Consumes: `HandFanLayout.Relayout` from Task 2.
- Produces:
  - `HandFanLayout.SetFocus(Card card)` / `HandFanLayout.ClearFocus(Card card)` — set/clear the hovered card and re-layout.

- [ ] **Step 1: Add focus state + lift/dim to HandFanLayout**

Edit `Assets/Scripts/Hand/HandFanLayout.cs`. Add serialized focus tunables, a `_focused` field, the `SetFocus`/`ClearFocus` API, and apply lift/scale/untilt to the focused card and dim to the rest. Replace the `Apply` call site and method:

```csharp
    [SerializeField] FanSettings fan = new FanSettings();
    [SerializeField] float focusLift = 40f;
    [SerializeField] float focusScale = 1.3f;
    [SerializeField] float dimBrightness = 0.86f;

    Card _focused;
    IReadOnlyList<Card> _last; // remember order for focus-driven relayouts

    public Transform Container => transform;

    public void SetFocus(Card card)
    {
        if (_focused == card) return;
        _focused = card;
        if (_last != null) Relayout(_last);
    }

    public void ClearFocus(Card card)
    {
        if (_focused != card) return;
        _focused = null;
        if (_last != null) Relayout(_last);
    }

    public void Relayout(IReadOnlyList<Card> orderedCards)
    {
        _last = orderedCards;
        var inHand = new List<Card>();
        foreach (var c in orderedCards)
            if (c != null && c.transform.parent == transform && c.gameObject.activeSelf)
                inHand.Add(c);

        var slots = FanMath.Solve(inHand.Count, fan);
        for (int i = 0; i < inHand.Count; i++)
            Apply(inHand[i], slots[i], inHand[i] == _focused);

        if (_focused != null && _focused.transform.parent == transform)
            _focused.transform.SetAsLastSibling(); // render on top
    }

    void Apply(Card card, FanSlot slot, bool focused)
    {
        var rt = (RectTransform)card.transform;
        if (focused)
        {
            rt.anchoredPosition = slot.AnchoredPosition + new Vector2(0f, focusLift);
            rt.localRotation = Quaternion.identity;          // untilt
            rt.localScale = Vector3.one * focusScale;
        }
        else
        {
            rt.anchoredPosition = slot.AnchoredPosition;
            rt.localRotation = Quaternion.Euler(0f, 0f, slot.TiltZ);
            rt.localScale = Vector3.one;
        }

        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null)
            cg.alpha = (focused || _focused == null) ? 1f : dimBrightness;
    }
```

> The dim uses `CanvasGroup.alpha` as a practical stand-in for "0.86 brightness." This phase aims for functional clarity; a true brightness multiply (tinting the card images) can come with the Phase 3 visual pass if desired.

- [ ] **Step 2: Report hover from Card**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs`, add the hover interfaces to the class declaration and implement them. Wounds and already-maximized cards do not take focus.

Change the class declaration:

```csharp
public class Card : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
```

Add these methods (near `OnPointerClick`):

```csharp
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isMaximized || cardSO.cardType == StatType.Wound) return;
        if (GameManager.Instance.cardCanvas.enabled) return; // inspector open elsewhere
        var hand = GameManager.Instance.playerHand.GetComponentInChildren<HandFanLayout>();
        hand?.SetFocus(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var hand = GameManager.Instance.playerHand.GetComponentInChildren<HandFanLayout>();
        hand?.ClearFocus(this);
    }
```

- [ ] **Step 3: Add a CanvasGroup to the Card prefab (Unity Editor)**

Open `Assets/Prefabs/Card.prefab`. Add a `CanvasGroup` component to the root `Card` GameObject (default alpha 1, Interactable on, Blocks Raycasts on). This is what `HandFanLayout.Apply` dims. Save the prefab.

- [ ] **Step 4: Manual Play-mode verification**

Enter Play mode. Expected: hovering a hand card lifts it ~40px, scales it 1.3×, straightens it, and renders it above its neighbors; the other cards dim to ~0.86. Moving the mouse off restores the arc and full brightness. Hovering a wound does nothing. Clicking a hovered card still opens the inspector (Phase 1 path intact).

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Hand/HandFanLayout.cs" "Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs" "Assets/Prefabs/Card.prefab"
git commit -m "feat: hover focus lifts/untilts the focused card and dims the rest

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Wound styling (distinct grey)

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/CardVisuals.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs:114-118` (`GetEmpowerTypeColor` / `Start`)
- Test: Manual Play-mode verification.

**Interfaces:**
- Consumes: existing `CardVisuals.ApplyEmpowerColor`.
- Produces: `CardVisuals.ApplyWoundStyle(GameObject card, Color woundGrey)`.

- [ ] **Step 1: Add the wound-grey helper**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/CardVisuals.cs`, add:

```csharp
    public static void ApplyWoundStyle(GameObject card, Color woundGrey)
    {
        var frontImage = card.GetComponentsInChildren<Image>()[0];
        frontImage.color = woundGrey;
    }
```

- [ ] **Step 2: Route wounds to the grey style in Card**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs`, add a serialized grey color near the empower colors:

```csharp
    [SerializeField] private Color woundGrey = new Color(0.55f, 0.55f, 0.55f, 1f);
```

Change `GetEmpowerTypeColor` to branch wounds to the grey style:

```csharp
    private void GetEmpowerTypeColor(GameObject card)
    {
        if (cardSO.cardType == StatType.Wound)
        {
            CardVisuals.ApplyWoundStyle(card, woundGrey);
            return;
        }
        CardVisuals.ApplyEmpowerColor(card, cardSO.empowerType,
            greenColor, redColor, purpleColor, yellowColor);
    }
```

- [ ] **Step 3: Manual Play-mode verification**

Force a wound into hand (e.g. via the existing `PlayerHand.AddWound` path / a debug call, or take combat damage if available). Expected: the wound card renders distinctly grey, does not lift/dim on hover (Task 3 already gates `OnPointerEnter` on `StatType.Wound`), and the inspector still reports it unplayable (Phase 1 `IsPlayable` already returns false for wounds).

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/PlayerScripts/CardVisuals.cs" "Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs"
git commit -m "feat: render wounds as distinct grey, non-focusable cards

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Re-layout coverage for save/load and the card-list view

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs` (`RebuildHand`, `HandToCardList`)
- Test: Manual Play-mode verification.

**Interfaces:**
- Consumes: `HandFanLayout.Relayout` / `PlayerHand.Relayout` from Task 2.

- [ ] **Step 1: Re-layout after a save-load rebuild**

In `PlayerHand.RebuildHand(...)`, confirm new cards are instantiated under `handLayout.Container` (Task 2 Step 2) and add `Relayout();` as the final statement so a resumed run fans correctly.

- [ ] **Step 2: Re-layout when returning from the card-list view**

In `PlayerHand.HandToCardList()`, the `else` branch (cardListCanvas closed) re-parents cards back to the hand. Ensure that branch parents to `handLayout.Container` and ends with `Relayout();`:

```csharp
    public void HandToCardList()
    {
        if (GameManager.Instance.cardListCanvas.enabled)
        {
            foreach (var card in cardsInPlay)
                card.transform.SetParent(GameManager.Instance.cardListParent.transform);
        }
        else
        {
            foreach (var card in cardsInPlay)
                card.transform.SetParent(handLayout.Container);
            Relayout();
        }
    }
```

- [ ] **Step 3: Manual Play-mode verification**

Two checks:
1. **Save/Load:** start a run, draw/modify the hand, save, quit to menu, resume. Expected: the rebuilt hand fans correctly (no grid snap, no stacked-at-origin cards).
2. **Card-list view:** open the "view whole hand" (`cardListCanvas`) and close it. Expected: cards leave the fan into the list, then return to a correctly balanced fan.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs"
git commit -m "fix: re-fan the hand after save-load rebuild and card-list view

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage (Phase 2 = "Fan hand presentation — static fan arc, focus lift, dim, wound styling, re-layout"):**
- Static fan arc (±33°, even spacing, edges lower) → Task 1 (math) + Task 2 (apply). ✅
- Focus lift (40px), scale (1.3×), untilt, z-on-top → Task 3. ✅
- Dim non-focused (~0.86) → Task 3 (CanvasGroup alpha; brightness caveat noted). ✅
- Wounds distinct grey + non-interactive → Task 4 (grey) + Task 3 (hover gate) + existing Phase 1 `IsPlayable` (play gate). ✅
- Re-layout on draw/discard/heal → Task 2 (hooks) ; save/load + card-list preserved → Task 5. ✅
- Out of scope honored: no center float / juice / gamepad (Global Constraints). ✅

**Placeholder scan:** No TBD/"handle edge cases"/"write tests for the above"; every code step shows full code. ✅

**Type consistency:** `FanSlot(Vector2 AnchoredPosition, float TiltZ)`, `FanSettings{SpreadDegrees,CardSpacing,ArcDrop}`, `FanMath.Solve(int,FanSettings)→FanSlot[]`, `HandFanLayout.Relayout(IReadOnlyList<Card>)`, `Container`, `SetFocus`/`ClearFocus`, `PlayerHand.Relayout()`, `CardVisuals.ApplyWoundStyle(GameObject,Color)` — names match across tasks. ✅

**Known caveat carried forward:** the dim approximates brightness via alpha; if the locked "0.86 brightness" must be exact, escalate to a per-image tint in the Phase 3 visual pass.
