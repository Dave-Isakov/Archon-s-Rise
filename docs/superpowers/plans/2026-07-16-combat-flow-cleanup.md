# Combat Flow Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make combat feel finished — a reliable intro transition every fight, an automatic reward-naming defeat message that ends the fight with no click on the enemy card, and a glow on enemy tokens the player can fight.

**Architecture:** A single `GameManager.ResolveDefeat(EnemyCard)` orchestrator sequences defeat → reward message → card pick → teardown through the existing `RewardQueue` modal arbiter. The intro tween is driven from code (`GameManager.PlayCombatIntro`) instead of reading a TMP `enabled` flag. Two thin pure helpers (`GlowPulse`, `DefeatMessage`) are unit-tested; the MonoBehaviour glue is verified in-editor.

**Tech Stack:** Unity 6000.5.1f1, C#, TextMeshPro, NUnit EditMode tests (or the mcs/mono pure-test harness while the editor is open).

## Global Constraints

- All reward/message modals go through `RewardQueue.Instance.Enqueue` — never open a reward or message canvas directly. Card picks enqueue via `Rewards.OfferCardChoice` (which self-enqueues), always *after* the reward message.
- Player-facing stat/reward words use the icon language (`IconMarkup` TMP sprite tags), never typed-out words.
- Never hand-edit scene/prefab YAML. Scene/prefab wiring (glow child sprite, `GameManager` serialized fields) is done by the user in the editor from step-by-step instructions.
- Pure logic classes each live in a folder with an asmdef already referenced by `ArchonsRise.Tests.EditMode.asmdef` (`ArchonsRise.CardPlay`, `ArchonsRise.UiLanguage`). MonoBehaviours that touch `GameManager`/`Player`/etc. stay in `Assets/Scripts/GameObjectScripts/` (Assembly-CSharp).
- `CombatRules` is not modified.

### Running EditMode tests while the Unity editor is open

Batch-mode `-runTests` is blocked by the editor lock, so pure tests can be run via the CLI harness:

```
MCS="C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mcs.bat"
MONO="C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mono.exe"
NUNIT=$(ls Library/PackageCache/com.unity.ext.nunit*/net472/unity-custom/nunit.framework.dll)
# Compile the test dll (list every source the test needs) + the reflection Runner, then run under mono:
"$MCS" -nologo -target:library "-out:Tests.dll" "-r:$NUNIT" <sources...> <TestFile.cs>
"$MCS" -nologo "-out:Runner.exe" Runner.cs           # Runner.cs = the reflection runner (see Task 1)
"$MONO" Runner.exe Tests.dll
```

`Runner.cs` reflection runner (write once to the scratchpad):

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
        foreach (var type in asm.GetTypes())
        {
            var tests = type.GetMethods()
                .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute"))
                .ToList();
            if (tests.Count == 0) continue;
            foreach (var m in tests)
            {
                object instance = m.IsStatic ? null : Activator.CreateInstance(type);
                try { m.Invoke(instance, null); pass++; }
                catch (Exception e)
                {
                    fail++;
                    var inner = e is TargetInvocationException ? e.InnerException : e;
                    Console.WriteLine($"FAIL {type.Name}.{m.Name}: {inner.Message}");
                }
            }
        }
        Console.WriteLine($"\n{pass} passed, {fail} failed");
        return fail == 0 ? 0 : 1;
    }
}
```

When the editor is closed, the normal Unity Test Runner (EditMode) runs these same files with no harness.

---

## File Structure

- `Assets/Scripts/CardPlay/GlowPulse.cs` — **new** pure helper: adjacency-glow alpha from time (Task 1).
- `Assets/Tests/EditMode/GlowPulseTests.cs` — **new** tests (Task 1).
- `Assets/Scripts/UiLanguage/DefeatMessage.cs` — **new** pure helper: composes the defeat + reward message in the icon language (Task 2).
- `Assets/Tests/EditMode/DefeatMessageTests.cs` — **new** tests (Task 2).
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs` — **modify**: `GetReward` returns `RewardSummary`, no longer self-enqueues the card pick (Task 3).
- `Assets/Scripts/Managers/GameManager.cs` — **modify**: add `PlayCombatIntro`, `ResolveDefeat`, teardown coroutine, serialized fields; reset banner in `CombatCanvasActive` (Task 4).
- `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` — **modify**: `ResolveAttack`/`CompleteInfluence` call `ResolveDefeat` instead of raising the event (Task 4).
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs` — **modify**: intro via `PlayCombatIntro`; glow field + pulse in `Update` (Tasks 4 & 5).
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs` — **modify**: remove dead code, simplify `OnPointerClick` (Task 6).

---

### Task 1: `GlowPulse` pure helper + tests

**Files:**
- Create: `Assets/Scripts/CardPlay/GlowPulse.cs`
- Test: `Assets/Tests/EditMode/GlowPulseTests.cs`

**Interfaces:**
- Produces: `static float GlowPulse.Alpha(float time, float min, float max, float speed)` — sine-oscillated alpha in `[min, max]`; `speed` is radians/second.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/GlowPulseTests.cs`:

```csharp
using NUnit.Framework;

public class GlowPulseTests
{
    [Test]
    public void Alpha_At_Time_Zero_Is_Midpoint()
    {
        // sin(0) = 0 -> normalized 0.5 -> midpoint of [0.3, 1.0] = 0.65
        Assert.AreEqual(0.65f, GlowPulse.Alpha(0f, 0.3f, 1.0f, 4f), 0.0001f);
    }

    [Test]
    public void Alpha_Peaks_At_Quarter_Period()
    {
        // sin(pi/2) = 1 -> normalized 1 -> max. speed = 1 so time = pi/2.
        Assert.AreEqual(1.0f, GlowPulse.Alpha((float)(System.Math.PI / 2), 0.3f, 1.0f, 1f), 0.0001f);
    }

    [Test]
    public void Alpha_Stays_Within_Bounds()
    {
        for (int i = 0; i < 200; i++)
        {
            float a = GlowPulse.Alpha(i * 0.137f, 0.3f, 1.0f, 4f);
            Assert.GreaterOrEqual(a, 0.3f - 0.0001f);
            Assert.LessOrEqual(a, 1.0f + 0.0001f);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (harness): compile with `GlowPulseTests.cs` only as source (GlowPulse not yet created).
Expected: FAIL — `The name 'GlowPulse' does not exist` (compile error) or missing type.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/CardPlay/GlowPulse.cs`:

```csharp
using System;

// Adjacency-glow alpha (spec 2026-07-16). Pure/mcs-testable: no UnityEngine
// dependency so the pulse math is unit-tested via the CLI harness. EnemyToken
// feeds Time.time in and applies the result to the halo SpriteRenderer's alpha.
public static class GlowPulse
{
    // Alpha oscillates on a sine between min and max. speed is radians/second.
    public static float Alpha(float time, float min, float max, float speed)
    {
        float t = (float)((Math.Sin(time * speed) + 1.0) * 0.5); // 0..1
        return min + (max - min) * t;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run (harness):
```
"$MCS" -nologo -target:library "-out:Tests.dll" "-r:$NUNIT" \
  "Assets/Scripts/CardPlay/GlowPulse.cs" "Assets/Tests/EditMode/GlowPulseTests.cs"
"$MONO" Runner.exe Tests.dll
```
Expected: `3 passed, 0 failed`.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/CardPlay/GlowPulse.cs" "Assets/Tests/EditMode/GlowPulseTests.cs"
git commit -m "feat: GlowPulse pure helper for enemy token adjacency glow"
```

---

### Task 2: `DefeatMessage` pure helper + tests

**Files:**
- Create: `Assets/Scripts/UiLanguage/DefeatMessage.cs`
- Test: `Assets/Tests/EditMode/DefeatMessageTests.cs`

**Interfaces:**
- Consumes: `IconMarkup.Cost(IconConcept, int)`, `IconMarkup.CrystalTag(EmpowerType)` (existing, `ArchonsRise.UiLanguage`); `EmpowerType` (`ArchonsRise.Enums`).
- Produces: `static string DefeatMessage.Compose(string enemyName, int exp, EmpowerType? crystal, bool cardPick)` — the post-combat defeat + reward line.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/DefeatMessageTests.cs`:

```csharp
using NUnit.Framework;

public class DefeatMessageTests
{
    [Test]
    public void Exp_Only_Names_Experience_With_Icon()
    {
        var s = DefeatMessage.Compose("Goblin", 12, null, false);
        Assert.AreEqual(
            "Goblin has been defeated. You receive " + IconMarkup.Cost(IconConcept.Experience, 12) + ".",
            s);
    }

    [Test]
    public void Includes_Crystal_Tag_When_Present()
    {
        var s = DefeatMessage.Compose("Ogre", 5, EmpowerType.Red, false);
        StringAssert.Contains(IconMarkup.CrystalTag(EmpowerType.Red), s);
    }

    [Test]
    public void Mentions_Card_Pick_When_Pending()
    {
        var s = DefeatMessage.Compose("Wolf", 3, null, true);
        StringAssert.Contains("a new card to choose", s);
    }

    [Test]
    public void No_Card_Phrase_When_Not_Pending()
    {
        var s = DefeatMessage.Compose("Rat", 1, null, false);
        StringAssert.DoesNotContain("card", s);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (harness) with sources `IconConcept.cs`, `IconMarkup.cs`, the `EmpowerType` enum source, and the test file (DefeatMessage not yet created).
Expected: FAIL — `The name 'DefeatMessage' does not exist`.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/UiLanguage/DefeatMessage.cs`:

```csharp
// Post-combat defeat + reward line in the icon language (spec 2026-07-16).
// Pure/mcs-testable: no UnityEngine dependency. GameManager.ResolveDefeat feeds
// the RewardSummary fields in and routes the result through ValidationMessage.
public static class DefeatMessage
{
    public static string Compose(string enemyName, int exp, EmpowerType? crystal, bool cardPick)
    {
        string msg = $"{enemyName} has been defeated. You receive "
                     + IconMarkup.Cost(IconConcept.Experience, exp);
        if (crystal.HasValue)
            msg += " " + IconMarkup.CrystalTag(crystal.Value);
        if (cardPick)
            msg += " and a new card to choose";
        return msg + ".";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run (harness):
```
EMPOWER=$(grep -rl "enum EmpowerType" Assets/Scripts | head -1)
"$MCS" -nologo -target:library "-out:Tests.dll" "-r:$NUNIT" \
  "Assets/Scripts/UiLanguage/IconConcept.cs" "Assets/Scripts/UiLanguage/IconMarkup.cs" \
  "$EMPOWER" "Assets/Scripts/UiLanguage/DefeatMessage.cs" \
  "Assets/Tests/EditMode/DefeatMessageTests.cs"
"$MONO" Runner.exe Tests.dll
```
Expected: `4 passed, 0 failed`.
(If `IconMarkup.CrystalTag` pulls in `EmpowerType.IsAllColors()`, add that extension's source file to the `mcs` source list. When the editor is closed, the Unity Test Runner resolves all of this automatically.)

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/UiLanguage/DefeatMessage.cs" "Assets/Tests/EditMode/DefeatMessageTests.cs"
git commit -m "feat: DefeatMessage composes reward-naming defeat text in the icon language"
```

---

### Task 3: `RewardSummary` + `Rewards.GetReward` refactor

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs`

**Interfaces:**
- Consumes: `RewardRules.SampleExp`, `RewardRules.Roll`, `tuning.CrystalChance/CardChance/CardPool`, `EnemiesSO.tier` (existing).
- Produces: `struct RewardSummary { int exp; EmpowerType? crystal; bool cardPick; int tier; }`; `RewardSummary Rewards.GetReward(EnemyCard)` (applies exp + crystal instantly, reports whether a card pick is pending, does **not** enqueue it). `OfferCardChoice(int, System.Action)` stays unchanged and is called by the orchestrator.

This task is Unity glue (touches `Player`, `CrystalInventory`, `RewardCanvas`), so it is verified by compiling + the Task 4 in-editor run rather than an isolated unit test — the pure exp/roll logic it calls is already covered by `RewardRulesTests`.

- [ ] **Step 1: Add the `RewardSummary` struct**

At the top of `Rewards.cs` (above the class), add:

```csharp
// What a defeat granted, so GameManager.ResolveDefeat can name it in the
// message and decide whether to open the card pick.
public struct RewardSummary
{
    public int exp;
    public EmpowerType? crystal; // null when no crystal rolled
    public bool cardPick;        // a card choice is pending (roll hit AND pool non-empty)
    public int tier;
}
```

- [ ] **Step 2: Change `GetReward` to return a summary**

Replace the existing `GetReward`:

```csharp
    // Wired from GameManager.ResolveDefeat. Applies exp (+ any crystal) instantly
    // and reports the result; the card pick, if any, is opened by the caller so
    // it lands after the defeat message. Dungeon fights pay experience only.
    public RewardSummary GetReward(EnemyCard enemy)
    {
        if (DungeonDelve.AnyInProgress) return GrantExpOnly(enemy.enemySO.tier);
        return Grant(enemy.enemySO.tier);
    }
```

- [ ] **Step 3: Change `GrantExpOnly` to return a summary**

Replace `GrantExpOnly`:

```csharp
    // Per-fight dungeon grant: the tier's bell exp sample, no bonus rolls.
    public RewardSummary GrantExpOnly(int tier)
    {
        int exp = RewardRules.SampleExp(tier, tuning.Data, max => Random.Range(0, max));
        player.PlayerExp += exp;
        return new RewardSummary { exp = exp, crystal = null, cardPick = false, tier = tier };
    }
```

(`GrantDungeonCompletion` calls `GrantExpOnly(tier)` for its side effect and ignores the return value — no change needed there.)

- [ ] **Step 4: Change `Grant` to return a summary and stop enqueuing the card**

Replace `Grant`:

```csharp
    RewardSummary Grant(int tier)
    {
        int exp = RewardRules.SampleExp(tier, tuning.Data, max => Random.Range(0, max));
        player.PlayerExp += exp;

        EmpowerType? crystal = null;
        if (RewardRules.Roll(tuning.CrystalChance(tier), () => Random.value))
        {
            var color = RandomCrystalColor();
            crystals.CreateCrystal(color);
            crystal = color;
        }

        var pool = tuning.CardPool(tier);
        bool cardPick = RewardRules.Roll(tuning.CardChance(tier), () => Random.value)
                        && pool != null && pool.Count > 0;

        return new RewardSummary { exp = exp, crystal = crystal, cardPick = cardPick, tier = tier };
    }
```

- [ ] **Step 5: Verify it compiles**

Reload the editor (or `Ctrl+R`) and confirm no console compile errors. `OfferCardChoice`, `GrantDungeonCompletion`, and `OfferCardChoiceForLevel` are unchanged.
Expected: clean compile.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs"
git commit -m "refactor: Rewards.GetReward returns a RewardSummary and defers the card pick"
```

---

### Task 4: `GameManager.ResolveDefeat`, intro coroutine, and caller rewiring

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs`

**Interfaces:**
- Consumes: `Rewards.GetReward` / `Rewards.OfferCardChoice` (Task 3), `DefeatMessage.Compose` (Task 2), existing `RewardQueue`, `CheckCombatants`, `ValidationMessage`, `commands.ClearStack`.
- Produces: `IEnumerator GameManager.PlayCombatIntro()`; `void GameManager.ResolveDefeat(EnemyCard)`.

- [ ] **Step 1: Add serialized fields to `GameManager`**

In `GameManager.cs`, next to the other combat fields (near `combatCanvas`, line ~18), add:

```csharp
    [SerializeField] Rewards rewards;
    [SerializeField] TextMeshProUGUI combatBanner; // the "Combat!" intro text
    [SerializeField] string combatIntroState = "CombatIntro";
    [SerializeField] float combatIntroDuration = 1.5f;
```

- [ ] **Step 2: Add `PlayCombatIntro` and reset the banner in `CombatCanvasActive`**

Replace `CombatCanvasActive` with:

```csharp
    public void CombatCanvasActive()
    {
        combatCanvas.enabled = true;
        combatCanvas.GetComponentInChildren<Animator>().enabled = true;
        if (combatBanner != null) combatBanner.enabled = false; // no intro flash for guardian/dungeon
        if (fleeButton != null) fleeButton.gameObject.SetActive(true);
    }

    // Field-combat intro: enable the canvas, replay the authored banner clip from
    // frame 0 (deterministic — no longer keyed off the banner TMP's enabled flag,
    // which never reset and made the intro play only once), wait its duration.
    public IEnumerator PlayCombatIntro()
    {
        combatCanvas.enabled = true;
        var animator = combatCanvas.GetComponentInChildren<Animator>(true);
        if (combatBanner != null) combatBanner.enabled = true;
        if (animator != null)
        {
            animator.enabled = true;
            animator.Play(combatIntroState, 0, 0f);
        }
        if (fleeButton != null) fleeButton.gameObject.SetActive(true);
        yield return new WaitForSeconds(combatIntroDuration);
    }
```

- [ ] **Step 3: Add `ResolveDefeat` and the teardown coroutine**

Add to `GameManager` (near `CheckCombatants`/`EndCombat`):

```csharp
    // Single defeat orchestrator (spec 2026-07-16). Applies rewards, shows a
    // reward-naming message, opens the card pick after it, then tears the fight
    // down — all serialized through RewardQueue so ordering is correct by
    // construction. Replaces the OnEnemyDefeat_GetRewards fan-out and the old
    // click-the-defeated-card teardown.
    public void ResolveDefeat(EnemyCard enemy)
    {
        RewardSummary summary = rewards.GetReward(enemy);

        ValidationMessage(DefeatMessage.Compose(
            enemy.enemySO.cardName, summary.exp, summary.crystal, summary.cardPick));

        if (summary.cardPick)
            rewards.OfferCardChoice(summary.tier); // self-enqueues, lands after the message

        RewardQueue.Instance.Enqueue(done => StartCoroutine(TeardownDefeat(enemy, done)));
    }

    // Runs as a coroutine so watcher Updates (GuardianAssault/DungeonDelve/
    // EnemyToken) react to isDefeated before the close check. CheckCombatants
    // must see the defeated card STILL present: childCount == 1 means it was the
    // last enemy (close); a spawned next-guardian makes it 2 (stay open).
    private IEnumerator TeardownDefeat(EnemyCard enemy, System.Action done)
    {
        enemy.isDefeated = true;
        yield return null; // let watcher Updates spawn any next-guardian card
        CheckCombatants(); // closes + EndCombat only if the defeated card is the last
        Destroy(enemy.gameObject);
        commands.ClearStack();
        done();
    }
```

- [ ] **Step 4: Point `Player` at `ResolveDefeat`**

In `Player.cs`, in `ResolveAttack`, replace the final line:

```csharp
        OnEnemyDefeat_GetRewards.Raise(enemy);
```
with:
```csharp
        GameManager.Instance.ResolveDefeat(enemy);
```

And in `CompleteInfluence`, replace:

```csharp
        OnEnemyDefeat_GetRewards.Raise(enemy);  // rewards + the defeat/cleanup chain; no counterattack ran = wound-free
```
with:
```csharp
        GameManager.Instance.ResolveDefeat(enemy);  // rewards + defeat message + teardown; no counterattack ran = wound-free
```

Delete the now-unused `[SerializeField] EnemyCardEvent OnEnemyDefeat_GetRewards;` field declaration (near line 57). Leave `onDefeat_WoundPlayer` and the wound `ValidationMessage` untouched.

- [ ] **Step 5: Drive the field intro through `PlayCombatIntro`**

In `EnemyToken.cs`, replace `StartCombat`:

```csharp
    IEnumerator StartCombat()
    {
        GameManager.Instance.activeCombatant = this;
        yield return GameManager.Instance.PlayCombatIntro();
        deck.GetNewEnemyCard(this);
    }
```

(The old `CombatCanvasActive()` + `WaitUntil(... TextMeshProUGUI.enabled == false)` are removed — `PlayCombatIntro` enables the canvas and owns the timing.)

- [ ] **Step 6: Editor wiring (user, manual — provide these as instructions)**

On the `GameManager` component in `GameBoard.unity`:
- Assign **Rewards** → the `Rewards` MonoBehaviour object in the scene.
- Assign **Combat Banner** → the "Combat!" `TextMeshProUGUI` under the combat canvas.
- Set **Combat Intro State** → the exact state name of the intro clip in the combat-canvas Animator controller (default `CombatIntro`).
- Set **Combat Intro Duration** → the intro clip's length in seconds (default `1.5`).

- [ ] **Step 7: Verify in-editor (manual)**

Run the scene and confirm (see Task 7 for the full script):
1. Clicking an adjacent enemy plays the intro banner **every** fight (not just the first), then the enemy card appears.
2. Defeating an enemy that grants only experience auto-shows "*<enemy> has been defeated. You receive [xp]N.*", the player clears it with Return, and the combat canvas closes with no click on the enemy card.
3. A defeat that also rolls a card shows the message, then the card-pick modal, then closes.
Expected: all three hold.

- [ ] **Step 8: Commit**

```bash
git add "Assets/Scripts/Managers/GameManager.cs" \
        "Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs" \
        "Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs"
git commit -m "feat: single ResolveDefeat orchestrator + code-driven combat intro"
```

---

### Task 5: Enemy token adjacency glow

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs`
- Prefab (user, manual): `Assets/Prefabs/GameTokens/EnemyToken.prefab`

**Interfaces:**
- Consumes: `GlowPulse.Alpha` (Task 1), existing `isAggro`, `MapFog.IsHidden(gridPos)`.

- [ ] **Step 1: Add the glow field and pulse logic**

In `EnemyToken.cs`, add the field near the other serialized fields:

```csharp
    [SerializeField] SpriteRenderer glow; // soft halo child, pulses while the player is adjacent
```

Add glow driving to `Update` (append to the existing `Update`, keeping the current defeated-card cleanup block):

```csharp
        // Adjacency affordance: the halo pulses while the player stands next to
        // this token (isAggro) and the token isn't fog-hidden.
        if (glow != null)
        {
            bool show = isAggro && !MapFog.IsHidden(gridPos);
            if (show)
            {
                if (!glow.enabled) glow.enabled = true;
                var c = glow.color;
                c.a = GlowPulse.Alpha(Time.time, 0.3f, 1.0f, 4f);
                glow.color = c;
            }
            else if (glow.enabled)
            {
                glow.enabled = false;
            }
        }
```

- [ ] **Step 2: Verify it compiles**

Reload the editor; confirm no compile errors.
Expected: clean compile.

- [ ] **Step 3: Prefab wiring (user, manual — provide as instructions)**

On `Assets/Prefabs/GameTokens/EnemyToken.prefab`:
- Add a child GameObject named `Glow` with a `SpriteRenderer` using a soft round halo sprite.
- Set its sorting so it renders **behind** the token sprite (lower order-in-layer, or a sorting layer behind the token).
- Start it disabled (uncheck the SpriteRenderer, or it will be turned off on the first non-adjacent frame anyway).
- Assign the token's **Glow** field → this child `SpriteRenderer`.

- [ ] **Step 4: Verify in-editor (manual)**

Run the scene: move the player character onto a hex adjacent to an enemy token and confirm the halo fades in and pulses; move away and confirm it turns off; a fog-hidden token never glows.
Expected: glow tracks adjacency.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs"
git commit -m "feat: enemy tokens glow while the player is adjacent"
```

---

### Task 6: `EnemyCard` dead-code cleanup

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs`

**Interfaces:**
- No new interface. Removes now-dead members; keeps the button hooks (`DefeatMonster`/`SiegeMonster`) that route into `Player.ResolveAttack`.

- [ ] **Step 1: Remove the commented-out dead code**

Delete the commented `CheckWounds` block, the commented `OnPointerEnter`/`OnPointerExit` blocks, and the commented reward stubs inside `DefeatMonster`. Keep `DefeatMonster`/`SiegeMonster` (they raise the still-used attack/siege validation events) and `EnableCombat`.

- [ ] **Step 2: Simplify `OnPointerClick`**

Teardown is now automatic (Task 4), so the `isDefeated == true` branch is dead. Replace `OnPointerClick` with just the out-of-range preview dismiss:

```csharp
    public void OnPointerClick(PointerEventData eventData)
    {
        // Out-of-range preview card: a click dismisses the peek (fight buttons are
        // disabled here). A real defeat now tears itself down via ResolveDefeat.
        if (!isDefeated && !fightButton.interactable)
        {
            GameManager.Instance.combatCanvas.enabled = false;
            Destroy(this.gameObject);
        }
    }
```

- [ ] **Step 3: Remove `DestroyEnemyObject` if now unused**

`DestroyEnemyObject(EnemyCard)` was the scene-wired `OnEnemyDefeat_GetRewards` listener (`isDefeated = true` + `ClearStack`); `ResolveDefeat`'s teardown does this now. Confirm no remaining references (search the solution for `DestroyEnemyObject`) and delete the method. If a scene UnityEvent still references it, leave the method but add a one-line comment that it is legacy; prefer removing the scene wiring.

- [ ] **Step 4: Verify it compiles and the flow still works**

Reload the editor; confirm clean compile. Re-run the defeat scenario from Task 4 Step 7 to confirm nothing regressed.
Expected: clean compile; defeat flow unchanged.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs"
git commit -m "refactor: drop dead combat code from EnemyCard, simplify OnPointerClick"
```

---

### Task 7: Full manual verification pass

**Files:** none (verification only).

- [ ] **Step 1: Intro replays every fight**

Start a run, fight an enemy, win, then fight a second enemy. Confirm the intro banner animates on **both** fights (the original bug: it only showed the first time).

- [ ] **Step 2: XP-only defeat is self-completing**

Defeat an enemy whose roll grants only experience. Confirm: the "*<enemy> has been defeated. You receive [xp]N.*" message appears on its own, the player dismisses it with Return, and the combat canvas closes — with **no** click on the enemy card.

- [ ] **Step 3: Defeat with a crystal and/or card**

Defeat enemies until you get a crystal roll (message names the crystal) and a card roll (message mentions a new card, then the card-pick modal opens after the message, then the canvas closes).

- [ ] **Step 4: Guardian chain stays open**

Start a guardian assault on a multi-guardian place. Defeat the first guardian; confirm the reward message shows, then the **next** guardian appears (canvas does not close mid-chain). Defeat the last; confirm the conquest message and the canvas closing.

- [ ] **Step 5: Dungeon delve**

Run a dungeon delve fight; confirm the exp-only reward message shows and the canvas closes cleanly.

- [ ] **Step 6: Flee still works**

Start a fight and Flee; confirm the wound + flee message and canvas teardown are unchanged.

- [ ] **Step 7: Token glow**

Confirm the adjacency glow from Task 5 Step 4 once more in a full run context.

- [ ] **Step 8: Final commit (if any polish tweaks were made)**

```bash
git add -A
git commit -m "chore: combat flow cleanup verification polish"
```

---

## Notes for the implementer

- If `Random` is ambiguous in `Rewards.cs`, it already resolves to `UnityEngine.Random` there — keep the existing usage.
- `RewardSummary` lives in Assembly-CSharp (declared in `Rewards.cs`), so `GameManager` and `Player` see it with no asmdef changes.
- Do not open any reward/message canvas directly; everything routes through `RewardQueue` / `ValidationMessage` / `OfferCardChoice`.
- The intro Animator clip itself is authored content — this plan only fixes how it is triggered and timed.
