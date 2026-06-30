# Card-Play Rework Phase 3b — Play-Commit Juice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a card play *land* — a colour-coded floating "+N" flies from the played card to its stat, the stat number pops and counts (up on play, down on undo), and an empowered card's reserved crystal drains toward the card as it's spent — all without touching game logic.

**Architecture:** Three additive presentation layers (per the 3b design). `StatsDisplay` stops rewriting text every frame and becomes a self-observing animator (count + punch-scale + colour flash) so it animates **up on play and down on undo for free**. A pure `StatEchoPlan` (in the EditMode-tested `ArchonsRise.CardPlay` assembly) turns a `[atk,def,inf,exp]` array into one `StatAmount` per boosted stat; a small `StatEchoes` MonoBehaviour spawns the floating labels, driven by `CardInspector.Play()`. The crystal-spend flourish hangs off the **existing** `CrystalInventory.EmpowerCrystal()`/`RegenCrystal()` consume/regen path. No command, `Player`, or selection logic changes.

**Tech Stack:** Unity 6 (Assembly-CSharp + asmdefs), C#, DOTween (vendored at `Assets/Plugins/Demigiant`, namespace `DG.Tweening`, already used in `Card`/`CardInspector`/`PlayerHand`), TextMeshPro, NUnit EditMode tests (`ArchonsRise.Tests.EditMode`).

## Global Constraints

- **Presentation only.** Do not change `CardPlaySelection`'s behavior, `CardInspector`'s routing to `CardEvent`/`PlayCommand`, `PlayCommand`/`PlayManager`, the crystal consume/restore *bookkeeping* (`playedCrystals` push/pop, `crystalsInInventory` add/remove, `RemoveCrystal`), or any `Player` stat method. 3b only animates *around* this logic. (Spec: "It adds visual hooks alongside the existing logic.")
- **Echo fidelity = floating "+N" only.** No card-ghost/clone. One label per boosted stat, in that stat's accent colour.
- **Undo feedback = stat count-down only.** No reverse "-N" flight. The stat-pop observer animates the number down on undo because it only watches the value. The crystal flourish still reverses (regen event).
- **Out of scope this pass:** the commit sweep to discard (deferred); reverse "-N" flight; card-ghost echo; gamepad. `DiscardPile.AddCardToDiscard` keeps its instant-deactivate behavior.
- **Colour source:** the existing `StatPalette.For(StatType)` (3a) is the single source of truth for echo-label colour and the stat flash. Attack `#ff5a5a` · Defend `#b06bff` · Influence `#ffd24d` · Explore `#54d98c`.
- **Stat array order is `[attack, defend, influence, explore]`** everywhere (matches `CardPlaySelection.PreviewStats` and `StatType` action order: Attack, Defend, Influence, Explore).
- **DOTween hygiene:** `DOKill()` the target (transform / CanvasGroup) before starting a new tween on it, and snap the cached value, so rapid play/undo can't stack tweens or desync a number from `Player`.
- **Avoid the DOTween TMPro module:** the vendored DOTween may not include TMP shortcuts (`TextMeshProUGUI.DOColor`/`DOFade`). Drive text colour via a `DOTween.To` float callback and label opacity via a `CanvasGroup`, never `tmp.DOColor`/`tmp.DOFade`.
- Each commit message ends with the project trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

---

## File Structure

- `Assets/Scripts/CardPlay/StatEchoPlan.cs` — **new** pure helper + `StatAmount` struct (in `ArchonsRise.CardPlay` so EditMode tests reference it). Maps `int[4]` → one `StatAmount` per non-zero entry. EditMode-tested.
- `Assets/Tests/EditMode/StatEchoPlanTests.cs` — **new** EditMode tests for `StatEchoPlan`.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/StatsDisplay.cs` — **modify** from per-frame rewrite to a self-observing count/pop/flash animator; add `AnchorFor(StatType)`.
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/StatEchoes.cs` — **new** MonoBehaviour: `Emit(origin, stat, amount)` spawns a floating "+N" that flies to the stat anchor and fades.
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs` — **modify** `Play()` to emit one echo per boosted stat (serialized `StatEchoes echoes`).
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/Crystal.cs` — **modify**: add `FlySpendThenHide(worldTarget)` and `PopIn()` (add `using DG.Tweening;`).
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs` — **modify** `EmpowerCrystal()`/`RegenCrystal()` to animate around the unchanged bookkeeping.
- `Assets/Prefabs/StatEcho.prefab` *(new, Unity Editor)* — the floating "+N" label (TMP + `CanvasGroup`).
- `Assets/Scenes/GameBoard.unity` / card-menu prefab *(Unity Editor)* — add the `StatEchoes` object, wire `stats`/`labelPrefab`/`container`; wire `CardInspector.echoes`; assign `StatsDisplay.defaultColor`. *(Editor steps described in-task.)*

> **Coordinate assumption:** the game's UI uses Screen-Space-Overlay canvases, so `transform.position` is shared screen-pixel space across canvases — the card centre, the echo label, and the stat anchors are directly comparable. The editor step parents the echo container under the StatsDisplay canvas; if any canvas is Screen-Space-Camera, convert via that canvas's camera (flagged in the manual-verify step).

---

## Task 1: StatEchoPlan pure helper (TDD)

**Files:**
- Create: `Assets/Scripts/CardPlay/StatEchoPlan.cs`
- Test: `Assets/Tests/EditMode/StatEchoPlanTests.cs`

**Interfaces:**
- Produces:
  - `struct StatAmount { StatType Stat; int Amount; StatAmount(StatType, int); }`
  - `static List<StatAmount> StatEchoPlan.NonZero(int[] stats)` — one `StatAmount` per non-zero entry of `[atk,def,inf,exp]`, in stat order (Attack, Defend, Influence, Explore). Null/short arrays → empty list.
- Consumed by: `CardInspector.Play()` (Task 3) to know which labels to emit.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/StatEchoPlanTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class StatEchoPlanTests
{
    [Test]
    public void AllZero_ProducesNoEchoes()
    {
        Assert.IsEmpty(StatEchoPlan.NonZero(new[] { 0, 0, 0, 0 }));
    }

    [Test]
    public void SingleStat_ProducesOneEchoWithStatAndAmount()
    {
        var plan = StatEchoPlan.NonZero(new[] { 0, 0, 3, 0 }); // influence = index 2
        Assert.AreEqual(1, plan.Count);
        Assert.AreEqual(StatType.Influence, plan[0].Stat);
        Assert.AreEqual(3, plan[0].Amount);
    }

    [Test]
    public void MultiStat_ProducesOnePerNonZero_InStatOrder()
    {
        var plan = StatEchoPlan.NonZero(new[] { 2, 0, 0, 5 }); // attack=2, explore=5
        Assert.AreEqual(2, plan.Count);
        Assert.AreEqual(StatType.Attack,  plan[0].Stat);
        Assert.AreEqual(2,                plan[0].Amount);
        Assert.AreEqual(StatType.Explore, plan[1].Stat);
        Assert.AreEqual(5,                plan[1].Amount);
    }

    [Test]
    public void NullArray_ProducesNoEchoes()
    {
        Assert.IsEmpty(StatEchoPlan.NonZero(null));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode suite (Unity → Window → General → Test Runner → EditMode → Run All), or headless:

```bash
Unity -batchmode -projectPath "." -runTests -testPlatform EditMode -testResults "TestResults.xml" -quit
```

Expected: FAIL — `StatEchoPlan` / `StatAmount` do not exist.

- [ ] **Step 3: Implement StatEchoPlan**

Create `Assets/Scripts/CardPlay/StatEchoPlan.cs`:

```csharp
using System.Collections.Generic;

// One boosted stat to echo: which stat, and how much it changed.
public readonly struct StatAmount
{
    public readonly StatType Stat;
    public readonly int Amount;
    public StatAmount(StatType stat, int amount) { Stat = stat; Amount = amount; }
}

// Turns a [attack, defend, influence, explore] stat array (as produced by
// CardPlaySelection.PreviewStats) into one StatAmount per non-zero entry, in stat
// order. Drives one floating "+N" label per boosted stat. Pure, no scene dependency.
public static class StatEchoPlan
{
    static readonly StatType[] Order =
        { StatType.Attack, StatType.Defend, StatType.Influence, StatType.Explore };

    public static List<StatAmount> NonZero(int[] stats)
    {
        var result = new List<StatAmount>();
        if (stats == null) return result;
        for (int i = 0; i < Order.Length && i < stats.Length; i++)
            if (stats[i] != 0) result.Add(new StatAmount(Order[i], stats[i]));
        return result;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode suite again. Expected: all four `StatEchoPlanTests` PASS; existing `StatPaletteTests` / `CardPlaySelectionTests` / `CardSnapshotTests` / `FanMathTests` stay green.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/CardPlay/StatEchoPlan.cs" "Assets/Tests/EditMode/StatEchoPlanTests.cs"
git commit -m "feat: StatEchoPlan maps stat array to per-stat echo amounts with EditMode tests

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

> After Unity regenerates the `.meta` files for the new scripts, commit those too.

---

## Task 2: StatsDisplay count / pop / flash (Layer 2)

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/StatsDisplay.cs` (whole file)
- Modify: `Assets/Scenes/GameBoard.unity` (Unity Editor): assign `defaultColor` on the StatsDisplay component.
- Test: Manual Play-mode verification.

**Interfaces:**
- Consumes: `StatPalette.For(StatType)` (3a), `Player.PlayerAttack/PlayerDefend/PlayerInfluence/PlayerExplore`.
- Produces: `Transform StatsDisplay.AnchorFor(StatType stat)` — the UI transform of that stat's number, used by `StatEchoes` (Task 3) as the flight target. Returns `null` for non-action stats.

This converts the per-frame text rewrite into change-detection that animates old→new. Because it only watches the `Player` value, the **same path animates up on play and down on undo** — that is the entire undo feedback (Global Constraints: count-down only).

- [ ] **Step 1: Replace StatsDisplay**

Replace the whole of `Assets/Scripts/GameObjectScripts/PlayerScripts/StatsDisplay.cs` with:

```csharp
using TMPro;
using UnityEngine;
using DG.Tweening;

// Watches the four Player stats and animates each number from its old value to its
// new one (count + punch-scale + colour flash) whenever it changes. Because it only
// observes the value, it counts UP on play and DOWN on undo with no command hook.
public class StatsDisplay : MonoBehaviour
{
    [SerializeField] Player player;
    [SerializeField] TextMeshProUGUI attackText;
    [SerializeField] TextMeshProUGUI defendText;
    [SerializeField] TextMeshProUGUI influenceText;
    [SerializeField] TextMeshProUGUI exploreText;
    [SerializeField] Color defaultColor = Color.white;
    [SerializeField] float animTime = 0.35f;

    int _atk, _def, _inf, _exp;
    Tween _atkT, _defT, _infT, _expT;

    void Start()
    {
        // Seed caches and labels without animating the initial values.
        _atk = player.PlayerAttack;    attackText.text    = _atk.ToString();
        _def = player.PlayerDefend;    defendText.text    = _def.ToString();
        _inf = player.PlayerInfluence; influenceText.text = _inf.ToString();
        _exp = player.PlayerExplore;   exploreText.text   = _exp.ToString();
    }

    void Update()
    {
        if (player.PlayerAttack    != _atk) Animate(attackText,    ref _atk, ref _atkT, player.PlayerAttack,    StatType.Attack);
        if (player.PlayerDefend    != _def) Animate(defendText,    ref _def, ref _defT, player.PlayerDefend,    StatType.Defend);
        if (player.PlayerInfluence != _inf) Animate(influenceText, ref _inf, ref _infT, player.PlayerInfluence, StatType.Influence);
        if (player.PlayerExplore   != _exp) Animate(exploreText,   ref _exp, ref _expT, player.PlayerExplore,   StatType.Explore);
    }

    void Animate(TextMeshProUGUI label, ref int cached, ref Tween handle, int newValue, StatType stat)
    {
        int from = cached;
        cached = newValue;                 // snap the cache immediately so we never re-trigger
        handle?.Kill();                    // kill any in-flight count for this stat

        // Count old -> new via a 0..1 progress float (avoids relying on a DOTween int plugin).
        handle = DOTween.To(() => 0f, p =>
        {
            label.text = Mathf.RoundToInt(Mathf.Lerp(from, newValue, p)).ToString();
        }, 1f, animTime).OnComplete(() => label.text = newValue.ToString());

        // Punch the number and flash it to the stat accent, then settle to default.
        label.transform.DOKill();
        label.transform.localScale = Vector3.one;
        label.transform.DOPunchScale(Vector3.one * 0.35f, animTime, 6, 0.6f);

        Color accent = StatPalette.For(stat);
        label.color = accent;
        DOTween.To(() => 0f, f => label.color = Color.Lerp(accent, defaultColor, f), 1f, animTime)
               .SetId(label);             // id so we can guarantee a clean colour each change
    }

    // The UI transform of a stat's number, used by StatEchoes as the flight target.
    public Transform AnchorFor(StatType stat)
    {
        if (stat == StatType.Attack)    return attackText.transform;
        if (stat == StatType.Defend)    return defendText.transform;
        if (stat == StatType.Influence) return influenceText.transform;
        if (stat == StatType.Explore)   return exploreText.transform;
        return null;
    }
}
```

- [ ] **Step 2: Editor — assign defaultColor**

In `GameBoard.unity`, select the StatsDisplay object. The four `…Text` fields and `player` stay wired as today. Set **Default Color** to the colour the stat numbers normally show (match the current TMP label colour — likely white). Leave `animTime` at `0.35`.

- [ ] **Step 3: Manual Play-mode verification**

Enter Play mode:
1. Play a single-stat card → that number **counts up**, punches, and flashes its accent colour (Attack red, etc.), then settles to default. The other three are unchanged.
2. Press **Undo** → the number **counts down** with the same punch/flash. No "-N" label (that's correct; it's Task 3 that flies on play only).
3. Spam play/undo on one card rapidly → the number always ends matching the real `Player` value (no desync, no stuck count). Console clean.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/PlayerScripts/StatsDisplay.cs" "Assets/Scenes/GameBoard.unity"
git commit -m "feat: StatsDisplay counts + pops + flashes on stat change (up on play, down on undo)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: StatEchoes floating "+N" + CardInspector wiring (Layer 1)

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/CardMenuScripts/StatEchoes.cs`
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs` (`Play()` + one serialized field)
- Create: `Assets/Prefabs/StatEcho.prefab` (Unity Editor)
- Modify: `Assets/Scenes/GameBoard.unity` (Unity Editor): add the `StatEchoes` object, wire it, wire `CardInspector.echoes`.
- Test: Manual Play-mode verification.

**Interfaces:**
- Consumes: `StatEchoPlan.NonZero` (Task 1), `StatsDisplay.AnchorFor` (Task 2), `StatPalette.For` (3a), `CardPlaySelection.PreviewStats`/`EffectiveEmpowered` (existing).
- Produces: `void StatEchoes.Emit(Vector3 originWorld, StatType stat, int amount)`.

- [ ] **Step 1: Write StatEchoes**

Create `Assets/Scripts/GameObjectScripts/CardMenuScripts/StatEchoes.cs`:

```csharp
using TMPro;
using UnityEngine;
using DG.Tweening;

// Spawns a colour-coded floating "+N" that flies from a played card to its stat
// number and fades. Play-only feedback; undo relies on StatsDisplay's count-down.
public class StatEchoes : MonoBehaviour
{
    [SerializeField] StatsDisplay stats;      // for AnchorFor(stat)
    [SerializeField] GameObject labelPrefab;  // StatEcho.prefab: TMP + CanvasGroup
    [SerializeField] Transform container;     // parent for spawned labels (top overlay canvas)
    [SerializeField] float flightTime = 0.45f;

    public void Emit(Vector3 originWorld, StatType stat, int amount)
    {
        if (amount == 0 || labelPrefab == null || stats == null) return;

        var go = Instantiate(labelPrefab, container != null ? container : transform);
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        var cg = go.GetComponent<CanvasGroup>();

        if (tmp != null)
        {
            tmp.text = (amount > 0 ? "+" : "") + amount;
            tmp.color = StatPalette.For(stat);
        }

        var t = go.transform;
        t.position = originWorld;

        Transform anchor = stats.AnchorFor(stat);
        Vector3 dest = anchor != null ? anchor.position : originWorld;

        t.DOMove(dest, flightTime).SetEase(Ease.InOutQuad);
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.DOFade(0f, flightTime).SetEase(Ease.InQuad);
        }
        Destroy(go, flightTime + 0.05f);
    }
}
```

- [ ] **Step 2: Wire CardInspector.Play to emit echoes**

In `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs`, add a serialized field in the "Phase 3a presentation" header block (or a new `[Header("Phase 3b juice")]`):

```csharp
    [Header("Phase 3b juice")]
    [SerializeField] StatEchoes echoes;
```

Replace the `Play()` method with (the only additions are capturing `origin`/`applied` before the command and the `foreach` emit after it — routing and `Close()` are unchanged):

```csharp
    public void Play()
    {
        if (Selection == null || !Selection.IsPlayable()) return;
        Card.IsEmpowered = Selection.EffectiveEmpowered();
        var evt = EventFor(Selection);
        if (evt == null) return;

        // Capture before the command/Close: the card is at centre and Selection is live.
        Vector3 origin = Card.transform.position;
        var applied = Selection.PreviewStats(Selection.EffectiveEmpowered());

        GameManager.Instance.commands.AddCommand(new PlayCommand(evt, Card));
        _reserved = null; // ownership passes to the real consume/undo path

        // Fire one "+N" per boosted stat (after Execute() so the crystal-spend flourish
        // leads the stat echo). Echoes are play-only; undo shows the stat count-down.
        if (echoes != null)
            foreach (var e in StatEchoPlan.NonZero(applied))
                echoes.Emit(origin, e.Stat, e.Amount);

        // Dismiss the menu so PLAY can't be clicked again; Close() returns the card to the hand.
        Close();
    }
```

- [ ] **Step 3: Editor — create the StatEcho label prefab**

Create `Assets/Prefabs/StatEcho.prefab`:
1. Under any canvas, create a UI object `StatEcho` with a **`TextMeshProUGUI`** (large bold, centred, default text "+0"; raycast target off).
2. Add a **`CanvasGroup`** to the root `StatEcho` object (alpha 1).
3. Drag it into `Assets/Prefabs/` to make the prefab, then delete the scene instance.

- [ ] **Step 4: Editor — add and wire the StatEchoes object**

In `GameBoard.unity`:
1. Add an empty `StatEchoes` GameObject **under the top-most overlay canvas** (the one that renders above the board and stats — the card-menu canvas is fine). Add the **`StatEchoes`** component.
2. Assign `stats` = the StatsDisplay component, `labelPrefab` = `StatEcho.prefab`, `container` = a child RectTransform of the StatsDisplay's canvas (so spawned labels share the stats' coordinate space). Leave `flightTime` at default.
3. Select the **CardInspector** object and assign its new `echoes` field to this `StatEchoes` component.

- [ ] **Step 5: Manual Play-mode verification**

Enter Play mode:
1. **Single-stat card** → on PLAY a "+N" in the stat's colour flies from the card's centre to the matching stat number and fades; the stat counts up (Task 2). Card returns to the fan (3a).
2. **Multi-stat or choice card** → one coloured "+N" per boosted stat, each landing on the correct stat number (Choice emits one for the chosen stat; a multi-flag Normal card emits one per flag).
3. **Empowered card** → the "+N" shows the empowered total (e.g. "+4" not "+2").
4. **Undo** → no "+N" or "-N" flies (correct); only the count-down from Task 2.
5. Confirm labels land *on* the numbers. If they're offset (a Screen-Space-Camera canvas), reparent `container` under the stats canvas / convert via its camera, then re-verify.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/CardMenuScripts/StatEchoes.cs" "Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs" "Assets/Prefabs/StatEcho.prefab" "Assets/Scenes/GameBoard.unity"
git commit -m "feat: floating +N stat echo flies from played card to its stat on play

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Crystal-spend flourish (Layer 3)

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/Crystal.cs` (add `using DG.Tweening;` + two methods)
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs` (`EmpowerCrystal()`/`RegenCrystal()`)
- Test: Manual Play-mode verification.

**Interfaces:**
- Consumes: `CardInspector`/`Player` consume path — `onEmpower_DestroyCrystalGameObject → CrystalInventory.EmpowerCrystal()` (play) and `onUndo_RegenerateCrystalGameObject → CrystalInventory.RegenCrystal()` (undo), both already wired on `Crystals.prefab`.
- Produces:
  - `void Crystal.FlySpendThenHide(Vector3 worldTarget)` — drains the crystal toward the card, then hides it (restoring its local pose so regen shows it in place).
  - `void Crystal.PopIn()` — scales the crystal back in on regen.

The **bookkeeping is unchanged** (`playedCrystals` push/pop, `crystalsInInventory` add/remove, `SetActive`): the flourish only animates around it. The played card's centre is `_card.transform.position` (set via `SetCard` in 3a's empower flow; the card is still at centre when `EmpowerCrystal()` runs inside `PlayCommand.Execute()`, before `Close()`).

- [ ] **Step 1: Add the flourish methods to Crystal**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/Crystal.cs`, add the import at the top:

```csharp
using DG.Tweening;
```

Add these two methods to the class (e.g. just after `SetReserved`):

```csharp
    // Play flourish: drain toward the played card, then hide. Restores the original
    // local pose on complete so a later RegenCrystal shows the crystal back in its slot.
    public void FlySpendThenHide(Vector3 worldTarget)
    {
        var t = transform;
        Vector3 homePos = t.localPosition;
        Vector3 homeScale = t.localScale;
        t.DOKill();
        t.DOMove(worldTarget, 0.3f).SetEase(Ease.InBack);
        t.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack)
         .OnComplete(() =>
         {
             t.localPosition = homePos;
             t.localScale = homeScale;
             gameObject.SetActive(false);
         });
    }

    // Undo flourish: pop the regenerated crystal back in at its slot.
    public void PopIn()
    {
        var t = transform;
        t.DOKill();
        Vector3 homeScale = t.localScale == Vector3.zero ? Vector3.one : t.localScale;
        t.localScale = Vector3.zero;
        t.DOScale(homeScale, 0.25f).SetEase(Ease.OutBack);
    }
```

- [ ] **Step 2: Animate around the bookkeeping in CrystalInventory**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs`, replace `EmpowerCrystal()`:

```csharp
    public void EmpowerCrystal()
    {
        var crystal = SelectEmpowerCrystal();
        if (crystal is null)
            crystal = FindAnyObjectByType<AllCrystal>();

        playedCrystals.Push(crystal);
        Debug.Log(crystal.color.ToString());

        // Same removal as RemoveCrystal() (list remove + deactivate), but the deactivate
        // is deferred to the end of the drain flourish toward the played card.
        crystalsInInventory.Remove(crystal);
        Vector3 target = _card != null ? _card.transform.position : crystal.transform.position;
        crystal.FlySpendThenHide(target);
    }
```

Replace `RegenCrystal()`:

```csharp
    public void RegenCrystal()
    {
        var crystal = playedCrystals.Pop();
        crystal.gameObject.SetActive(true);
        crystalsInInventory.Add(crystal);
        crystal.PopIn();
        Debug.Log(crystal.color);
    }
```

> Note: this swaps the single `crystal.RemoveCrystal()` call in `EmpowerCrystal` for the explicit `crystalsInInventory.Remove(crystal)` + animated hide — identical state, deferred deactivate. `RemoveCrystal()` itself is untouched (still used by `Crystallize`, `OnPointerClick`).

- [ ] **Step 3: Manual Play-mode verification**

Enter Play mode with at least one matching crystal in the inventory:
1. **Empower** a card (toggle on → 3a reserved dim), then **PLAY** → the reserved crystal drains/shrinks toward the played card and disappears, just before the "+N" flies (Task 3). Inventory count drops by one.
2. **Undo** → the crystal **pops back in** at its inventory slot; count restored. The stat counts down (Task 2).
3. Play + commit (End Turn / spend influence) → crystal stays spent (no regen); console clean.
4. Spam empower-play / undo rapidly → crystal never ends up stuck shrunk, mis-placed, or double-counted (the `DOKill()` guards + restored local pose hold).

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/Crystal.cs" "Assets/Scripts/GameObjectScripts/GameBoardObjects/CrystalInventory.cs"
git commit -m "feat: crystal-spend flourish drains to card on play, pops back on undo

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage (3b = "play-commit juice: echo +N, stat pop/count, crystal flourish"):**
- Floating "+N" echo, one per boosted stat, stat-coloured, play-only → Task 1 (`StatEchoPlan`, TDD) + Task 3 (`StatEchoes` + `Play()` wiring). ✅
- Stat pop / count-up, up on play and **down on undo for free** → Task 2 (`StatsDisplay` self-observing animator). ✅
- Crystal-spend flourish on play, reverse on undo, via the existing consume/regen events → Task 4. ✅
- Colour from existing `StatPalette` → Tasks 2 & 3 use `StatPalette.For`. ✅
- Out-of-scope honored: no commit sweep (`DiscardPile` untouched), no reverse "-N" flight (Task 3 emits on play only), no card-ghost, no command/`Player`/selection changes (Task 4 keeps bookkeeping; `Play()` only adds emits). ✅

**Placeholder scan:** No "TBD"/"handle edge cases"/"write tests for the above" — Task 1 shows full code + tests; Tasks 2–4 show full code; editor steps list concrete objects and assignments. ✅

**Type consistency:** `StatAmount { Stat, Amount }` and `StatEchoPlan.NonZero(int[]) → List<StatAmount>` (Task 1) consumed verbatim in Task 3's `foreach (var e in StatEchoPlan.NonZero(applied)) echoes.Emit(origin, e.Stat, e.Amount)`. `StatsDisplay.AnchorFor(StatType) → Transform` (Task 2) consumed by `StatEchoes.Emit` (Task 3). `StatEchoes.Emit(Vector3, StatType, int)` defined Task 3 Step 1, called Task 3 Step 2. `Crystal.FlySpendThenHide(Vector3)`/`PopIn()` defined Task 4 Step 1, called Task 4 Step 2. `StatPalette.For`, `PreviewStats`, `EffectiveEmpowered` match existing signatures. ✅

**Ordering / dependency check:** Task 3 consumes Tasks 1 & 2; Task 4 is independent (consumes only existing events). Task 1 is pure-logic TDD; Tasks 2–4 are code + manual verification (consistent with 3a). ✅

**Known caveats carried forward:** Echo/flourish positioning assumes Screen-Space-Overlay shared coordinates; Task 3 Step 5 flags the camera-canvas fallback. The flourish defers the crystal's `SetActive(false)` to a tween callback — if the same crystal object were spent again before the tween completed it would already be out of `crystalsInInventory`, so it can't be re-selected; the `DOKill()` + restored local pose keep a mid-flight undo correct.
