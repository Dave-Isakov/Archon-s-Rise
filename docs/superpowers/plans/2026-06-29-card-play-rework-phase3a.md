# Card-Play Rework Phase 3a — Pop-out Float + Crystalline Dark Styling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the functional Phase-1 card menu into the approved "Crystalline Dark" pop-out — the focused card floats from its fan slot to center over a dimmed board, and the Choice/Improvise/Empower/Play sections render glassy panels with clear selected / available / locked states from a shared stat-accent palette.

**Architecture:** A pure `StatPalette` (in the EditMode-tested `ArchonsRise.CardPlay` assembly) is the single source of truth for the locked stat colors. A small reusable `StatSegment` MonoBehaviour renders one Choice/Improvise option from that palette; `ChoiceBanner`/`ImprovisePanel` drive segments instead of toggling `interactable`. `EmpowerPanel`/`PlayBar` gain preview text + accent styling. The center float upgrades the existing `Card.SetCardObjectToMax/Normal` snap to DOTween, and `CardInspector.Open/Close` fade a board scrim + the pop-out panels in. No selection logic, command/undo, crystal consume/restore, fan math, or `Player` stat method changes.

**Tech Stack:** Unity 6 (Assembly-CSharp + asmdefs), C#, DOTween (vendored at `Assets/Plugins/Demigiant`, namespace `DG.Tweening`, already used in `PlayerHand`), TextMeshPro, NUnit EditMode tests (`ArchonsRise.Tests.EditMode`).

## Global Constraints

- **Presentation only.** Do not change `CardPlaySelection`'s existing behavior, `CardInspector`'s routing to `CardEvent`/`PlayCommand`, the crystal consume/restore path (`CrystalInventory.EmpowerCrystal/RegenCrystal`), `FanMath`/`HandFanLayout`, or any `Player` stat method. (Spec: "This is presentation only.")
- **Phase 3a scope only.** No juice (echo-flight, stat pop/count-up, crystal-spend flourish, commit-to-discard sweep) — that is Phase 3b. **`StatsDisplay` is not touched in 3a.** No gamepad / Input System (Phase 4).
- **Locked palette (verbatim hex):** Attack `#ff5a5a` (red) · Defend `#b06bff` (purple) · Influence `#ffd24d` (yellow) · Explore `#54d98c` (green) · Empower `#5fd0e6` (cyan). Muted base `#aeb8cc`; Locked tint `#39405a`.
- **Locked visual rules:** selection reads by **color + glow**, never by greying the chosen button. Inactive sections **dim + lock + show a one-line reason** and destroy no stored state (the underlying mutual-exclusion is already enforced in Phase-1 logic).
- **Center-float feel:** card arcs from its fan slot to `enlargeCardPosition` scaling up over ~**0.25s** ease-out-back; board dims to ~40% (scrim alpha ≈ 0.6); panels fade in. Back/Cancel/play-commit returns the card to its fan slot (tweened) via the existing minimize path.
- **DOTween hygiene:** `DOKill()` the target before starting a new tween on it, so rapid open/close/Back can't stack tweens.
- Each commit message ends with the project trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

---

## File Structure

- `Assets/Scripts/CardPlay/StatPalette.cs` — **new** pure static palette (in `ArchonsRise.CardPlay` so EditMode tests can reference it; Assembly-CSharp also references this assembly, so the section views can use it). EditMode-tested.
- `Assets/Scripts/CardPlay/CardPlaySelection.cs` — **modify** to add an additive, read-only `PreviewStats(bool empowered)` query (drives the `+2 → +4` preview); refactor `ResolveStats()` to delegate to it (behavior preserved, guarded by existing tests).
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/StatSegment.cs` — **new** reusable per-option view (Selected / Available / Locked from `StatPalette`).
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ChoiceBanner.cs` — **modify** to drive `StatSegment`s and render the locked-while-improvising state.
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ImprovisePanel.cs` — **modify** to drive `StatSegment`s and render the locked-while-empowered state.
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/EmpowerPanel.cs` — **modify** to show the cyan reserved indicator + `+N → +N` preview + locked reason.
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/PlayBar.cs` — **modify** only if needed for label color; styling is mostly editor data.
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs` — **modify** to fade a board scrim + the pop-out panels on Open (instant teardown on Close).
- `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs` — **modify** `SetCardObjectToMax` / `SetCardObjectToNormal` to tween (add `using DG.Tweening;`).
- `Assets/Tests/EditMode/StatPaletteTests.cs` — **new** EditMode tests for `StatPalette`.
- `Assets/Tests/EditMode/CardPlaySelectionTests.cs` — **modify** to add `PreviewStats` tests.
- `Assets/Prefabs/CardMenu*.prefab` / `Assets/Scenes/GameBoard.unity` — **modify** (Unity Editor): glassy panel art, `StatSegment` components + wiring, reason-text objects, preview/reserved-indicator objects, board-scrim object + `CanvasGroup`s. *(Editor steps described in-task.)*

> **Palette-home decision:** `StatPalette` lives in `ArchonsRise.CardPlay`, not Assembly-CSharp, because the EditMode test assembly references `ArchonsRise.CardPlay` but cannot reference Assembly-CSharp. That assembly already implicitly references UnityEngine, so `UnityEngine.Color` is available there.

---

## Task 1: StatPalette pure palette (TDD)

**Files:**
- Create: `Assets/Scripts/CardPlay/StatPalette.cs`
- Test: `Assets/Tests/EditMode/StatPaletteTests.cs`

**Interfaces:**
- Produces:
  - `static Color StatPalette.For(StatType stat)` — Attack/Defend/Influence/Explore → their accent; any other flag → `Muted`.
  - `static readonly Color StatPalette.Empower` (cyan), `StatPalette.Muted`, `StatPalette.Locked`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/StatPaletteTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class StatPaletteTests
{
    [Test]
    public void EachActionStatHasADistinctColor()
    {
        var atk = StatPalette.For(StatType.Attack);
        var def = StatPalette.For(StatType.Defend);
        var inf = StatPalette.For(StatType.Influence);
        var exp = StatPalette.For(StatType.Explore);

        Assert.AreNotEqual(atk, def);
        Assert.AreNotEqual(atk, inf);
        Assert.AreNotEqual(atk, exp);
        Assert.AreNotEqual(def, inf);
        Assert.AreNotEqual(def, exp);
        Assert.AreNotEqual(inf, exp);
        Assert.AreNotEqual(atk, StatPalette.Empower);
    }

    [Test]
    public void AttackIsReddest_DefendIsViolet_ExploreIsGreenest()
    {
        var atk = StatPalette.For(StatType.Attack);
        Assert.Greater(atk.r, atk.g);          // red dominant
        Assert.Greater(atk.r, atk.b);

        var exp = StatPalette.For(StatType.Explore);
        Assert.Greater(exp.g, exp.r);          // green dominant
        Assert.Greater(exp.g, exp.b);

        var def = StatPalette.For(StatType.Defend);
        Assert.Greater(def.b, def.g);          // blue/violet over green
    }

    [Test]
    public void NonActionFlagsFallBackToMuted()
    {
        Assert.AreEqual(StatPalette.Muted, StatPalette.For(StatType.Wound));
        Assert.AreEqual(StatPalette.Muted, StatPalette.For(StatType.None));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode suite (Unity → Window → General → Test Runner → EditMode → Run All), or headless:

```bash
Unity -batchmode -projectPath "." -runTests -testPlatform EditMode -testResults "TestResults.xml" -quit
```

Expected: FAIL — `StatPalette` does not exist.

- [ ] **Step 3: Implement StatPalette**

Create `Assets/Scripts/CardPlay/StatPalette.cs`:

```csharp
using UnityEngine;

// Single source of truth for the locked stat-accent colors. Pure data, no scene
// dependency. Consumed by StatSegment / EmpowerPanel / PlayBar now and by the
// Phase-3b echo-flight later. Retune by editing the hex literals here.
public static class StatPalette
{
    // Action-stat accents (Color32 -> Color is implicit; values are the locked hex).
    static readonly Color Attack    = new Color32(0xff, 0x5a, 0x5a, 0xff); // red
    static readonly Color Defend    = new Color32(0xb0, 0x6b, 0xff, 0xff); // purple
    static readonly Color Influence = new Color32(0xff, 0xd2, 0x4d, 0xff); // yellow
    static readonly Color Explore   = new Color32(0x54, 0xd9, 0x8c, 0xff); // green

    public static readonly Color Empower = new Color32(0x5f, 0xd0, 0xe6, 0xff); // cyan "currency"
    public static readonly Color Muted   = new Color32(0xae, 0xb8, 0xcc, 0xff); // unselected base
    public static readonly Color Locked  = new Color32(0x39, 0x40, 0x5a, 0xff); // dim/locked tint

    public static Color For(StatType stat)
    {
        if (stat == StatType.Attack)    return Attack;
        if (stat == StatType.Defend)    return Defend;
        if (stat == StatType.Influence) return Influence;
        if (stat == StatType.Explore)   return Explore;
        return Muted;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode suite again. Expected: all `StatPaletteTests` PASS; existing `CardPlaySelectionTests` / `CardSnapshotTests` / `FanMathTests` remain green.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/CardPlay/StatPalette.cs" "Assets/Tests/EditMode/StatPaletteTests.cs"
git commit -m "feat: StatPalette single-source stat-accent colors with EditMode tests

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

> After Unity regenerates the `.meta` files for the new scripts, commit those too (`git add` picks them up once they exist).

---

## Task 2: CardPlaySelection.PreviewStats read-only query (TDD)

**Files:**
- Modify: `Assets/Scripts/CardPlay/CardPlaySelection.cs:65-87` (`ResolveStats`)
- Test: `Assets/Tests/EditMode/CardPlaySelectionTests.cs` (append)

**Interfaces:**
- Produces: `int[] CardPlaySelection.PreviewStats(bool empowered)` — the `[atk,def,inf,exp]` this selection would apply if its empower flag were `empowered`, **without** mutating the selection. `ResolveStats()` becomes `PreviewStats(EffectiveEmpowered())`.
- Consumed by: `EmpowerPanel` (Task 4) for the `+N → +N` preview.

This is an **additive read-only query**. Existing methods and their behavior are unchanged; the existing `ResolveStats` tests guard the refactor.

- [ ] **Step 1: Write the failing tests**

Append to `Assets/Tests/EditMode/CardPlaySelectionTests.cs` (inside the class):

```csharp
    // PreviewStats reports the would-be totals for a hypothetical empower flag,
    // independent of the live Empowered flag, and never mutates the selection.
    [Test]
    public void PreviewStats_ReportsBaseAndEmpoweredWithoutMutating()
    {
        var s = new CardPlaySelection(Strike());          // single Attack, base 2 / empower 3
        Assert.AreEqual(new[] { 2, 0, 0, 0 }, s.PreviewStats(false));
        Assert.AreEqual(new[] { 3, 0, 0, 0 }, s.PreviewStats(true));
        // unchanged: still not empowered, ResolveStats still base
        Assert.IsFalse(s.EffectiveEmpowered());
        Assert.AreEqual(new[] { 2, 0, 0, 0 }, s.ResolveStats());
    }

    [Test]
    public void PreviewStats_Choice_UsesChosenStatOnly()
    {
        var s = new CardPlaySelection(Rally());           // Attack|Influence choice, base 2/2 emp 4/4
        s.SetChoiceStat(StatType.Influence);
        Assert.AreEqual(new[] { 0, 0, 2, 0 }, s.PreviewStats(false));
        Assert.AreEqual(new[] { 0, 0, 4, 0 }, s.PreviewStats(true));
    }

    [Test]
    public void PreviewStats_Improvise_IgnoresEmpower()
    {
        var s = new CardPlaySelection(Rally());
        s.SetImproviseStat(StatType.Defend);
        Assert.AreEqual(new[] { 0, 1, 0, 0 }, s.PreviewStats(false));
        Assert.AreEqual(new[] { 0, 1, 0, 0 }, s.PreviewStats(true)); // flat +1 regardless
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode suite. Expected: FAIL — `PreviewStats` not defined.

- [ ] **Step 3: Refactor ResolveStats to delegate to PreviewStats**

In `Assets/Scripts/CardPlay/CardPlaySelection.cs`, replace the `ResolveStats()` method (lines ~65-87) with:

```csharp
    public int[] ResolveStats() => PreviewStats(EffectiveEmpowered());

    // Read-only preview: the [atk,def,inf,exp] this selection would apply if its
    // empower flag were `empowered`. Does not mutate. Improvise ignores `empowered`
    // (flat +1). Used live by ResolveStats and by the Empower panel's "+N -> +N".
    public int[] PreviewStats(bool empowered)
    {
        var result = new int[4]; // [attack, defend, influence, explore]

        switch (Mode)
        {
            case PlayMode.Improvise:
                AddStat(result, ImproviseStat, ImproviseValue);
                break;

            case PlayMode.Choice:
                AddStat(result, ChoiceStat, empowered ? _card.EmpowerOf(ChoiceStat) : _card.BaseOf(ChoiceStat));
                break;

            default: // Normal — every set action flag contributes
                foreach (var s in ActionStats)
                    if (_card.CardType.HasFlag(s))
                        AddStat(result, s, empowered ? _card.EmpowerOf(s) : _card.BaseOf(s));
                break;
        }
        return result;
    }
```

(The `emp` local is gone; the `empowered` parameter replaces it. `AddStat`, `ActionStats`, `ImproviseValue` are unchanged.)

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode suite. Expected: the three new `PreviewStats_*` tests PASS, **and every existing `CardPlaySelectionTests` test stays green** (the refactor preserves `ResolveStats` behavior).

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/CardPlay/CardPlaySelection.cs" "Assets/Tests/EditMode/CardPlaySelectionTests.cs"
git commit -m "feat: CardPlaySelection.PreviewStats read-only empower preview query

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: StatSegment + Choice/Improvise locked states

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/StatSegment.cs`
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ChoiceBanner.cs`
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ImprovisePanel.cs`
- Modify: `Assets/Prefabs/CardMenu*.prefab` (Unity Editor): add `StatSegment` to each option button, wire arrays + reason objects, apply glassy panel art.
- Test: Manual Play-mode verification.

**Interfaces:**
- Consumes: `StatPalette.For` / `Muted` / `Locked` (Task 1).
- Produces:
  - `StatSegment.State { Selected, Available, Locked }`
  - `StatSegment.Stat` (`StatType`), `StatSegment.Button` (`UnityEngine.UI.Button`), `StatSegment.SetState(State)`.

- [ ] **Step 1: Write StatSegment**

Create `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/StatSegment.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One Choice/Improvise option button. Renders Selected / Available / Locked from
// StatPalette so selection reads as colour + glow, never as a greyed-out button.
public class StatSegment : MonoBehaviour
{
    public enum State { Selected, Available, Locked }

    [SerializeField] StatType stat;
    [SerializeField] Image background;        // the segment fill
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Button button;
    [SerializeField] GameObject glow;         // optional outer-glow object, lit only when Selected

    public StatType Stat => stat;
    public Button Button => button;

    public void SetState(State state)
    {
        Color accent = StatPalette.For(stat);
        switch (state)
        {
            case State.Selected:
                background.color = accent;
                label.color = new Color(0.05f, 0.07f, 0.09f, 1f); // dark text on bright fill
                button.interactable = true;                       // clickable; selection shown by colour
                if (glow != null) glow.SetActive(true);
                break;

            case State.Available:
                background.color = new Color(accent.r, accent.g, accent.b, 0.14f);
                label.color = StatPalette.Muted;
                button.interactable = true;
                if (glow != null) glow.SetActive(false);
                break;

            case State.Locked:
                background.color = new Color(StatPalette.Locked.r, StatPalette.Locked.g, StatPalette.Locked.b, 0.40f);
                label.color = StatPalette.Locked;
                button.interactable = false;
                if (glow != null) glow.SetActive(false);
                break;
        }
    }
}
```

- [ ] **Step 2: Rewrite ChoiceBanner to drive segments**

Replace `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ChoiceBanner.cs` with:

```csharp
using UnityEngine;

// Shows one segment per set action-flag on a choice card. Selecting a segment sets
// the choice stat. Hidden for non-choice cards. While Improvise is active the banner
// stays visible but locks (dim + reason) and destroys no stored choice.
public class ChoiceBanner : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;          // the banner container to show/hide
    [SerializeField] StatSegment[] segments;   // Attack / Defend / Influence / Explore
    [SerializeField] GameObject lockedReason;  // "Locked while improvising"

    // Lifetime subscription (not per-enable): Render hides this banner by deactivating
    // its own GameObject (root == self) for non-choice cards. OnEnable/OnDisable would
    // let that SetActive(false) unsubscribe us with no way back. Awake/OnDestroy survive
    // self-deactivation.
    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        foreach (var seg in segments)
        {
            var captured = seg; // avoid closure-over-loop-var capturing the last element
            captured.Button.onClick.AddListener(() => inspector.ChooseStat(captured.Stat));
        }
    }

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool show = sel != null && card != null && card.cardSO.isChoice;
        root.SetActive(show);
        if (!show) return;

        bool locked = sel.Mode == PlayMode.Improvise;
        if (lockedReason != null) lockedReason.SetActive(locked);

        foreach (var seg in segments)
        {
            bool available = card.cardSO.cardType.HasFlag(seg.Stat);
            seg.gameObject.SetActive(available);
            if (!available) continue;

            if (locked)
                seg.SetState(StatSegment.State.Locked);
            else if (sel.Mode == PlayMode.Choice && sel.ChoiceStat == seg.Stat)
                seg.SetState(StatSegment.State.Selected);
            else
                seg.SetState(StatSegment.State.Available);
        }
    }
}
```

- [ ] **Step 3: Rewrite ImprovisePanel to drive segments**

Replace `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ImprovisePanel.cs` with:

```csharp
using UnityEngine;

// Four +1 stat options. Selecting one puts the selection into Improvise mode. Hidden
// for non-empowerable cards (Wounds / EmpowerType.None). While Empower is active the
// panel stays visible but locks (dim + reason) and destroys no stored improvise stat.
public class ImprovisePanel : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;
    [SerializeField] StatSegment[] segments;   // Attack / Defend / Influence / Explore
    [SerializeField] GameObject lockedReason;  // "Locked while empowered"

    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        foreach (var seg in segments)
        {
            var captured = seg;
            captured.Button.onClick.AddListener(() => inspector.ImproviseStat(captured.Stat));
        }
    }

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool canImprovise = sel != null && card != null
                            && card.cardSO.empowerType != EmpowerType.None;
        root.SetActive(canImprovise);
        if (!canImprovise) return;

        bool locked = sel.EffectiveEmpowered(); // empower chosen -> improvise locked
        if (lockedReason != null) lockedReason.SetActive(locked);

        foreach (var seg in segments)
        {
            if (locked)
                seg.SetState(StatSegment.State.Locked);
            else if (sel.Mode == PlayMode.Improvise && sel.ImproviseStat == seg.Stat)
                seg.SetState(StatSegment.State.Selected);
            else
                seg.SetState(StatSegment.State.Available);
        }
    }
}
```

- [ ] **Step 4: Editor — add StatSegment components, wire arrays, style panels**

Open the card-menu prefab (the one carrying `ChoiceBanner` / `ImprovisePanel`; find it via the `CardInspector` reference in `GameBoard.unity`, or `Assets/Prefabs/CardMenu*.prefab`). For each Choice and Improvise option button:
1. Add a **`StatSegment`** component. Assign its `stat`, `background` (the button's fill `Image`), `label` (the button's `TextMeshProUGUI`), `button` (the `Button`), and an optional `glow` child object.
2. On the **`ChoiceBanner`** component: assign `segments` (the four `StatSegment`s — Attack/Defend/Influence/Explore) and `lockedReason` (a "Locked while improvising" text object inside the banner). Keep `root` as today.
3. On the **`ImprovisePanel`** component: assign `segments` and `lockedReason` ("Locked while empowered"). Keep `root`.
4. Apply the **Crystalline Dark** look to both panels: glassy background `Image` (dark `rgba` fill ≈ `#1c243a` @ ~0.5 alpha, 1px accent border, soft outer glow), rounded corners sprite, readable TMP. (The per-segment colors come from `StatSegment` at runtime; the panel chrome is static art here.)

> The old per-stat `Button` fields (`attackButton`…`exploreButton`) are gone from both scripts. Unity will drop those serialized references when the component recompiles; re-wire via the `segments` array only.

- [ ] **Step 5: Manual Play-mode verification**

Enter Play mode and open cards from the fan:
1. **Choice card** (e.g. Rally / Attack|Influence): banner shows only Attack + Influence segments; the chosen one is colour-filled + glowing, the other muted. Clicking the other switches the highlight. Play label tracks it.
2. **Switch to Improvise** (click an improvise stat): the Choice banner stays visible but its segments go Locked/dim and "Locked while improvising" appears; switching back to a choice restores the prior selection (Phase-1 logic intact).
3. **Empowerable card, toggle Empower on**: the Improvise panel segments go Locked/dim with "Locked while empowered"; toggling Empower off restores them. Console clean.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/StatSegment.cs" "Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ChoiceBanner.cs" "Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ImprovisePanel.cs" "Assets/Prefabs"
git commit -m "feat: StatSegment renders choice/improvise selection by colour with locked states

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Empower preview + reserved indicator, Play bar styling

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/EmpowerPanel.cs`
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/PlayBar.cs` (label color only)
- Modify: `Assets/Prefabs/CardMenu*.prefab` (Unity Editor): cyan empower chrome, reserved-crystal indicator, preview text, Play-bar gradient.
- Test: Manual Play-mode verification.

**Interfaces:**
- Consumes: `CardPlaySelection.PreviewStats` (Task 2), `StatPalette` (Task 1), the existing `inspector.SetEmpowered` and `Crystal.SetReserved` reservation wiring (Phase 1).

- [ ] **Step 1: Add preview + reserved indicator + locked reason to EmpowerPanel**

Replace `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/EmpowerPanel.cs` with:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Toggles the empower flag. Locked (non-interactable) when CanEmpower() is false
// (Improvise active, or a non-empowerable card). Shows the cyan reserved indicator
// and a "+base -> +empowered" preview of the card's total output.
public class EmpowerPanel : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;
    [SerializeField] Toggle empowerToggle;
    [SerializeField] GameObject reservedIndicator; // cyan crystal mark, on when empower active
    [SerializeField] TextMeshProUGUI previewLabel; // "+2 -> +4"
    [SerializeField] GameObject lockedReason;      // "Locked while improvising"

    // Lifetime subscription, not per-enable (root == self). See ChoiceBanner for detail.
    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        empowerToggle.onValueChanged.AddListener(OnToggle);
    }

    void OnToggle(bool value)
    {
        if (_suppress) return; // ignore programmatic changes during Render
        inspector.SetEmpowered(value);
    }

    bool _suppress;

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool show = sel != null && card != null;
        root.SetActive(show);
        if (!show) return;

        bool can = sel.CanEmpower();
        empowerToggle.interactable = can;

        // Locked reason only when this is an empowerable card blocked by Improvise mode
        // (not for plain non-empowerable cards, where the whole panel reads inert).
        if (lockedReason != null)
            lockedReason.SetActive(!can && card.cardSO.empowerType != EmpowerType.None);

        _suppress = true;
        empowerToggle.isOn = sel.EffectiveEmpowered();
        _suppress = false;

        if (reservedIndicator != null)
            reservedIndicator.SetActive(sel.EffectiveEmpowered());

        if (previewLabel != null)
        {
            int baseTotal = Sum(sel.PreviewStats(false));
            int empTotal  = Sum(sel.PreviewStats(true));
            previewLabel.text = $"+{baseTotal} → +{empTotal}"; // "+2 -> +4"
            previewLabel.color = StatPalette.Empower;
        }
    }

    static int Sum(int[] stats)
    {
        int total = 0;
        foreach (var v in stats) total += v;
        return total;
    }
}
```

- [ ] **Step 2: Tint the Play label to the resolved stat**

In `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/PlayBar.cs`, extend `Render()` so the label picks up the resolved stat's accent (the bar gradient itself is editor art). Replace the `Render()` body:

```csharp
    void Render()
    {
        var sel = inspector.Selection;
        if (sel == null) return;
        bool playable = sel.IsPlayable();
        playButton.interactable = playable;
        playLabel.text = playable ? $"PLAY · {sel.Describe()}" : "Cannot play";
        playLabel.color = playable ? StatPalette.For(sel.ResolvedStat()) : StatPalette.Muted;
    }
```

- [ ] **Step 3: Editor — empower chrome, reserved indicator, preview, Play-bar gradient**

In the card-menu prefab:
1. **EmpowerPanel:** cyan panel chrome (border/glow `#5fd0e6`). Add a **reserved indicator** child (a small cyan crystal `Image`) and assign it to `reservedIndicator`. Add a **preview** `TextMeshProUGUI` and assign to `previewLabel`. Add a "Locked while improvising" text and assign to `lockedReason`.
2. **PlayBar:** give the bar background a horizontal **gradient** (an `Image` with a gradient sprite, or two-stop). Leave the `playButton`/`playLabel`/`backButton` wiring as is.

- [ ] **Step 4: Manual Play-mode verification**

Enter Play mode:
1. Empowerable card, toggle **Empower on** → cyan reserved indicator appears, a crystal in the inventory dims (existing `SetReserved`), preview reads e.g. `+2 → +4`, the card/Play label reflect the empowered value. Toggle **off** → reverts; reserved crystal restored.
2. Switch to **Improvise** → Empower toggle disables and "Locked while improvising" shows; no crystal gets reserved.
3. Non-empowerable card (e.g. `EmpowerType.None`) → Empower panel reads inert (toggle off/disabled), no locked-reason text.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/EmpowerPanel.cs" "Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/PlayBar.cs" "Assets/Prefabs"
git commit -m "feat: empower preview + cyan reserved indicator, play label stat tint

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Center float + board scrim + panel fade-in

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs:158-174` (`SetCardObjectToMax` / `SetCardObjectToNormal`; add `using DG.Tweening;`)
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs` (fade scrim + panels on Open)
- Modify: `Assets/Scenes/GameBoard.unity` / card-menu prefab (Unity Editor): board-scrim object + `CanvasGroup`s, wire on `CardInspector`.
- Test: Manual Play-mode verification.

**Interfaces:**
- Consumes: existing `GameManager.enlargeCardPosition`, `HandFanLayout.Container`, `PlayerHand.Relayout` (Phase 2), `CardMenuCanvas.OnCanvas/OffCanvas`.
- Produces: animated open/close; `CardInspector` gains serialized `boardScrim` / `popoutGroup` `CanvasGroup`s.

- [ ] **Step 1: Tween the maximize/minimize in Card.cs**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs`, add the DOTween import at the top (after the existing `using` lines):

```csharp
using DG.Tweening;
```

Replace `SetCardObjectToMax` and `SetCardObjectToNormal` (lines ~157-174) with:

```csharp
    //Maximizes the card: arcs it from its fan slot to the centre, scaling up.
    public void SetCardObjectToMax(Card card)
    {
        var t = card.gameObject.transform;
        t.SetParent(GameManager.Instance.enlargeCardPosition.transform, true); // keep world pos
        t.DOKill();
        Vector3 target = GameManager.Instance.enlargeCardPosition.transform.position;
        t.DOMove(target, 0.25f).SetEase(Ease.OutBack);
        t.DOScale(new Vector3(4f, 4f, 1f), 0.25f).SetEase(Ease.OutBack);
        t.DOLocalRotate(Vector3.zero, 0.25f).SetEase(Ease.OutBack); // upright at centre
        card.isMaximized = true;
    }

    //Returns the card to its fan slot (tweened) in the player hand.
    public void SetCardObjectToNormal(Card card)
    {
        var t = card.gameObject.transform;
        Vector3 fromWorld = t.position;

        var hand = GameManager.Instance.playerHand.GetComponentInChildren<HandFanLayout>();
        t.SetParent(hand.Container, false);
        card.isMaximized = false;
        GameManager.Instance.playerHand.GetComponent<PlayerHand>().Relayout(); // sets the slot pose

        // Capture the slot pose Relayout just applied, snap back to the centre, tween home.
        Vector3 toWorld = t.position;
        Vector3 toScale = t.localScale;
        Vector3 toLocalEuler = t.localEulerAngles;

        t.position = fromWorld;
        t.localScale = new Vector3(4f, 4f, 1f);
        t.DOKill();
        t.DOMove(toWorld, 0.22f).SetEase(Ease.OutCubic);
        t.DOScale(toScale, 0.22f).SetEase(Ease.OutCubic);
        t.DOLocalRotate(toLocalEuler, 0.22f).SetEase(Ease.OutCubic);
    }
```

> Note: `SetParent(..., true)` on maximize preserves world position so the move tween starts from the fan slot. On minimize we reparent first (so `Relayout` can compute the slot pose), capture that pose, then animate from the remembered centre position back to it.

- [ ] **Step 2: Fade the board scrim + panels in CardInspector**

In `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs`, add the import:

```csharp
using DG.Tweening;
```

Add serialized fields (in the existing field block, e.g. under the routing headers):

```csharp
    [Header("Phase 3a presentation")]
    [SerializeField] CanvasGroup boardScrim;   // full-screen dim behind the pop-out
    [SerializeField] CanvasGroup popoutGroup;  // CanvasGroup wrapping the four panels
    [SerializeField] float scrimAlpha = 0.6f;  // board dims to ~40% visible
    [SerializeField] float fadeTime = 0.2f;
```

In `Open(Card card)`, after `Menu?.OnCanvas();` add `FadeIn();`. In `Close()`, after `Menu?.OffCanvas();` add `SnapClosed();`. Add the two helpers to the class:

```csharp
    void FadeIn()
    {
        if (boardScrim != null)
        {
            boardScrim.DOKill();
            boardScrim.alpha = 0f;
            boardScrim.DOFade(scrimAlpha, fadeTime);
        }
        if (popoutGroup != null)
        {
            popoutGroup.DOKill();
            popoutGroup.alpha = 0f;
            popoutGroup.DOFade(1f, fadeTime);
        }
    }

    // Close is synchronous (Play()/undo rely on it), so the scrim + panels clear
    // instantly; the card's tween back to its fan slot carries the closing motion.
    void SnapClosed()
    {
        if (boardScrim != null) { boardScrim.DOKill(); boardScrim.alpha = 0f; }
        if (popoutGroup != null) { popoutGroup.DOKill(); popoutGroup.alpha = 0f; }
    }
```

- [ ] **Step 3: Editor — scrim object + CanvasGroups, wire on CardInspector**

In `GameBoard.unity` / the card-menu prefab:
1. Add a full-screen **board scrim**: a dark `Image` (black, alpha driven by code) under the card-menu canvas, ordered **behind** the pop-out card and panels but in front of the board. Add a **`CanvasGroup`** to it; set starting alpha 0.
2. Add a **`CanvasGroup`** to the container that wraps the four section panels (the pop-out group). Starting alpha 0.
3. On the **`CardInspector`** component, assign `boardScrim` and `popoutGroup` to those two `CanvasGroup`s. Leave `scrimAlpha`/`fadeTime` at defaults.
4. Confirm the scrim does **not** block the card or panel raycasts in a way that breaks clicks (the pop-out panels/card sit above it; the scrim may keep `Blocks Raycasts` to swallow clicks on the dimmed board, which is desirable).

- [ ] **Step 4: Manual Play-mode verification**

Enter Play mode and exercise all three open/close entry points (the shared maximize/minimize path is the main regression risk):
1. **Click a fan card** → it arcs from its slot to centre, scaling up over ~0.25s; the board dims (~40%); the four panels fade in.
2. **Back** (or click the maximized card) → panels/scrim clear and the card tweens back into its fan slot at the correct tilt/scale; the fan re-balances. No stacked/duplicated cards.
3. **Play a card** → `Close()` runs, then `MinimizeAfterPlay()` returns the card to the fan (marked played); it cannot be reopened to re-trigger Play.
4. **Spam** click/Back rapidly → no tween stacking, no card stuck at centre or wrong scale (the `DOKill()` guards hold).

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs" "Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs" "Assets/Scenes/GameBoard.unity" "Assets/Prefabs"
git commit -m "feat: animate card float to centre with board dim and panel fade-in

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage (Phase 3a = "center float + wrap-panel styling, Crystalline Dark, full art direction"):**
- Shared stat-accent palette (locked colors) → Task 1 (`StatPalette` + tests). ✅
- `+2 → +4` empower preview needs base-vs-empowered without mutating → Task 2 (`PreviewStats`, TDD). ✅
- Selected/available/locked read by colour, not greying; locked sections dim + reason, destroy no state → Task 3 (`StatSegment`, Choice/Improvise locked states). ✅
- Empower cyan chrome + reserved indicator + preview; Play bar accent → Task 4. ✅
- Center float (arc to centre, ~0.25s ease-out-back), board dim ~40%, panel fade-in, reverse on Back/commit → Task 5. ✅
- Out of scope honored: no juice / `StatsDisplay` untouched / no gamepad (Global Constraints; Task 5 keeps Close synchronous, no echo/stat-pop). ✅

**Placeholder scan:** No "TBD"/"handle edge cases"/"write tests for the above" — every code step shows full code; every editor step lists concrete components and assignments. ✅

**Type consistency:** `StatPalette.For(StatType)→Color`, `.Empower/.Muted/.Locked`; `CardPlaySelection.PreviewStats(bool)→int[]`, `ResolveStats()=PreviewStats(EffectiveEmpowered())`; `StatSegment.State{Selected,Available,Locked}`, `.Stat`, `.Button`, `.SetState(State)`; `ChoiceBanner.segments`/`lockedReason`, `ImprovisePanel.segments`/`lockedReason`; `EmpowerPanel.reservedIndicator`/`previewLabel`/`lockedReason`; `CardInspector.boardScrim`/`popoutGroup`/`scrimAlpha`/`fadeTime` — names consistent across tasks. ✅

**Ordering / dependency check:** Task 3 & 4 consume Task 1; Task 4 consumes Task 2; Task 5 is independent of 3/4. Tasks 1-2 are pure-logic TDD; 3-5 are editor + manual verification (consistent with Phase 2). ✅

**Known caveat carried forward:** Close is synchronous, so the scrim/panels clear instantly rather than fading out (the card's return tween carries the motion). A fully-animated close can come as a later polish pass if desired. Board dim is alpha-based (the Phase-2 brightness caveat).
