# Combat Preview and Trait Icons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make opening a fight (field, guardian, or dungeon) a free look with no committed cost, gate all spending behind Engage/Siege/Influence through one funnel, retire the now-redundant separate preview screen, and give the 13 `EnemyTrait` flags real icon art with an in-card hover legend.

**Architecture:** `CombatController` gains a `Committed` bool and an idempotent `Commit()` funnel called from the two existing methods every commit action already passes through (`Engage()`, `NotifyDefeated()`). Each of the three fight-opening call sites (`EnemyToken`, `GuardianAssault`, `DungeonPanel`/`DungeonDelve`) hands `OpenFight` a callback with its cost-payment instead of paying eagerly. A new `Decline()` gives a free, penalty-free close while `!Committed`. The whole separate preview subsystem (`EnemyPreviewPanel`, `EnemyPreviewEntry`, `PreviewTrigger` + 3 subclasses) is deleted — the only enemy-info surface is the real `EnemyCard`, whose trait badges become real icons (TMP Sprite Assets swapped in via the existing `IconMarkup.TraitBadge` seam) with a new hover tooltip for the badge legend.

**Tech Stack:** Unity (C#), TextMeshPro sprite assets, NUnit via the repo's CLI pure-test harness (`tools/pure-tests/run.sh`).

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-30-combat-preview-and-trait-icons-design.md` — every task below implements a numbered section of it; section references appear in each task.
- Pure, Unity-free classes get CLI-run NUnit tests via `tools/pure-tests/run.sh` (TDD: red, green, commit). `CombatController` and every other `MonoBehaviour` touched here has **no existing automated test coverage** in this codebase (confirmed: `Assets/Tests` has zero `CombatController` references) — that's the established pattern for MonoBehaviour orchestration in this project (see `.superpowers/sdd/task-16-brief.md`: pure rules are CLI-tested, MonoBehaviour wiring is verified manually in Play mode). Tasks 4-6 below follow that pattern: implement, then a concrete manual Play-mode checklist — do not invent automated tests for these.
- Never hand-edit `.unity`/`.prefab` YAML. Every prefab/scene change is a manual step for the user, written as an exact checklist.
- New/changed TMP sprite tags follow the existing format exactly: `<sprite="name" index=0>`, tinted as `<sprite="name" index=0 color=#HEX>` (see `IconMarkup.CrystalTag` and `Assets/Tests/EditMode/IconMarkupTests.cs`).

---

## Task 1: Trait badges become sprite tags

**Spec:** §5.1

**Files:**
- Modify: `Assets/Scripts/UiLanguage/IconMarkup.cs`
- Modify: `Assets/Tests/EditMode/EnemyTraitCopyTests.cs`

**Interfaces:**
- Consumes: `EnemyTrait` (existing enum, `Assets/Scripts/Enums/Enums/EnemyTrait.cs`), `IconMarkup.AuraTint` (existing `"#F5D90A"` constant), `IconMarkup.IsAuraTrait` (existing, unchanged).
- Produces: `IconMarkup.TraitBadge(EnemyTrait) -> string` and `IconMarkup.TraitBadgeTinted(EnemyTrait) -> string` now return TMP sprite tags instead of letters. Signatures unchanged — every existing call site (`EnemyCard.RefreshTraitBadges`, the deleted `EnemyPreviewEntry`) keeps compiling with no changes.

- [ ] **Step 1: Write the failing tests**

Replace the letter-based tests in `Assets/Tests/EditMode/EnemyTraitCopyTests.cs`. Delete `HulkingIsK_BecauseHarryingTookH` entirely (the letter-collision constraint it encoded no longer applies once badges are icons — see spec §5.7) and add:

```csharp
    [Test]
    public void TraitBadge_ReturnsItsSpriteAssetName()
    {
        Assert.AreEqual("<sprite=\"traitArmored\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Armored));
        Assert.AreEqual("<sprite=\"traitElusive\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Elusive));
        Assert.AreEqual("<sprite=\"traitHulking\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Hulking));
        Assert.AreEqual("<sprite=\"traitSwift\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Swift));
        Assert.AreEqual("<sprite=\"traitBrutal\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Brutal));
        Assert.AreEqual("<sprite=\"traitToxic\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Toxic));
        Assert.AreEqual("<sprite=\"traitLeech\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Leech));
        Assert.AreEqual("<sprite=\"traitHarrying\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Harrying));
        Assert.AreEqual("<sprite=\"traitVengeful\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Vengeful));
        Assert.AreEqual("<sprite=\"traitWarlord\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Warlord));
        Assert.AreEqual("<sprite=\"traitMiasma\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Miasma));
        Assert.AreEqual("<sprite=\"traitIronclad\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Ironclad));
        Assert.AreEqual("<sprite=\"traitOutrider\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Outrider));
    }

    [Test]
    public void TraitBadgeTinted_AurasCarryColorAsSpriteAttribute_NotAWrappingColorTag()
    {
        // TMP sprites only honor color as an attribute ON the <sprite> tag itself
        // (and only with Tint enabled on the glyph) — a wrapping <color> tag does
        // nothing to a sprite glyph. See IconMarkup.CrystalTag for the existing
        // precedent this must match.
        Assert.AreEqual("<sprite=\"traitWarlord\" index=0 color=#F5D90A>",
            IconMarkup.TraitBadgeTinted(EnemyTrait.Warlord));
        Assert.AreEqual("<sprite=\"traitMiasma\" index=0 color=#F5D90A>",
            IconMarkup.TraitBadgeTinted(EnemyTrait.Miasma));
        Assert.AreEqual("<sprite=\"traitIronclad\" index=0 color=#F5D90A>",
            IconMarkup.TraitBadgeTinted(EnemyTrait.Ironclad));
        Assert.AreEqual("<sprite=\"traitOutrider\" index=0 color=#F5D90A>",
            IconMarkup.TraitBadgeTinted(EnemyTrait.Outrider));
    }

    [Test]
    public void TraitBadgeTinted_SelfTraitsAreUntinted()
    {
        Assert.AreEqual("<sprite=\"traitArmored\" index=0>", IconMarkup.TraitBadgeTinted(EnemyTrait.Armored));
        Assert.AreEqual("<sprite=\"traitBrutal\" index=0>", IconMarkup.TraitBadgeTinted(EnemyTrait.Brutal));
    }
```

Keep `EveryTraitHasANonEmptyBadge` and `AllBadgesAreUnique` — they're already generic over whatever `TraitBadge` returns, no change needed.

- [ ] **Step 2: Run tests to verify they fail**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/GameScriptableObjectTypes/EnemyTraitTuningSO.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitCopy.cs Assets/Scripts/UiLanguage/IconConcept.cs Assets/Scripts/UiLanguage/IconMarkup.cs Assets/Tests/EditMode/EnemyTraitCopyTests.cs`

Expected: FAIL — `TraitBadge_ReturnsItsSpriteAssetName` and `TraitBadgeTinted_...` assert `"A"` (etc.) instead of the sprite tag; `HulkingIsK_BecauseHarryingTookH` no longer exists so it can't fail, confirm no compile error from its removal.

- [ ] **Step 3: Implement the sprite-tag badges**

In `Assets/Scripts/UiLanguage/IconMarkup.cs`, replace the `TraitBadge` and `TraitBadgeTinted` methods:

```csharp
    // First letter throughout, except Hulking = K (hulK) which yields to Harrying.
    public static string TraitBadge(EnemyTrait t)
    {
        string name = TraitSpriteName(t);
        return name.Length == 0 ? "" : $"<sprite=\"{name}\" index=0>";
    }

    static string TraitSpriteName(EnemyTrait t)
    {
        switch (t)
        {
            case EnemyTrait.Armored:  return "traitArmored";
            case EnemyTrait.Brutal:   return "traitBrutal";
            case EnemyTrait.Elusive:  return "traitElusive";
            case EnemyTrait.Harrying: return "traitHarrying";
            case EnemyTrait.Hulking:  return "traitHulking";
            case EnemyTrait.Ironclad: return "traitIronclad";
            case EnemyTrait.Leech:    return "traitLeech";
            case EnemyTrait.Miasma:   return "traitMiasma";
            case EnemyTrait.Outrider: return "traitOutrider";
            case EnemyTrait.Swift:    return "traitSwift";
            case EnemyTrait.Toxic:    return "traitToxic";
            case EnemyTrait.Vengeful: return "traitVengeful";
            case EnemyTrait.Warlord:  return "traitWarlord";
            default: return "";
        }
    }
```

Replace the old letter-based `case EnemyTrait.Armored: return "A";` block entirely — `TraitSpriteName` above is its full replacement, so delete the old switch inside the old `TraitBadge`.

Then replace `TraitBadgeTinted`:

```csharp
    // Auras render tinted so "which of these is buffing the others" — the read
    // the Siege targeting puzzle depends on — needs no hover. TMP sprites only
    // honor color as an attribute ON the <sprite> tag (and only with Tint
    // enabled on the glyph) — NOT a wrapping <color> rich-text tag, which is
    // why this builds the tag directly instead of wrapping TraitBadge's output.
    public static string TraitBadgeTinted(EnemyTrait t)
    {
        string name = TraitSpriteName(t);
        if (name.Length == 0) return "";
        return IsAuraTrait(t)
            ? $"<sprite=\"{name}\" index=0 color=#{AuraTint.TrimStart('#')}>"
            : $"<sprite=\"{name}\" index=0>";
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/GameScriptableObjectTypes/EnemyTraitTuningSO.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitCopy.cs Assets/Scripts/UiLanguage/IconConcept.cs Assets/Scripts/UiLanguage/IconMarkup.cs Assets/Tests/EditMode/EnemyTraitCopyTests.cs`

Expected: `--- N passed, 0 failed ---` (N = however many tests remain in the file after the deletion/additions).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UiLanguage/IconMarkup.cs Assets/Tests/EditMode/EnemyTraitCopyTests.cs
git commit -m "feat: trait badges render as sprite tags instead of letters"
```

---

## Task 2: Extract the shared trait-legend line format

**Spec:** §5.2

**Files:**
- Modify: `Assets/Scripts/CardPlay/EnemyTraitCopy.cs`
- Modify: `Assets/Tests/EditMode/EnemyTraitCopyTests.cs`

**Interfaces:**
- Consumes: `IconMarkup.TraitBadgeTinted` (Task 1), `IconMarkup.TraitName` (existing), `EnemyTraitCopy.Rule` (existing), `EnemyTraitCopy.Split` (existing).
- Produces: `EnemyTraitCopy.LegendLine(EnemyTrait, EnemyTraitTuning) -> string` and `EnemyTraitCopy.Legend(EnemyTrait mask, EnemyTraitTuning) -> string`. Task 6's `EnemyTraitTooltip` calls `Legend`.

- [ ] **Step 1: Write the failing tests**

Add to `Assets/Tests/EditMode/EnemyTraitCopyTests.cs`:

```csharp
    [Test]
    public void LegendLine_IsBadgeThenNameThenRule()
    {
        var tuning = new EnemyTraitTuning();
        string expected = IconMarkup.TraitBadgeTinted(EnemyTrait.Armored) + " " +
                           IconMarkup.TraitName(EnemyTrait.Armored) + " — " +
                           EnemyTraitCopy.Rule(EnemyTrait.Armored, tuning);
        Assert.AreEqual(expected, EnemyTraitCopy.LegendLine(EnemyTrait.Armored, tuning));
    }

    [Test]
    public void Legend_JoinsOneLegendLinePerSetTrait()
    {
        var tuning = new EnemyTraitTuning();
        var mask = EnemyTrait.Armored | EnemyTrait.Toxic;
        string expected = EnemyTraitCopy.LegendLine(EnemyTrait.Armored, tuning) + "\n" +
                           EnemyTraitCopy.LegendLine(EnemyTrait.Toxic, tuning);
        Assert.AreEqual(expected, EnemyTraitCopy.Legend(mask, tuning));
    }

    [Test]
    public void Legend_NoneIsEmpty()
    {
        Assert.AreEqual("", EnemyTraitCopy.Legend(EnemyTrait.None, new EnemyTraitTuning()));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/GameScriptableObjectTypes/EnemyTraitTuningSO.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitCopy.cs Assets/Scripts/UiLanguage/IconConcept.cs Assets/Scripts/UiLanguage/IconMarkup.cs Assets/Tests/EditMode/EnemyTraitCopyTests.cs`

Expected: compile FAILS with "does not contain a definition for `LegendLine`".

- [ ] **Step 3: Implement `LegendLine`/`Legend`**

Add to `Assets/Scripts/CardPlay/EnemyTraitCopy.cs`, inside the `EnemyTraitCopy` class:

```csharp
    // Badge + name + rule, one line — the format both the live card's hover
    // tooltip (Task 6) and (formerly) the pre-fight preview render identically,
    // because there is exactly one implementation of it.
    public static string LegendLine(EnemyTrait t, EnemyTraitTuning tuning)
        => IconMarkup.TraitBadgeTinted(t) + " " + IconMarkup.TraitName(t) + " — " + Rule(t, tuning);

    // One LegendLine per set trait in mask, newline-joined. "" for EnemyTrait.None.
    public static string Legend(EnemyTrait mask, EnemyTraitTuning tuning)
    {
        var lines = Split(mask);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(LegendLine(lines[i], tuning));
        }
        return sb.ToString();
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `tools/pure-tests/run.sh Assets/Scripts/Enums/Enums/EnemyTrait.cs Assets/Scripts/GameScriptableObjectTypes/EnemyTraitTuningSO.cs Assets/Scripts/CardPlay/EnemyCombatant.cs Assets/Scripts/CardPlay/EnemyTraitCopy.cs Assets/Scripts/UiLanguage/IconConcept.cs Assets/Scripts/UiLanguage/IconMarkup.cs Assets/Tests/EditMode/EnemyTraitCopyTests.cs`

Expected: `--- N passed, 0 failed ---`

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CardPlay/EnemyTraitCopy.cs Assets/Tests/EditMode/EnemyTraitCopyTests.cs
git commit -m "feat: extract shared trait legend-line format"
```

---

## Task 3: Delete the separate preview subsystem

**Spec:** §3

**Files:**
- Delete: `Assets/Scripts/GameObjectScripts/Preview/EnemyPreviewPanel.cs` (+ `.meta`)
- Delete: `Assets/Scripts/GameObjectScripts/Preview/EnemyPreviewEntry.cs` (+ `.meta`)
- Delete: `Assets/Scripts/GameObjectScripts/Preview/EnemyPreviewData.cs` (+ `.meta`)
- Delete: `Assets/Scripts/GameObjectScripts/Preview/PreviewTrigger.cs` (+ `.meta`)
- Delete: `Assets/Scripts/GameObjectScripts/Preview/EnemyTokenPreviewTrigger.cs` (+ `.meta`)
- Delete: `Assets/Scripts/GameObjectScripts/Preview/PlacePreviewTrigger.cs` (+ `.meta`)
- Delete: `Assets/Scripts/GameObjectScripts/PlaceUI/FanPreviewTrigger.cs` (+ `.meta`)
- Modify: `Assets/Scripts/CardPlay/PreviewRules.cs` (remove `RemainingGuardians`, its only callers are the deleted `PlacePreviewTrigger`/`FanPreviewTrigger`)
- Modify: `Assets/Tests/EditMode/PreviewRulesTests.cs` (remove `RemainingGuardians_ReturnsTailAfterDefeats`)

**Interfaces:**
- Consumes: nothing new.
- Produces: nothing new. `PreviewRules.CanPreview`, `PreviewRules.EncounterVisible`, and `PreviewRules.ClampAxis` all **survive** — Task 4 gives `EncounterVisible`/`CanPreview` a new caller (`CombatController.OpenFight`) and Task 6 gives `ClampAxis` a new caller (`EnemyTraitTooltip`). Only `RemainingGuardians` is dead (its two callers are both deleted here and nothing else calls it).

None of `EnemyToken.cs`, `TownToken.cs`, `DungeonToken.cs`, or `PlaceFanSlot`/`AssaultButton` reference these classes in code — every deleted `PreviewTrigger` subclass is a *sibling* component discovered by Unity via `[RequireComponent]`, not a C# reference, so this deletion does not break compilation anywhere else. It does leave orphaned "Missing Script" components on scene/prefab GameObjects until Task 7's manual cleanup removes them — harmless (Unity just won't run them) until then.

- [ ] **Step 1: Verify no other code references the classes being deleted**

Run: `grep -rl "EnemyPreviewPanel\|EnemyPreviewEntry\|EnemyPreviewData\|PreviewTrigger\|FanPreviewTrigger" Assets/Scripts --include=*.cs | grep -v "GameObjectScripts/Preview/" | grep -v "PlaceUI/FanPreviewTrigger.cs"`

Expected: no output (confirms nothing outside the files being deleted references them). If this prints a file, stop and read it — the deletion isn't safe as scoped.

- [ ] **Step 2: Delete the files**

```bash
git rm "Assets/Scripts/GameObjectScripts/Preview/EnemyPreviewPanel.cs" "Assets/Scripts/GameObjectScripts/Preview/EnemyPreviewPanel.cs.meta"
git rm "Assets/Scripts/GameObjectScripts/Preview/EnemyPreviewEntry.cs" "Assets/Scripts/GameObjectScripts/Preview/EnemyPreviewEntry.cs.meta"
git rm "Assets/Scripts/GameObjectScripts/Preview/EnemyPreviewData.cs" "Assets/Scripts/GameObjectScripts/Preview/EnemyPreviewData.cs.meta"
git rm "Assets/Scripts/GameObjectScripts/Preview/PreviewTrigger.cs" "Assets/Scripts/GameObjectScripts/Preview/PreviewTrigger.cs.meta"
git rm "Assets/Scripts/GameObjectScripts/Preview/EnemyTokenPreviewTrigger.cs" "Assets/Scripts/GameObjectScripts/Preview/EnemyTokenPreviewTrigger.cs.meta"
git rm "Assets/Scripts/GameObjectScripts/Preview/PlacePreviewTrigger.cs" "Assets/Scripts/GameObjectScripts/Preview/PlacePreviewTrigger.cs.meta"
git rm "Assets/Scripts/GameObjectScripts/PlaceUI/FanPreviewTrigger.cs" "Assets/Scripts/GameObjectScripts/PlaceUI/FanPreviewTrigger.cs.meta"
```

If the `Preview` folder is now empty, also remove its now-orphaned `.meta`: check with `ls "Assets/Scripts/GameObjectScripts/Preview/"` — if empty, `git rm "Assets/Scripts/GameObjectScripts/Preview.meta"` too (Unity will recreate the folder/meta automatically if anything is ever added back).

- [ ] **Step 3: Remove the now-dead `RemainingGuardians`**

In `Assets/Scripts/CardPlay/PreviewRules.cs`, delete the `RemainingGuardians<T>` method entirely (the generic tail-of-roster helper). Leave `CanPreview`, `EncounterVisible`, and `ClampAxis` untouched.

In `Assets/Tests/EditMode/PreviewRulesTests.cs`, delete the `RemainingGuardians_ReturnsTailAfterDefeats` test. Leave the other three tests untouched.

- [ ] **Step 4: Run the surviving PreviewRules tests**

Run: `tools/pure-tests/run.sh Assets/Scripts/CardPlay/PreviewRules.cs Assets/Tests/EditMode/PreviewRulesTests.cs`

Expected: `--- 3 passed, 0 failed ---`

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CardPlay/PreviewRules.cs Assets/Tests/EditMode/PreviewRulesTests.cs
git commit -m "refactor: delete the separate enemy-preview subsystem

The real EnemyCard is about to become free to open (next task) via a
committed-gate on the combat canvas, which makes the separate hover
preview screen (EnemyPreviewPanel/Entry, PreviewTrigger and its three
subclasses) fully redundant. RemainingGuardians goes with its only
two callers."
```

Note: this leaves "Missing Script" warnings on the scene/prefab GameObjects that hosted these components until Task 7's manual cleanup. That's expected and harmless — Unity will log a warning but nothing breaks.

---

## Task 4: `CombatController` commit gate

**Spec:** §2, §3.1, §5.3

**Files:**
- Modify: `Assets/Scripts/Managers/CombatController.cs`

**Interfaces:**
- Consumes: `PreviewRules.CanPreview()`, `PreviewRules.EncounterVisible(IReadOnlyList<bool>)` (existing, survives Task 3), `EnemyCardMorph.MorphBack(Vector2, System.Action)` (existing, `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyCardMorph.cs`), `GameManager.Instance.CloseCombatCanvas()` (existing).
- Produces: `CombatController.Committed` (public bool getter), `CombatController.Decline()` (public, no-arg), `CombatController.OpenFight(...)` gains a 6th parameter `System.Action onCommit = null`. Task 5's three entry points call the new `OpenFight` overload shape and rely on `Commit()` firing exactly once. Task 7 wires a click-off catcher to `Decline()`.

This is MonoBehaviour orchestration with no existing automated test seam (see Global Constraints) — implement directly, verify in Play mode per Step 3.

- [ ] **Step 1: Add the commit gate and blind-state check to `OpenFight`**

In `Assets/Scripts/Managers/CombatController.cs`:

Add two new serialized fields near the other `[Header("Enemy placement...")]` fields (put them in a new header):

```csharp
    [Header("Blind state (spec 2026-07-30 §3.1)")]
    [SerializeField] GameObject blindState;   // the "You cannot see..." object; toggled, never destroyed
    [SerializeField] TextMeshProUGUI blindText;
```

Add `using TMPro;` to the top of the file if it isn't already imported (check first — it is not, per the current file's `using` block).

Add the new state next to the existing `Phase`/`resolving` fields:

```csharp
    // True once the player has spent something on this fight — the turn's
    // action, a visit's action, Explore, Siege, or Influence (spec 2026-07-30
    // §2). False from OpenFight until the first Engage/Siege-kill/Influence.
    public bool Committed { get; private set; }
    System.Action pendingOnCommit; // this fight's context-specific cost payment, run once by Commit()
```

Replace the `OpenFight` signature and body. Old:
```csharp
    public void OpenFight(List<EnemySpawn> spawns, CombatContext context,
        TownToken guardianPlace = null, EnemyToken fieldToken = null, DungeonToken dungeonToken = null)
    {
        this.context = context;
        this.guardianPlace = guardianPlace;
        this.fieldToken = fieldToken;
        this.dungeonToken = dungeonToken;
        live.Clear();
        pendingShrineType = -1;   // a new fight inherits no owed shrine reward
        pendingShrineSo = null;

        var parent = GameManager.Instance.enemyCardCombatPosition.transform;
        // Clear any stragglers (a fled fight's survivors, or an out-of-range peek
        // card) so a new fight never inherits stale cards.
        foreach (var stale in parent.GetComponentsInChildren<EnemyCard>())
            Destroy(stale.gameObject);

        var prefab = FindAnyObjectByType<EnemyDeck>().PrefabEnemyCard;
```

New:
```csharp
    public void OpenFight(List<EnemySpawn> spawns, CombatContext context,
        TownToken guardianPlace = null, EnemyToken fieldToken = null, DungeonToken dungeonToken = null,
        System.Action onCommit = null)
    {
        this.context = context;
        this.guardianPlace = guardianPlace;
        this.fieldToken = fieldToken;
        this.dungeonToken = dungeonToken;
        this.pendingOnCommit = onCommit;
        Committed = false;
        live.Clear();
        pendingShrineType = -1;   // a new fight inherits no owed shrine reward
        pendingShrineSo = null;

        var parent = GameManager.Instance.enemyCardCombatPosition.transform;
        // Clear any stragglers (a fled fight's survivors, or an out-of-range peek
        // card) so a new fight never inherits stale cards.
        foreach (var stale in parent.GetComponentsInChildren<EnemyCard>())
            Destroy(stale.gameObject);

        // Blind-source gate (spec 2026-07-30 §3.1): the same aggregation the
        // deleted EnemyPreviewPanel used, now checked once at open time instead
        // of by a separate hover screen. Nothing sets a blind source today
        // (CanPreview always returns true), so this path is not yet reachable —
        // it exists so a future blindness source has somewhere to land.
        var visible = new List<bool>(spawns.Count);
        for (int i = 0; i < spawns.Count; i++) visible.Add(PreviewRules.CanPreview());
        bool blind = !PreviewRules.EncounterVisible(visible);
        if (blindState != null) blindState.SetActive(blind);
        if (blind)
        {
            if (blindText != null)
                blindText.text = spawns.Count == 1
                    ? "You cannot see the enemy you are about to confront."
                    : "You cannot see the enemies you are about to confront.";
            GameManager.Instance.combatCanvas.enabled = true;
            SetPhase(CombatPhase.Siege);
            return; // no cards, nothing to Engage/Siege/Influence against — Decline() is the only exit
        }

        var prefab = FindAnyObjectByType<EnemyDeck>().PrefabEnemyCard;
```

The rest of `OpenFight` (the spawn loop through `SetPhase(CombatPhase.Siege)`) is unchanged.

- [ ] **Step 2: Add `Commit()`, call it from `Engage()` and `NotifyDefeated()`, add `Decline()`**

Add a new private method, near `SetPhase`:

```csharp
    // The one funnel (spec 2026-07-30 §2.2): every player-facing action that
    // starts spending something on this fight passes through Engage() or
    // NotifyDefeated() before doing anything else. Idempotent, so calling it
    // from both is safe — by the time NotifyDefeated fires for an Attack-phase
    // kill, Engage() already committed and this is a no-op.
    void Commit()
    {
        if (Committed) return;
        Committed = true;
        pendingOnCommit?.Invoke();
        pendingOnCommit = null;
    }
```

In `Engage()`, add the call right after the existing phase guard:
```csharp
    public void Engage()
    {
        if (Phase != CombatPhase.Siege) return;
        Commit();

        var player = FindAnyObjectByType<Player>();
```
(rest of `Engage()` unchanged)

In `NotifyDefeated(EnemyCard card, bool wasInfluence)`, add the call right after the existing early-return guard:
```csharp
    public void NotifyDefeated(EnemyCard card, bool wasInfluence)
    {
        if (!live.Remove(card)) return;
        Commit();

        // No re-layout: survivors hold the slots they opened the fight in.
```
(rest of `NotifyDefeated` unchanged)

Add `Decline()` and its private helpers near `Withdraw()`/`EndFight`:

```csharp
    // Free "never mind" exit (spec 2026-07-30 §2.4), available only before
    // anything was spent. Deliberately NOT Withdraw() with the penalty removed
    // — Withdraw() pays a flee cost and banks partial progress for a fight that
    // was actually fought; nothing here was fought, so none of that applies.
    public void Decline()
    {
        if (Committed) return;

        Phase = CombatPhase.Resolved;
        pendingOnCommit = null;
        if (blindState != null) blindState.SetActive(false);

        if (context == CombatContext.Field && fieldToken != null && live.Count > 0)
        {
            StartCoroutine(MorphAwayThenClose());
            return;
        }
        CloseDeclined();
    }

    IEnumerator MorphAwayThenClose()
    {
        Vector2 toLocal = OriginLocalPoint(OriginWorld());
        var cards = new List<EnemyCard>(live);
        int pending = 0;
        foreach (var card in cards)
            if (card != null && card.GetComponent<EnemyCardMorph>() != null) pending++;

        foreach (var card in cards)
        {
            if (card == null) continue;
            var morph = card.GetComponent<EnemyCardMorph>();
            if (morph != null) morph.MorphBack(toLocal, () => pending--);
        }
        if (pending > 0) yield return new WaitUntil(() => pending <= 0);

        fieldToken.SetBoardVisible(true);
        CloseDeclined();
    }

    void CloseDeclined()
    {
        foreach (var card in live)
            if (card != null) Destroy(card.gameObject);
        live.Clear();

        GameManager.Instance.CloseCombatCanvas();
        guardianPlace = null;
        fieldToken = null;
        dungeonToken = null;

        if (onCombatPhaseChanged != null) onCombatPhaseChanged.Raise();
    }
```

- [ ] **Step 3: Manual verification in the Unity Editor**

Since this class has no automated test coverage, verify by hand in Play mode (the `blindState`/`blindText` fields can stay unwired for this pass — Task 7 wires them; leaving them `null` is safe, every access above is null-checked):

1. Enter Play mode, walk up to a field enemy and click it. Confirm the combat canvas opens and the enemy card appears.
2. Open the Console window — confirm no null-reference errors from the new code.
3. Check the Inspector on the `CombatController` instance (or add a temporary `Debug.Log(Committed)` if easier) — confirm `Committed` is `false` immediately after opening.
4. Exit Play mode. This task only adds the gate; nothing yet calls `Decline()` or supplies a real `onCommit` (Task 5), so `Committed` will never flip yet — that's expected for this task's scope.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Managers/CombatController.cs
git commit -m "feat: add a commit gate to CombatController

Committed stays false from OpenFight until Commit() fires, which
happens exactly once, funneled through Engage() and NotifyDefeated()
— the two methods every commit-worthy player action already passes
through. Decline() gives a free close while uncommitted. Entry points
don't yet supply onCommit or call Decline() — next task."
```

---

## Task 5: Entry points defer their cost, dungeon panel drops its redundant preview

**Spec:** §2.3, §3, §5.4

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/GuardianAssault.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonDelve.cs`
- Modify: `Assets/Scripts/GameObjectScripts/DungeonMenuScripts/DungeonPanel.cs`

**Interfaces:**
- Consumes: `CombatController.OpenFight(..., System.Action onCommit)` (Task 4).
- Produces: `DungeonDelve.Begin(DungeonToken, System.Action onCommit)` — signature changes from single-arg; its only caller (`DungeonPanel.PerformDelve`) is updated in this same task.

- [ ] **Step 1: `EnemyToken.StartCombat` defers `BeginAction()`**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs`, replace the `StartCombat` coroutine. Old:
```csharp
    IEnumerator StartCombat()
    {
        // A fight is the turn's one action (spec 2026-07-21): block a second
        // encounter, and taking this one performs the implicit Explore->Action
        // transition (commits the movement stack, locks further movement).
        if (TurnPhaseController.Instance != null)
        {
            if (!TurnPhaseController.Instance.CanInteract)
            {
                GameLog.Instance.Post("You've already taken your action this turn.");
                yield break;
            }
            TurnPhaseController.Instance.BeginAction();
        }

        GameManager.Instance.activeCombatant = this;
        yield return GameManager.Instance.PlayCombatIntro();
        var spawns = new List<CombatController.EnemySpawn>
        {
            new CombatController.EnemySpawn(enemy, bonusHP, bonusAttack)
        };
        CombatController.Instance.OpenFight(spawns, CombatContext.Field, fieldToken: this);
    }
```
New:
```csharp
    IEnumerator StartCombat()
    {
        // Opening is a free look (spec 2026-07-30 §2.3): still gated on the
        // turn's action being available at all (no point opening a fight you
        // can't commit to), but BeginAction() itself waits for a real commit.
        if (TurnPhaseController.Instance != null && !TurnPhaseController.Instance.CanInteract)
        {
            GameLog.Instance.Post("You've already taken your action this turn.");
            yield break;
        }

        GameManager.Instance.activeCombatant = this;
        yield return GameManager.Instance.PlayCombatIntro();
        var spawns = new List<CombatController.EnemySpawn>
        {
            new CombatController.EnemySpawn(enemy, bonusHP, bonusAttack)
        };
        CombatController.Instance.OpenFight(spawns, CombatContext.Field, fieldToken: this,
            onCommit: () => { if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.BeginAction(); });
    }
```

- [ ] **Step 2: `GuardianAssault.Begin` defers `CommitVisitAction()`**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/GuardianAssault.cs`, replace `Begin`. Old:
```csharp
    public void Begin(TownToken town)
    {
        // Assaulting is the visit's committed action (spec 2026-07-22): spend the
        // turn's action now (the AssaultButton is gated so this only fires when the
        // visit still owns it).
        if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();

        // Tear down the place menu the button click came from.
        foreach (var card in FindObjectsByType<TownCard>())
            Destroy(card.gameObject);
        GameManager.Instance.townCanvas.enabled = false;
        GameManager.Instance.CombatCanvasActive(); // canvas chrome + multi-purpose button, no field banner

        var roster = town.townSO.guardians;
        int already = ConquestTracker.Instance.DefeatedCount(town.gridPos);
        var spawns = new List<CombatController.EnemySpawn>();
        for (int i = already; i < roster.Count; i++)
            spawns.Add(new CombatController.EnemySpawn(roster[i], 0, 0)); // guardians unscaled

        CombatController.Instance.OpenFight(spawns, CombatContext.Guardian, town);
    }
```
New:
```csharp
    public void Begin(TownToken town)
    {
        // Tear down the place menu the button click came from.
        foreach (var card in FindObjectsByType<TownCard>())
            Destroy(card.gameObject);
        GameManager.Instance.townCanvas.enabled = false;
        GameManager.Instance.CombatCanvasActive(); // canvas chrome + multi-purpose button, no field banner

        var roster = town.townSO.guardians;
        int already = ConquestTracker.Instance.DefeatedCount(town.gridPos);
        var spawns = new List<CombatController.EnemySpawn>();
        for (int i = already; i < roster.Count; i++)
            spawns.Add(new CombatController.EnemySpawn(roster[i], 0, 0)); // guardians unscaled

        // Opening the assault is now a free look (spec 2026-07-30 §2.3);
        // assaulting is still the visit's committed action, but only once the
        // player actually commits inside the fight.
        CombatController.Instance.OpenFight(spawns, CombatContext.Guardian, town,
            onCommit: () => { if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction(); });
    }
```

- [ ] **Step 3: `DungeonDelve.Begin` takes and forwards `onCommit`**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonDelve.cs`, replace `Begin`. Old:
```csharp
    public void Begin(DungeonToken token)
    {
        GameManager.Instance.CombatCanvasActive();

        int slot = DungeonTracker.Instance.DefeatedCount(token.gridPos); // 0..2 → tier 1..3
        var spawns = new List<CombatController.EnemySpawn>
        {
            new CombatController.EnemySpawn(token.dungeonSO.enemies[slot], 0, 0)
        };
        CombatController.Instance.OpenFight(spawns, CombatContext.Dungeon, dungeonToken: token);
    }
```
New:
```csharp
    public void Begin(DungeonToken token, System.Action onCommit)
    {
        GameManager.Instance.CombatCanvasActive();

        int slot = DungeonTracker.Instance.DefeatedCount(token.gridPos); // 0..2 → tier 1..3
        var spawns = new List<CombatController.EnemySpawn>
        {
            new CombatController.EnemySpawn(token.dungeonSO.enemies[slot], 0, 0)
        };
        CombatController.Instance.OpenFight(spawns, CombatContext.Dungeon, dungeonToken: token, onCommit: onCommit);
    }
```

- [ ] **Step 4: `DungeonPanel.PerformDelve` defers Explore payment and `CommitVisitAction()`, and drops the redundant next-enemy preview**

In `Assets/Scripts/GameObjectScripts/DungeonMenuScripts/DungeonPanel.cs`:

Remove the `previewText` field and the `UpdatePreview` method (spec §3 — once Delve opens the real card for free, this simplified duplicate has no job). Old field:
```csharp
    [SerializeField] TextMeshProUGUI previewText;
```
Delete this line entirely.

Old `UpdatePreview` method:
```csharp
    private void UpdatePreview(DungeonsSO so, int cleared)
    {
        var next = so.enemies[cleared];
        previewText.text = PreviewRules.CanPreview()
            ? $"Next: {next.cardName}   {IconMarkup.Cost(IconConcept.Attack, next.enemyAttack)}   {IconMarkup.Cost(IconConcept.Hp, next.enemyHP)}"
            : "You cannot see the enemy you are about to confront.";
    }
```
Delete this method entirely, and its call site in `Refresh()`. Old `Refresh()` tail:
```csharp
        delveButton.gameObject.SetActive(!complete);
        delveButtonText.text = $"Delve — {IconMarkup.Cost(IconConcept.Explore, so.exploreCost)}";
        var player = FindAnyObjectByType<Player>();
        UpdateDelveInteractable(player != null ? player.PlayerExplore : 0);

        if (complete) { previewText.text = ""; return; }

        UpdatePreview(so, cleared);
    }
```
New:
```csharp
        delveButton.gameObject.SetActive(!complete);
        delveButtonText.text = $"Delve — {IconMarkup.Cost(IconConcept.Explore, so.exploreCost)}";
        var player = FindAnyObjectByType<Player>();
        UpdateDelveInteractable(player != null ? player.PlayerExplore : 0);
    }
```
(The `cleared` parameter to `Refresh`'s caller shape is unaffected — check `Refresh()`'s own signature/local `cleared` variable is still used earlier in the method for `progressText`; do not remove that usage, only the `UpdatePreview(so, cleared)` call at the tail.)

Now replace `PerformDelve`. Old:
```csharp
    public static void PerformDelve(DungeonToken token)
    {
        if (token == null) return;
        var player = FindAnyObjectByType<Player>();
        int cost = token.dungeonSO.exploreCost;
        if (player.PlayerExplore < cost)
        {
            GameLog.Instance.Post(
                $"You need {cost} Explore to delve into {token.dungeonSO.cardName}.");
            return;
        }
        player.PlayerExplore -= cost;
        player.GetCurrentExplore();
        // Delving is the visit's committed action (spec 2026-07-22): spend the
        // turn's action now. This also commits the movement stack.
        if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
        // Delving is a firm decision: commit all pending plays so the explore
        // that paid for it can't be undone into a negative total.
        GameManager.Instance.commands.ClearStack();

        DungeonDelve.Instance.Begin(token);
    }
```
New:
```csharp
    public static void PerformDelve(DungeonToken token)
    {
        if (token == null) return;
        var player = FindAnyObjectByType<Player>();
        int cost = token.dungeonSO.exploreCost;
        if (player.PlayerExplore < cost)
        {
            GameLog.Instance.Post(
                $"You need {cost} Explore to delve into {token.dungeonSO.cardName}.");
            return;
        }

        // Opening the delve is now a free look (spec 2026-07-30 §2.3): the
        // affordability check above still gates opening, but paying Explore,
        // committing the visit's action, and locking the undo stack all wait
        // for a real commit inside the fight.
        DungeonDelve.Instance.Begin(token, onCommit: () =>
        {
            player.PlayerExplore -= cost;
            player.GetCurrentExplore();
            if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
            // Delving is a firm decision: commit all pending plays so the explore
            // that paid for it can't be undone into a negative total.
            GameManager.Instance.commands.ClearStack();
        });
    }
```

- [ ] **Step 5: Manual verification in the Unity Editor**

1. Confirm the project has no compile errors in the Console after these edits (the `previewText` field removal will show a "missing field" warning in the Inspector on the DungeonPanel prefab — expected, cleaned up in Task 7).
2. Enter Play mode. Walk onto a field enemy's cell, click it, confirm the combat canvas opens and the turn's action is **not yet marked spent** (check the HUD's turn indicator, or `TurnPhaseController.Instance.CanInteract` if you have a debug view).
3. Press the enemy card's Siege button (if affordable) or the Engage button — confirm the turn action is now spent.
4. Repeat for a town guardian assault (open the town fan, press Assault) and a dungeon delve (open the dungeon panel or fan, press Delve) — in both cases confirm nothing is spent (visit action, Explore) until a real commit inside the canvas.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs Assets/Scripts/GameObjectScripts/GameBoardObjects/GuardianAssault.cs Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonDelve.cs Assets/Scripts/GameObjectScripts/DungeonMenuScripts/DungeonPanel.cs
git commit -m "feat: defer combat-entry cost payment to the commit gate

Field, guardian, and dungeon entry points all hand OpenFight their
cost-payment as a callback instead of paying eagerly, so opening any
fight is now a free look — matching the 'opening is a free peek'
convention TurnPhaseController.BeginVisit/CommitVisitAction already
established for town/dungeon menus. DungeonPanel's separate
next-enemy text preview is dropped: once Delve opens the real card
for free, it duplicated the same information one panel back."
```

---

## Task 6: Trait badge hover tooltip

**Spec:** §5.5, §6

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyTraitBadgeHover.cs`
- Create: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyTraitTooltip.cs`

**Interfaces:**
- Consumes: `EnemyTraitCopy.Legend(EnemyTrait, EnemyTraitTuning)` (Task 2), `EnemyTraitCopy.Split(EnemyTrait)` (existing), `PreviewRules.ClampAxis(float, float, float, float)` (existing, survives Task 3), `EnemyCard.Traits` (existing), `CombatController.Instance.Tuning` (existing).
- Produces: `EnemyTraitTooltip.Instance` (singleton, mirrors `CombatController`/the deleted `EnemyPreviewPanel`'s pattern), `EnemyTraitTooltip.Show(EnemyTrait, EnemyTraitTuning, Vector3 screenPosition)`, `EnemyTraitTooltip.Hide()`. Task 7 places one instance in the combat scene and wires `EnemyTraitBadgeHover` onto `EnemyCard`'s `traitBadges` GameObject.

Both are MonoBehaviours with no automated test seam (see Global Constraints) — implement directly, verify in Play mode per Step 3.

- [ ] **Step 1: `EnemyTraitTooltip` — screen-anchored, edge-clamped, single text block**

Create `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyTraitTooltip.cs`:

```csharp
using TMPro;
using UnityEngine;

// One enemy's trait legend (badge + name + rule per trait), shown on hover over
// an EnemyCard's badge row (spec 2026-07-30 §5.5/§6). The only place trait
// info renders now that the separate preview screen is gone — used identically
// whether the fight is committed or not.
//
// Positioning mirrors the deleted EnemyPreviewPanel: project a screen point
// onto the canvas plane, then slide the box back on-screen via
// PreviewRules.ClampAxis if it would clip an edge. That math wasn't the
// problem being fixed; having two competing content sources was.
public class EnemyTraitTooltip : MonoBehaviour
{
    public static EnemyTraitTooltip Instance { get; private set; }

    [SerializeField] GameObject root;          // toggled on Show/Hide
    [SerializeField] RectTransform panelRect;  // moved to the screen position
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Vector2 offset = new Vector2(0f, 30f); // nudge off the badge row (screen px)
    [SerializeField] float screenMargin = 12f;

    Canvas canvas;
    RectTransform canvasRect;

    void Awake()
    {
        Instance = this;
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas != null) canvasRect = canvas.transform as RectTransform;

        if (root != null)
        {
            var cg = root.GetComponent<CanvasGroup>();
            if (cg == null) cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; // never steal the hover it's drawn over
            cg.interactable = false;
            root.SetActive(false);
        }
    }

    public void Show(EnemyTrait traits, EnemyTraitTuning tuning, Vector3 screenPosition)
    {
        string text = EnemyTraitCopy.Legend(traits, tuning);
        if (string.IsNullOrEmpty(text)) { Hide(); return; }

        if (root != null) root.SetActive(true);
        if (label != null) label.text = text;

        PlaceAtScreenPoint((Vector2)screenPosition + offset);

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        var corners = new Vector3[4];
        panelRect.GetWorldCorners(corners);
        Camera cam = canvas != null ? canvas.worldCamera : null;
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        float width = topRight.x - bottomLeft.x;
        float height = topRight.y - bottomLeft.y;

        float clampedX = PreviewRules.ClampAxis(bottomLeft.x, width, Screen.width, screenMargin);
        float clampedY = PreviewRules.ClampAxis(bottomLeft.y, height, Screen.height, screenMargin);
        PlaceAtScreenPoint((Vector2)screenPosition + offset + new Vector2(clampedX - bottomLeft.x, clampedY - bottomLeft.y));
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    void PlaceAtScreenPoint(Vector2 screenPoint)
    {
        if (canvasRect == null) { panelRect.position = screenPoint; return; }
        Camera cam = canvas != null ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPoint, cam, out Vector3 world))
            panelRect.position = world;
    }
}
```

Add `using UnityEngine.UI;` alongside the existing `using` lines (needed for `LayoutRebuilder`).

- [ ] **Step 2: `EnemyTraitBadgeHover` — the pointer trigger on the badge row**

Create `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyTraitBadgeHover.cs`:

```csharp
using UnityEngine;
using UnityEngine.EventSystems;

// Hover trigger for one EnemyCard's trait badge row (spec 2026-07-30 §5.5).
// Lives on the traitBadges GameObject itself (already a raycast target), not
// on the whole card — trait info is reachable by hovering the badges
// specifically, not by hovering anywhere on the card.
public class EnemyTraitBadgeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] EnemyCard card;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EnemyTraitTooltip.Instance == null || card == null) return;
        var tuning = CombatController.Instance != null ? CombatController.Instance.Tuning : new EnemyTraitTuning();

        // transform.position on a Screen Space - Camera canvas is world space,
        // not screen pixels — convert via this GameObject's own canvas/camera,
        // the same pattern EnemyCard's other screen-position math already uses.
        var canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null ? canvas.rootCanvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, transform.position);

        EnemyTraitTooltip.Instance.Show(card.Traits, tuning, screenPoint);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (EnemyTraitTooltip.Instance != null) EnemyTraitTooltip.Instance.Hide();
    }
}
```

- [ ] **Step 3: Manual verification in the Unity Editor**

Neither component is wired into a scene/prefab yet (Task 7 does that) — for this task, verify only that the project compiles clean with these two new files:

1. Open the Unity Editor (or let it recompile if already open) and check the Console for compile errors referencing `EnemyTraitTooltip.cs` or `EnemyTraitBadgeHover.cs`.
2. Confirm `EnemyTraitCopy.Legend` (Task 2) and `PreviewRules.ClampAxis` (survives Task 3) resolve — no red squiggles / CS0103 in the IDE.

Full behavioral verification (hover shows the tooltip, positioned correctly, clamped on-screen) happens in Task 7 once both are wired into the scene.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyTraitTooltip.cs Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyTraitBadgeHover.cs
git commit -m "feat: add the trait badge hover tooltip"
```

---

## Task 7: Manual Unity-editor work

**Spec:** §7

**Files:** none (all steps are manual, in-editor — done by you, not by editing files directly)

This task has no code changes. Do these in order; each depends on the previous compiling clean, which Tasks 1-6 already established.

- [ ] **Step 1: Create the 13 TMP Sprite Assets — ⛔ PARKED 2026-07-30, BLOCKED ON ART**

> **Do not do this step with the current PNGs.** Two things changed since this plan was written; both are recorded in spec §5.1 and §7.
>
> **(a) There is no "Tint" step, and never needed to be.** This step originally said to enable a per-glyph **Tint** option on 4 aura icons. No such option exists. Verified against this project's TMP: sprite assets carry no tint field in their character or glyph tables (the shipped `crystal.asset`, which tints correctly today, has none), and `TMP_Text.cs` applies the `color` attribute to a sprite whether or not tinting is flagged. The separate `tint=1` *tag attribute* means only "also multiply by the surrounding text colour." The material's colour field is material-wide and would tint every use of a glyph — not the right lever; leave it white.
>
> **(b) The amber aura tint is dropped entirely, and the source art is unusable.** `IconMarkup.TraitBadgeTinted`/`AuraTint`/`IsAuraTrait` are deleted; all call sites use plain `TraitBadge`. TMP tints by *multiplying*, so a colour only reads on white/near-white art — and these are full-colour painted scenes (Warlord blue, Miasma green, Outrider purple all go muddy; Ironclad is already gold, so it's a no-op). Independently of the tint, the art is wrong for inline badges: all 13 are **RGB with no alpha channel**, so each renders as an opaque square block in a text run, where every working glyph in the game (e.g. `crystal.png`) is an RGBA cutout. At badge size (~24px) `Toxic`/`Miasma` collapse into one green mass and `Leech`/`Brutal` into one red mass; `Elusive`/`Hulking`/`Harrying` are full figures with no readable silhouette; `Swift` is a modern running shoe.
>
> **What's needed to unpark:** flat, single-subject, high-contrast glyphs on transparency — monochrome if the amber aura tint is ever to come back. Then create all 13 identically (no per-glyph tint step) and place them in `Assets/TextMesh Pro/Resources/Sprite Assets/` alongside the existing glyphs, named exactly as the table below — `IconMarkup.TraitSpriteName` references these literally.
>
> **Steps 2-8 below do not depend on this and should proceed now.** Badges keep rendering as the previous letters until real art lands.

The 13 names `IconMarkup.TraitSpriteName` expects (source column is the *current, rejected* art, already renamed in place under `Assets/Images/500FreeSkillIcons/Icons/`):

| Current (rejected) PNG | TMP Sprite Asset name |
|---|---|
| `traitArmored.png` | `traitArmored` |
| `traitElusive.png` | `traitElusive` |
| `traitHulking.png` | `traitHulking` |
| `traitSwift.png` | `traitSwift` |
| `traitBrutal.png` | `traitBrutal` |
| `traitToxic.png` | `traitToxic` |
| `traitLeech.png` | `traitLeech` |
| `traitHarrying.png` | `traitHarrying` |
| `traitVengeful.png` | `traitVengeful` |
| `traitWarlord.png` | `traitWarlord` |
| `traitMiasma.png` | `traitMiasma` |
| `traitIronclad.png` | `traitIronclad` |
| `traitOutrider.png` | `traitOutrider` |

- [ ] **Step 2: Fix `EnemyCard`'s `traitBadges` RectTransform**

> **This is a hard prerequisite for Step 3, not just cosmetics.** Unity raycasts against the RectTransform's **rect**, not the rendered glyphs. At `Size Delta (0, 0)` the row has zero area, so `EnemyTraitBadgeHover` can never receive a pointer event and the tooltip silently never opens — while the badge still *draws*, because TMP overflows a zero rect happily. This is exactly what happened on the first attempt (2026-07-30). `EnemyTraitBadgeHover.Start` now logs a warning if the rect is degenerate, so check the Console if the tooltip doesn't appear.

Open `Assets/Prefabs/EnemyCard.prefab`. Find the `Trait` GameObject (holds the `traitBadges` TMP text, wired in `EnemyCard`'s inspector). It's currently anchored at a fixed `(25, 35)` pixel offset with `Size Delta (0, 0)` — a leftover from when it held one placeholder letter. Re-anchor it to sit in the card's existing HP/Attack/Influence stat row (or immediately below it), with **a real width and height** big enough to lay out several icon glyphs side by side without overlapping neighboring elements. Enter Play mode afterward and confirm a multi-trait enemy's badges render side by side, fully visible, not clipped by the card's edge — and that the row is actually hoverable.

- [ ] **Step 3: Wire `EnemyTraitBadgeHover` onto the badge row**

On the same `Trait` GameObject from Step 2 (inside `EnemyCard.prefab`), add the `EnemyTraitBadgeHover` component (Task 6). Drag the prefab's root `EnemyCard` component into its `card` field.

- [ ] **Step 4: Place `EnemyTraitTooltip` in the combat scene**

In the scene that hosts the combat canvas (check where the deleted `EnemyPreviewPanel` used to live — same canvas), create a new UI GameObject for the tooltip: a root panel (with a `CanvasGroup`, auto-added by `EnemyTraitTooltip.Awake` if missing) containing a `TextMeshProUGUI` label. Add the `EnemyTraitTooltip` component, wire `root`/`panelRect`/`label` in its inspector.

> **Layout is flexible — `root` may be this GameObject or a child, active or inactive.** This was originally a strict two-object requirement: putting the component on the same object it deactivates meant `Awake()` never ran, `Instance` stayed null, and every hover silently did nothing with no error. That trap was removed in code (2026-07-30) — `Instance` now resolves lazily via `FindAnyObjectByType<EnemyTraitTooltip>(FindObjectsInactive.Include)`, the same idiom `DungeonToken`/`ShrineToken`/`TownToken` already use to reach an inactive panel, and initialisation is lazy so it runs on first `Show()` regardless. Visibility is CanvasGroup alpha, not `SetActive`.
>
> `panelRect` still must be wired — `Show()` returns early without it, so the tooltip would never appear.

- [ ] **Step 5: Add the blind-state UI in the combat canvas**

`CombatController` (Task 4) has unwired `blindState`/`blindText` fields. In the combat canvas, add a simple text panel for "You cannot see the enemy/enemies you are about to confront." (matching what the deleted `EnemyPreviewPanel.blindState`/`blindText` used to show), start it inactive, and wire both fields on the `CombatController` component in the inspector.

- [ ] **Step 6: Wire the click-off decline**

The combat canvas needs a `ClickOffCatcher` (`Assets/Scripts/GameObjectScripts/PlaceUI/ClickOffCatcher.cs`, existing component — same click-off idiom used elsewhere in this game) sitting at sibling index 0 behind the canvas content, sized to fill the canvas. Wire its `onClickOff` UnityEvent to `CombatController.Decline()`. No `SetArmed` calls needed — the combat canvas is toggled via `Canvas.enabled`, which the catcher's own doc comment confirms needs no arming, and `Decline()` itself already no-ops once `Committed` is true, so click-off is automatically inert during a real fight.

> **Check the sort order against the hand/HUD canvas before you trust it.** Sibling index 0 only puts the catcher behind *this* canvas's content — it says nothing about the separate hand canvas. If the combat canvas sorts above the hand, a full-screen catcher will swallow the card clicks the entire Siege phase depends on. Either confirm the sort order leaves the hand clickable, or size the catcher to exclude the hand band (`CombatController` already reserves `handKeepOut`, 200px).

- [ ] **Step 7: Delete the retired prefab and orphaned scene references**

Delete `Assets/Prefabs/EnemyPreviewEntry .prefab` (note the literal space before `.prefab`) via the Project window (Delete, not just removing a reference).

In every scene/prefab that had an `EnemyPreviewPanel`, `PreviewTrigger`, `EnemyTokenPreviewTrigger`, `PlacePreviewTrigger`, or `FanPreviewTrigger` component (Task 3 deleted their scripts, leaving "Missing Script" placeholders) — remove those components. Likely locations: the `GameBoard.unity` scene (the old `EnemyPreviewPanel` GameObject itself), `EnemyToken`'s prefab (`EnemyTokenPreviewTrigger` sibling component), and the town/dungeon fan-slot and Assault-button prefabs (`FanPreviewTrigger`/`PlacePreviewTrigger` sibling components). Search: select each prefab/scene, look for a component whose inspector shows "The associated script cannot be loaded" or similar, and remove it.

- [ ] **Step 8: Full manual verification pass**

With everything wired, in Play mode:

1. Open a field encounter — confirm nothing is spent and the turn action is still available (check the turn HUD).
2. Click off (click anywhere outside the cards/buttons) — confirm the canvas closes, the token reappears on the map, and nothing changed (still no action spent).
3. Reopen the same encounter, press Siege on a killable enemy (or Engage if none is affordable) — confirm the turn action is now spent, and click-off no longer closes the canvas.
4. Repeat steps 1-3 for a guardian assault (visit action) and a dungeon delve (Explore + visit action).
5. Hover the badge row, both before and after committing — confirm the same legend text (badge + name + rule per trait) appears both times, positioned near the badges and never clipped off-screen near a screen edge. Check **three** enemies, because the trait count is what varies:
   - a **single-trait** enemy — one legend line, tooltip still shows (there is no multi-trait requirement anywhere in the code);
   - a **multi-trait** enemy — one line per trait, newline-joined; this is the widest box, so it's the one most likely to clip at a screen edge;
   - a **trait-less** enemy — the badge row is deactivated entirely (`RefreshTraitBadges` only activates it when there's at least one badge), so there is nothing to hover and no tooltip. That's correct, not a bug.
6. ~~Confirm aura-trait badges render with the amber tint.~~ **Dropped 2026-07-30** — the amber aura tint is gone (see Step 1). Badges render untinted; auras are told apart by their own icon and the hover legend. Nothing to verify here until real badge art lands.
7. Confirm no "Missing Script" warnings remain in the Console when opening any scene touched in Step 7.

- [ ] **Step 9: Commit the asset/scene/prefab changes**

```bash
git add Assets/Prefabs Assets/Scenes Assets/TextMesh\ Pro
git status
```

Review the status output — it should show the new sprite assets, the deleted `EnemyPreviewEntry .prefab`, and the modified `EnemyCard.prefab`/scene(s)/dungeon panel prefab. Then:

```bash
git commit -m "content: wire trait icons, badge hover tooltip, and combat-preview click-off

Manual Unity-editor work: 13 TMP sprite assets for the trait badges,
EnemyCard's badge-row layout fix, EnemyTraitBadgeHover +
EnemyTraitTooltip wired into the combat scene, blind-state UI, the
click-off catcher wired to CombatController.Decline(), and cleanup of
every scene/prefab reference orphaned by deleting the old preview
subsystem."
```

---

## Self-Review Notes

- **Spec coverage:** §1-2 (problem, commit-gate mechanism) → Task 4. §2.3 (who pays what) → Task 5. §2.4 (Decline) → Task 4. §3 (retirement list) → Task 3 (code) + Task 7 Step 7 (scene/prefab cleanup) + Task 5 Step 4 (DungeonPanel's own redundant preview). §3.1 (blind state) → Task 4 Step 1 (code) + Task 7 Step 5 (UI). §4 (icon table) → Task 7 Step 1. §5.1-5.2 (IconMarkup/EnemyTraitCopy) → Tasks 1-2. §5.3 (CombatController) → Task 4. §5.4 (entry points) → Task 5. §5.5/§6 (tooltip) → Task 6 (code) + Task 7 Steps 3-4 (wiring). §5.6 (deletions) → Task 3. §5.7 (test updates) → Task 1. §7 (manual work) → Task 7. §8 (testing) → Task 7 Step 8, plus the automated coverage in Tasks 1-2.
- **Type consistency:** `OpenFight`'s new `onCommit` parameter, `Commit()`, `Decline()`, `Committed`, `EnemyTraitCopy.Legend`/`LegendLine`, `EnemyTraitTooltip.Show`/`Hide`, `EnemyTraitBadgeHover.card` all match their declared shapes consistently across the task that defines them and the tasks that consume them.
- **No placeholders:** every code step above is complete, runnable code — none deferred to "similar to Task N" or left as a description without a diff.
