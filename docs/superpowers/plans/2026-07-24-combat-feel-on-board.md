# Combat Feel — Fighting on the Board Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make combat visibly take place on the board — transparent backdrop, a semicircle enemy arc around the player, unique enemy art, idle card sway, and a token↔card morph.

**Architecture:** One pure geometry class (`CombatLayoutRules`, mcs-tested) computes arc slots; `CombatController` applies them. Everything else is presentation-only MonoBehaviours (`CombatBackdrop`, `EnemyCardIdleSway`, `EnemyCardMorph`) plus a sprite field on `EnemiesSO`. No combat logic changes — spend, phases, kill banking, and rewards resolve exactly as today; the visuals catch up.

**Tech Stack:** Unity 6000.5.1f1, C#, uGUI (Screen Space – Camera canvas), NUnit EditMode + the Mono `mcs` pure-test harness.

## Global Constraints

- **Pure files must not reference `UnityEngine`.** `CombatLayoutRules.cs` is compiled by the mcs harness — it returns a plain `struct`, never `Vector2`. (Ref: [[unity-pure-test-harness-mcs]].)
- **New pure class needs its own folder asmdef AND a tests-asmdef reference**, or EditMode tests fail CS0103. MonoBehaviours stay in the main assembly (`Assets/Scripts/GameObjectScripts/…`). (Ref: [[pure-class-asmdef-placement]].)
- **Never hand-edit scene/prefab/`.asset`/`.meta` YAML.** All prefab, scene, and component-wiring authoring is USER editor work performed from step-by-step instructions. `.asmdef` files are plain JSON and **may** be edited directly. (Ref: [[manual-unity-edits-for-risky-changes]].)
- **Editor is usually open**, so `Unity.exe -runTests` is blocked. Verify pure classes RED/GREEN via the mcs harness; ask the USER to confirm in Window ▸ General ▸ Test Runner. (Ref: [[unity-editmode-tests-while-editor-open]].)
- **Presentation only.** None of this code may gate combat logic. The FX run after the controller has already resolved the fight state.
- mcs: `C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge\bin\mcs.bat`; mono: `…\MonoBleedingEdge\bin\mono.exe`; nunit: `Library\PackageCache\com.unity.ext.nunit@*\net472\unity-custom\nunit.framework.dll`.

---

## File Structure

**New — pure logic (own asmdef):**
- `Assets/Scripts/CombatLayout/CombatLayoutRules.cs` — arc geometry (`Slot` struct + `SlotFor`/`AngleFor`/`ScaleFor`).
- `Assets/Scripts/CombatLayout/ArchonsRise.CombatLayout.asmdef` — `references: []`, `autoReferenced: true`.
- `Assets/Tests/EditMode/CombatLayoutRulesTests.cs` — NUnit tests.

**New — MonoBehaviours (main assembly):**
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/CombatBackdrop.cs` — fades the backdrop image.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardIdleSway.cs` — subtle idle motion on a child pivot.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardMorph.cs` — token↔card fly-in / return.

**Modified:**
- `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef` — add `ArchonsRise.CombatLayout` reference.
- `Assets/Scripts/GameScriptableObjectTypes/EnemiesSO.cs` — add `cardArt`.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs` — set artwork sprite.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardDefeatFx.cs` — stop the sway before FX.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs` — `SetBoardVisible`.
- `Assets/Scripts/Managers/GameManager.cs` — backdrop fade hooks.
- `Assets/Scripts/Managers/CombatController.cs` — arc applier, morph-in, morph-back.

---

## Task 1: `CombatLayoutRules` pure geometry (TDD)

**Files:**
- Create: `Assets/Scripts/CombatLayout/CombatLayoutRules.cs`
- Create: `Assets/Scripts/CombatLayout/ArchonsRise.CombatLayout.asmdef`
- Create: `Assets/Tests/EditMode/CombatLayoutRulesTests.cs`
- Modify: `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`

**Interfaces:**
- Produces:
  - `struct CombatLayoutRules.Slot { public float X, Y, Scale, TiltDeg; }`
  - `static float CombatLayoutRules.ScaleFor(int count)`
  - `static float CombatLayoutRules.AngleFor(int index, int count, float arcDegrees)`
  - `static CombatLayoutRules.Slot CombatLayoutRules.SlotFor(int index, int count, float radius, float arcDegrees)`
  - `const int CombatLayoutRules.CrowdThreshold = 3`

- [ ] **Step 1: Create the asmdef**

Create `Assets/Scripts/CombatLayout/ArchonsRise.CombatLayout.asmdef`:

```json
{
    "name": "ArchonsRise.CombatLayout",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": true
}
```

- [ ] **Step 2: Reference it from the test assembly**

In `Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef`, add `"ArchonsRise.CombatLayout"` to the `references` array (alongside `"ArchonsRise.Avatar"`).

- [ ] **Step 3: Write the failing tests**

Create `Assets/Tests/EditMode/CombatLayoutRulesTests.cs`:

```csharp
using NUnit.Framework;

public class CombatLayoutRulesTests
{
    [Test]
    public void SingleCard_SitsDeadCentreTop()
    {
        var s = CombatLayoutRules.SlotFor(0, 1, 300f, 120f);
        Assert.AreEqual(0f, s.X, 1e-3f);   // cos(90) == 0
        Assert.AreEqual(300f, s.Y, 1e-3f); // sin(90) * radius
        Assert.AreEqual(1f, s.Scale, 1e-4f);
    }

    [Test]
    public void TwoCards_AreSymmetricAcrossTop()
    {
        var left = CombatLayoutRules.SlotFor(0, 2, 300f, 120f);
        var right = CombatLayoutRules.SlotFor(1, 2, 300f, 120f);
        Assert.AreEqual(left.X, -right.X, 1e-3f); // mirror across x=0
        Assert.AreEqual(left.Y, right.Y, 1e-3f);  // same height
    }

    [Test]
    public void Index0_IsLeft_XIncreasesRightward()
    {
        // Angle sweeps left (index 0, larger angle) to right; X strictly increases.
        var a = CombatLayoutRules.SlotFor(0, 3, 300f, 120f);
        var b = CombatLayoutRules.SlotFor(1, 3, 300f, 120f);
        var c = CombatLayoutRules.SlotFor(2, 3, 300f, 120f);
        Assert.Less(a.X, b.X);
        Assert.Less(b.X, c.X);
        Assert.AreEqual(0f, b.X, 1e-3f); // middle card centred
    }

    [Test]
    public void Scale_FullUpToThreshold_ThenStepsDown()
    {
        Assert.AreEqual(1f, CombatLayoutRules.ScaleFor(1), 1e-4f);
        Assert.AreEqual(1f, CombatLayoutRules.ScaleFor(CombatLayoutRules.CrowdThreshold), 1e-4f);
        Assert.Less(CombatLayoutRules.ScaleFor(CombatLayoutRules.CrowdThreshold + 1), 1f);
    }

    [Test]
    public void Scale_IsFlooredForLargeRosters()
    {
        Assert.GreaterOrEqual(CombatLayoutRules.ScaleFor(20), 0.6f);
    }

    [Test]
    public void AngleFor_CountOneOrZero_IsTop()
    {
        Assert.AreEqual(90f, CombatLayoutRules.AngleFor(0, 1, 120f), 1e-4f);
        Assert.AreEqual(90f, CombatLayoutRules.AngleFor(0, 0, 120f), 1e-4f);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail (RED)**

The class does not exist yet. Compile the tests with the mcs harness in the scratchpad; expect a compile error (`CombatLayoutRules` not found). Set up once:

```bash
SP="C:/Users/DAVE'S~1/AppData/Local/Temp/claude/c--Users-Dave-s-Comp-source-repos-Archon-s-Rise/6fd9f259-a148-44de-b93b-0ba835f5bd90/scratchpad"
MONO="C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin"
NUNIT=$(ls "C:/Users/Dave's Comp/source/repos/Archon's Rise"/Library/PackageCache/com.unity.ext.nunit@*/net472/unity-custom/nunit.framework.dll | head -1)
cp "$NUNIT" "$SP/nunit.framework.dll"
```

Create the reflection runner `$SP/Runner.cs`:

```csharp
using System;
using System.Reflection;
using NUnit.Framework;

class Runner
{
    static int Main(string[] args)
    {
        var asm = Assembly.LoadFrom(args[0]);
        int pass = 0, fail = 0;
        foreach (var type in asm.GetTypes())
        {
            object inst = null;
            foreach (var m in type.GetMethods())
            {
                if (m.GetCustomAttribute(typeof(TestAttribute)) == null) continue;
                if (inst == null) inst = Activator.CreateInstance(type);
                try { m.Invoke(inst, null); pass++; Console.WriteLine("PASS " + type.Name + "." + m.Name); }
                catch (Exception e) { fail++; Console.WriteLine("FAIL " + type.Name + "." + m.Name + ": " + (e.InnerException ?? e).Message); }
            }
        }
        Console.WriteLine(pass + " passed, " + fail + " failed");
        return fail == 0 ? 0 : 1;
    }
}
```

Compile the runner once, then attempt the RED build (tests + not-yet-written class):

```bash
"$MONO/mcs.bat" -nologo "-out:$SP/Runner.exe" "-r:$SP/nunit.framework.dll" "$SP/Runner.cs"
"$MONO/mcs.bat" -nologo -target:library "-out:$SP/Layout.dll" "-r:$SP/nunit.framework.dll" \
  "Assets/Tests/EditMode/CombatLayoutRulesTests.cs"
```

Expected: FAIL to compile — `error CS0103: The name 'CombatLayoutRules' does not exist`.

- [ ] **Step 5: Implement `CombatLayoutRules`**

Create `Assets/Scripts/CombatLayout/CombatLayoutRules.cs`:

```csharp
using System;

// Pure geometry for arranging enemy cards in a semicircle around the
// screen-centre player during combat (spec 2026-07-24). No UnityEngine
// dependency so it compiles under the mcs pure-test harness, like CombatRules /
// DefeatFxMath. The applier in CombatController converts Slot into
// anchoredPosition / scale / rotation.
public static class CombatLayoutRules
{
    // One card's placement in the arc origin's local UI space: +X right, +Y up,
    // in pixels; Scale is uniform; TiltDeg is a Z rotation (degrees).
    public struct Slot { public float X, Y, Scale, TiltDeg; }

    // Cards past this count shrink so a large roster never overlaps.
    public const int CrowdThreshold = 3;

    // Uniform scale for a fight of `count` cards: full up to the crowd threshold,
    // then a gentle step-down floored so cards never vanish.
    public static float ScaleFor(int count)
    {
        if (count <= CrowdThreshold) return 1f;
        float s = 1f - 0.12f * (count - CrowdThreshold);
        return s < 0.6f ? 0.6f : s;
    }

    // Angle (degrees from +X, CCW) of card `index` of `count`, fanned evenly
    // across `arcDegrees` and centred on straight-up (90). Index 0 is leftmost
    // (angle > 90); the last index is rightmost (angle < 90).
    public static float AngleFor(int index, int count, float arcDegrees)
    {
        if (count <= 1) return 90f;
        float step = arcDegrees / (count - 1);
        return 90f + arcDegrees * 0.5f - step * index;
    }

    public static Slot SlotFor(int index, int count, float radius, float arcDegrees)
    {
        float deg = AngleFor(index, count, arcDegrees);
        double rad = deg * Math.PI / 180.0;
        Slot slot;
        slot.X = radius * (float)Math.Cos(rad);
        slot.Y = radius * (float)Math.Sin(rad);
        slot.Scale = ScaleFor(count);
        slot.TiltDeg = (90f - deg) * 0.15f; // slight outward lean along the arc
        return slot;
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass (GREEN)**

```bash
"$MONO/mcs.bat" -nologo -target:library "-out:$SP/Layout.dll" "-r:$SP/nunit.framework.dll" \
  "Assets/Scripts/CombatLayout/CombatLayoutRules.cs" \
  "Assets/Tests/EditMode/CombatLayoutRulesTests.cs"
"$MONO/mono.exe" "$SP/Runner.exe" "$SP/Layout.dll"
```

Expected: `6 passed, 0 failed`.

- [ ] **Step 7: Commit**

```bash
git add "Assets/Scripts/CombatLayout" "Assets/Tests/EditMode/CombatLayoutRulesTests.cs" "Assets/Tests/EditMode/ArchonsRise.Tests.EditMode.asmdef"
git commit -m "feat: CombatLayoutRules — pure semicircle arc geometry"
```

- [ ] **Step 8: USER confirms in Unity**

Ask the USER to let Unity recompile, generate `.meta` files for the new folder/asmdef/scripts, and confirm `CombatLayoutRulesTests` shows green in Window ▸ General ▸ Test Runner.

---

## Task 2: Unique enemy art field + wiring

**Files:**
- Modify: `Assets/Scripts/GameScriptableObjectTypes/EnemiesSO.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs`

**Interfaces:**
- Produces: `EnemiesSO.cardArt` (`Sprite`, nullable); `EnemyCard` sets `artwork.sprite` in `Start()`.

- [ ] **Step 1: Add the sprite field to `EnemiesSO`**

In `Assets/Scripts/GameScriptableObjectTypes/EnemiesSO.cs`, add after `public int tier = 1;`:

```csharp
    // Per-enemy portrait shown on the combat card (spec 2026-07-24). Nullable:
    // an unauthored enemy shows the plain card, never a broken frame. Art is
    // authored later (M3 content).
    public Sprite cardArt;
```

(`EnemiesSO` already has `using UnityEngine;`.)

- [ ] **Step 2: Add the artwork reference to `EnemyCard`**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs`, add to the serialized field block (near the other `[SerializeField]` lines, ~line 27):

```csharp
    [SerializeField] Image artwork; // per-enemy portrait; disabled when the SO has none
```

- [ ] **Step 3: Assign the sprite in `Start()`**

In `EnemyCard.Start()`, add after `enemyName.text = enemySO.cardName;` (~line 36):

```csharp
        if (artwork != null)
        {
            if (enemySO.cardArt != null) { artwork.sprite = enemySO.cardArt; artwork.enabled = true; }
            else artwork.enabled = false;
        }
```

- [ ] **Step 4: Verify it compiles**

Ask the USER to let Unity recompile and confirm no console errors.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/GameScriptableObjectTypes/EnemiesSO.cs" "Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs"
git commit -m "feat: enemy card artwork field (EnemiesSO.cardArt + EnemyCard.artwork)"
```

- [ ] **Step 6: USER editor work**

Provide these steps for the USER:
1. Open `Assets/Prefabs/EnemyCard.prefab`.
2. Add a child UI `Image` for the portrait (sized/positioned within the card frame, behind the text). Name it `Artwork`.
3. Select the root `EnemyCard` component and drag that `Image` into the new `Artwork` slot.
4. Optionally assign a `cardArt` sprite on one `EnemiesSO` asset to sanity-check; leave others null and confirm those still render the plain card.

---

## Task 3: `CombatBackdrop` transparent battlefield

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/GameBoardObjects/CombatBackdrop.cs`
- Modify: `Assets/Scripts/Managers/GameManager.cs`

**Interfaces:**
- Consumes: nothing from prior tasks.
- Produces: `CombatBackdrop.FadeToBattle()`, `CombatBackdrop.Restore()`; `GameManager.combatBackdrop` serialized ref.

- [ ] **Step 1: Create `CombatBackdrop`**

Create `Assets/Scripts/GameObjectScripts/GameBoardObjects/CombatBackdrop.cs`:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Fades the combat canvas background so the board (and the world-space player
// avatar) show through once a fight is underway (spec 2026-07-24). Background
// IMAGE only — HUD, hand, buttons, and enemy cards stay fully opaque.
// Presentation only; never gates combat.
public class CombatBackdrop : MonoBehaviour
{
    [SerializeField] Image background;                       // full-screen combat backdrop
    [SerializeField, Range(0f, 1f)] float combatAlpha = 0.35f;
    [SerializeField] float fadeDuration = 0.35f;

    Coroutine fade;

    // Drop the backdrop to the battle tint (called after the intro beat).
    public void FadeToBattle() => StartFade(combatAlpha);

    // Return to fully opaque (called at combat close, so the next fight fades in).
    public void Restore() => StartFade(1f);

    void StartFade(float target)
    {
        if (background == null) return;
        if (fade != null) { StopCoroutine(fade); fade = null; }
        // If the canvas is already disabled we can't run a coroutine — snap so the
        // alpha is still correct for the next fight.
        if (!gameObject.activeInHierarchy) { SetAlpha(target); return; }
        fade = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        float start = background.color.a;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            SetAlpha(Mathf.Lerp(start, target, t / fadeDuration));
            yield return null;
        }
        SetAlpha(target);
        fade = null;
    }

    void SetAlpha(float a) { var c = background.color; c.a = a; background.color = c; }
}
```

- [ ] **Step 2: Add the serialized ref to `GameManager`**

In `Assets/Scripts/Managers/GameManager.cs`, add near the other canvas fields (~line 18):

```csharp
    [SerializeField] CombatBackdrop combatBackdrop; // fades the battlefield after the intro
```

- [ ] **Step 3: Fade in on both combat-entry paths**

In `GameManager.CombatCanvasActive()` (guardian/dungeon, no banner), add before the closing brace:

```csharp
        if (combatBackdrop != null) combatBackdrop.FadeToBattle();
```

In `GameManager.PlayCombatIntro()` (field), add at the very end, after `yield return new WaitForSeconds(combatIntroDuration);`:

```csharp
        if (combatBackdrop != null) combatBackdrop.FadeToBattle();
```

- [ ] **Step 4: Restore on close**

In `GameManager.CloseCombatCanvas()`, add before `combatCanvas.enabled = false;`:

```csharp
        if (combatBackdrop != null) combatBackdrop.Restore();
```

- [ ] **Step 5: Verify it compiles**

Ask the USER to let Unity recompile and confirm no console errors.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/CombatBackdrop.cs" "Assets/Scripts/Managers/GameManager.cs"
git commit -m "feat: CombatBackdrop fades the battlefield after the combat intro"
```

- [ ] **Step 7: USER editor work + verify**

Provide these steps:
1. On the combat canvas, add `CombatBackdrop` to the object that owns the full-screen background `Image`.
2. Drag that background `Image` into the `Background` slot; set `combatAlpha` (~0.35) and `fadeDuration`.
3. Wire the `CombatBackdrop` into `GameManager.combatBackdrop`.
4. Play: start a fight → after the intro the background fades to the tint and the board/player show through; HUD, hand, and cards stay opaque. End the fight → the backdrop is opaque again on the next fight.

---

## Task 4: Semicircle layout applier in `CombatController`

**Files:**
- Modify: `Assets/Scripts/Managers/CombatController.cs`

**Interfaces:**
- Consumes: `CombatLayoutRules.SlotFor` (Task 1).
- Produces: `CombatController.LayoutLive()`, `ApplySlot(EnemyCard, CombatLayoutRules.Slot)`; serialized `arcRadius`, `arcDegrees`, `baseCardScale`.

- [ ] **Step 1: Add layout inspector fields**

In `CombatController`, add after the existing `[SerializeField]` block (~line 16):

```csharp
    [Header("Enemy arc layout (spec 2026-07-24)")]
    [SerializeField] float arcRadius = 320f;    // px from the screen-centre player
    [SerializeField] float arcDegrees = 120f;   // total fan spread
    [SerializeField] float baseCardScale = 1.75f; // preserves the pre-arc card size
```

- [ ] **Step 2: Replace the stacked placement in `OpenFight`**

In `OpenFight`, change the spawn loop body — remove the two placement lines and let `LayoutLive` position everything after the set is built. Replace:

```csharp
            var go = Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(1.75f, 1.75f, 1f);
            var card = go.GetComponent<EnemyCard>();
```

with:

```csharp
            var go = Instantiate(prefab, parent);
            var card = go.GetComponent<EnemyCard>();
```

Then, immediately after the `foreach` spawn loop closes (before `GameManager.Instance.combatCanvas.enabled = true;`), add:

```csharp
        LayoutLive();
```

- [ ] **Step 3: Add the applier methods**

Add these methods to `CombatController` (e.g. after `SetPhase`):

```csharp
    // Positions the live set in an even arc around the screen-centre player
    // (spec 2026-07-24). Called on spawn and after any mid-fight removal so gaps
    // close. Presentation only.
    void LayoutLive()
    {
        int n = live.Count;
        for (int i = 0; i < n; i++)
            ApplySlot(live[i], CombatLayoutRules.SlotFor(i, n, arcRadius, arcDegrees));
    }

    void ApplySlot(EnemyCard card, CombatLayoutRules.Slot slot)
    {
        var rt = (RectTransform)card.transform;
        rt.anchoredPosition = new Vector2(slot.X, slot.Y);
        float s = baseCardScale * slot.Scale;
        rt.localScale = new Vector3(s, s, 1f);
        rt.localRotation = Quaternion.Euler(0f, 0f, slot.TiltDeg);
    }
```

- [ ] **Step 4: Re-lay the survivors after a defeat**

In `NotifyDefeated`, add right after `if (!live.Remove(card)) return;`:

```csharp
        LayoutLive(); // close the gap the removed card left
```

- [ ] **Step 5: Verify it compiles**

Ask the USER to let Unity recompile and confirm no console errors.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Managers/CombatController.cs"
git commit -m "feat: arrange enemy cards in a semicircle arc (CombatLayoutRules applier)"
```

- [ ] **Step 7: USER editor work + verify**

Provide these steps:
1. On `enemyCardCombatPosition`, **remove any layout group component** (e.g. Horizontal/Grid Layout Group) so explicit `anchoredPosition` is honored.
2. Confirm the enemy card prefab root is a `RectTransform` anchored to centre (so `anchoredPosition` is measured from the arc origin).
3. Play: single fight → card sits centre-top; guardian/dungeon multi-fight → cards fan in an even arc, clear of the hand (bottom) and HUD (corners). Tune `arcRadius` / `arcDegrees` on `CombatController`. Kill one of several → survivors re-centre.

---

## Task 5: `EnemyCardIdleSway`

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardIdleSway.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardDefeatFx.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `EnemyCardIdleSway.Stop()`; `EnemyCardDefeatFx` calls it before its FX.

- [ ] **Step 1: Create `EnemyCardIdleSway`**

Create `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardIdleSway.cs`:

```csharp
using UnityEngine;

// Subtle idle motion on an enemy card during combat (spec 2026-07-24). Drives a
// CHILD content pivot, never the card root — the CombatController layout applier
// owns the root's anchoredPosition, so the two never fight. Presentation only.
public class EnemyCardIdleSway : MonoBehaviour
{
    [SerializeField] RectTransform pivot;        // the content child, not the card root
    [SerializeField] float posAmplitude = 4f;    // px
    [SerializeField] float tiltAmplitude = 1.5f; // degrees
    [SerializeField] float period = 3.2f;        // seconds per cycle

    float phase;
    bool stopped;

    void OnEnable()
    {
        // Random phase so a row of cards never sways in lockstep.
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        if (stopped || pivot == null) return;
        float w = (Time.time / period) * Mathf.PI * 2f + phase;
        pivot.anchoredPosition = new Vector2(Mathf.Sin(w) * posAmplitude,
                                             Mathf.Cos(w * 0.5f) * posAmplitude * 0.5f);
        pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(w) * tiltAmplitude);
    }

    // Halt and re-centre so a defeat/return FX starts from a neutral pose.
    public void Stop()
    {
        stopped = true;
        if (pivot != null)
        {
            pivot.anchoredPosition = Vector2.zero;
            pivot.localRotation = Quaternion.identity;
        }
    }
}
```

- [ ] **Step 2: Stop the sway before the defeat FX**

In `EnemyCardDefeatFx`, add a serialized ref near the other fields (~line 21):

```csharp
    [SerializeField] EnemyCardIdleSway idleSway; // halted before the defeat FX plays
```

Change the two public entry points so the sway is stopped first:

```csharp
    public void PlayDestroy(System.Action onComplete)
    {
        if (idleSway != null) idleSway.Stop();
        StartCoroutine(DestroyRoutine(onComplete));
    }
    public void PlayFade(System.Action onComplete)
    {
        if (idleSway != null) idleSway.Stop();
        StartCoroutine(FadeRoutine(onComplete));
    }
```

- [ ] **Step 3: Verify it compiles**

Ask the USER to let Unity recompile and confirm no console errors.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardIdleSway.cs" "Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardDefeatFx.cs"
git commit -m "feat: idle sway on enemy cards, halted before the defeat FX"
```

- [ ] **Step 5: USER editor work + verify**

Provide these steps:
1. In `EnemyCard.prefab`, add a child `Content` (`RectTransform`) that parents the card's visual children (name/HP/attack/artwork). This is the sway pivot; the root stays owned by the layout applier and defeat FX.
2. Add `EnemyCardIdleSway` to the card root; drag `Content` into `pivot`.
3. Drag the `EnemyCardIdleSway` into `EnemyCardDefeatFx.idleSway`.
4. Play: cards sway gently and out of sync; on defeat the sway stops and the dissolve/fade plays centred.

---

## Task 6: `EnemyCardMorph` + morph-in from the source token

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardMorph.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs`
- Modify: `Assets/Scripts/Managers/CombatController.cs`

**Interfaces:**
- Consumes: `CombatController.OriginWorld()`/`OriginLocalPoint(...)` (added here); the arc slot set by `ApplySlot` (Task 4).
- Produces: `EnemyCardMorph.MorphIn(Vector2 fromLocal)`, `EnemyCardMorph.MorphBack(Vector2 toLocal, System.Action done)`; `EnemyToken.SetBoardVisible(bool)`.

- [ ] **Step 1: Create `EnemyCardMorph`**

Create `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardMorph.cs`:

```csharp
using System.Collections;
using UnityEngine;

// Morphs an enemy card between the board (its source token) and its arc slot
// (spec 2026-07-24). MorphIn: emerge from the token and fan to the slot.
// MorphBack: return to the token on a flee. Presentation only — the fight is
// already resolved when these run. Shares the CanvasGroup EnemyCardDefeatFx
// requires.
[RequireComponent(typeof(CanvasGroup))]
public class EnemyCardMorph : MonoBehaviour
{
    [SerializeField] float morphDuration = 0.3f;
    [SerializeField] float startScaleMul = 0.3f; // fraction of slot scale at spawn

    CanvasGroup group;
    RectTransform rt;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        rt = (RectTransform)transform;
    }

    // Fly in from `fromLocal` (the token projected into the arc-origin local
    // space) to the already-set slot. Call AFTER the applier has placed the card.
    public void MorphIn(Vector2 fromLocal)
        => StartCoroutine(MorphInRoutine(fromLocal, rt.anchoredPosition, rt.localScale, rt.localRotation));

    IEnumerator MorphInRoutine(Vector2 from, Vector2 to, Vector3 slotScale, Quaternion slotRot)
    {
        group.alpha = 0f;
        for (float t = 0f; t < morphDuration; t += Time.deltaTime)
        {
            float p = t / morphDuration;
            rt.anchoredPosition = Vector2.Lerp(from, to, p);
            rt.localScale = Vector3.Lerp(slotScale * startScaleMul, slotScale, p);
            rt.localRotation = Quaternion.Lerp(Quaternion.identity, slotRot, p);
            group.alpha = p;
            yield return null;
        }
        rt.anchoredPosition = to;
        rt.localScale = slotScale;
        rt.localRotation = slotRot;
        group.alpha = 1f;
    }

    // Return to `toLocal` (the token) and fade out, then invoke done. The caller
    // destroys the card.
    public void MorphBack(Vector2 toLocal, System.Action done)
        => StartCoroutine(MorphBackRoutine(toLocal, done));

    IEnumerator MorphBackRoutine(Vector2 toLocal, System.Action done)
    {
        Vector2 from = rt.anchoredPosition;
        Vector3 fromScale = rt.localScale;
        for (float t = 0f; t < morphDuration; t += Time.deltaTime)
        {
            float p = t / morphDuration;
            rt.anchoredPosition = Vector2.Lerp(from, toLocal, p);
            rt.localScale = Vector3.Lerp(fromScale, fromScale * startScaleMul, p);
            group.alpha = 1f - p;
            yield return null;
        }
        group.alpha = 0f;
        done?.Invoke();
    }
}
```

- [ ] **Step 2: Add `SetBoardVisible` to `EnemyToken`**

In `EnemyToken`, add a field near the top (~line 16):

```csharp
    bool boardHidden; // true while this token's card is up in combat
```

Guard the glow logic — add at the very top of `Update()`:

```csharp
        if (boardHidden) return;
```

Add the method:

```csharp
    // Hides/shows the board icon (and glow) while this token's combat card is up
    // (spec 2026-07-24). The card renders the enemy during the fight; a field
    // flee restores the icon.
    public void SetBoardVisible(bool visible)
    {
        boardHidden = !visible;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = visible;
    }
```

- [ ] **Step 3: Add origin projection helpers to `CombatController`**

Add these methods to `CombatController`:

```csharp
    // World position the cards emerge from / return to, per fight context.
    Vector3 OriginWorld()
    {
        if (context == CombatContext.Field && fieldToken != null) return fieldToken.transform.position;
        if (context == CombatContext.Guardian && guardianPlace != null) return guardianPlace.transform.position;
        if (context == CombatContext.Dungeon && dungeonToken != null) return dungeonToken.transform.position;
        return GameManager.Instance.enemyCardCombatPosition.transform.position; // fallback: centre
    }

    // Project a board world position into the arc origin's local UI space, so a
    // card can fly from its token to its slot. Board camera -> screen -> canvas.
    Vector2 OriginLocalPoint(Vector3 worldPos)
    {
        var canvas = GameManager.Instance.combatCanvas;
        var parent = (RectTransform)GameManager.Instance.enemyCardCombatPosition.transform;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(Camera.main, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, canvas.worldCamera, out Vector2 local);
        return local;
    }
```

- [ ] **Step 4: Trigger morph-in and hide the field token in `OpenFight`**

In `OpenFight`, replace the `LayoutLive();` call (added in Task 4) with:

```csharp
        LayoutLive();
        Vector2 fromLocal = OriginLocalPoint(OriginWorld());
        foreach (var card in live)
        {
            var morph = card.GetComponent<EnemyCardMorph>();
            if (morph != null) morph.MorphIn(fromLocal);
        }
        if (context == CombatContext.Field && fieldToken != null)
            fieldToken.SetBoardVisible(false);
```

- [ ] **Step 5: Verify it compiles**

Ask the USER to let Unity recompile and confirm no console errors.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardMorph.cs" "Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs" "Assets/Scripts/Managers/CombatController.cs"
git commit -m "feat: enemy cards morph in from their source token; field token hides in combat"
```

- [ ] **Step 7: USER editor work + verify**

Provide these steps:
1. In `EnemyCard.prefab`, add `EnemyCardMorph` to the card root (the required `CanvasGroup` is already present from `EnemyCardDefeatFx`).
2. Play a field fight: the card emerges from the monster token, fans to its slot, and the board icon disappears while the card is up.
3. Play a guardian/dungeon assault: cards spill out of the place token and fan to their slots; the place itself stays visible. Tune `morphDuration` if desired. (Alignment of the emerge point is camera-dependent — confirm it reads from the token; adjust if the board and UI use different cameras.)

---

## Task 7: Morph-back on flee + field-token restore

**Files:**
- Modify: `Assets/Scripts/Managers/CombatController.cs`

**Interfaces:**
- Consumes: `EnemyCardMorph.MorphBack` (Task 6), `OriginLocalPoint`/`OriginWorld` (Task 6), `EnemyToken.SetBoardVisible` (Task 6).
- Produces: `CombatController.FinishEnd(bool)` (the extracted payout+close tail).

- [ ] **Step 1: Split `EndFight` into decision + `FinishEnd`**

Replace the current `EndFight(bool paidFlee)` method. Change the header and the first few lines so it either morphs survivors back first (flee) or finishes immediately, and move the payout/close body into `FinishEnd`. Replace:

```csharp
    void EndFight(bool paidFlee)
    {
        Phase = CombatPhase.Resolved;
        resolving = false;

        // A flee leaves survivors in the logical set — destroy their cards now so
        // the next fight starts clean (killed cards are already self-destroying).
        foreach (var card in live)
            if (card != null) Destroy(card.gameObject);
        live.Clear();
```

with:

```csharp
    void EndFight(bool paidFlee)
    {
        Phase = CombatPhase.Resolved;
        resolving = false;

        // A flee leaves survivors: morph them back to the board first, then finish.
        // (A cleared fight reaches EndFight from the last kill's FX with live empty.)
        if (paidFlee && live.Count > 0)
        {
            StartCoroutine(MorphSurvivorsBackThenFinish());
            return;
        }
        FinishEnd(paidFlee);
    }

    IEnumerator MorphSurvivorsBackThenFinish()
    {
        Vector2 toLocal = OriginLocalPoint(OriginWorld());
        var survivors = new List<EnemyCard>(live);
        int pending = 0;
        foreach (var card in survivors)
            if (card != null && card.GetComponent<EnemyCardMorph>() != null) pending++;

        foreach (var card in survivors)
        {
            if (card == null) continue;
            var morph = card.GetComponent<EnemyCardMorph>();
            if (morph != null) morph.MorphBack(toLocal, () => pending--);
        }
        if (pending > 0) yield return new WaitUntil(() => pending <= 0);

        if (context == CombatContext.Field && fieldToken != null)
            fieldToken.SetBoardVisible(true);
        FinishEnd(paidFlee: true);
    }

    void FinishEnd(bool paidFlee)
    {
        // Destroy any remaining cards (fled survivors; killed cards self-destruct).
        foreach (var card in live)
            if (card != null) Destroy(card.gameObject);
        live.Clear();
```

Everything from the `foreach (var (name, summary) in pendingRewards)` block through the final `onCombatPhaseChanged.Raise();` stays as-is — it is now the body of `FinishEnd`, so the method's closing brace remains at the end of that block.

- [ ] **Step 2: Add the `using` for coroutines/collections**

`CombatController.cs` already has `using System.Collections.Generic;` and `using UnityEngine;`. Add at the top if not present:

```csharp
using System.Collections;
```

- [ ] **Step 3: Verify it compiles**

Ask the USER to let Unity recompile and confirm no console errors.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/Managers/CombatController.cs"
git commit -m "feat: survivors morph back to the board on flee; field token reappears"
```

- [ ] **Step 5: USER verify**

Provide these steps:
1. Play a multi-enemy fight, kill one, then flee in the Attack phase: surviving cards travel back to the source token and fade; the field token's icon reappears; wounds/reward messages resolve as before.
2. Confirm a fully-won fight (no survivors) still closes immediately with the defeat dissolve — no regression.
3. Confirm a guardian retreat morphs survivors back to the keep and the keep stays visible.

---

## Self-Review

**Spec coverage:**
- Part 1 Transparent battlefield → Task 3. ✓
- Part 2 Semicircle layout (pure rules + applier) → Tasks 1, 4. ✓
- Part 3 Unique enemy art → Task 2. ✓
- Part 4 Idle card sway → Task 5. ✓
- Part 5 Token↔card morph (origin, morph-in, morph-back, board visibility) → Tasks 6, 7. ✓
- Coordinate spaces & sequencing (intro → fade → morph-in → run → exit) → Task 3 hook order + Task 6 OpenFight order. ✓
- Empty-safe `cardArt` fallback → Task 2 Step 3. ✓
- `CombatLayoutRules` mcs-tested → Task 1. ✓

**Placeholder scan:** No TBD/TODO; every code step shows complete code; every command has expected output.

**Type consistency:** `Slot{X,Y,Scale,TiltDeg}`, `SlotFor/AngleFor/ScaleFor/CrowdThreshold`, `LayoutLive/ApplySlot`, `FadeToBattle/Restore`, `Stop`, `MorphIn(Vector2)/MorphBack(Vector2, Action)`, `SetBoardVisible(bool)`, `OriginWorld/OriginLocalPoint`, `EndFight/FinishEnd/MorphSurvivorsBackThenFinish` are used consistently across tasks.
