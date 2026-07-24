# Avatar Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the player's single-clip Animator with a four-state machine (Idle / Walk / Fight / Hurt) that each character overrides with its own clips.

**Architecture:** A new `Avatar` child under the `PlayerPosition` prefab root owns the `SpriteRenderer`, `Animator`, and a `PlayerAvatar` singleton — the root keeps `PlayerPosition.cs`, the Main Camera, and the six move arrows, so animating the avatar can never drag the camera. One base `AnimatorController` is authored once; each `CharacterSO` supplies an `AnimatorOverrideController` filling the same four slots. Interrupt/priority logic lives in a pure, mcs-testable `AvatarStateRules`; the `Animator` calls are a thin shell over it.

**Tech Stack:** Unity 6000.5.1f1, C# (Mono/mcs), NUnit EditMode + the mcs CLI pure-test harness, Unity Animator + AnimatorOverrideController, URP 2D sprites.

## Global Constraints

- **Depends on the character plan.** `CharacterSO.AnimatorController` comes from `docs/superpowers/plans/2026-07-23-character-as-data-and-toughness.md` (Task 4). Land that plan first.
- **Pure classes live in an asmdef folder, MonoBehaviours in Assembly-CSharp.** `AvatarState`/`AvatarStateRules` go in a new `Assets/Scripts/Avatar/` (assembly `ArchonsRise.Avatar`); `PlayerAvatar` is a MonoBehaviour in Assembly-CSharp.
- **Pure test harness = Mono mcs, not csc.** Compile with `"C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge\bin\mcs.bat"` and run the reflection runner under `mono.exe` (same dir). nunit ref: `Library\PackageCache\com.unity.ext.nunit*\net472\unity-custom\nunit.framework.dll`, **copied next to the built DLL + runner in the scratchpad**. csc is C# 5 and rejects `=>`.
- **No C# 8 switch expressions in pure code** — use `if`/ternary/classic `switch`.
- **Animation never gates logic.** Explore spend, fog reveal, `ShouldCommitOnMove`, and phase transitions all fire immediately, exactly as today. The avatar catches up visually.
- **Undo snaps, never animates.** An undo is a correction, not a journey.
- **Never move the prefab root's transform for animation.** The Main Camera is a child of it. All visual motion happens on the `Avatar` child's `localPosition`.
- **State names are contract.** `Idle`, `Walk`, `Fight`, `Hurt` are the AnimatorOverrideController slot keys — never rename after the first character ships.
- **Never hand-edit scene/prefab YAML.** All prefab, controller, and override-controller work is USER editor work in Task 5. `.asmdef` files are plain JSON and **may** be edited directly.
- **Commit after every task.** End commit bodies with `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

**Spec:** `docs/superpowers/specs/2026-07-23-multi-character-and-avatar-animation-design.md` (Part D).

## File Structure

**Create (pure, `ArchonsRise.Avatar`):**
- `Assets/Scripts/Avatar/ArchonsRise.Avatar.asmdef` — new assembly.
- `Assets/Scripts/Avatar/AvatarState.cs` — the four-state enum.
- `Assets/Scripts/Avatar/AvatarStateRules.cs` — priority + interrupt rules.

**Create (tests, pure):**
- `Assets/Tests/EditMode/AvatarStateRulesTests.cs`

**Modify (tests config):**
- `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` — reference `ArchonsRise.Avatar`.

**Create (MonoBehaviour, Assembly-CSharp):**
- `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerAvatar.cs` — the animator shell.

**Modify (Assembly-CSharp):**
- `Assets/Scripts/GameObjectScripts/PlayerScripts/ExplorationController.cs:164-170` — walk hook.
- `Assets/Scripts/Managers/CombatController.cs` — fight + hurt hooks.

---

### Task 1: Pure `AvatarStateRules`

**Files:**
- Create: `Assets/Scripts/Avatar/ArchonsRise.Avatar.asmdef`
- Create: `Assets/Scripts/Avatar/AvatarState.cs`
- Create: `Assets/Scripts/Avatar/AvatarStateRules.cs`
- Modify: `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`
- Test: `Assets/Tests/EditMode/AvatarStateRulesTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `enum AvatarState { Idle, Walk, Fight, Hurt }`; `AvatarStateRules.Priority(AvatarState)` → int; `AvatarStateRules.IsOneShot(AvatarState)` → bool; `AvatarStateRules.ShouldPlay(AvatarState current, AvatarState incoming)` → bool; `AvatarStateRules.ResumeAfter(bool isMoving)` → `AvatarState`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/AvatarStateRulesTests.cs`:

```csharp
using NUnit.Framework;

public class AvatarStateRulesTests
{
    [Test]
    public void Priority_RanksHurtAboveFightAboveWalkAboveIdle()
    {
        Assert.Greater(AvatarStateRules.Priority(AvatarState.Hurt),
                       AvatarStateRules.Priority(AvatarState.Fight));
        Assert.Greater(AvatarStateRules.Priority(AvatarState.Fight),
                       AvatarStateRules.Priority(AvatarState.Walk));
        Assert.Greater(AvatarStateRules.Priority(AvatarState.Walk),
                       AvatarStateRules.Priority(AvatarState.Idle));
    }

    [Test]
    public void Hurt_InterruptsFight()
    {
        Assert.IsTrue(AvatarStateRules.ShouldPlay(AvatarState.Fight, AvatarState.Hurt));
    }

    [Test]
    public void Fight_DoesNotInterruptHurt()
    {
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Hurt, AvatarState.Fight));
    }

    [Test]
    public void Walk_DoesNotInterruptFightOrHurt()
    {
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Fight, AvatarState.Walk));
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Hurt, AvatarState.Walk));
    }

    [Test]
    public void Fight_InterruptsWalkAndIdle()
    {
        Assert.IsTrue(AvatarStateRules.ShouldPlay(AvatarState.Walk, AvatarState.Fight));
        Assert.IsTrue(AvatarStateRules.ShouldPlay(AvatarState.Idle, AvatarState.Fight));
    }

    [Test]
    public void AState_NeverRetriggersItself()
    {
        // Mirrors "Can Transition To Self off" on the Any State transitions.
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Fight, AvatarState.Fight));
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Hurt, AvatarState.Hurt));
        Assert.IsFalse(AvatarStateRules.ShouldPlay(AvatarState.Walk, AvatarState.Walk));
    }

    [Test]
    public void OneShots_AreFightAndHurtOnly()
    {
        Assert.IsTrue(AvatarStateRules.IsOneShot(AvatarState.Fight));
        Assert.IsTrue(AvatarStateRules.IsOneShot(AvatarState.Hurt));
        Assert.IsFalse(AvatarStateRules.IsOneShot(AvatarState.Walk));
        Assert.IsFalse(AvatarStateRules.IsOneShot(AvatarState.Idle));
    }

    [Test]
    public void ResumeAfter_ReturnsToWalkWhileMovingElseIdle()
    {
        Assert.AreEqual(AvatarState.Walk, AvatarStateRules.ResumeAfter(true));
        Assert.AreEqual(AvatarState.Idle, AvatarStateRules.ResumeAfter(false));
    }
}
```

- [ ] **Step 2: Run the pure harness to verify it fails (RED)**

```bash
MCS="/c/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mcs.bat"
MONO="/c/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mono.exe"
NUNIT=$(ls Library/PackageCache/com.unity.ext.nunit*/net472/unity-custom/nunit.framework.dll | head -1)
SCRATCH="/c/Users/DAVE'S~1/AppData/Local/Temp/claude/c--Users-Dave-s-Comp-source-repos-Archon-s-Rise/78663f39-8fe0-4f8a-a88d-5186df8613bb/scratchpad"
mkdir -p "$SCRATCH" && cp "$NUNIT" "$SCRATCH/"
"$MCS" -nologo -target:library "-out:$SCRATCH/AvatarStateRulesTests.dll" "-r:$NUNIT" \
  Assets/Tests/EditMode/AvatarStateRulesTests.cs \
  Assets/Scripts/Avatar/AvatarState.cs Assets/Scripts/Avatar/AvatarStateRules.cs
```

Expected: FAIL — `error CS2001: Source file 'Assets/Scripts/Avatar/AvatarState.cs' could not be found.`

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Avatar/AvatarState.cs`:

```csharp
// The player avatar's four animation states (spec 2026-07-23, Part D). These
// names are the AnimatorOverrideController slot keys AND the base controller's
// state names — never rename them after the first character ships.
public enum AvatarState { Idle, Walk, Fight, Hurt }
```

Create `Assets/Scripts/Avatar/AvatarStateRules.cs`:

```csharp
// Pure avatar interrupt/priority rules. No Unity dependency, matching the
// CombatRules / TurnPhaseRules pattern, so PlayerAvatar stays a thin shell over
// testable logic.
public static class AvatarStateRules
{
    // Higher wins. Hurt outranks Fight because taking a hit should always read,
    // even mid-swing; both outrank locomotion.
    public static int Priority(AvatarState state)
    {
        if (state == AvatarState.Hurt)  return 3;
        if (state == AvatarState.Fight) return 2;
        if (state == AvatarState.Walk)  return 1;
        return 0;
    }

    // One-shots play to completion and then resume; Idle/Walk are looping
    // locomotion states driven by a bool.
    public static bool IsOneShot(AvatarState state)
        => state == AvatarState.Fight || state == AvatarState.Hurt;

    // Whether an incoming request takes over from what is playing. A request
    // that loses is DROPPED, not queued — a stale animation firing seconds
    // later reads worse than not firing at all.
    public static bool ShouldPlay(AvatarState current, AvatarState incoming)
    {
        // Never retrigger the playing state: mirrors "Can Transition To Self
        // off" on the Any State transitions, so a second kill in the same fight
        // cannot restart the swing mid-play.
        if (incoming == current) return false;
        return Priority(incoming) > Priority(current);
    }

    // Where a finished one-shot lands: still walking -> Walk, otherwise Idle.
    public static AvatarState ResumeAfter(bool isMoving)
        => isMoving ? AvatarState.Walk : AvatarState.Idle;
}
```

Create `Assets/Scripts/Avatar/ArchonsRise.Avatar.asmdef`:

```json
{
    "name": "ArchonsRise.Avatar",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": true
}
```

- [ ] **Step 4: Let the EditMode tests see the new assembly**

In `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`, add `"ArchonsRise.Avatar"` to the
`references` array (alphabetically, right after `"ArchonsRise.CardPlay"` is fine — order is not
significant, but keep it tidy):

```json
        "ArchonsRise.CardList",
        "ArchonsRise.CardPlay",
        "ArchonsRise.Avatar",
        "ArchonsRise.Doom",
```

Without this, the EditMode suite fails with `CS0103: The name 'AvatarStateRules' does not exist`.

- [ ] **Step 5: Run the harness to verify it passes (GREEN)**

```bash
"$MCS" -nologo -target:library "-out:$SCRATCH/AvatarStateRulesTests.dll" "-r:$NUNIT" \
  Assets/Tests/EditMode/AvatarStateRulesTests.cs \
  Assets/Scripts/Avatar/AvatarState.cs Assets/Scripts/Avatar/AvatarStateRules.cs
"$MONO" "$SCRATCH/Runner.exe" "$SCRATCH/AvatarStateRulesTests.dll"
```

Expected: 8/8 PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Avatar/ Assets/Tests/EditMode/AvatarStateRulesTests.cs \
        Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef
git commit -m "feat: pure AvatarStateRules (Idle/Walk/Fight/Hurt priority)

Hurt outranks Fight, neither is interruptible by Walk, and nothing
retriggers itself — mirrors 'Can Transition To Self off' on the Any State
transitions. Losing requests are dropped, not queued.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: `PlayerAvatar` component

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerAvatar.cs`

**Interfaces:**
- Consumes: `AvatarStateRules` (Task 1), `DataManager.Instance.ActiveCharacter.AnimatorController` (character plan, Task 4).
- Produces: `PlayerAvatar.Instance`; `PlayerAvatar.Play(AvatarState)`; `PlayerAvatar.PlayWalk(Vector3 offset)`.

**Why a `localPosition` offset instead of tweening to the target:** `ExplorationController.ApplyMove` snaps the *root* so all logic is instantly correct, and the Main Camera is parented to that root. Tweening the root would drag the camera and risk desyncing logic. Instead the root snaps and the avatar child is pushed *back* by the delta, then eased to zero — the character visibly walks into place while every rule has already resolved.

- [ ] **Step 1: Write the implementation**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerAvatar.cs`:

```csharp
using System.Collections;
using UnityEngine;

// The player's animated sprite (spec 2026-07-23, Part D). Lives on an `Avatar`
// CHILD of the PlayerPosition root — the root owns PlayerPosition.cs, the Main
// Camera, and the move arrows, so nothing here can drag the camera.
//
// Presentation only: this component never gates gameplay. Explore spend, fog
// reveal, and phase transitions have all already resolved by the time it runs.
public class PlayerAvatar : MonoBehaviour
{
    public static PlayerAvatar Instance { get; private set; }

    [SerializeField] Animator animator;
    [SerializeField] float moveDuration = 0.25f;
    // How long a Fight/Hurt clip owns the avatar before it resumes. Kept as a
    // scalar rather than read from clip length so an override controller with
    // differently-timed art cannot strand the state machine.
    [SerializeField] float oneShotDuration = 0.4f;

    static readonly int IsWalking    = Animator.StringToHash("isWalking");
    static readonly int FightTrigger = Animator.StringToHash("fight");
    static readonly int HurtTrigger  = Animator.StringToHash("hurt");

    AvatarState current = AvatarState.Idle;
    bool isMoving;
    Coroutine walkRoutine;
    Coroutine oneShotRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        // Per-character clips arrive as an AnimatorOverrideController on the
        // CharacterSO. A null one leaves the base controller in place, so a
        // half-authored character renders instead of crashing.
        var character = DataManager.Instance != null ? DataManager.Instance.ActiveCharacter : null;
        if (character != null && character.AnimatorController != null)
            animator.runtimeAnimatorController = character.AnimatorController;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // A one-shot request (Fight/Hurt). Dropped rather than queued when it loses
    // the priority contest — a stale swing firing seconds late reads worse than
    // no swing at all.
    public void Play(AvatarState state)
    {
        if (!AvatarStateRules.ShouldPlay(current, state)) return;
        current = state;

        if (state == AvatarState.Fight)     animator.SetTrigger(FightTrigger);
        else if (state == AvatarState.Hurt) animator.SetTrigger(HurtTrigger);

        if (!AvatarStateRules.IsOneShot(state)) return;
        if (oneShotRoutine != null) StopCoroutine(oneShotRoutine);
        oneShotRoutine = StartCoroutine(ResumeAfterOneShot());
    }

    IEnumerator ResumeAfterOneShot()
    {
        yield return new WaitForSeconds(oneShotDuration);
        current = AvatarStateRules.ResumeAfter(isMoving);
        oneShotRoutine = null;
    }

    // Called after the root has ALREADY snapped to the destination. `offset` is
    // (from - to): the avatar starts displaced backward and eases home, so the
    // character appears to walk into the hex it is logically already on.
    public void PlayWalk(Vector3 offset)
    {
        if (walkRoutine != null) StopCoroutine(walkRoutine);
        walkRoutine = StartCoroutine(WalkRoutine(offset));
    }

    IEnumerator WalkRoutine(Vector3 offset)
    {
        isMoving = true;
        // The slide always runs (the avatar must end up at its parent's origin),
        // but the walk CLIP only takes over if it wins against what is playing.
        bool animate = AvatarStateRules.ShouldPlay(current, AvatarState.Walk);
        if (animate)
        {
            current = AvatarState.Walk;
            animator.SetBool(IsWalking, true);
        }

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(offset, Vector3.zero, t / moveDuration);
            yield return null;
        }
        transform.localPosition = Vector3.zero;

        isMoving = false;
        if (animate)
        {
            animator.SetBool(IsWalking, false);
            if (current == AvatarState.Walk) current = AvatarState.Idle;
        }
        walkRoutine = null;
    }
}
```

- [ ] **Step 2: Verify Unity compiles**

Let Unity recompile. Expected: no console errors. (Nothing calls it yet, and the `Avatar` child does
not exist until Task 5 — that is fine.)

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerAvatar.cs
git commit -m "feat: PlayerAvatar — animator shell over AvatarStateRules

Slides the avatar child home after the root snaps, so logic resolves
instantly and the camera (a child of that root) never gets dragged by an
animation.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Walk hook in `ExplorationController`

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/ExplorationController.cs:164-170`

**Interfaces:**
- Consumes: `PlayerAvatar.Instance.PlayWalk(Vector3)` (Task 2).
- Produces: nothing new — `ApplyMove`'s signature is unchanged.

- [ ] **Step 1: Write the implementation**

Replace `ApplyMove` in `Assets/Scripts/GameObjectScripts/PlayerScripts/ExplorationController.cs`:

```csharp
    public void ApplyMove(Vector3 worldPos, int exploreDelta, bool refund = false)
    {
        Vector3 from = player.transform.position;
        player.transform.position = worldPos;
        playerExplore += refund ? exploreDelta : -exploreDelta;
        onSuccessfulExplore_AdjustPlayersExplore.Raise(playerExplore);
        sendNewPositionOfPlayer.Raise(player);

        // Cosmetic catch-up ONLY — every rule above has already resolved against
        // the snapped position. An undo (refund) snaps with no walk: an undo is a
        // correction, not a journey.
        if (!refund && PlayerAvatar.Instance != null)
            PlayerAvatar.Instance.PlayWalk(from - worldPos);
    }
```

- [ ] **Step 2: Verify Unity compiles**

Let Unity recompile. Expected: no console errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlayerScripts/ExplorationController.cs
git commit -m "feat: walk animation on move (undo still snaps)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Fight and Hurt hooks in `CombatController`

**Files:**
- Modify: `Assets/Scripts/Managers/CombatController.cs` (`ResolveDefend`, `NotifyDefeated`)

**Interfaces:**
- Consumes: `PlayerAvatar.Instance.Play(AvatarState)` (Task 2).
- Produces: nothing new.

- [ ] **Step 1: Add the Hurt hook**

In `ResolveDefend()`, immediately after the wound loop:

```csharp
        int wounds = CombatRules.GroupWoundCount(player.PlayerDefend, total, player.PlayerToughness);
        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        for (int i = 0; i < wounds; i++) hand.AddWound();

        // Taking the group counterattack reads on the avatar (spec D4).
        if (wounds > 0 && PlayerAvatar.Instance != null)
            PlayerAvatar.Instance.Play(AvatarState.Hurt);
```

> `player.PlayerToughness` assumes the character plan has landed. If it still reads `player.PlayerHP`, land that plan first.

- [ ] **Step 2: Add the Fight hook**

In `NotifyDefeated(EnemyCard card, bool wasInfluence)`, immediately after the live-set removal:

```csharp
        if (!live.Remove(card)) return;

        // Attack/Siege kills swing; Influence removals are the fade-and-drift
        // track and play no attack animation (spec D4).
        if (!wasInfluence && PlayerAvatar.Instance != null)
            PlayerAvatar.Instance.Play(AvatarState.Fight);
```

- [ ] **Step 3: Verify Unity compiles**

Let Unity recompile. Expected: no console errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Managers/CombatController.cs
git commit -m "feat: Fight on a non-influence kill, Hurt on counterattack wounds

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: USER editor work + acceptance

**This task is performed by the USER in the Unity editor**, per the project's standing practice — never by hand-editing YAML.

- [ ] **Step 1: Add the `Avatar` child to the prefab**

Open `Assets/Prefabs/GridPrefab/PlayerPosition.prefab` in prefab mode. The root currently holds
`SpriteRenderer`, `Animator`, `PlayerPosition.cs`, the Main Camera child, and six move-arrow children.

1. Create an empty child of the root named exactly `Avatar`, at `localPosition (0,0,0)`.
2. **Remove** the `SpriteRenderer` and `Animator` from the **root**.
3. Add a `SpriteRenderer` to `Avatar`; set its sprite and `Sorting Order` to what the root's had
   (`Sorting Order 20`).
4. Add an `Animator` to `Avatar`.
5. Add `PlayerAvatar` to `Avatar`; assign its `animator` field to that same `Animator`.

Leave `PlayerPosition.cs`, the Main Camera, and the arrows on the root untouched.

- [ ] **Step 2: Build the base controller**

Create `Assets/Animations/PlayerAvatar.controller`.

**Parameters:** `isWalking` (Bool), `fight` (Trigger), `hurt` (Trigger).

**States** (names must match exactly — they are the override slot keys):

| State | Clip |
|---|---|
| `Idle` (default) | `Assets/Animations/PlayerAnim.anim` |
| `Walk` | `Assets/Animations/PlayerWalk.anim` |
| `Fight` | `Assets/Images/Hero Knight - Pixel Art/Animations/HeroKnight_Attack1.anim` |
| `Hurt` | `Assets/Images/Hero Knight - Pixel Art/Animations/HeroKnight_Hurt.anim` |

**Transitions:**
- `Idle → Walk`: condition `isWalking = true`, **Has Exit Time off**.
- `Walk → Idle`: condition `isWalking = false`, **Has Exit Time off**.
- `Any State → Fight`: condition `fight`, **Has Exit Time off**, **Can Transition To Self OFF**.
- `Any State → Hurt`: condition `hurt`, **Has Exit Time off**, **Can Transition To Self OFF**.
- `Fight → Idle`: **Has Exit Time on** (1.0), no conditions.
- `Hurt → Idle`: **Has Exit Time on** (1.0), no conditions.

Assign this controller to the `Avatar`'s `Animator`.

> `PlayerWalk.anim` is currently wired into nothing — this is where it finally gets used. The old
> `Assets/Animations/PlayerPosition.controller` is now unreferenced and can be deleted once the new
> one is confirmed working.

- [ ] **Step 3: Create the first override controller**

`Assets > Create > Animator Override Controller`, name it `Warlord_Avatar` (match your character).
Set its base to `PlayerAvatar.controller`. Its four slots auto-populate with the base clips; leave
them as-is for now — this proves the override path works before any new art exists.

Assign it to the character asset's `animatorController` field.

- [ ] **Step 4: Tune the timings**

Play the game and adjust `PlayerAvatar`'s `moveDuration` (default 0.25s) so the slide matches the
walk clip's pace, and `oneShotDuration` (default 0.4s) so it roughly matches the attack/hurt clip
lengths.

- [ ] **Step 5: Acceptance checklist**

- [ ] The avatar idles on the map.
- [ ] Moving to an adjacent hex plays Walk and the character slides into place, then returns to Idle.
- [ ] **Undo of a move snaps with no walk animation.**
- [ ] A move's explore spend, fog reveal, and phase transition all happen immediately — the tween
      never delays them.
- [ ] Killing an enemy in the Attack phase triggers Fight.
- [ ] Removing an enemy via Influence does **not** trigger Fight.
- [ ] A counterattack inflicting ≥ 1 wound triggers Hurt.
- [ ] Two kills in quick succession do not restart the swing mid-play.
- [ ] Clearing `animatorController` on the character still renders — the base controller takes over.
- [ ] The camera never moves during a Fight or Hurt clip.
- [ ] `AvatarStateRulesTests` 8/8 green, and the full EditMode suite green in Test Runner.

- [ ] **Step 6: Commit the asset changes**

```bash
git add Assets/
git commit -m "content: Avatar child node, base animator controller, first override

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
