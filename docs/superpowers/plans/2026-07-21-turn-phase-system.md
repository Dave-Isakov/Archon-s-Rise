# Turn-Phase System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure a turn into a strict Explore→Action→End sequence with a one-action cap, make the round a Doom-band-scaled "day" that auto-ends, make movement undoable, and surface a phase + day-countdown HUD.

**Architecture:** Add two small pure rule classes (`TurnPhaseRules`, `RoundRules`) in a new `ArchonsRise.TurnFlow` assembly and a `DoomRules.TurnsForBand` band→budget lookup, all TDD'd via the mcs CLI harness. A scene singleton `TurnPhaseController` (Assembly-CSharp) owns phase / action-taken / turns-remaining state, decides on each End-Turn press whether to reuse the existing `endTheTurn` or `endTheRound` event chains, and raises `onPhaseChanged` / `onTurnsRemainingChanged` for the HUD. Movement becomes an undoable `MoveCommand`; the fog-reveal branch commits the stack instead. All gameplay entry points query the controller before moving or interacting.

**Tech Stack:** Unity 6000.5.1f1, C#, ScriptableObject GameEvent/Listener bus, Command-pattern undo (`PlayManager`), NUnit EditMode tests, mcs CLI pure-test harness.

## Global Constraints

- **Pure logic is Unity-free and lives in its own asmdef.** New pure classes go under `Assets/Scripts/<Domain>/` with an `ArchonsRise.<Domain>.asmdef`; add that assembly to `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` `references` in the same task, or EditMode tests fail CS0103. MonoBehaviours that touch `GameManager`/`PlayerHand`/etc. stay in the main assembly under `Assets/Scripts/GameObjectScripts/` or `Assets/Scripts/Managers/`.
- **Editor is usually open** (holds the project lock), so batch-mode `-runTests` is unavailable. Verify pure classes RED/GREEN with the **mcs CLI harness** (Task 1 sets it up); the user confirms the full suite green in Window ▸ General ▸ Test Runner at the end.
- **mcs harness paths (verbatim):**
  - mcs: `C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge\bin\mcs.bat`
  - mono: `C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge\bin\mono.exe`
  - nunit dll: `Library\PackageCache\com.unity.ext.nunit@*\net472\unity-custom\nunit.framework.dll`
- **Never hand-edit scene/prefab YAML.** Scene/prefab wiring (new TMPs, event assets, listeners, removed buttons) is performed by the USER from the step-by-step editor instructions each such task provides; those tasks are accepted by a manual in-editor checklist, not an automated test.
- **No save-schema bump.** Turns-remaining + round ride the existing round/turn save fields; phase resets to Explore on load.
- **Every design/dev decision** gets appended to `.claude/skills/archons-rise-roadmap/decisions-log.md` (Task 12).
- Commit message trailer for every commit: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## File Structure

**New (pure, `ArchonsRise.TurnFlow` assembly):**
- `Assets/Scripts/TurnFlow/ArchonsRise.TurnFlow.asmdef` — new assembly (references `ArchonsRise.CardPlay` for `DrawVerdict`).
- `Assets/Scripts/TurnFlow/TurnPhase.cs` — the `TurnPhase` enum.
- `Assets/Scripts/TurnFlow/TurnPhaseRules.cs` — `CanMove`, `CanInteract`, `ShouldCommitOnMove`.
- `Assets/Scripts/TurnFlow/RoundRules.cs` — `IsRoundOver`, `NextTurnsRemaining`.

**New (scene, Assembly-CSharp):**
- `Assets/Scripts/Managers/TurnPhaseController.cs` — the phase/round-budget singleton.
- `Assets/Scripts/Managers/Commands/MoveCommand.cs` — undoable movement command.

**Modified:**
- `Assets/Scripts/Doom/DoomTuning.cs` — three per-band turn-budget fields.
- `Assets/Scripts/Doom/DoomRules.cs` — `TurnsForBand`.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/DirectionButton.cs` — movement pushes `MoveCommand`; fog-reveal branch commits.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` — `Exploration` no longer clears the stack.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs` — combat start gated by `CanInteract` + `BeginAction`.
- Place-menu open + dungeon-delve entry points — gated by `CanInteract` + `BeginAction`.
- `Assets/Scripts/GameObjectScripts/DeckScripts/EndTurnButton.cs` — routes through the controller.
- `Assets/Scripts/GameObjectScripts/DeckScripts/EndRoundButton.cs` — deleted (button removed).
- `Assets/Scripts/CardPlay/TurnButtonGate.cs` — End Turn no longer disabled on deck-empty.
- `Assets/Scripts/GameObjectScripts/DeckScripts/TurnFlowShortcuts.cs` — drop the End-Round fallback.
- `Assets/Scripts/Managers/GameManager.cs` — HUD countdown text driven by an event, not per-frame.
- Save/load (`DataManager` / `SaveSerializer`) — restore round + turns-remaining; reset phase.
- Tutorial content + `Assets/Tests/EditMode/TutorialCopyValidationTests.cs`.
- `.claude/skills/archons-rise-design/mechanics.md`, `balance.md`; `.claude/skills/archons-rise-roadmap/milestones.md`, `decisions-log.md`.

**Test files:**
- `Assets/Tests/EditMode/TurnPhaseRulesTests.cs`
- `Assets/Tests/EditMode/RoundRulesTests.cs`
- `Assets/Tests/EditMode/DoomRulesTests.cs` (add `TurnsForBand` cases; create if absent)

---

## Task 1: `TurnPhase` enum + `TurnPhaseRules` (pure, TDD) + mcs harness setup

**Files:**
- Create: `Assets/Scripts/TurnFlow/ArchonsRise.TurnFlow.asmdef`
- Create: `Assets/Scripts/TurnFlow/TurnPhase.cs`
- Create: `Assets/Scripts/TurnFlow/TurnPhaseRules.cs`
- Create: `Assets/Tests/EditMode/TurnPhaseRulesTests.cs`
- Modify: `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` (add `ArchonsRise.TurnFlow`)
- Create (scratchpad, harness): `Runner.cs`

**Interfaces:**
- Produces:
  - `enum TurnPhase { Explore, Action, End }`
  - `static class TurnPhaseRules`
    - `bool CanMove(TurnPhase phase)` — true only in `Explore`.
    - `bool CanInteract(TurnPhase phase, bool actionTaken)` — true in `Explore` or `Action` when `!actionTaken` (taking the action performs the Explore→Action move itself); false once `actionTaken`.
    - `bool ShouldCommitOnMove(bool revealedNewFog)` — returns `revealedNewFog`.

- [ ] **Step 1: Create the `ArchonsRise.TurnFlow` assembly definition**

Create `Assets/Scripts/TurnFlow/ArchonsRise.TurnFlow.asmdef`:

```json
{
    "name": "ArchonsRise.TurnFlow",
    "rootNamespace": "",
    "references": ["ArchonsRise.CardPlay"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

(The `ArchonsRise.CardPlay` reference is for `DrawVerdict`, used by `RoundRules` in Task 2 — declaring it now avoids a second asmdef edit.)

- [ ] **Step 2: Add the assembly to the EditMode test references**

In `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`, add `"ArchonsRise.TurnFlow"` to the `references` array (alphabetical, after `ArchonsRise.Tutorial`):

```json
        "ArchonsRise.Tutorial",
        "ArchonsRise.TurnFlow",
        "ArchonsRise.UnitPlay",
```

- [ ] **Step 3: Write the failing test**

Create `Assets/Tests/EditMode/TurnPhaseRulesTests.cs`:

```csharp
using NUnit.Framework;

public class TurnPhaseRulesTests
{
    [Test]
    public void Move_Allowed_Only_In_Explore()
    {
        Assert.IsTrue(TurnPhaseRules.CanMove(TurnPhase.Explore));
        Assert.IsFalse(TurnPhaseRules.CanMove(TurnPhase.Action));
        Assert.IsFalse(TurnPhaseRules.CanMove(TurnPhase.End));
    }

    [Test]
    public void Interact_Allowed_Until_Action_Is_Spent()
    {
        // From Explore (implicit transition) or Action, only while not yet spent.
        Assert.IsTrue(TurnPhaseRules.CanInteract(TurnPhase.Explore, false));
        Assert.IsTrue(TurnPhaseRules.CanInteract(TurnPhase.Action, false));
        // Spent: no second interaction.
        Assert.IsFalse(TurnPhaseRules.CanInteract(TurnPhase.Explore, true));
        Assert.IsFalse(TurnPhaseRules.CanInteract(TurnPhase.Action, true));
        // Never in End.
        Assert.IsFalse(TurnPhaseRules.CanInteract(TurnPhase.End, false));
    }

    [Test]
    public void Move_Commits_Only_When_It_Reveals_New_Fog()
    {
        Assert.IsTrue(TurnPhaseRules.ShouldCommitOnMove(true));
        Assert.IsFalse(TurnPhaseRules.ShouldCommitOnMove(false));
    }
}
```

- [ ] **Step 4: Create the harness runner + verify the test fails (RED)**

Create `<scratchpad>/Runner.cs` (reused by every pure task):

```csharp
using System;
using System.Linq;
using System.Reflection;

class Runner
{
    static int Main(string[] args)
    {
        var asm = Assembly.LoadFrom(args[0]);
        int pass = 0, fail = 0;
        foreach (var t in asm.GetTypes())
        {
            var tests = t.GetMethods().Where(m =>
                m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute")).ToList();
            if (tests.Count == 0) continue;
            foreach (var m in tests)
            {
                try { m.Invoke(Activator.CreateInstance(t), null); pass++; Console.WriteLine("PASS " + t.Name + "." + m.Name); }
                catch (Exception e) { fail++; Console.WriteLine("FAIL " + t.Name + "." + m.Name + ": " + (e.InnerException ?? e).Message); }
            }
        }
        Console.WriteLine(pass + " passed, " + fail + " failed");
        return fail;
    }
}
```

Run this PowerShell block (compiles the sources for this task + the test into a DLL and runs it). `TurnPhaseRules.cs` does not exist yet, so compilation fails = RED:

```powershell
$mcs  = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge\bin\mcs.bat"
$mono = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge\bin\mono.exe"
$nunit = (Get-ChildItem "Library\PackageCache\com.unity.ext.nunit@*\net472\unity-custom\nunit.framework.dll" | Select-Object -First 1).FullName
$scratch = "<scratchpad>"   # replace with the session scratchpad path
& $mcs -nologo -target:library "-out:$scratch\turnflow.dll" "-r:$nunit" `
    "Assets\Scripts\TurnFlow\TurnPhase.cs" "Assets\Scripts\TurnFlow\TurnPhaseRules.cs" `
    "Assets\Tests\EditMode\TurnPhaseRulesTests.cs"
```

Expected: compile error `error CS2001: Source file '...TurnPhaseRules.cs' could not be found` (RED).

- [ ] **Step 5: Write the enum + minimal implementation**

Create `Assets/Scripts/TurnFlow/TurnPhase.cs`:

```csharp
// The three phases of a turn (spec 2026-07-21). Strictly one-way:
// Explore -> Action -> End, then a new turn begins at Explore.
public enum TurnPhase { Explore, Action, End }
```

Create `Assets/Scripts/TurnFlow/TurnPhaseRules.cs`:

```csharp
// Pure turn-phase gating (spec 2026-07-21). No Unity dependency so it is
// mcs-CLI-testable, matching the DrawGate/CombatRules pattern.
public static class TurnPhaseRules
{
    // Movement is Explore-only; taking the action ends exploring.
    public static bool CanMove(TurnPhase phase) => phase == TurnPhase.Explore;

    // Exactly one interaction per turn. Starting it from Explore performs the
    // implicit Explore->Action transition, so both phases allow it while the
    // action is unspent; End never does.
    public static bool CanInteract(TurnPhase phase, bool actionTaken)
        => !actionTaken && (phase == TurnPhase.Explore || phase == TurnPhase.Action);

    // A move commits the undo stack only when it uncovers previously-hidden
    // fog (irreversible knowledge); an ordinary move stays undoable.
    public static bool ShouldCommitOnMove(bool revealedNewFog) => revealedNewFog;
}
```

- [ ] **Step 6: Recompile + run to verify GREEN**

Re-run the PowerShell block from Step 4, then execute the runner:

```powershell
& $mcs -nologo "-out:$scratch\Runner.exe" "-r:$nunit" "$scratch\Runner.cs"
& $mono "$scratch\Runner.exe" "$scratch\turnflow.dll"
```

Expected output includes `PASS TurnPhaseRulesTests.Move_Allowed_Only_In_Explore` (×3 tests) and `3 passed, 0 failed`.

- [ ] **Step 7: Commit**

```powershell
git add "Assets/Scripts/TurnFlow" "Assets/Tests/EditMode/TurnPhaseRulesTests.cs" "Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef"
git commit -m "feat: TurnPhase enum + pure TurnPhaseRules (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

(Unity generates the `.meta` files for the new folder/scripts on next focus; include them in a later commit if they appear, or let the user commit them from the editor.)

---

## Task 2: `RoundRules` (pure, TDD)

**Files:**
- Create: `Assets/Scripts/TurnFlow/RoundRules.cs`
- Create: `Assets/Tests/EditMode/RoundRulesTests.cs`

**Interfaces:**
- Consumes: `DrawVerdict` (from `ArchonsRise.CardPlay`).
- Produces:
  - `static class RoundRules`
    - `int NextTurnsRemaining(int turnsRemaining)` — `turnsRemaining - 1`, floored at 0.
    - `bool IsRoundOver(int turnsRemainingAfterDecrement, bool deckCanRefill)` — true when the budget is spent (`<= 0`) OR the deck can't refill (`!deckCanRefill`).
    - `bool DeckCanRefill(DrawVerdict verdict)` — `verdict != DrawVerdict.DeckEmpty`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/RoundRulesTests.cs`:

```csharp
using NUnit.Framework;

public class RoundRulesTests
{
    [Test]
    public void Turns_Decrement_And_Floor_At_Zero()
    {
        Assert.AreEqual(2, RoundRules.NextTurnsRemaining(3));
        Assert.AreEqual(0, RoundRules.NextTurnsRemaining(1));
        Assert.AreEqual(0, RoundRules.NextTurnsRemaining(0));
    }

    [Test]
    public void Round_Over_When_Budget_Spent()
    {
        Assert.IsTrue(RoundRules.IsRoundOver(0, deckCanRefill: true));
        Assert.IsFalse(RoundRules.IsRoundOver(1, deckCanRefill: true));
    }

    [Test]
    public void Round_Over_When_Deck_Cannot_Refill()
    {
        // Budget remains but the deck is dry -> forced rest.
        Assert.IsTrue(RoundRules.IsRoundOver(2, deckCanRefill: false));
    }

    [Test]
    public void Deck_Can_Refill_Unless_Empty()
    {
        Assert.IsTrue(RoundRules.DeckCanRefill(DrawVerdict.Draw));
        Assert.IsTrue(RoundRules.DeckCanRefill(DrawVerdict.HandFull));
        Assert.IsFalse(RoundRules.DeckCanRefill(DrawVerdict.DeckEmpty));
    }
}
```

- [ ] **Step 2: Verify RED**

Run (adds the two new sources to the compile set):

```powershell
& $mcs -nologo -target:library "-out:$scratch\turnflow.dll" "-r:$nunit" `
    "Assets\Scripts\TurnFlow\TurnPhase.cs" "Assets\Scripts\TurnFlow\TurnPhaseRules.cs" "Assets\Scripts\TurnFlow\RoundRules.cs" `
    "Assets\Scripts\CardPlay\DrawGate.cs" `
    "Assets\Tests\EditMode\TurnPhaseRulesTests.cs" "Assets\Tests\EditMode\RoundRulesTests.cs"
```

Expected: `error CS2001: Source file '...RoundRules.cs' could not be found` (RED).

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/TurnFlow/RoundRules.cs`:

```csharp
// Pure "day" (round) budget math (spec 2026-07-21). The per-band starting
// budget comes from DoomRules.TurnsForBand; this class only counts it down and
// decides when the day is over. Unity-free / mcs-testable.
public static class RoundRules
{
    // One turn spent; never negative.
    public static int NextTurnsRemaining(int turnsRemaining)
        => turnsRemaining > 0 ? turnsRemaining - 1 : 0;

    // The day ends when the budget is spent OR the deck can no longer refill the
    // hand (a forced rest so a short deck can't strand the player mid-day).
    public static bool IsRoundOver(int turnsRemainingAfterDecrement, bool deckCanRefill)
        => turnsRemainingAfterDecrement <= 0 || !deckCanRefill;

    public static bool DeckCanRefill(DrawVerdict verdict)
        => verdict != DrawVerdict.DeckEmpty;
}
```

- [ ] **Step 4: Verify GREEN**

Re-run the Step 2 compile block, then:

```powershell
& $mono "$scratch\Runner.exe" "$scratch\turnflow.dll"
```

Expected: all `TurnPhaseRulesTests` + `RoundRulesTests` pass, `7 passed, 0 failed`.

- [ ] **Step 5: Commit**

```powershell
git add "Assets/Scripts/TurnFlow/RoundRules.cs" "Assets/Tests/EditMode/RoundRulesTests.cs"
git commit -m "feat: pure RoundRules day-budget math (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Doom-band turn budgets — `DoomTuning` fields + `DoomRules.TurnsForBand` (TDD)

**Files:**
- Modify: `Assets/Scripts/Doom/DoomTuning.cs`
- Modify: `Assets/Scripts/Doom/DoomRules.cs`
- Create (or modify if present): `Assets/Tests/EditMode/DoomRulesTests.cs`

**Interfaces:**
- Consumes: `DoomTuning` (from `ArchonsRise.Doom`).
- Produces: `int DoomRules.TurnsForBand(int doom, DoomTuning t)` — `lowBandTurns` in the low band (`doom <= lowBandMax`), `midBandTurns` in the mid band (`<= midBandMax`), `highBandTurns` above. Days shorten as Doom climbs.

- [ ] **Step 1: Add the per-band budget fields to `DoomTuning`**

In `Assets/Scripts/Doom/DoomTuning.cs`, after the `midBandMax` line, add:

```csharp
    // Turns per round ("day" length) by band — generous early, shorter as Doom
    // climbs (spec 2026-07-21). Starting values; tune in balance.md.
    public int lowBandTurns = 6;
    public int midBandTurns = 4;
    public int highBandTurns = 3;
```

- [ ] **Step 2: Write the failing test**

Create/append `Assets/Tests/EditMode/DoomRulesTests.cs`:

```csharp
using NUnit.Framework;

public class DoomRulesTests
{
    static DoomTuning T() => new DoomTuning(); // defaults: lowBandMax 6, midBandMax 13

    [Test]
    public void TurnsForBand_Shrinks_As_Doom_Climbs()
    {
        var t = T();
        Assert.AreEqual(6, DoomRules.TurnsForBand(0, t));   // low band
        Assert.AreEqual(6, DoomRules.TurnsForBand(6, t));   // low band edge
        Assert.AreEqual(4, DoomRules.TurnsForBand(7, t));   // mid band
        Assert.AreEqual(4, DoomRules.TurnsForBand(13, t));  // mid band edge
        Assert.AreEqual(3, DoomRules.TurnsForBand(14, t));  // high band
        Assert.AreEqual(3, DoomRules.TurnsForBand(99, t));  // clamped high
    }
}
```

- [ ] **Step 3: Verify RED**

```powershell
& $mcs -nologo -target:library "-out:$scratch\doom.dll" "-r:$nunit" `
    "Assets\Scripts\Doom\DoomTuning.cs" "Assets\Scripts\Doom\DoomRules.cs" `
    "Assets\Tests\EditMode\DoomRulesTests.cs"
```

Expected: `error CS0117: 'DoomRules' does not contain a definition for 'TurnsForBand'` (RED).

- [ ] **Step 4: Implement `TurnsForBand`**

In `Assets/Scripts/Doom/DoomRules.cs`, add after `MaxTier`:

```csharp
    // Turns in a round ("day" length) at this doom: longer in the low band,
    // shorter as the bands escalate (spec 2026-07-21).
    public static int TurnsForBand(int doom, DoomTuning t)
        => doom <= t.lowBandMax ? t.lowBandTurns
         : doom <= t.midBandMax ? t.midBandTurns
         : t.highBandTurns;
```

- [ ] **Step 5: Verify GREEN**

Re-run the Step 3 compile block, then `& $mono "$scratch\Runner.exe" "$scratch\doom.dll"`. Expected: `TurnsForBand_Shrinks_As_Doom_Climbs` passes.

- [ ] **Step 6: Commit**

```powershell
git add "Assets/Scripts/Doom/DoomTuning.cs" "Assets/Scripts/Doom/DoomRules.cs" "Assets/Tests/EditMode/DoomRulesTests.cs"
git commit -m "feat: Doom-band turn budgets (DoomRules.TurnsForBand) (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 7: USER editor step — set the budgets on the DoomTuning asset**

Ask the user: in `Assets/Scripts/ScriptableObjectData/DoomTuning.asset`, confirm the new `Low/Mid/High Band Turns` fields read 6 / 4 / 3 (Unity fills serialized defaults for new fields on existing assets as 0 — they must be set explicitly). This is editor work; provide it as an instruction, not a YAML edit.

---

## Task 4: `TurnPhaseController` + its events (scene singleton)

**Files:**
- Create: `Assets/Scripts/Managers/TurnPhaseController.cs`
- Create (editor): `onPhaseChanged` VoidEvent asset, `onTurnsRemainingChanged` IntEvent asset.

**Interfaces:**
- Consumes: `TurnPhaseRules`, `RoundRules`, `DoomRules.TurnsForBand`, `DrawGate.Evaluate`, `DoomClock.Instance`, existing `endTheTurn` / `endTheRound` VoidEvents, `GameManager.Instance.commands.ClearStack()`, `RunEndController.HasEnded`.
- Produces (relied on by Tasks 5–9):
  - `TurnPhaseController.Instance`
  - `TurnPhase CurrentPhase { get; }`
  - `int TurnsRemaining { get; }`
  - `bool CanMove` / `bool CanInteract`
  - `void BeginAction()` — commit the movement stack, mark the action spent, enter Action.
  - `void EndTurnPressed()` — run the turn-end chain, decrement the day, auto-end the round when over.
  - `void LoadState(int turnsRemaining)` — restore on load, phase → Explore.

- [ ] **Step 1: Write the controller**

Create `Assets/Scripts/Managers/TurnPhaseController.cs`:

```csharp
using UnityEngine;

// Owns the turn/round phase state (spec 2026-07-21). Strict Explore->Action->End
// turns inside a Doom-band-scaled "day". The Explore->Action transition is
// implicit (taking the action); End Turn is the only turn-flow control; the
// round auto-ends when its turn budget is spent or the deck can't refill.
public class TurnPhaseController : MonoBehaviour
{
    public static TurnPhaseController Instance { get; private set; }

    [SerializeField] DoomTuningSO doomTuning;      // per-band turn budgets
    [SerializeField] VoidEvent onPhaseChanged;
    [SerializeField] IntEvent onTurnsRemainingChanged;
    [SerializeField] VoidEvent endTheTurn;         // existing turn-end listener chain
    [SerializeField] VoidEvent endTheRound;        // existing round-end listener chain

    public TurnPhase CurrentPhase { get; private set; }
    public int TurnsRemaining { get; private set; }
    bool actionTaken;

    public bool CanMove     => TurnPhaseRules.CanMove(CurrentPhase);
    public bool CanInteract => TurnPhaseRules.CanInteract(CurrentPhase, actionTaken);

    void Awake() { Instance = this; }

    void Start()
    {
        // A load path calls LoadState before the first Start-driven round; guard so
        // we don't stomp a restored budget.
        if (TurnsRemaining <= 0) StartRound();
        else BeginTurn();
    }

    // Implicit Explore->Action: committing the movement stack (can't undo the path
    // once you commit to the encounter/visit), spend the action, enter Action.
    public void BeginAction()
    {
        GameManager.Instance.commands.ClearStack();
        actionTaken = true;
        SetPhase(TurnPhase.Action);
    }

    // The only turn-flow control. Commits, runs the turn-end chain, decrements the
    // day, and auto-ends the round when the budget is spent or the deck is dry.
    public void EndTurnPressed()
    {
        GameManager.Instance.commands.ClearStack();

        bool deckCanRefill = RoundRules.DeckCanRefill(CurrentDrawVerdict());
        endTheTurn.Raise(); // pools reset, hand top-up, TurnPlus (existing chain)

        int next = RoundRules.NextTurnsRemaining(TurnsRemaining);
        if (RoundRules.IsRoundOver(next, deckCanRefill))
        {
            endTheRound.Raise(); // reshuffle + Doom tick + unit/skill refresh (existing chain)
            if (RunEndController.HasEnded) return; // Doom tick may have lost the run
            StartRound();        // budget from the post-tick band
        }
        else
        {
            TurnsRemaining = next;
            onTurnsRemainingChanged.Raise(TurnsRemaining);
            BeginTurn();
        }
    }

    // Load path: restore the remaining budget; phase always resets to Explore.
    public void LoadState(int turnsRemaining)
    {
        TurnsRemaining = turnsRemaining;
        onTurnsRemainingChanged.Raise(TurnsRemaining);
        BeginTurn();
    }

    void StartRound()
    {
        int doom = DoomClock.Instance != null ? DoomClock.Instance.Doom : 0;
        TurnsRemaining = DoomRules.TurnsForBand(doom, doomTuning.tuning);
        onTurnsRemainingChanged.Raise(TurnsRemaining);
        BeginTurn();
    }

    void BeginTurn()
    {
        actionTaken = false;
        SetPhase(TurnPhase.Explore);
    }

    void SetPhase(TurnPhase phase)
    {
        CurrentPhase = phase;
        if (onPhaseChanged != null) onPhaseChanged.Raise();
    }

    DrawVerdict CurrentDrawVerdict()
    {
        var deck = FindAnyObjectByType<PlayerDeck>();
        var hand = FindAnyObjectByType<PlayerHand>();
        var player = FindAnyObjectByType<Player>();
        if (deck == null || hand == null || player == null) return DrawVerdict.Draw;
        return DrawGate.Evaluate(deck.CardsInDeck.Count, hand.cardsInPlay.Count, player.PlayerHandSize);
    }
}
```

> Note the `SetPhase(TurnPhase.End)` is intentionally not used as a resting state — `End` exists in the enum for `CanInteract`/`CanMove` correctness and future use, but `EndTurnPressed` transitions straight from the current phase into the next turn's `Explore` (or a new round). This keeps the player from ever being stuck in a dead `End` phase.

- [ ] **Step 2: USER editor steps — create the events + place the controller**

Provide the user these steps (editor work, no YAML edits):
1. Create a **VoidEvent** asset `Assets/Scripts/GameEvents/Events/Turn Events/onPhaseChanged.asset` (right-click ▸ Create ▸ the project's VoidEvent menu item, matching existing VoidEvent assets).
2. Create an **IntEvent** asset `Assets/Scripts/GameEvents/Events/Turn Events/onTurnsRemainingChanged.asset`.
3. Add a `TurnPhaseController` component to the **GameManager** GameObject (or a dedicated `TurnFlow` GameObject) in `GameBoard.unity`.
4. Assign its fields: `doomTuning` = the existing `DoomTuning.asset`; `onPhaseChanged`/`onTurnsRemainingChanged` = the two new assets; `endTheTurn`/`endTheRound` = the **same** VoidEvent assets the current End Turn / End Round buttons raise (find them on `EndTurnButton`/`EndRoundButton`).

- [ ] **Step 3: Manual acceptance**

In Play mode (nothing else rewired yet), confirm via the Console/Inspector that on Start `TurnsRemaining` initializes to 6 (low band) and `CurrentPhase` is `Explore`. No regressions to existing play yet (the controller isn't wired into buttons until Task 7). Then commit.

- [ ] **Step 4: Commit**

```powershell
git add "Assets/Scripts/Managers/TurnPhaseController.cs"
git commit -m "feat: TurnPhaseController phase + day-budget state machine (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

(Commit the new event `.asset`s + scene changes from the editor once the user has wired them.)

---

## Task 5: Undoable movement — `MoveCommand` + `DirectionButton` / `Player` rework

**Files:**
- Create: `Assets/Scripts/Managers/Commands/MoveCommand.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/DirectionButton.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` (`Exploration` stops clearing the stack)

**Interfaces:**
- Consumes: `ICommands`, `GameManager.Instance.commands` (`PlayManager`), `TurnPhaseController.Instance` (Task 4), `TurnPhaseRules.ShouldCommitOnMove`.
- Produces: `class MoveCommand : ICommands` with an execute/undo pair that repositions the player and adjusts the explore pool reversibly.

- [ ] **Step 1: Read the existing command interface**

Open `Assets/Scripts/Managers/Commands/ICommands.cs` and `PlayCommand.cs` to match the `Execute()`/`Undo()`/`Commit()` shape and how `PlayManager.AddCommand` runs `Execute()` on push.

- [ ] **Step 2: Write `MoveCommand`**

Create `Assets/Scripts/Managers/Commands/MoveCommand.cs`:

```csharp
using UnityEngine;

// Undoable board move (spec 2026-07-21). Execute repositions the player and
// spends explore; Undo restores both. Only the no-new-fog branch of
// DirectionButton.Explore builds one of these — a fog-revealing step commits the
// stack instead (irreversible knowledge), so a MoveCommand never re-hides fog.
public class MoveCommand : ICommands
{
    readonly DirectionButton button;
    readonly Vector3 from;
    readonly Vector3 to;
    readonly int exploreCost;

    public MoveCommand(DirectionButton button, Vector3 from, Vector3 to, int exploreCost)
    {
        this.button = button;
        this.from = from;
        this.to = to;
        this.exploreCost = exploreCost;
    }

    public void Execute() => button.ApplyMove(to, exploreCost);
    public void Undo()    => button.ApplyMove(from, +exploreCost, refund: true);
}
```

(If `ICommands` declares `Commit()`, add an empty `public void Commit() { }` — a committed move needs no discard bookkeeping.)

- [ ] **Step 3: Refactor `DirectionButton.Explore` into undoable move vs committing reveal**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/DirectionButton.cs`, replace the body of `Explore()` and add an `ApplyMove` helper. The fog-reveal branch keeps spending explore and now **commits** the stack; the move branch is pushed as a `MoveCommand`:

```csharp
    public void Explore()
    {
        if (TurnPhaseController.Instance != null && !TurnPhaseController.Instance.CanMove)
        {
            GameManager.Instance.ValidationMessage("You can only move during the Explore phase.");
            return;
        }

        var target = player.gridPos + player.compass[direction];
        if (EnemyOccupies(target))
        {
            GameManager.Instance.ValidationMessage("An enemy blocks the way — attack it instead!");
            return;
        }

        if (playerExplore < explore)
        {
            GameManager.Instance.ValidationMessage($"Need {explore} to explore!");
            return;
        }

        var adjTile = player.gridPos + player.compass[direction];
        player.UpdateCompass(adjTile, compass);

        if (fog.HasTile(adjTile))
        {
            // Fog-reveal step: spend explore, uncover fog, and COMMIT — revealed
            // knowledge can't be undone (TurnPhaseRules.ShouldCommitOnMove(true)).
            playerExplore -= explore;
            onSuccessfulExplore_AdjustPlayersExplore.Raise(playerExplore);
            foreach (Directions d in System.Enum.GetValues(typeof(Directions)))
                fog.SetTile(adjTile + compass[d], null);
            var tile = adjTile + compass[direction];
            player.UpdateCompass(tile, compass);
            foreach (Directions d in System.Enum.GetValues(typeof(Directions)))
                fog.SetTile(tile + compass[d], null);
            GameManager.Instance.commands.ClearStack();
        }
        else
        {
            // Ordinary move: undoable.
            var from = player.transform.position;
            var to = gameboard.CellToWorld(gameboard.LocalToCell(from) + player.compass[direction]);
            GameManager.Instance.commands.AddCommand(new MoveCommand(this, from, to, explore));
        }
    }

    // Reposition the player and adjust the explore pool. Used by MoveCommand for
    // both execute (spend) and undo (refund). Raises the position + explore events
    // so the map buttons and HUD stay in sync.
    public void ApplyMove(Vector3 worldPos, int exploreDelta, bool refund = false)
    {
        player.transform.position = worldPos;
        playerExplore += refund ? exploreDelta : -exploreDelta;
        onSuccessfulExplore_AdjustPlayersExplore.Raise(playerExplore);
        sendNewPositionOfPlayer.Raise(player);
    }
```

- [ ] **Step 4: Stop `Player.Exploration` from clearing the stack**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`, `Exploration` currently sets explore and clears the stack. Movement is now undoable, so remove the clear:

```csharp
    public void Exploration(int newExplore)
    {
        playerExplore = newExplore;
        // No ClearStack: movement is undoable now (spec 2026-07-21). The undo
        // stack commits on action-start, End Turn, round-end, or a fog reveal.
    }
```

- [ ] **Step 5: Manual acceptance (editor, Play mode)**

Because this is scene-coupled it is verified in-editor, not by the CLI harness:
1. Move onto an already-revealed hex → press **Undo** → player returns to the previous hex and the spent Explore is refunded.
2. Move into fog (reveal step) → **Undo** is now a no-op for that step (stack was committed); fog stays revealed.
3. Explore cost still deducts correctly; the arrow buttons still enable/disable by adjacency.

- [ ] **Step 6: Commit**

```powershell
git add "Assets/Scripts/Managers/Commands/MoveCommand.cs" "Assets/Scripts/GameObjectScripts/PlayerScripts/DirectionButton.cs" "Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs"
git commit -m "feat: undoable MoveCommand; fog-reveal commits (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Gate interactions — one action per turn via `BeginAction`

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameTokens/EnemyToken.cs` (combat start)
- Modify: the place-menu open entry point (e.g. `TownMenu`/place token click handler)
- Modify: the dungeon-delve entry point (`DungeonPanel` / delve button)

**Interfaces:**
- Consumes: `TurnPhaseController.Instance.CanInteract`, `TurnPhaseController.Instance.BeginAction()`.

- [ ] **Step 1: Find the interaction entry points**

Locate where combat starts (`EnemyToken.StartCombat` / the token click), where a place menu opens (search for the town/keep/castle token click → menu enable), and where a dungeon delve begins (search `DungeonDelve` / `DungeonPanel` delve button). Read each so the guard is inserted at the single true entry.

- [ ] **Step 2: Add the guard + `BeginAction` to combat start**

At the top of the combat-start entry in `EnemyToken` (before the canvas/intro fires), add:

```csharp
        if (TurnPhaseController.Instance != null)
        {
            if (!TurnPhaseController.Instance.CanInteract)
            {
                GameManager.Instance.ValidationMessage("You've already taken your action this turn.");
                return;
            }
            TurnPhaseController.Instance.BeginAction();
        }
```

- [ ] **Step 3: Add the same guard to place-menu open and dungeon-delve entry**

Insert the identical guard block at the true entry point of: (a) opening a Town/Keep/Castle menu, and (b) beginning a dungeon delve. A place *visit* is one action, so guard the **menu open**, not each service inside it — recruit/heal/buy/assault within the open menu stay free (they do not call `BeginAction`).

- [ ] **Step 4: Manual acceptance (editor, Play mode)**

1. Start a fight → after it resolves, moving is blocked and a second fight/place/delve shows "already taken your action."
2. Open a town, recruit **and** heal **and** buy in the same visit — all allowed (one visit = one action).
3. Pressing End Turn resets the cap: next turn you can act again.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: one-action-per-turn gating via BeginAction (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: End Turn routes through the controller; remove End Round

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/DeckScripts/EndTurnButton.cs`
- Delete: `Assets/Scripts/GameObjectScripts/DeckScripts/EndRoundButton.cs`
- Modify: `Assets/Scripts/CardPlay/TurnButtonGate.cs`
- Modify: `Assets/Scripts/GameObjectScripts/DeckScripts/TurnFlowShortcuts.cs`

**Interfaces:**
- Consumes: `TurnPhaseController.Instance.EndTurnPressed()`.

- [ ] **Step 1: Route End Turn through the controller**

In `EndTurnButton.cs`, replace the direct `endTheTurn.Raise()` in both the click listener and `Trigger()` with `TurnPhaseController.Instance.EndTurnPressed()`, and drop the `OnPointerClick` `ClearStack` (the controller commits). Keep the `HandFullUnplayed` guard exactly as-is. The `Update()` interactable gate changes per Step 3.

```csharp
    public void OnPointerClick(PointerEventData eventData) { /* no-op: controller commits */ }

    public bool Trigger()
    {
        if (!endTurnButton.interactable) return false;
        if (HandFullUnplayed())
        {
            GameManager.Instance.ValidationMessage("You cannot end the turn with a full hand.");
            return true;
        }
        TurnPhaseController.Instance.EndTurnPressed();
        return true;
    }
```

And in `Start()` the `onClick` listener:

```csharp
        endTurnButton.onClick.AddListener(() =>
        {
            if (HandFullUnplayed())
            {
                GameManager.Instance.ValidationMessage("You cannot end the turn with a full hand.");
                return;
            }
            TurnPhaseController.Instance.EndTurnPressed();
        });
```

- [ ] **Step 2: Simplify `TurnButtonGate` — End Turn no longer disabled on deck-empty**

The deck-empty case now auto-ends the round through `EndTurnPressed`, so End Turn stays enabled; only combat disables it:

```csharp
public static class TurnButtonGate
{
    // End Turn is available except mid-fight. (Deck-empty no longer blocks it —
    // pressing End Turn auto-ends the round instead; spec 2026-07-21.)
    public static bool EndTurn(bool inCombat) => !inCombat;
}
```

Update `EndTurnButton.Update()` to the new signature and drop the `DrawVerdict` gating (keep the `onDeckCannotRefillTutorial` one-shot fire off the verdict, since the tutorial still teaches the dry-deck rest):

```csharp
    private void Update()
    {
        if (deck == null || hand == null || player == null) return;
        var verdict = DrawGate.Evaluate(deck.CardsInDeck.Count, hand.cardsInPlay.Count, player.PlayerHandSize);
        if (verdict == DrawVerdict.DeckEmpty && lastVerdict != DrawVerdict.DeckEmpty
            && onDeckCannotRefillTutorial != null)
            onDeckCannotRefillTutorial.Raise();
        lastVerdict = verdict;
        endTurnButton.interactable = TurnButtonGate.EndTurn(
            GameManager.Instance.activeCombatant != null || GuardianAssault.AnyInProgress);
    }
```

Also update `TurnButtonGateTests.cs` (`Assets/Tests/EditMode/TurnButtonGateTests.cs`) to the single-arg signature: keep the "disabled in combat / enabled otherwise" cases, delete the deck-empty and `EndRound` cases.

- [ ] **Step 3: Delete `EndRoundButton` + drop the shortcut fallback**

Delete `Assets/Scripts/GameObjectScripts/DeckScripts/EndRoundButton.cs`. In `TurnFlowShortcuts.cs`, remove the `endRound` field and the fallback so North simply triggers End Turn:

```csharp
        if (GameControls.Gameplay.EndTurn.WasPressedThisFrame())
            endTurn.Trigger();
```

- [ ] **Step 4: USER editor steps**

1. In `GameBoard.unity`, remove the **End Round** button GameObject from the HUD (or hide it), and remove any `endTheRound` listener that pointed at the deleted script — the `endTheRound` **event asset stays** (the controller raises it).
2. Remove the `endRound` reference on the `TurnFlowShortcuts` component.
3. Confirm the `EndTurnButton`'s `endTheTurn` event field is still assigned (used only by the controller now, but harmless).

- [ ] **Step 5: Manual acceptance**

1. End Turn advances turns; the day countdown drops by 1 (HUD lands in Task 8 — for now verify via `TurnsRemaining` in the Inspector).
2. On the last turn of the day, End Turn triggers a full reshuffle + Doom tick + unit/skill refresh, and the budget resets from the new band.
3. With an empty deck, End Turn ends the round instead of being disabled.
4. No End Round button remains; gamepad North still ends the turn.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat: End Turn via controller; auto round-end; remove End Round (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: HUD — day countdown + phase label (event-driven)

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs` (stop per-frame Round/Turn text)
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/PhaseHud.cs` (drives both TMPs off events)

**Interfaces:**
- Consumes: `onPhaseChanged` (VoidEvent), `onTurnsRemainingChanged` (IntEvent), `TurnPhaseController.Instance`.

- [ ] **Step 1: Remove the per-frame Round/Turn text**

In `GameManager.cs`, delete the `roundTurnText.text = ...` assignment in `Update()` (and remove the now-unused `Update` body if nothing else remains). The label is re-driven by `PhaseHud`.

- [ ] **Step 2: Write `PhaseHud`**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/PhaseHud.cs`:

```csharp
using TMPro;
using UnityEngine;

// Event-driven HUD (spec 2026-07-21): a "Turns left" day countdown (repurposed
// from the old Round/Turn label) plus a phase label. Updated off the controller's
// events, never per-frame.
public class PhaseHud : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI turnsLeftText; // the repurposed Round/Turn TMP
    [SerializeField] TextMeshProUGUI phaseText;     // new TMP beside it

    // Wired to onTurnsRemainingChanged (IntListener, dynamic int arg).
    public void OnTurnsRemainingChanged(int turnsLeft)
    {
        if (turnsLeftText != null) turnsLeftText.text = "Turns left: " + turnsLeft;
    }

    // Wired to onPhaseChanged (VoidListener).
    public void OnPhaseChanged()
    {
        if (phaseText == null || TurnPhaseController.Instance == null) return;
        phaseText.text = "Phase: " + TurnPhaseController.Instance.CurrentPhase;
    }
}
```

- [ ] **Step 3: USER editor steps**

1. Add a **PhaseHud** component near the existing HUD; assign `turnsLeftText` = the existing Round/Turn `TextMeshProUGUI` (`GameManager.roundTurnText`'s object), and create a new sibling TMP for `phaseText`.
2. On the `onTurnsRemainingChanged` **IntEvent**, add a listener calling `PhaseHud.OnTurnsRemainingChanged` — **pick the Dynamic int** entry in the method dropdown (a Static call passes a hardcoded 0).
3. On the `onPhaseChanged` **VoidEvent**, add a `VoidListener` calling `PhaseHud.OnPhaseChanged`.

- [ ] **Step 4: Manual acceptance**

1. New round shows `Turns left: 6` (low band) and `Phase: Explore`.
2. Taking an action flips the label to `Phase: Action`; End Turn decrements `Turns left`; on 0 it resets to the band value.
3. As Doom crosses into mid/high bands the reset value drops to 4 then 3. No per-frame flicker.

- [ ] **Step 5: Commit**

```powershell
git add "Assets/Scripts/Managers/GameManager.cs" "Assets/Scripts/GameObjectScripts/PlayerScripts/PhaseHud.cs"
git commit -m "feat: event-driven day-countdown + phase HUD (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Save/load — restore the day budget; reset phase

**Files:**
- Modify: the save writer/reader (`Assets/Scripts/Managers/DataManager.cs` and/or `Assets/Scripts/SaveData/SaveSerializer.cs`) where round/turn are already serialized.
- Modify: `Assets/Scripts/Managers/GameManager.cs` if it exposes the round/turn used by the save.

**Interfaces:**
- Consumes: `TurnPhaseController.Instance.TurnsRemaining`, `TurnPhaseController.Instance.LoadState(int)`.

- [ ] **Step 1: Locate the existing round/turn persistence**

Search the save code for where `GameManager.Round` / `Turn` (or `roundNum`/`turnNum`) are written and read. `TurnsRemaining` reuses this existing slot — **no new schema field / no version bump**.

- [ ] **Step 2: Persist `TurnsRemaining` in the existing turn slot**

Where the save currently writes the turn number, write `TurnPhaseController.Instance.TurnsRemaining` instead (keep writing the round number as before, for Doom/spawn continuity). On load, after the scene and `DoomClock` are restored, call:

```csharp
        if (TurnPhaseController.Instance != null)
            TurnPhaseController.Instance.LoadState(savedTurnsRemaining);
```

`LoadState` sets `TurnsRemaining` and resets the phase to Explore with `actionTaken = false` (per spec). Ensure this runs **after** `DoomClock.SetLoaded` so a later same-round `StartRound` (if the deck is dry) would read the right band.

- [ ] **Step 3: Manual acceptance**

1. Mid-run, note `Turns left: N`, quit, reload → `Turns left: N` restored and `Phase: Explore`.
2. Doom, deck, crystals, position unchanged (existing M1 behavior intact).

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat: persist day budget via existing turn slot; reset phase on load (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: Tutorial refresh (content + copy validation)

**Files:**
- Modify: the guided-rail `TutorialStepSO` assets + `HelpEntrySO` (USER editor content authoring).
- Modify: `Assets/Tests/EditMode/TutorialCopyValidationTests.cs` (pin the new copy).

**Interfaces:**
- Consumes: `onPhaseChanged` / `onTurnsRemainingChanged` as rail-step triggers.

- [ ] **Step 1: Update the copy-validation test to the new authored strings**

Read `TutorialCopyValidationTests.cs` to see how it pins authored copy (by id → expected substring). Replace the free-form move/fight/end-turn rail expectations with the phase copy. Add/adjust cases so these substrings are asserted on the relevant step assets:
- Explore step: `"Explore phase"` and `"move"`.
- Action step: `"one action"` (fight, visit, or delve).
- End step: `"End the turn"` and `"day"`.

Write the exact `Assert.That(step.body, Does.Contain("..."))` lines matching the test's existing style.

- [ ] **Step 2: Verify RED against current assets**

Run the EditMode suite (or the mcs harness against the tutorial sources) — the copy assertions fail until the assets are re-authored. Expected: FAIL on the new substrings.

- [ ] **Step 3: USER editor content authoring**

Provide the user exact copy to enter on the assets:
- **Explore step** (`TutorialStepSO`, triggered on round start / first `onPhaseChanged` to Explore): *"This is the Explore phase — spend Explore to move and uncover the map. You can undo moves until you reveal new ground."*
- **Action step** (triggered on `onPhaseChanged` to Action / `BeginAction`): *"You've taken your one action for the turn — a fight, a place visit, or a dungeon delve. Movement is done until next turn."*
- **End step** (triggered near turn end): *"Press End Turn when you're done. The day counts down; at zero it refreshes your hand and Doom rises."*
- **Help entry** (`HelpEntrySO` on the phase/countdown HUD, via a `TutorialTarget` id): the Explore→Action→End rhythm + the one-action rule + the shrinking-day/Doom cadence.
Also drop a `TutorialTarget` on the phase-label object so the highlight frame can point at it.

- [ ] **Step 4: Verify GREEN + manual rail check**

Re-run the copy-validation test → PASS. In a fresh profile, walk the rail: Explore step on round start, Action step after taking an action, End step at turn end; the `?` help opens the phase copy.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: tutorial rail teaches Explore/Action/End + the day (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 11: Full-suite green + play-through acceptance

**Files:** none (verification only).

- [ ] **Step 1: USER runs the EditMode suite in Unity**

Ask the user to open Window ▸ General ▸ Test Runner and run all EditMode tests. Expected: `TurnPhaseRulesTests`, `RoundRulesTests`, `DoomRulesTests`, `TurnButtonGateTests` (updated), `TutorialCopyValidationTests`, and all pre-existing suites are green.

- [ ] **Step 2: Play-through acceptance checklist**

Confirm in one Play session:
- [ ] A turn starts in Explore; moving is allowed and undoable; a fog-reveal move can't be undone.
- [ ] Taking one action blocks a second action and further movement that turn.
- [ ] A place visit allows all its services in one action.
- [ ] End Turn decrements `Turns left`; the phase label tracks state.
- [ ] The day auto-ends at 0 (reshuffle + Doom tick + refresh); budget resets from the current band (6→4→3 across bands).
- [ ] Empty deck ends the round on End Turn (no dead-hand stall); no End Round button exists.
- [ ] Save/quit/reload restores `Turns left` and lands in Explore.
- [ ] The tutorial rail teaches the three phases and the day.

- [ ] **Step 3: Commit any stray `.meta`/scene/asset files** the editor produced during wiring.

```powershell
git add -A
git commit -m "chore: commit editor-generated meta/scene wiring for turn phases (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 12: Documentation + roadmap bookkeeping

**Files:**
- Modify: `.claude/skills/archons-rise-design/mechanics.md` (Turn/Round Flow section)
- Modify: `.claude/skills/archons-rise-design/balance.md` (per-band `turnsPerRound` table)
- Modify: `.claude/skills/archons-rise-roadmap/milestones.md` (add M2.13, note Spec 2 after it)
- Modify: `.claude/skills/archons-rise-roadmap/decisions-log.md` (append the decisions)
- Modify: `.claude/skills/archons-rise-roadmap/status.md` (turn/round reality)

- [ ] **Step 1: Rewrite the mechanics Turn/Round Flow section**

Replace the current free-form description with: strict Explore→Action→End; one encounter/visit per turn; implicit Explore→Action on taking the action; End Turn as the only control; the round as a Doom-band-scaled "day" that auto-ends (turn budget spent or deck can't refill) with a forced rest (reshuffle + Doom tick + refresh); movement undoable except on fog reveal.

- [ ] **Step 2: Add the per-band turn budget to balance.md**

Add a row/table: low band = 6, mid = 4, high = 3 turns per round (starting values), noting they live on `DoomTuning` and shrink the "day" as Doom climbs.

- [ ] **Step 3: Add milestone M2.13**

In `milestones.md`, add **M2.13 — Turn phases & shrinking rounds** (goal, scope, acceptance from this plan), and a one-line note that **Spec 2 — multi-enemy phased combat** is queued next. Update **Current Focus** if appropriate.

- [ ] **Step 4: Append decisions to decisions-log.md**

Append dated (2026-07-21) entries for: the Explore→Action→End turn model; one-encounter/one-visit action; implicit transitions + End Turn as sole control; the Doom-band "day" with automatic round-end and per-band `turnsPerRound`; the repurposed HUD countdown; the movement-undoable / commit-on-fog-reveal undo rule; the no-schema-bump load reset.

- [ ] **Step 5: Update status.md** to reflect the new turn/round reality.

- [ ] **Step 6: Commit**

```powershell
git add ".claude/skills"
git commit -m "docs: mechanics/balance/roadmap for turn phases (M2.13)`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review (completed during authoring)

- **Spec coverage:** §1 turn state machine → Tasks 1, 5, 6, 7; §2 round-as-day → Tasks 2, 3, 4, 7; §3 phase architecture → Tasks 1, 4; §4 undo rework → Tasks 1, 5; §5 HUD/buttons → Tasks 7, 8; §6 tutorial+save → Tasks 9, 10; testing → Tasks 1–3, 11; docs/decisions → Task 12. No uncovered spec section.
- **Type consistency:** `BeginAction()`, `EndTurnPressed()`, `CanMove`, `CanInteract`, `TurnsRemaining`, `LoadState(int)`, `ApplyMove(Vector3,int,bool)`, `TurnPhaseRules.CanMove/CanInteract/ShouldCommitOnMove`, `RoundRules.NextTurnsRemaining/IsRoundOver/DeckCanRefill`, `DoomRules.TurnsForBand` are used with the same signatures across every referencing task.
- **Placeholder scan:** every code step carries complete code; scene-wiring steps are explicit editor instructions (this repo's tested convention), each with a manual acceptance check; no TBD/"handle edge cases"/"similar to Task N".
- **Risk to confirm at execution:** `ICommands` may or may not declare `Commit()` — Task 5 Step 1 reads the interface and adapts `MoveCommand` accordingly; and the exact save read/write site is located in Task 9 Step 1 before editing.
