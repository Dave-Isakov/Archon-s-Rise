# Player Log & Toast Rail — Implementation Plan (Plan 3 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retire the blocking message canvas. Informational events become non-blocking corner toasts plus entries in an openable history, so a player never has to click to get back to play.

**Architecture:** A pure `PlayerLogCore` ring buffer (cap 100, newest-first, day dividers derived at render time) behind a lazily-created `GameLog` scene singleton. `GameLog.Post` appends and hands the text to a `ToastRail` that fades toasts out on its own. `GameManager.ValidationMessage` becomes a forwarder, then its 36 call sites are renamed mechanically and the canvas plus its five input guards are deleted.

**Tech Stack:** Unity 6000.5.1f1, C# (Mono/mcs for pure tests), NUnit, TextMeshPro, DOTween (already used for UI fades), Unity UI (uGUI).

Spec: `docs/superpowers/specs/2026-07-28-minimal-place-ui-and-player-log-design.md` (Section 4)
Independent of Plans 1 and 2 — can run before, after, or alongside them.

## Global Constraints

- **Never hand-edit scene or prefab YAML.** Wiring steps produce editor instructions for the user and stop.
- **Pure classes are tested from the CLI** with `tools/pure-tests/run.sh`, not batch-mode Unity.
- **A new pure folder needs its own asmdef AND a reference added to `ArchonsRise.Tests.EditMode`**, or the EditMode tests fail with `CS0103` even though the game compiles.
- **The routing rule has no exceptions:** anything that needs a decision stays a modal; anything that merely informs becomes a toast. All 36 `ValidationMessage` call sites demote.
- **The log is not saved.** No save-schema bump, no migrator, no migrator-test changes. If any step seems to need one, stop — it means the design was misread.
- **Toasts must never block input:** the rail's `CanvasGroup` sets `blocksRaycasts = false` and its canvas sorts above everything, so a toast can float over a card-pick modal without eating a click.
- Commit after every task.

---

## File Structure

**Pure layer — `Assets/Scripts/Log/` (new folder, new `ArchonsRise.Log` asmdef, no Unity dependency)**
- `LogEntry.cs` — the entry struct.
- `PlayerLogCore.cs` — ring buffer, newest-first ordering, divider derivation.

**Scene layer — `Assets/Scripts/GameObjectScripts/LogUI/` (new folder, main assembly)**
- `GameLog.cs` — lazy scene singleton; the one entry point (`Post`).
- `ToastRail.cs` — spawns, stacks and fades toasts.
- `Toast.cs` — one toast's own fade/dwell lifecycle.
- `LogPanel.cs` — the scrollable history.

**Deleted**
- `Assets/Scripts/Managers/MessageController.cs` — exists solely to dismiss the message canvas.

**Modified**
- `Assets/Scripts/Managers/GameManager.cs` — forwarder, then removal of the canvas fields.
- `Assets/Scripts/Managers/DataManager.cs`, `HandFocusController.cs`, `UnitsLane.cs`, `UnitInspectorNavController.cs`, `RunEndController.cs` — drop the `messageCanvas.enabled` guards.
- `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` — add `ArchonsRise.Log`.

---

### Task 1: `PlayerLogCore` (pure, TDD)

**Files:**
- Create: `Assets/Scripts/Log/LogEntry.cs`
- Create: `Assets/Scripts/Log/PlayerLogCore.cs`
- Create: `Assets/Scripts/Log/ArchonsRise.Log.asmdef`
- Create: `Assets/Tests/EditMode/PlayerLogCoreTests.cs`
- Modify: `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `struct LogEntry { int Day; string Text; }`
  - `PlayerLogCore.Capacity` (const int, 100), `.Append(int day, string text)`, `.Count`, `.Entries` (`IReadOnlyList<LogEntry>`, newest first), `.Clear()`, `.NeedsDayDivider(int index)`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/PlayerLogCoreTests.cs`:

```csharp
using NUnit.Framework;

public class PlayerLogCoreTests
{
    [Test]
    public void AppendingPastCapacityEvictsOldest()
    {
        var log = new PlayerLogCore();
        for (int i = 0; i < PlayerLogCore.Capacity + 5; i++) log.Append(1, "e" + i);
        Assert.AreEqual(PlayerLogCore.Capacity, log.Count);
        Assert.AreEqual("e104", log.Entries[0].Text, "newest survives");
        Assert.AreEqual("e5", log.Entries[PlayerLogCore.Capacity - 1].Text, "oldest five evicted");
    }

    [Test]
    public void EntriesAreNewestFirst()
    {
        var log = new PlayerLogCore();
        log.Append(1, "first");
        log.Append(1, "second");
        Assert.AreEqual("second", log.Entries[0].Text);
        Assert.AreEqual("first", log.Entries[1].Text);
    }

    [Test]
    public void DividerMarksTheFirstEntryAndEveryDayChange()
    {
        var log = new PlayerLogCore();
        log.Append(1, "d1a");
        log.Append(1, "d1b");
        log.Append(2, "d2a");
        Assert.IsTrue(log.NeedsDayDivider(0), "newest entry always opens a day header");
        Assert.IsTrue(log.NeedsDayDivider(1), "the day 2 -> day 1 boundary");
        Assert.IsFalse(log.NeedsDayDivider(2), "same day as the entry above it");
    }

    [Test]
    public void DividerSurvivesEvictionOfADaysFirstEntry()
    {
        var log = new PlayerLogCore();
        log.Append(1, "oldest");
        for (int i = 0; i < PlayerLogCore.Capacity; i++) log.Append(2, "d2-" + i);
        Assert.AreEqual(PlayerLogCore.Capacity, log.Count);
        Assert.IsTrue(log.NeedsDayDivider(0));
        for (int i = 1; i < log.Count; i++)
            Assert.IsFalse(log.NeedsDayDivider(i), "only one day remains after eviction");
    }

    [Test]
    public void ClearEmptiesTheBuffer()
    {
        var log = new PlayerLogCore();
        log.Append(1, "x");
        log.Clear();
        Assert.AreEqual(0, log.Count);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
tools/pure-tests/run.sh \
  Assets/Scripts/Log/LogEntry.cs \
  Assets/Scripts/Log/PlayerLogCore.cs \
  Assets/Tests/EditMode/PlayerLogCoreTests.cs
```

Expected: the shell reports the source files do not exist (`error CS2001`).

- [ ] **Step 3: Write `LogEntry`**

`Assets/Scripts/Log/LogEntry.cs`:

```csharp
// One line in the player log. Text already carries IconMarkup sprite tags, so
// entries render exactly like the messages they replaced, with no reformatting.
public readonly struct LogEntry
{
    public readonly int Day;
    public readonly string Text;

    public LogEntry(int day, string text)
    {
        Day = day;
        Text = text;
    }
}
```

- [ ] **Step 4: Write `PlayerLogCore`**

`Assets/Scripts/Log/PlayerLogCore.cs`:

```csharp
using System.Collections.Generic;

// The run's message history (spec 2026-07-28). A capped ring buffer, newest
// first, grouped by day. In-memory and NOT saved: the log answers "what did I
// just miss", which a session covers, and persisting it would cost a save-schema
// bump plus a migrator plus every migrator test that asserts the version.
//
// Day dividers are DERIVED (NeedsDayDivider) rather than stored as pseudo-
// entries, so eviction can never orphan a header.
public class PlayerLogCore
{
    public const int Capacity = 100;

    // Oldest-first storage; newestFirst is the render order, rebuilt on change.
    private readonly List<LogEntry> entries = new List<LogEntry>();
    private readonly List<LogEntry> newestFirst = new List<LogEntry>();

    public int Count { get { return entries.Count; } }
    public IReadOnlyList<LogEntry> Entries { get { return newestFirst; } }

    public void Append(int day, string text)
    {
        entries.Add(new LogEntry(day, text));
        if (entries.Count > Capacity) entries.RemoveAt(0);
        Rebuild();
    }

    public void Clear()
    {
        entries.Clear();
        Rebuild();
    }

    // True when the entry at this newest-first index should be preceded by a day
    // header: either it is the newest entry, or the entry above it is a different
    // day.
    public bool NeedsDayDivider(int index)
    {
        if (index < 0 || index >= newestFirst.Count) return false;
        if (index == 0) return true;
        return newestFirst[index].Day != newestFirst[index - 1].Day;
    }

    private void Rebuild()
    {
        newestFirst.Clear();
        for (int i = entries.Count - 1; i >= 0; i--) newestFirst.Add(entries[i]);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run the Step 2 command.
Expected: `--- 5 passed, 0 failed ---`.

- [ ] **Step 6: Create the asmdef**

`Assets/Scripts/Log/ArchonsRise.Log.asmdef`:

```json
{
    "name": "ArchonsRise.Log",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

`noEngineReferences: true` enforces the "pure means no UnityEngine" constraint at compile time.

- [ ] **Step 7: Reference it from the tests asmdef**

In `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`, add `"ArchonsRise.Log"` to the `references` array, keeping it alphabetical — between `"ArchonsRise.Leveling"` and `"ArchonsRise.Places"`:

```json
        "ArchonsRise.Leveling",
        "ArchonsRise.Log",
        "ArchonsRise.Places",
```

Without this, `PlayerLogCoreTests` fails inside Unity with `CS0103: The name 'PlayerLogCore' does not exist`, even though the CLI harness passes.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Log/ Assets/Tests/EditMode/PlayerLogCoreTests.cs Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef
git commit -m "feat: pure PlayerLogCore ring buffer with derived day dividers"
```

---

### Task 2: `GameLog` singleton

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/LogUI/GameLog.cs`

**Interfaces:**
- Consumes: `PlayerLogCore` (Task 1), `GameManager.Instance.Round`, `RunEndController.HasEnded`.
- Produces: `GameLog.Instance`, `GameLog.Post(string message)`, `GameLog.Log` (the core, for `LogPanel`), `GameLog.RailChanged` (event the rail subscribes to).

- [ ] **Step 1: Write `GameLog`**

```csharp
using System;
using UnityEngine;

// The one entry point for player-facing messages (spec 2026-07-28). Replaces
// GameManager.ValidationMessage and its blocking canvas: Post appends to the
// history AND raises a toast, and never blocks input.
//
// Lazily creates its own scene GameObject (the RewardQueue / ConquestTracker
// pattern) so no scene wiring is required; being scene-scoped means a new run
// starts with an empty log.
public class GameLog : MonoBehaviour
{
    private static GameLog instance;
    public static GameLog Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("GameLog").AddComponent<GameLog>();
            return instance;
        }
    }

    public PlayerLogCore Log { get; } = new PlayerLogCore();

    // Raised with the text of each new entry. ToastRail subscribes; LogPanel
    // reads Log directly when it opens.
    public event Action<string> Posted;

    public void Post(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        // The run-end screen is terminal: nothing may appear over it. Same guard
        // ValidationMessage carried.
        if (RunEndController.HasEnded) return;

        int day = GameManager.Instance != null ? GameManager.Instance.Round : 0;
        Log.Append(day, message);
        Posted?.Invoke(message);
    }
}
```

- [ ] **Step 2: Verify it compiles**

Ask the user to let Unity recompile and confirm the Console is clean. Nothing calls `Post` yet.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/LogUI/GameLog.cs
git commit -m "feat: GameLog scene singleton"
```

---

### Task 3: `Toast` and `ToastRail`

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/LogUI/Toast.cs`
- Create: `Assets/Scripts/GameObjectScripts/LogUI/ToastRail.cs`

**Interfaces:**
- Consumes: `GameLog.Instance.Posted` (Task 2), DOTween.
- Produces: `Toast.Play(string text, float dwell)`, `Toast.BeginFadeNow()`, `ToastRail` (subscribes to `GameLog.Posted` in `OnEnable`).

- [ ] **Step 1: Write `Toast`**

```csharp
using TMPro;
using UnityEngine;
using DG.Tweening;

// One toast's lifecycle: fade in, dwell, fade out, destroy. The rail owns
// stacking and count; this owns only its own timing, so an early eviction is
// just BeginFadeNow.
[RequireComponent(typeof(CanvasGroup))]
public class Toast : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] float fadeTime = 0.2f;

    CanvasGroup group;
    Tween active;

    void Awake() => group = GetComponent<CanvasGroup>();

    public void Play(string text, float dwell)
    {
        if (label != null) label.text = text;
        if (group == null) group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        // Toasts never take clicks — the rail floats over modals and must not
        // eat them (spec 2026-07-28).
        group.blocksRaycasts = false;
        group.interactable = false;

        active = DOTween.Sequence()
            .Append(group.DOFade(1f, fadeTime))
            .AppendInterval(dwell)
            .Append(group.DOFade(0f, fadeTime))
            .OnComplete(() => { if (this != null) Destroy(gameObject); });
    }

    // Called by the rail when a newer toast pushes this one past the visible cap.
    public void BeginFadeNow()
    {
        if (active != null) active.Kill();
        if (group == null) group = GetComponent<CanvasGroup>();
        active = group.DOFade(0f, fadeTime)
            .OnComplete(() => { if (this != null) Destroy(gameObject); });
    }

    void OnDestroy()
    {
        if (active != null) active.Kill();
    }
}
```

- [ ] **Step 2: Write `ToastRail`**

```csharp
using System.Collections.Generic;
using UnityEngine;

// The corner stack of transient messages (spec 2026-07-28). Replaces the
// click-to-dismiss message canvas: a toast fades on its own, so nothing stands
// between the player and the next decision.
//
// The rail's own CanvasGroup must have blocksRaycasts = false and its canvas
// must sort above everything, so a toast can float over a card-pick modal
// without ever eating a click.
public class ToastRail : MonoBehaviour
{
    [SerializeField] Transform container;   // vertical layout, newest at one end
    [SerializeField] Toast toastPrefab;
    [SerializeField] float dwellSeconds = 3.5f;
    [SerializeField] int maxVisible = 4;

    readonly List<Toast> live = new();

    void OnEnable()
    {
        GameLog.Instance.Posted += OnPosted;
    }

    void OnDisable()
    {
        if (GameLog.Instance != null) GameLog.Instance.Posted -= OnPosted;
    }

    void OnPosted(string text)
    {
        if (toastPrefab == null || container == null) return;

        live.RemoveAll(t => t == null);

        // A fifth toast pushes the oldest into an early fade rather than
        // letting the stack grow off-screen.
        while (live.Count >= maxVisible)
        {
            var oldest = live[0];
            live.RemoveAt(0);
            if (oldest != null) oldest.BeginFadeNow();
        }

        var toast = Instantiate(toastPrefab, container);
        live.Add(toast);
        toast.Play(text, dwellSeconds);
    }
}
```

- [ ] **Step 3: Hand the user the editor authoring steps**

> 1. Create a `ToastCanvas` (Screen Space – Camera, matching the other canvases) with a **higher sort order than every other canvas** so toasts float on top. On its root add a `CanvasGroup` with **Interactable off and Blocks Raycasts off**.
> 2. Under it add an empty `Container` with a Vertical Layout Group, anchored to the corner you want toasts to appear in.
> 3. Create a `Toast` prefab: a panel with a `CanvasGroup`, a TMP text child, and the `Toast` component with `label` wired.
> 4. Add the `ToastRail` component to `ToastCanvas` and wire `container` and `toastPrefab`.
> 5. Nothing posts yet — verification happens in Task 5.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/LogUI/Toast.cs Assets/Scripts/GameObjectScripts/LogUI/ToastRail.cs
git commit -m "feat: toast rail with self-fading, non-blocking toasts"
```

---

### Task 4: `LogPanel` history

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/LogUI/LogPanel.cs`

**Interfaces:**
- Consumes: `GameLog.Instance.Log` (Task 2), `PlayerLogCore.Entries` / `.NeedsDayDivider` (Task 1), `ClickOffCatcher` (Plan 1 Task 4 — if Plan 1 has not run, add a temporary close button and replace it during Plan 2).
- Produces: `LogPanel.Open()`, `LogPanel.Close()`, `LogPanel.Toggle()`.

- [ ] **Step 1: Write `LogPanel`**

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The openable message history (spec 2026-07-28). Newest first, with a day
// header wherever the core says one belongs. Rebuilt on open rather than kept
// live: it is a review surface, not a HUD element.
//
// Closes by clicking off, per the 2026-07-28 sweep. No exit button.
[RequireComponent(typeof(Canvas))]
public class LogPanel : MonoBehaviour
{
    [SerializeField] Transform entryContainer;      // vertical layout inside a scroll view
    [SerializeField] GameObject entryPrefab;        // TMP text
    [SerializeField] GameObject dayHeaderPrefab;    // TMP text, styled as a divider
    [SerializeField] ClickOffCatcher catcher;

    readonly List<GameObject> spawned = new();

    Canvas _canvas;
    Canvas Canvas => _canvas != null ? _canvas : (_canvas = GetComponent<Canvas>());

    void Start()
    {
        Canvas.enabled = false; // start closed regardless of the authored state
    }

    // Wired to the HUD's log button.
    public void Toggle()
    {
        if (Canvas.enabled) Close();
        else Open();
    }

    public void Open()
    {
        Rebuild();
        Canvas.enabled = true;
        if (catcher != null) catcher.SetArmed(true);
    }

    // Public so the ClickOffCatcher's UnityEvent can bind to it.
    public void Close()
    {
        ClearEntries();
        Canvas.enabled = false;
        if (catcher != null) catcher.SetArmed(false);
    }

    void Rebuild()
    {
        ClearEntries();
        var log = GameLog.Instance.Log;
        for (int i = 0; i < log.Entries.Count; i++)
        {
            if (log.NeedsDayDivider(i))
                Spawn(dayHeaderPrefab, $"Day {log.Entries[i].Day}");
            Spawn(entryPrefab, log.Entries[i].Text);
        }
    }

    void Spawn(GameObject prefab, string text)
    {
        if (prefab == null || entryContainer == null) return;
        var go = Instantiate(prefab, entryContainer);
        var label = go.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = text;
        spawned.Add(go);
    }

    void ClearEntries()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}
```

- [ ] **Step 2: Hand the user the editor authoring steps**

> 1. Create a `LogCanvas` with a Scroll View. Inside its Content add a Vertical Layout Group.
> 2. Create two prefabs: an entry (TMP text, left-aligned, wrapping) and a day header (TMP text, styled as a divider).
> 3. Add the `LogPanel` component to `LogCanvas` and wire `entryContainer` (the Content object), `entryPrefab`, `dayHeaderPrefab`.
> 4. Add a full-screen `Image` at **sibling index 0** under `LogCanvas` with the `ClickOffCatcher` component, wire its `onClickOff` to `LogPanel.Close`, and wire the panel's `catcher` field to it.
> 5. Add a log button to the HUD and wire its OnClick to `LogPanel.Toggle`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/LogUI/LogPanel.cs
git commit -m "feat: openable player log history panel"
```

---

### Task 5: Route messages through `GameLog`

One-line change that converts all 36 call sites at once, so the whole game can be play-tested before any deletion happens.

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs:104-117`

**Interfaces:**
- Consumes: `GameLog.Instance.Post` (Task 2).
- Produces: `GameManager.ValidationMessage` still exists but no longer opens a canvas.

- [ ] **Step 1: Replace the body of `ValidationMessage`**

```csharp
    // Deprecated shim (spec 2026-07-28): messages are no longer blocking modals.
    // Kept for one commit so every call site keeps compiling while the game is
    // play-tested; Task 6 renames the call sites and deletes this.
    public void ValidationMessage(string message) => GameLog.Instance.Post(message);
```

Delete the now-unused `messageDone` field and the `ReturnButton` method:

```csharp
    private System.Action messageDone;

    public void ReturnButton()
    {
        messageCanvas.enabled = false;
        var done = messageDone;
        messageDone = null;
        done?.Invoke();
    }
```

- [ ] **Step 2: Ask the user to play-verify**

> Play a fight and defeat an enemy. The reward line should appear as a toast in the corner and fade on its own after a few seconds — **no click needed**. If a card pick was rolled, it should open immediately with the toast floating over it, and clicking a card must still work. Try clicking a distant hex to trigger "you must be standing here": that should toast too, not block. Then open the log from the HUD and confirm the messages are listed newest first under a day header.

- [ ] **Step 3: Commit after the user confirms**

```bash
git add Assets/Scripts/Managers/GameManager.cs
git commit -m "feat: route validation messages to the non-blocking player log"
```

---

### Task 6: Delete the message canvas and its guards

**Files:**
- Modify: all 21 files containing `ValidationMessage(` (mechanical rename)
- Modify: `Assets/Scripts/Managers/GameManager.cs`
- Delete: `Assets/Scripts/Managers/MessageController.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Managers/DataManager.cs:101`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/HandFocusController.cs:26-40`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/UnitsLane.cs:51`
- Modify: `Assets/Scripts/GameObjectScripts/UnitMenuScripts/UnitInspectorNavController.cs:17,30-31`
- Modify: `Assets/Scripts/Managers/RunEndController.cs:77`

- [ ] **Step 1: Rename the call sites**

```bash
grep -rl "ValidationMessage(" Assets/Scripts --include=*.cs \
  | xargs sed -i 's/GameManager\.Instance\.ValidationMessage(/GameLog.Instance.Post(/g'
```

Then find the remaining in-class calls (inside `GameManager` itself, which called it unqualified) and change each `ValidationMessage(` to `GameLog.Instance.Post(`:

```bash
grep -rn "ValidationMessage(" Assets/Scripts --include=*.cs
```

Expected after both: only the shim's own declaration remains.

- [ ] **Step 2: Delete the shim and the canvas fields**

In `GameManager.cs` delete the `ValidationMessage` method, and these two fields:

```csharp
    public Canvas messageCanvas;
    public TextMeshProUGUI messageText;
```

and the `returnButton` field:

```csharp
    public Button returnButton;
```

and the two `Awake` lines:

```csharp
        messageCanvas.gameObject.SetActive(true);
        messageCanvas.enabled = false;
```

- [ ] **Step 3: Delete `MessageController`**

```bash
git rm Assets/Scripts/Managers/MessageController.cs Assets/Scripts/Managers/MessageController.cs.meta
```

- [ ] **Step 4: Drop the five guards**

Each of these existed only because the old canvas modally captured input. Toasts do not, so every guard is deleted outright rather than re-pointed.

`DataManager.cs` — delete:

```csharp
        if (GameManager.Instance.messageCanvas.enabled) return;
```

`HandFocusController.cs` — delete the whole message block and the `_messageWasUp` field:

```csharp
        if (gm.messageCanvas.enabled)
        {
            // A validation message is a modal: MessageController owns input, we do
            // nothing. Keep focus as-is so it resumes when the message clears.
            _messageWasUp = true;
            return;
        }
        if (_messageWasUp)
        {
            // Swallow the frame the message closed on so the A/B that dismissed it
            // can't also open a card here (independent of Update ordering).
            _messageWasUp = false;
            return;
        }
```

`UnitsLane.cs` — change:

```csharp
        if (gm.messageCanvas.enabled || gm.cardCanvas.enabled || gm.unitCanvas.enabled) return;
```

to:

```csharp
        if (gm.cardCanvas.enabled || gm.unitCanvas.enabled) return;
```

`UnitInspectorNavController.cs` — delete the `_messageWasUp` field and these two lines:

```csharp
        if (GameManager.Instance.messageCanvas.enabled) { _messageWasUp = true; return; }
        if (_messageWasUp) { _messageWasUp = false; return; }
```

`RunEndController.cs` — delete:

```csharp
            if (gm.messageCanvas != null)    gm.messageCanvas.enabled = false;
```

- [ ] **Step 5: Verify the build is clean**

Ask the user to let Unity recompile. Expected: zero errors, and zero remaining references to `messageCanvas`. Verify with:

```bash
grep -rn "messageCanvas\|ValidationMessage\|MessageController" Assets/Scripts --include=*.cs
```

Expected: no output.

- [ ] **Step 6: Hand the user the editor cleanup step**

> Delete the `MessageCanvas` GameObject from the scene, and the `MessageController` component wherever it lived. The `GameManager` inspector will have lost its `messageCanvas`, `messageText` and `returnButton` slots — that is expected.

- [ ] **Step 7: Play-verify, then commit**

> Full pass: defeat enemies, walk into invalid moves, try to delve without Explore, engage a shrine and get a sour roll. Every one of those should toast without blocking. Nothing should require a click to continue except the card pick and the skill pick. Gamepad Submit/Cancel should no longer be swallowed anywhere.

```bash
git add -A Assets/Scripts
git commit -m "refactor: delete the message canvas and its five input guards"
```

---

## Plan 3 Acceptance

1. Defeating several enemies in one fight stacks reward toasts that fade on their own; no click is needed to return to play.
2. A card pick opens immediately underneath the toasts, and clicking a card still works (proves the rail does not block raycasts).
3. Invalid actions ("you must be standing here", "you need N Explore") toast instead of blocking.
4. The HUD log button opens the history: newest first, grouped under day headers, closing by click-off.
5. After 100+ messages the log holds exactly 100 and the oldest are gone, with no orphaned day header at the top.
6. A new run starts with an empty log.
7. `grep -rn "messageCanvas\|ValidationMessage\|MessageController" Assets/Scripts --include=*.cs` returns nothing.
8. Save/load works unchanged — no schema version was touched.
