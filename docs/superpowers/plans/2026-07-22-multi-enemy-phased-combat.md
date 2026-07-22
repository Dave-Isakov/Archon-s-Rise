# Multi-Enemy Phased Combat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace single-enemy combat with a phased **Siege → Defend → Attack → auto-flee** engine shared by field, dungeon, and guardian fights; spawn a guarded place's whole remaining roster at once; and add Balatro-style defeat juice.

**Architecture:** Pillar-critical logic goes in pure, mcs-testable classes in `ArchonsRise.CardPlay` (`CombatPhase`, `CombatPhaseRules`, a `CombatRules` group-counterattack helper, `DefeatFxMath`). A new `CombatController` MonoBehaviour singleton owns the phase machine, the logical live-enemy set, the multi-purpose button, and a per-fight `CombatContext`; it absorbs combat glue currently scattered across `GameManager`, `GuardianAssault`, and per-enemy teardown. Defeat visuals live on an `EnemyCardDefeatFx` component and are presentation-only (kills are banked before animating).

**Tech Stack:** Unity 6000.5.1f1, C# (Mono/mcs), URP, NUnit EditMode + the mcs CLI pure-test harness, TextMeshPro, uGUI (Screen Space – Camera canvas).

## Global Constraints

- **Pure classes live in an asmdef folder, MonoBehaviours in Assembly-CSharp.** `CombatPhase`, `CombatPhaseRules`, the `CombatRules` extension, and `DefeatFxMath` go in `Assets/Scripts/CardPlay/` (assembly `ArchonsRise.CardPlay`, already referenced by both `Assembly-CSharp` and `ArchonsRise.Tests.EditMode`). New MonoBehaviours go under `Assets/Scripts/GameObjectScripts/` or `Assets/Scripts/Managers/` (Assembly-CSharp).
- **Pure test harness = Mono mcs, not csc.** Compile with `"C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge\bin\mcs.bat"` and run the reflection runner under `mono.exe` (same dir). nunit ref: `Library\PackageCache\com.unity.ext.nunit*\net472\unity-custom\nunit.framework.dll`, copied next to the built DLL + runner in the scratchpad. csc is C# 5 and rejects `=>`.
- **No C# 8 switch expressions in pure code** — use `if`/ternary/classic `switch` (match the existing `TurnPhaseRules`/`CombatRules` style).
- **Never hand-edit scene/prefab YAML.** All scene, prefab, material, and Shader Graph work is USER editor work performed from the step-by-step instructions in Tasks 10–11.
- **Flee cost values (unchanged):** field/dungeon = **1 wound**; guardian retreat = **3 wounds** (`PlaceRules.RetreatWoundCount`).
- **Combat stays in the board scene** (`GameBoard.unity`) under the existing `combatCanvas`.
- **No save-schema bump** — guardian progress already persists via `ConquestTracker.RecordDefeat`.
- **Commit after every task.** End PR/commit bodies per repo convention.

## File Structure

**Create (pure, `ArchonsRise.CardPlay`):**
- `Assets/Scripts/CardPlay/CombatPhase.cs` — `CombatPhase` enum.
- `Assets/Scripts/CardPlay/CombatPhaseRules.cs` — phase gating + button label.
- `Assets/Scripts/CardPlay/DefeatFxMath.cs` — shake envelope + dissolve progress.

**Modify (pure):**
- `Assets/Scripts/CardPlay/CombatRules.cs` — add `GroupWoundCount`.
- `Assets/Tests/EditMode/CombatRulesTests.cs` — add group-counterattack tests.

**Create (tests, pure):**
- `Assets/Tests/EditMode/CombatPhaseRulesTests.cs`
- `Assets/Tests/EditMode/DefeatFxMathTests.cs`

**Create (MonoBehaviour, Assembly-CSharp):**
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardDefeatFx.cs` — defeat animations.
- `Assets/Scripts/Managers/CombatController.cs` — phase machine, live set, context, button, reward tally.

**Modify (MonoBehaviour):**
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs` — phase-gated buttons; route kills through the controller; trigger FX.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` — per-enemy resolves notify the controller; Engage clears Siege.
- `Assets/Scripts/Managers/GameManager.cs` — reward tally/payout helpers; retire the standalone Flee path.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/GuardianAssault.cs` — simultaneous-roster spawn via `CombatController.OpenFight`; delete the chain loop.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs` — open field fights via `OpenFight`.
- `Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonDelve.cs` — open delve fights via `OpenFight`.

**Docs/skills:**
- `.claude/skills/archons-rise-design/mechanics.md`, `.claude/skills/archons-rise-roadmap/decisions-log.md`, `.claude/skills/archons-rise-roadmap/milestones.md`, and a memory file.

---

### Task 1: `CombatPhase` enum + pure `CombatPhaseRules`

**Files:**
- Create: `Assets/Scripts/CardPlay/CombatPhase.cs`
- Create: `Assets/Scripts/CardPlay/CombatPhaseRules.cs`
- Test: `Assets/Tests/EditMode/CombatPhaseRulesTests.cs`

**Interfaces:**
- Produces: `enum CombatPhase { Siege, Attack, Resolved }`; `static class CombatPhaseRules` with `bool CanSiege(CombatPhase)`, `bool CanInfluence(CombatPhase)`, `bool CanNormalAttack(CombatPhase)`, `string ButtonLabel(CombatPhase)`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/CombatPhaseRulesTests.cs`:

```csharp
using NUnit.Framework;

public class CombatPhaseRulesTests
{
    [Test]
    public void Siege_And_Influence_Only_In_Siege_Phase()
    {
        Assert.IsTrue(CombatPhaseRules.CanSiege(CombatPhase.Siege));
        Assert.IsTrue(CombatPhaseRules.CanInfluence(CombatPhase.Siege));
        Assert.IsFalse(CombatPhaseRules.CanSiege(CombatPhase.Attack));
        Assert.IsFalse(CombatPhaseRules.CanInfluence(CombatPhase.Attack));
        Assert.IsFalse(CombatPhaseRules.CanSiege(CombatPhase.Resolved));
    }

    [Test]
    public void NormalAttack_Only_In_Attack_Phase()
    {
        Assert.IsFalse(CombatPhaseRules.CanNormalAttack(CombatPhase.Siege));
        Assert.IsTrue(CombatPhaseRules.CanNormalAttack(CombatPhase.Attack));
        Assert.IsFalse(CombatPhaseRules.CanNormalAttack(CombatPhase.Resolved));
    }

    [Test]
    public void Button_Label_Tracks_Phase()
    {
        Assert.AreEqual("Engage", CombatPhaseRules.ButtonLabel(CombatPhase.Siege));
        Assert.AreEqual("Withdraw", CombatPhaseRules.ButtonLabel(CombatPhase.Attack));
        Assert.AreEqual("", CombatPhaseRules.ButtonLabel(CombatPhase.Resolved));
    }
}
```

- [ ] **Step 2: Run the pure harness to verify it fails (RED)**

Compile just the new test against not-yet-existing sources — expect a compile error (types undefined). Use the mcs harness (see Global Constraints). One-time scratchpad setup, then:

```bash
MCS="/c/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mcs.bat"
NUNIT=$(ls Library/PackageCache/com.unity.ext.nunit*/net472/unity-custom/nunit.framework.dll | head -1)
SCRATCH="/c/Users/DAVE'S~1/AppData/Local/Temp/claude/c--Users-Dave-s-Comp-source-repos-Archon-s-Rise/69dfbe32-96e8-4c62-951b-8b07b42ddb88/scratchpad"
cp "$NUNIT" "$SCRATCH/"
"$MCS" -nologo -target:library "-out:$SCRATCH/CombatPhaseRulesTests.dll" "-r:$NUNIT" \
  Assets/Tests/EditMode/CombatPhaseRulesTests.cs Assets/Scripts/CardPlay/CombatPhase.cs Assets/Scripts/CardPlay/CombatPhaseRules.cs
```
Expected: FAIL — `CombatPhase.cs`/`CombatPhaseRules.cs` do not exist yet (mcs: "error CS2001: Source file ... could not be found").

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/CardPlay/CombatPhase.cs`:

```csharp
// Sub-phase of a single fight (spec 2026-07-21, Spec 2). Siege -> (Defend, the
// instantaneous Engage transition) -> Attack -> Resolved. Lives in CardPlay so
// it is mcs/EditMode-testable alongside CombatRules.
public enum CombatPhase { Siege, Attack, Resolved }
```

`Assets/Scripts/CardPlay/CombatPhaseRules.cs`:

```csharp
// Pure phase gating for the phased combat model (spec 2026-07-21, Spec 2).
// No Unity dependency, matching the CombatRules/TurnPhaseRules pattern.
public static class CombatPhaseRules
{
    // Siege and Influence are wound-free removals available only BEFORE Engage.
    public static bool CanSiege(CombatPhase phase)     => phase == CombatPhase.Siege;
    public static bool CanInfluence(CombatPhase phase) => phase == CombatPhase.Siege;

    // Normal attacks land only after the counterattack (Attack phase).
    public static bool CanNormalAttack(CombatPhase phase) => phase == CombatPhase.Attack;

    // The single multi-purpose button's caption per phase.
    public static string ButtonLabel(CombatPhase phase)
    {
        if (phase == CombatPhase.Siege)  return "Engage";
        if (phase == CombatPhase.Attack) return "Withdraw";
        return "";
    }
}
```

- [ ] **Step 4: Run the harness to verify it passes (GREEN)**

Rebuild the test DLL (now sources exist), then run under Mono with the reflection runner (see `unity-pure-test-harness-mcs` memory for the runner). Expected: 3/3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CardPlay/CombatPhase.cs Assets/Scripts/CardPlay/CombatPhaseRules.cs Assets/Tests/EditMode/CombatPhaseRulesTests.cs
git commit -m "feat: CombatPhase enum + pure CombatPhaseRules (Spec 2)"
```

---

### Task 2: `CombatRules.GroupWoundCount` (group counterattack)

**Files:**
- Modify: `Assets/Scripts/CardPlay/CombatRules.cs`
- Test: `Assets/Tests/EditMode/CombatRulesTests.cs`

**Interfaces:**
- Consumes: `CombatRules.WoundCount(AttackKind, int, int, int)` (existing).
- Produces: `static int CombatRules.GroupWoundCount(int defend, int totalEnemyAttack, int playerHP)`.

- [ ] **Step 1: Write the failing test**

Append to `Assets/Tests/EditMode/CombatRulesTests.cs` (inside the class):

```csharp
    [Test]
    public void Group_Counterattack_Sums_Attack_Into_HP_Bites()
    {
        // Two survivors, Attack 3 + 4 = 7, Defend 2, HP 3 -> shortfall 5 -> 2 wounds (i=0,3).
        Assert.AreEqual(2, CombatRules.GroupWoundCount(2, 7, 3));
    }

    [Test]
    public void Group_Counterattack_Zero_When_Defend_Covers_Total()
    {
        Assert.AreEqual(0, CombatRules.GroupWoundCount(7, 7, 3));
        Assert.AreEqual(0, CombatRules.GroupWoundCount(9, 7, 3));
    }

    [Test]
    public void Group_Counterattack_Thinned_Total_Yields_Fewer_Wounds()
    {
        // Full group total 8 -> shortfall 8, HP 2 -> 4 wounds.
        Assert.AreEqual(4, CombatRules.GroupWoundCount(0, 8, 2));
        // Siege removed one (total now 3) -> shortfall 3, HP 2 -> 2 wounds. Siege-thinning pays off.
        Assert.AreEqual(2, CombatRules.GroupWoundCount(0, 3, 2));
    }
```

- [ ] **Step 2: Run the harness to verify it fails (RED)**

Compile `CombatRulesTests.cs` + `CombatRules.cs` via mcs. Expected: FAIL — `CombatRules` has no `GroupWoundCount` (mcs: "error CS0117: `CombatRules' does not contain a definition for `GroupWoundCount'").

- [ ] **Step 3: Write minimal implementation**

Add to `CombatRules` (after `WoundCount`):

```csharp
    // The group counterattack: every surviving enemy hits at once, so their
    // Attack sums into ONE comparison against Defend, then the existing HP-bite
    // rule applies. Because Siege/Influence remove enemies before Engage, a
    // thinner survivor set means a smaller total and fewer wounds.
    public static int GroupWoundCount(int defend, int totalEnemyAttack, int playerHP)
        => WoundCount(AttackKind.Normal, defend, totalEnemyAttack, playerHP);
```

- [ ] **Step 4: Run the harness to verify it passes (GREEN)**

Rebuild + run. Expected: all `CombatRulesTests` PASS (original 7 + 3 new).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CardPlay/CombatRules.cs Assets/Tests/EditMode/CombatRulesTests.cs
git commit -m "feat: CombatRules.GroupWoundCount for the summed group counterattack (Spec 2)"
```

---

### Task 3: `DefeatFxMath` (pure shake + dissolve math)

**Files:**
- Create: `Assets/Scripts/CardPlay/DefeatFxMath.cs`
- Test: `Assets/Tests/EditMode/DefeatFxMathTests.cs`

**Interfaces:**
- Produces: `static float DefeatFxMath.ShakeEnvelope(float t, float duration, float amplitude)`; `static float DefeatFxMath.DissolveProgress(float t, float duration)`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/DefeatFxMathTests.cs`:

```csharp
using NUnit.Framework;

public class DefeatFxMathTests
{
    [Test]
    public void ShakeEnvelope_Full_At_Start_Zero_At_End()
    {
        Assert.AreEqual(10f, DefeatFxMath.ShakeEnvelope(0f, 0.2f, 10f), 1e-4f);
        Assert.AreEqual(0f, DefeatFxMath.ShakeEnvelope(0.2f, 0.2f, 10f), 1e-4f);
        Assert.AreEqual(0f, DefeatFxMath.ShakeEnvelope(0.5f, 0.2f, 10f), 1e-4f); // past end
    }

    [Test]
    public void ShakeEnvelope_Decays_Linearly()
    {
        Assert.AreEqual(5f, DefeatFxMath.ShakeEnvelope(0.1f, 0.2f, 10f), 1e-4f); // halfway -> half
    }

    [Test]
    public void ShakeEnvelope_Zero_Duration_Is_Zero()
    {
        Assert.AreEqual(0f, DefeatFxMath.ShakeEnvelope(0f, 0f, 10f), 1e-4f);
    }

    [Test]
    public void DissolveProgress_Clamps_Zero_To_One()
    {
        Assert.AreEqual(0f, DefeatFxMath.DissolveProgress(0f, 0.4f), 1e-4f);
        Assert.AreEqual(0.5f, DefeatFxMath.DissolveProgress(0.2f, 0.4f), 1e-4f);
        Assert.AreEqual(1f, DefeatFxMath.DissolveProgress(0.4f, 0.4f), 1e-4f);
        Assert.AreEqual(1f, DefeatFxMath.DissolveProgress(1f, 0.4f), 1e-4f); // past end clamps
        Assert.AreEqual(1f, DefeatFxMath.DissolveProgress(0f, 0f), 1e-4f);   // zero duration
    }
}
```

- [ ] **Step 2: Run the harness to verify it fails (RED)**

Compile `DefeatFxMathTests.cs` + `DefeatFxMath.cs` via mcs. Expected: FAIL — source file not found.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/CardPlay/DefeatFxMath.cs`:

```csharp
// Pure timing math for the defeat FX (spec 2026-07-21, Spec 2). The trig for
// the actual shake oscillation stays in the MonoBehaviour; this exposes the
// testable envelope + dissolve ramp, matching the GlowPulse pure-helper style.
public static class DefeatFxMath
{
    // Linearly-decaying shake amplitude: full at t=0, 0 at/after duration.
    public static float ShakeEnvelope(float t, float duration, float amplitude)
    {
        if (duration <= 0f || t >= duration) return 0f;
        if (t <= 0f) return amplitude;
        return amplitude * (1f - t / duration);
    }

    // Normalized dissolve/fade progress 0->1 over duration, clamped.
    public static float DissolveProgress(float t, float duration)
    {
        if (duration <= 0f) return 1f;
        float p = t / duration;
        if (p < 0f) return 0f;
        if (p > 1f) return 1f;
        return p;
    }
}
```

- [ ] **Step 4: Run the harness to verify it passes (GREEN)**

Rebuild + run. Expected: 4/4 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CardPlay/DefeatFxMath.cs Assets/Tests/EditMode/DefeatFxMathTests.cs
git commit -m "feat: DefeatFxMath pure shake/dissolve timing (Spec 2)"
```

---

### Task 4: `EnemyCardDefeatFx` component

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardDefeatFx.cs`

**Interfaces:**
- Consumes: `DefeatFxMath.ShakeEnvelope`, `DefeatFxMath.DissolveProgress`.
- Produces: `void PlayDestroy(System.Action onComplete)`, `void PlayFade(System.Action onComplete)` on component `EnemyCardDefeatFx`. Both self-destroy the GameObject after invoking `onComplete`.

*No automated test — MonoBehaviour with Unity timing; verified in Task 11's play-mode acceptance. This task just adds the compilable component.*

- [ ] **Step 1: Write the component**

`Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardDefeatFx.cs`:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Plays an enemy card's defeat animation, then destroys the GameObject and
// invokes a completion callback. Presentation ONLY: the CombatController banks
// the kill (logical-set removal, undo commit, guardian record, reward tally)
// BEFORE calling these, so a cut-short animation can never desync combat.
[RequireComponent(typeof(CanvasGroup))]
public class EnemyCardDefeatFx : MonoBehaviour
{
    [Header("Shake + dissolve (Siege / Attack)")]
    [SerializeField] float shakeDuration = 0.15f;
    [SerializeField] float shakeAmplitude = 12f;   // px on the RectTransform
    [SerializeField] float shakeFrequency = 30f;   // Hz
    [SerializeField] float dissolveDuration = 0.4f;
    [SerializeField] Image dissolveImage;          // card art using the dissolve material

    [Header("Fade (Influence)")]
    [SerializeField] float fadeDuration = 0.35f;
    [SerializeField] float fadeRise = 20f;         // px upward drift

    static readonly int DissolveId = Shader.PropertyToID("_DissolveAmount");
    CanvasGroup group;
    Material dissolveMat;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        // Instance the material so tweening _DissolveAmount never touches the shared asset.
        if (dissolveImage != null)
            dissolveMat = dissolveImage.material = new Material(dissolveImage.material);
    }

    public void PlayDestroy(System.Action onComplete) => StartCoroutine(DestroyRoutine(onComplete));
    public void PlayFade(System.Action onComplete)    => StartCoroutine(FadeRoutine(onComplete));

    IEnumerator DestroyRoutine(System.Action onComplete)
    {
        var rt = (RectTransform)transform;
        Vector2 origin = rt.anchoredPosition;
        for (float t = 0f; t < shakeDuration; t += Time.deltaTime)
        {
            float env = DefeatFxMath.ShakeEnvelope(t, shakeDuration, shakeAmplitude);
            rt.anchoredPosition = origin + Vector2.right * (env * Mathf.Sin(t * shakeFrequency * 2f * Mathf.PI));
            yield return null;
        }
        rt.anchoredPosition = origin;
        for (float t = 0f; t < dissolveDuration; t += Time.deltaTime)
        {
            if (dissolveMat != null) dissolveMat.SetFloat(DissolveId, DefeatFxMath.DissolveProgress(t, dissolveDuration));
            yield return null;
        }
        if (dissolveMat != null) dissolveMat.SetFloat(DissolveId, 1f);
        onComplete?.Invoke();
        Destroy(gameObject);
    }

    IEnumerator FadeRoutine(System.Action onComplete)
    {
        var rt = (RectTransform)transform;
        Vector2 origin = rt.anchoredPosition;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            float p = DefeatFxMath.DissolveProgress(t, fadeDuration);
            group.alpha = 1f - p;
            rt.anchoredPosition = origin + Vector2.up * (fadeRise * p);
            yield return null;
        }
        group.alpha = 0f;
        onComplete?.Invoke();
        Destroy(gameObject);
    }
}
```

- [ ] **Step 2: Verify it compiles**

Let Unity recompile (or `mcs` is not applicable — this is Assembly-CSharp). Check the Editor Console shows no errors for `EnemyCardDefeatFx`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardDefeatFx.cs
git commit -m "feat: EnemyCardDefeatFx defeat animation component (Spec 2)"
```

---

### Task 5: `CombatController` core — state, live set, `OpenFight`, phase/button

**Files:**
- Create: `Assets/Scripts/Managers/CombatController.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs` (add `ApplyPhase`)

**Interfaces:**
- Consumes: `CombatPhase`, `CombatPhaseRules`, `GameManager.Instance` (`enemyCardCombatPosition`, `combatCanvas`), `EnemyDeck.PrefabEnemyCard`, `VoidEvent`.
- Produces on `CombatController` (singleton `Instance`):
  - `enum CombatContext { Field, Guardian, Dungeon }`
  - `struct EnemySpawn { EnemiesSO so; int bonusHP; int bonusAttack; }` with ctor `EnemySpawn(EnemiesSO, int, int)`
  - `void OpenFight(System.Collections.Generic.List<EnemySpawn> spawns, CombatContext context, TownToken guardianPlace)`
  - `CombatPhase Phase { get; }`, `bool CanSiege`, `bool CanInfluence`, `bool CanNormalAttack`
  - (Engage/kill/withdraw come in Tasks 6–8.)
- Produces on `EnemyCard`: `void ApplyPhase(CombatPhase phase)`.

- [ ] **Step 1: Write the `CombatController` core**

`Assets/Scripts/Managers/CombatController.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum CombatContext { Field, Guardian, Dungeon }

// Owns one phased fight (spec 2026-07-21, Spec 2): the CombatPhase machine, the
// logical live-enemy set, the per-fight context, and the single multi-purpose
// button. Engage/kill/withdraw are added in Tasks 6-8.
public class CombatController : MonoBehaviour
{
    public static CombatController Instance { get; private set; }

    [SerializeField] Button multiButton;            // the repurposed Flee button
    [SerializeField] TMPro.TextMeshProUGUI multiButtonLabel;
    [SerializeField] VoidEvent onCombatPhaseChanged; // HUD phase label listens

    public CombatPhase Phase { get; private set; }
    public bool CanSiege        => CombatPhaseRules.CanSiege(Phase);
    public bool CanInfluence    => CombatPhaseRules.CanInfluence(Phase);
    public bool CanNormalAttack => CombatPhaseRules.CanNormalAttack(Phase);

    readonly List<EnemyCard> live = new();   // logical set; resolution keys off THIS, not childCount
    CombatContext context;
    TownToken guardianPlace;

    public struct EnemySpawn
    {
        public EnemiesSO so; public int bonusHP; public int bonusAttack;
        public EnemySpawn(EnemiesSO so, int bonusHP, int bonusAttack)
        { this.so = so; this.bonusHP = bonusHP; this.bonusAttack = bonusAttack; }
    }

    void Awake() { Instance = this; }

    public void OpenFight(List<EnemySpawn> spawns, CombatContext context, TownToken guardianPlace)
    {
        this.context = context;
        this.guardianPlace = guardianPlace;
        live.Clear();

        var prefab = FindAnyObjectByType<EnemyDeck>().PrefabEnemyCard;
        var parent = GameManager.Instance.enemyCardCombatPosition.transform;
        foreach (var s in spawns)
        {
            var go = Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(1.75f, 1.75f, 1f);
            var card = go.GetComponent<EnemyCard>();
            card.enemySO = s.so;
            card.bonusHP = s.bonusHP;
            card.bonusAttack = s.bonusAttack;
            live.Add(card);
        }

        GameManager.Instance.combatCanvas.enabled = true;
        SetPhase(CombatPhase.Siege);
    }

    void SetPhase(CombatPhase phase)
    {
        Phase = phase;
        foreach (var card in live) card.ApplyPhase(phase);
        if (multiButtonLabel != null) multiButtonLabel.text = CombatPhaseRules.ButtonLabel(phase);
        if (onCombatPhaseChanged != null) onCombatPhaseChanged.Raise();
    }
}
```

*Note: multiple cards currently stack at `localPosition = zero`; Task 11 lays out the guardian pair with proper offsets in the editor. Card auto-wires its own buttons in `Start` (Task 6 makes those routes phase-aware).*

- [ ] **Step 2: Add `ApplyPhase` to `EnemyCard`**

In `EnemyCard.cs`, add:

```csharp
    // Phase-gates this card's buttons (spec 2026-07-21, Spec 2). Siege/Influence
    // are live only in the Siege phase; Fight only in the Attack phase.
    public void ApplyPhase(CombatPhase phase)
    {
        if (siegeButton != null)     siegeButton.interactable     = CombatPhaseRules.CanSiege(phase);
        if (fightButton != null)     fightButton.interactable     = CombatPhaseRules.CanNormalAttack(phase);
        if (influenceButton != null) influenceButton.interactable = CombatPhaseRules.CanInfluence(phase) && enemySO.canInfluence;
    }
```

- [ ] **Step 3: Verify compile**

Unity recompiles clean (no Console errors). `CombatController` and `EnemyCard.ApplyPhase` exist. Scene fields (`multiButton`, `multiButtonLabel`, `onCombatPhaseChanged`) are unwired for now — wired in Tasks 10–11.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Managers/CombatController.cs Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCard.cs
git commit -m "feat: CombatController core (phase machine, live set, OpenFight) + EnemyCard.ApplyPhase (Spec 2)"
```

---

### Task 6: Engage + route kills through the controller (deferred reward tally)

**Files:**
- Modify: `Assets/Scripts/Managers/CombatController.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`
- Modify: `Assets/Scripts/Managers/GameManager.cs`
- Read first: `Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs` (confirm `RewardSummary GetReward(EnemyCard)` and `OfferCardChoice(int tier)` signatures used below).

**Interfaces:**
- Consumes: `CombatRules.GroupWoundCount`, `Player` (`PlayerDefend`, `PlayerHP`, `PlayerSiege`), `PlayerHand.AddWound()`, `Rewards.GetReward(EnemyCard)`, `RewardSummary`, `DefeatMessage.Compose`, `RewardQueue`, `ConquestTracker`, `EnemyCardDefeatFx`.
- Produces on `CombatController`: `void Engage()`; `void NotifyDefeated(EnemyCard card, bool wasInfluence)`; `bool HasLiveEnemies { get; }`.

- [ ] **Step 1: Add Engage + kill routing to `CombatController`**

Add fields + methods to `CombatController`:

```csharp
    // Captured reward summaries for killed enemies; paid out at fight-end so a
    // kill mid-fight never pops a modal that interrupts Siege/Attack decisions.
    readonly List<RewardSummary> pendingRewards = new();

    public bool HasLiveEnemies => live.Count > 0;

    // The Defend resolution (spec 2026-07-21, Spec 2): summed survivor Attack vs
    // Defend in one HP-bite comparison; unspent Siege is consumed by committing.
    public void Engage()
    {
        if (Phase != CombatPhase.Siege) return;
        var player = FindAnyObjectByType<Player>();

        int total = 0;
        foreach (var card in live) total += card.EffectiveAttack;

        int wounds = CombatRules.GroupWoundCount(player.PlayerDefend, total, player.PlayerHP);
        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        for (int i = 0; i < wounds; i++) hand.AddWound();

        player.PlayerDefend = Mathf.Max(0, player.PlayerDefend - total);
        player.PlayerSiege = 0;                       // Siege is a Siege-phase-only currency
        GameManager.Instance.commands.ClearStack();   // Engage is a commit point

        if (wounds > 0)
            GameManager.Instance.ValidationMessage($"The enemies strike back! You are wounded {wounds} times.");

        SetPhase(CombatPhase.Attack);
    }

    // Called when a specific enemy is removed (Siege/Attack kill, or Influence).
    // Banks the kill immediately; the FX plays out and self-destroys the card.
    public void NotifyDefeated(EnemyCard card, bool wasInfluence)
    {
        if (!live.Remove(card)) return;

        GameManager.Instance.commands.ClearStack();   // a kill is irreversible

        if (context == CombatContext.Guardian && guardianPlace != null)
            ConquestTracker.Instance.RecordDefeat(guardianPlace.gridPos);

        pendingRewards.Add(GameManager.Instance.rewards.GetReward(card)); // capture now, pay at end

        var fx = card.GetComponent<EnemyCardDefeatFx>();
        if (wasInfluence) fx.PlayFade(null); else fx.PlayDestroy(null);

        if (!HasLiveEnemies) WinFight();
    }

    // Win/withdraw payout + close are added in Task 7; placeholder wired there.
    void WinFight() { EndFight(paidFlee: false); }
```

*(`EndFight` is defined in Task 7. This task leaves `WinFight` calling it so the two tasks compose; if executing strictly in order, add the Task 7 body before play-testing.)*

- [ ] **Step 2: Re-point `Player` resolves at the controller**

In `Player.cs`, change `ResolveAttack` so a defeat notifies the controller instead of calling `GameManager.ResolveDefeat` directly, and Siege spends only in the Siege phase:

Replace the body tail of `ResolveAttack` (the `GameManager.Instance.ResolveDefeat(enemy);` line and the wound/counterattack block that duplicated combat) with routing. New `ResolveAttack`:

```csharp
    void ResolveAttack(EnemyCard enemy, AttackKind kind)
    {
        int hp = enemy.EffectiveHP;
        if (!CombatRules.CanDefeat(kind, playerAttack, playerSiege, hp))
        {
            string need = kind == AttackKind.Siege ? "Siege" : "Attack";
            GameManager.Instance.ValidationMessage($"You need {hp} {need} to defeat this enemy.");
            return;
        }

        if (kind == AttackKind.Siege) playerSiege -= hp;
        else                          playerAttack -= hp;   // Attack phase: no Siege pool left to borrow

        // No per-enemy counterattack here — the group counterattack ran at Engage.
        CombatController.Instance.NotifyDefeated(enemy, wasInfluence: false);
    }
```

And in `CompleteInfluence`, replace `GameManager.Instance.ResolveDefeat(enemy);` with:

```csharp
        CombatController.Instance.NotifyDefeated(enemy, wasInfluence: true);
```

- [ ] **Step 3: Add the fight-end reward payout helper to `GameManager`**

`ResolveDefeat` is now driven by the controller's tally. Add a helper the controller calls in Task 7 to pay one captured summary through `RewardQueue`:

```csharp
    // Pay one captured reward summary (spec 2026-07-21, Spec 2 deferred payout).
    // Mirrors the old ResolveDefeat body minus teardown (the FX owns teardown).
    public void PayReward(string enemyName, RewardSummary summary)
    {
        ValidationMessage(DefeatMessage.Compose(enemyName, summary.exp, summary.crystal, summary.cardPick));
        if (summary.cardPick) rewards.OfferCardChoice(summary.tier);
    }
```

*Read `Rewards.cs` and `RewardSummary` first; if `RewardSummary` lacks a `name`/enemy field, capture the enemy name alongside the summary in the controller's `pendingRewards` (change it to `List<(string name, RewardSummary summary)>`). Adjust Task 7's payout loop to match.*

- [ ] **Step 4: Verify compile + manual smoke (deferred to Task 7 for full flow)**

Unity compiles clean. Full fight flow is acceptance-tested after Task 7 (Withdraw/EndFight) and Task 11 (wiring). For now confirm no Console errors and that `ResolveDefeat`'s old callers are updated (search for `ResolveDefeat` — only the now-removed internal path should remain; delete the old `ResolveDefeat`/`TeardownDefeat` in Task 7).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Managers/CombatController.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs Assets/Scripts/Managers/GameManager.cs
git commit -m "feat: Engage group counterattack + route kills through CombatController with deferred rewards (Spec 2)"
```

---

### Task 7: Withdraw, fight-end payout, and retire the standalone Flee path

**Files:**
- Modify: `Assets/Scripts/Managers/CombatController.cs`
- Modify: `Assets/Scripts/Managers/GameManager.cs`

**Interfaces:**
- Consumes: `PlaceRules.RetreatWoundCount`, `PlayerHand.AddWound`, `RewardQueue`, `RunEndRules.IsVictory`, `ConquestTracker`, `RunEndController`.
- Produces on `CombatController`: `void Withdraw()`; `void EndFight(bool paidFlee)`.

- [ ] **Step 1: Add Withdraw + EndFight to `CombatController`**

```csharp
    // The multi-purpose button in the Attack phase. Survivors alive => this IS
    // the flee (field/dungeon 1 wound, guardian 3-wound retreat). Kills banked.
    public void Withdraw()
    {
        if (Phase != CombatPhase.Attack) return;

        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        int cost = context == CombatContext.Guardian ? PlaceRules.RetreatWoundCount : 1;
        for (int i = 0; i < cost; i++) hand.AddWound();

        GameManager.Instance.ValidationMessage(context == CombatContext.Guardian
            ? $"You retreat from the assault and suffer {cost} wounds. Your progress is kept."
            : "You flee the battle and suffer a wound!");

        EndFight(paidFlee: true);
    }

    void EndFight(bool paidFlee)
    {
        Phase = CombatPhase.Resolved;

        // Pay every captured reward through the queue, in kill order.
        foreach (var summary in pendingRewards)
            RewardQueue.Instance.Enqueue(done => { GameManager.Instance.PayReward(summary); done(); });
        pendingRewards.Clear();

        // Guardian conquest / win check when the roster is cleared.
        if (!paidFlee && context == CombatContext.Guardian && guardianPlace != null
            && ConquestTracker.Instance.IsConquered(guardianPlace.gridPos))
        {
            GameManager.Instance.ValidationMessage(
                $"{guardianPlace.townSO.cardName} is conquered! Its services are now open to you.");
            if (RunEndRules.IsVictory(ConquestTracker.Instance.ConqueredCastleCount()))
                RunEndController.RequestEnd(RunOutcome.Victory);
        }

        GameManager.Instance.CloseCombatCanvas();
        guardianPlace = null;
    }
```

*Adjust `PayReward` call to whatever tuple/shape Task 6 Step 3 settled on (name + summary). If `pendingRewards` stores tuples, unpack here.*

- [ ] **Step 2: Point the multi-purpose button + delete old Flee/ResolveDefeat**

In `GameManager.cs`:
- Delete `ResolveDefeat`, `TeardownDefeat`, and `CheckCombatants` (their roles now live in the controller: reward capture in `NotifyDefeated`, auto-win in `HasLiveEnemies`, teardown in the FX).
- Delete the field/guardian/dungeon branching body of `FleeCombat` (the button now calls the controller). Keep `CloseCombatCanvas`/`EndCombat` (still used by `EndFight`).
- The old `fleeButton` becomes the multi-purpose button. Its `onClick` must call the controller. Rewire in the editor (Task 10): `Siege` phase → `Engage`, `Attack` phase → `Withdraw`. Simplest single-listener approach — add a dispatcher on the controller:

```csharp
    // The one button's click, dispatched by current phase.
    public void OnMultiButton()
    {
        if (Phase == CombatPhase.Siege) Engage();
        else if (Phase == CombatPhase.Attack) Withdraw();
    }
```

Wire the button's `OnClick` to `CombatController.OnMultiButton` in Task 10.

- [ ] **Step 3: Verify compile**

Unity compiles clean; no lingering references to `ResolveDefeat`/`TeardownDefeat`/`CheckCombatants` (search the project). `GuardianAssault`/`DungeonDelve`/`EnemyToken` still reference old paths — updated in Tasks 8–9, so expect *those* to error until then; if executing strictly in order, do Steps in Tasks 8–9 before play-testing.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Managers/CombatController.cs Assets/Scripts/Managers/GameManager.cs
git commit -m "feat: Withdraw + deferred fight-end payout; retire standalone Flee/ResolveDefeat (Spec 2)"
```

---

### Task 8: Simultaneous guardians via `OpenFight`

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/GuardianAssault.cs`

**Interfaces:**
- Consumes: `CombatController.OpenFight`, `CombatController.EnemySpawn`, `ConquestTracker.DefeatedCount`, `TownToken.townSO.guardians`.
- Produces: `GuardianAssault.Begin(TownToken)` now spawns the whole remaining roster at once; `Retreat`/`InProgress`/`Update` chain deleted (the controller owns retreat + resolution).

- [ ] **Step 1: Rewrite `GuardianAssault` to delegate to the controller**

Replace `GuardianAssault.cs` with:

```csharp
using System.Collections.Generic;
using UnityEngine;

// Opens a guarded-place assault as one phased multi-enemy fight (spec 2026-07-21,
// Spec 2): the WHOLE remaining roster spawns at once. Per-kill banking +
// 3-wound retreat (both in CombatController) preserve resumable conquest.
public class GuardianAssault : MonoBehaviour
{
    private static GuardianAssault instance;
    public static GuardianAssault Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("GuardianAssault").AddComponent<GuardianAssault>();
            return instance;
        }
    }

    public void Begin(TownToken town)
    {
        // Tear down the place menu the button click came from.
        foreach (var card in FindObjectsByType<TownCard>(FindObjectsSortMode.None))
            Destroy(card.gameObject);
        GameManager.Instance.townCanvas.enabled = false;

        var roster = town.townSO.guardians;
        int already = ConquestTracker.Instance.DefeatedCount(town.gridPos);
        var spawns = new List<CombatController.EnemySpawn>();
        for (int i = already; i < roster.Count; i++)
            spawns.Add(new CombatController.EnemySpawn(roster[i], 0, 0)); // guardians unscaled

        CombatController.Instance.OpenFight(spawns, CombatContext.Guardian, town);
    }
}
```

*If other code referenced `GuardianAssault.AnyInProgress`/`InProgress`/`Retreat` (e.g. turn/round gating, `EndTurnButton`, `FleeCombat`), replace those checks with `CombatController.Instance.Phase != CombatPhase.Resolved` style guards, or a new `CombatController.InCombat` property. Grep `GuardianAssault.` and `AnyInProgress` and update each call site in this step; add `public bool InCombat => Phase == CombatPhase.Siege || Phase == CombatPhase.Attack;` to the controller if needed.*

- [ ] **Step 2: Verify compile + grep call sites**

`git grep -n "GuardianAssault\.\|AnyInProgress"` — every hit compiles against the new API. Unity Console clean.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/GuardianAssault.cs Assets/Scripts/GameObjectScripts/DeckScripts/EndTurnButton.cs
git commit -m "feat: simultaneous-guardian assault via CombatController.OpenFight (Spec 2)"
```

---

### Task 9: Field + dungeon entry points open via `OpenFight`

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonDelve.cs`
- Read first: `DungeonDelve.cs` (confirm how it spawns its per-delve enemy + its `Flee`).

**Interfaces:**
- Consumes: `CombatController.OpenFight`, `CombatController.EnemySpawn`, `TurnPhaseController` gating (unchanged from Spec 1).
- Produces: field token opens a 1-enemy `Field` fight; a delve opens a 1-enemy `Dungeon` fight.

- [ ] **Step 1: Field token opens through the controller**

In `EnemyToken.StartCombat`, after the `TurnPhaseController` `BeginAction` gate, replace the intro + `deck.GetNewEnemyCard(this)` spawn with:

```csharp
        GameManager.Instance.activeCombatant = this;
        yield return GameManager.Instance.PlayCombatIntro();
        var spawns = new System.Collections.Generic.List<CombatController.EnemySpawn>
        {
            new CombatController.EnemySpawn(enemy, bonusHP, bonusAttack)
        };
        CombatController.Instance.OpenFight(spawns, CombatContext.Field, null);
```

*Keep the out-of-range preview path (`OnPointerClick` non-aggro branch) as-is — it shows a peek card, not a fight.*

- [ ] **Step 2: Dungeon delve opens through the controller**

In `DungeonDelve`, replace its per-delve card spawn with an `OpenFight` call passing the delve's authored enemy as a single `Dungeon`-context spawn (mirror Step 1). Its existing `Flee` (1 wound) is superseded by `CombatController.Withdraw()` under `Dungeon` context — delete `DungeonDelve.Flee` and any `AnyInProgress`, updating `FleeCombat`/gating call sites as in Task 8 Step 1.

*Exact edit depends on `DungeonDelve.cs`; read it, then convert its spawn+flee to the controller the same way. Dungeon fights remain exp-only — the controller's reward capture uses `Rewards.GetReward`, which already yields the delve's exp-only summary (verify against `DungeonDelve`'s current reward call and keep that path).*

- [ ] **Step 3: Verify compile + grep**

`git grep -n "GetNewEnemyCard\|DungeonDelve\.Flee\|AnyInProgress"` — no stale combat-open or flee paths remain. Console clean.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonDelve.cs Assets/Scripts/Managers/GameManager.cs
git commit -m "feat: field + dungeon fights open via CombatController.OpenFight (Spec 2)"
```

---

### Task 10: HUD wiring — phase label + multi-purpose button (USER editor)

**Files:** `Assets/Scenes/GameBoard.unity` (editor only), `Assets/Prefabs/EnemyCard.prefab` (editor only). **No YAML hand-editing.**

Follow these steps in the Unity Editor and confirm each:

- [ ] **Step 1:** Create a `VoidEvent` asset `onCombatPhaseChanged` (Project → Create → the project's VoidEvent menu, matching `onPhaseChanged`'s asset). Assign it to `CombatController.onCombatPhaseChanged`.
- [ ] **Step 2:** In `combatCanvas`, add a TMP text `PhaseLabel` beside the existing combat UI. Add a `GameEventListener` (VoidEvent) on it listening to `onCombatPhaseChanged`, whose response sets the label to `CombatController.Instance.Phase` (via a tiny `PhaseLabelHud` MonoBehaviour reading `CombatController.Instance.Phase` and formatting `Siege Phase` / `Counterattack!` / `Attack Phase`). Create `PhaseLabelHud` if needed (Assembly-CSharp).
- [ ] **Step 3:** Select the existing **Flee** button. Set `CombatController.multiButton` to it and `CombatController.multiButtonLabel` to its child TMP. Clear its old `OnClick` (`GameManager.FleeCombat`) and set a single `OnClick` → `CombatController.OnMultiButton`.
- [ ] **Step 4:** On the `CombatController` GameObject (add one to the scene if absent — a persistent manager object like the others), confirm `multiButton`, `multiButtonLabel`, `onCombatPhaseChanged` are assigned.
- [ ] **Step 5: Acceptance (play mode):** Start a field fight → button reads **Engage**, `PhaseLabel` reads **Siege Phase**, Fight button disabled, Siege/Influence enabled per pools. Press Engage → label flips to **Attack Phase**, button reads **Withdraw**, Fight enabled. Confirm no Console errors.
- [ ] **Step 6: Commit** (editor-generated scene/prefab/asset + any `PhaseLabelHud.cs`):

```bash
git add -A
git commit -m "chore: wire phase-label HUD + multi-purpose combat button (Spec 2, editor)"
```

---

### Task 11: Dissolve shader, material, prefab FX wiring + guardian layout (USER editor)

**Files:** new Shader Graph + material + noise texture, `Assets/Prefabs/EnemyCard.prefab` (editor). **No YAML hand-editing.**

- [ ] **Step 1:** Create a URP **Shader Graph** targeting **UGUI/Canvas** (Sprite/Canvas-compatible), name it `EnemyCardDissolve`. Inputs: `_MainTex` (the card art via the UI vertex path), a `_NoiseTex` (Texture2D), and a `_DissolveAmount` (Float 0–1). Logic: sample noise; `Alpha = step(_DissolveAmount, noise) * texAlpha` (burns away where noise < amount); optional emissive edge band where `noise` is within a small epsilon of `_DissolveAmount` for a glow. Save.
- [ ] **Step 2:** Create a `Material` `EnemyCardDissolveMat` using that shader; assign a tiling noise texture to `_NoiseTex`; default `_DissolveAmount = 0`.
- [ ] **Step 3:** On `EnemyCard.prefab`: add a `CanvasGroup` to the root. Add the `EnemyCardDefeatFx` component; assign `dissolveImage` = the card-art `Image`; set that Image's material to `EnemyCardDissolveMat`. Tune durations/amplitude to taste (defaults are reasonable).
- [ ] **Step 4:** Confirm the combat-canvas layout positions **two** cards side-by-side (guardian pair). If `enemyCardCombatPosition` centers a single card, add a small horizontal layout/offset so `OpenFight` spawns don't fully overlap (a `HorizontalLayoutGroup` on `enemyCardCombatPosition`, or offset spawns in `OpenFight`). Prefer the layout group (no code change).
- [ ] **Step 5: Acceptance (play mode):**
  - Field kill via Fight → card **shakes then dissolves** away; reward message appears after the fight ends.
  - Influence an enemy → card **fades + drifts up** (no dissolve).
  - Castle assault → **both guardians spawn at once**; Siege/Attack a guardian → it dissolves and is banked; **Withdraw** with one alive → 3-wound message, and re-entering the Castle spawns **only the survivor**.
  - Clear both guardians → conquest message + (2nd Castle) victory.
  - Verify no `childCount`-based close bug: a dissolving card lingering ~0.5s does not re-open/close combat incorrectly.
- [ ] **Step 6: Commit:**

```bash
git add -A
git commit -m "chore: dissolve shader/material + EnemyCard FX wiring + guardian layout (Spec 2, editor)"
```

---

### Task 12: Docs, decisions log, milestone, memory

**Files:**
- Modify: `.claude/skills/archons-rise-design/mechanics.md`
- Modify: `.claude/skills/archons-rise-roadmap/decisions-log.md`
- Modify: `.claude/skills/archons-rise-roadmap/milestones.md`
- Create: a memory file + `MEMORY.md` pointer.

- [ ] **Step 1:** In `mechanics.md`, rewrite the combat portion of the Run Loop / add a "Combat" subsection: the phased **Siege → Defend → Attack → auto-flee** model; Siege as a Siege-phase-only currency cleared at Engage; the summed single counterattack; Influence resolved in the Siege phase; simultaneous guardians with per-kill banking + 3-wound retreat; deferred reward payout; the two-track defeat FX.
- [ ] **Step 2:** Append a `decisions-log.md` entry (dated 2026-07-22) recording each decision from the spec's "Decisions to record" list.
- [ ] **Step 3:** In `milestones.md`, add **M2.14 — Multi-enemy phased combat** (goal, scope, acceptance from this plan) marked as Spec 2 of the turn/combat change; update **Current Focus** in the roadmap `SKILL.md` if appropriate.
- [ ] **Step 4:** Write a memory `phased-combat-controller.md` (type: project) capturing that combat now runs through `CombatController` (phase machine + logical live set + context), that resolution keys off the logical set not `childCount`, and that kills bank before FX; add the `MEMORY.md` pointer line. Link `[[siege-cards-co-flag-attack]]` and `[[m2-deferred-followups]]` (the simultaneous-guardian item is now delivered).
- [ ] **Step 5: Commit:**

```bash
git add .claude/skills docs
git commit -m "docs: mechanics/decisions/milestone for multi-enemy phased combat (M2.14)"
```

---

## Self-Review

**Spec coverage:**
- Phased model (Siege→Defend→Attack→auto-flee) → Tasks 1, 5, 6, 7. ✅
- Siege cleared at Engage, Attack/Defend carry → Task 6 (`Engage` sets `PlayerSiege = 0`; Attack/Defend untouched). ✅
- Summed single counterattack (Siege thins it) → Task 2 (`GroupWoundCount`) + Task 6 (`Engage`). ✅
- Influence in Siege phase → Tasks 1 (`CanInfluence`), 5 (`ApplyPhase`), 6 (`NotifyDefeated(..., wasInfluence:true)`). ✅
- Deferred reward payout via RewardQueue → Task 6 (capture) + Task 7 (payout). ✅
- Per-kill guardian banking + 3-wound retreat + resumability → Tasks 6 (`RecordDefeat`), 7 (`Withdraw` cost), 8 (survivor-only respawn). ✅
- One repurposed multi-purpose button; Flee retired → Tasks 7 + 10. ✅
- Simultaneous guardians → Task 8. ✅
- Same-scene → honored throughout (no scene split). ✅
- Two-track defeat FX (dissolve vs fade) → Tasks 3, 4, 11. ✅
- No-bail-in-Siege-phase → emergent (button = Engage in Siege; no Withdraw until Attack) — Tasks 5/7. ✅
- Pure/TDD via mcs; MonoBehaviour via manual acceptance → Tasks 1–3 (TDD), 4–11 (manual). ✅
- Docs updates → Task 12. ✅

**Placeholder scan:** Tasks flag three "read this file first" steps (Rewards.cs, DungeonDelve.cs, call-site greps) — these are genuine verification points with concrete code shown, not deferred work. No "TODO/implement later".

**Type consistency:** `EnemySpawn(EnemiesSO, int, int)`, `OpenFight(List<EnemySpawn>, CombatContext, TownToken)`, `NotifyDefeated(EnemyCard, bool)`, `Engage()`, `Withdraw()`, `OnMultiButton()`, `ApplyPhase(CombatPhase)`, `GroupWoundCount(int,int,int)`, `ShakeEnvelope`/`DissolveProgress`, `PlayDestroy`/`PlayFade(System.Action)` — used consistently across tasks. `pendingRewards` shape (summary vs name+summary tuple) is explicitly reconciled in Task 6 Step 3 / Task 7 Step 1 against the real `RewardSummary`.
