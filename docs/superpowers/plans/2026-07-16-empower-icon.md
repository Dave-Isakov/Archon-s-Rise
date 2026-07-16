# Empower Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the concept Empower its own canonical glyph so the literal word "Empower" reads as an icon in authored card/skill descriptions and on the ConvertBanner label.

**Architecture:** Extend the existing M2.11 icon language: add one `IconConcept.Empower` member with an `IconMarkup.TmpName` of `"empower"`, wire the one code label (ConvertBanner) through `IconMarkup.Tag`, and (USER editor) author the `empower` TMP Sprite Asset + a 17th `IconRegistry` entry and sweep authored descriptions. Pure logic is TDD'd with the mcs harness; assets/wiring are USER editor work.

**Tech Stack:** Unity 6000.5.1f1, C#, NUnit (EditMode), Mono `mcs` CLI harness for pure logic, TextMeshPro, ScriptableObject data.

## Global Constraints

- **The Unity editor holds the compile lock** — batch-mode tests won't run while it's open. Pure logic (`IconConcept.cs`, `IconMarkup.cs`, `IconMarkupTests.cs`) is RED/GREEN-verified with the mcs harness; EditMode tests run in the editor's Test Runner at acceptance. MonoBehaviour code (`ConvertBanner.cs`) is verified by the editor compiling cleanly when the user focuses it.
- **mcs is C# ~7** — no switch expressions, tuples, or `out var` in files it compiles.
- **All scene/prefab/asset authoring is USER editor work** — tasks marked **USER (editor)** are step-by-step instructions; never hand-edit scene/prefab/.asset YAML.
- **TMP sprite-asset names are case-sensitive and canonical**: the new name is `empower`. Asset filename = tag name, exactly.
- **Empower is a modifier concept, not an action stat** — it is NOT added to `IconMarkup.ActionStatOrder` and NOT handled by `IconMarkup.TryForStat`.
- **The CardInspector validation message stays prose** — "empower" there is a verb mid-sentence (out of scope, user decision 2026-07-16).
- Spec: `docs/superpowers/specs/2026-07-16-empower-icon-design.md`.
- Commit at the end of every task.

## mcs Harness (used by Task 1)

The reflection runner and nunit copy from the M2.11 session live in `<scratchpad>` already. If absent, recreate per `docs/superpowers/plans/2026-07-15-m2.11-ui-language-iconography.md` ("mcs Harness"). Setup vars (PowerShell, repo root; `<scratchpad>` = the session scratchpad dir):

```powershell
$mcs = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge\bin\mcs.bat"
$nunit = (Get-ChildItem "Library\PackageCache\com.unity.ext.nunit*\net472\unity-custom\nunit.framework.dll").FullName
$s = "<scratchpad>"
Copy-Item $nunit $s -Force   # re-copy: Unity may have re-imported a new PackageCache version
```

---

### Task 1: IconConcept.Empower + IconMarkup.TmpName (pure)

**Files:**
- Modify: `Assets/Scripts/UiLanguage/IconConcept.cs`
- Modify: `Assets/Scripts/UiLanguage/IconMarkup.cs`
- Test: `Assets/Tests/EditMode/IconMarkupTests.cs` (add one assertion)

**Interfaces:**
- Consumes: existing `IconConcept` enum + `IconMarkup.TmpName`/`Tag` (M2.11).
- Produces: `IconConcept.Empower`; `IconMarkup.TmpName(IconConcept.Empower) == "empower"`; therefore `IconMarkup.Tag(IconConcept.Empower) == "<sprite=\"empower\" index=0>"`. Used by Task 2 (ConvertBanner) and Task 3 (authoring/registry).

- [ ] **Step 1: Add the failing assertion** — in `Assets/Tests/EditMode/IconMarkupTests.cs`, inside `Tag_NewConceptsGetNewNames`, add as the last assertion:

```csharp
        Assert.AreEqual("<sprite=\"empower\" index=0>", IconMarkup.Tag(IconConcept.Empower));
```

- [ ] **Step 2: Run to verify failure (RED)**

```powershell
& $mcs -nologo -target:library "-out:$s\Icon.dll" "-r:$nunit" "Assets\Scripts\Enums\Enums\StatType.cs" "Assets\Scripts\Enums\Enums\EmpowerType.cs" "Assets\Scripts\Enums\EmpowerTypeExtensions.cs" "Assets\Scripts\UiLanguage\IconConcept.cs" "Assets\Scripts\UiLanguage\IconMarkup.cs" "Assets\Tests\EditMode\IconMarkupTests.cs"
```

Expected: compile error — `IconConcept.Empower` does not exist.

- [ ] **Step 3: Add the enum member** — in `Assets/Scripts/UiLanguage/IconConcept.cs`, add `Empower` after `Dungeon`:

```csharp
    Castle,
    Dungeon,
    Empower,
}
```

- [ ] **Step 4: Add the TmpName case** — in `Assets/Scripts/UiLanguage/IconMarkup.cs`, add the case immediately after the `Dungeon` case in the `TmpName` switch:

```csharp
            case IconConcept.Dungeon:    return "dungeon";
            case IconConcept.Empower:    return "empower";
            default:                     return "";
```

- [ ] **Step 5: Run to verify GREEN**

```powershell
& $mcs -nologo -target:library "-out:$s\Icon.dll" "-r:$nunit" "Assets\Scripts\Enums\Enums\StatType.cs" "Assets\Scripts\Enums\Enums\EmpowerType.cs" "Assets\Scripts\Enums\EmpowerTypeExtensions.cs" "Assets\Scripts\UiLanguage\IconConcept.cs" "Assets\Scripts\UiLanguage\IconMarkup.cs" "Assets\Tests\EditMode\IconMarkupTests.cs"
& "$s\Runner.exe" "$s\Icon.dll"
```

Expected: `8 passed, 0 failed` (the existing `TmpName_NonEmptyForEveryConcept` foreach now also exercises `Empower`).

- [ ] **Step 6: Commit**

```powershell
git add "Assets/Scripts/UiLanguage/IconConcept.cs" "Assets/Scripts/UiLanguage/IconMarkup.cs" "Assets/Tests/EditMode/IconMarkupTests.cs"
git commit -m "feat: IconConcept.Empower glyph in IconMarkup (empower-icon)"
```

---

### Task 2: ConvertBanner label speaks the empower glyph

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ConvertBanner.cs:44`

**Interfaces:**
- Consumes: `IconMarkup.Tag(IconConcept.Empower)` (Task 1). `ConvertBanner` is in the default `Assembly-CSharp`, which auto-references `ArchonsRise.UiLanguage` — no asmdef change.
- Produces: no new symbols.

- [ ] **Step 1: Replace the locked-reason string** — in `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ConvertBanner.cs`, replace:

```csharp
                : "Empower to unlock";
```

with:

```csharp
                : $"{IconMarkup.Tag(IconConcept.Empower)} to unlock";
```

- [ ] **Step 2: Verify + commit.** No mcs coverage (MonoBehaviour) — correctness is the editor compiling clean at Task 3 Step 1. The IDE may show a transient "IconConcept does not exist" until Unity re-imports; it clears on editor focus.

```powershell
git add "Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/ConvertBanner.cs"
git commit -m "feat: ConvertBanner 'Empower to unlock' uses the empower glyph (empower-icon)"
```

---

### Task 3: USER (editor) — author the sprite, registry entry, description sweep

**Files (all in editor):**
- Create: `Assets/TextMesh Pro/Resources/Sprite Assets/empower` (TMP Sprite Asset)
- Modify: `Assets/Resources/IconRegistry.asset` (add 17th entry)
- Modify: authored card/skill `.asset` descriptions containing the word "Empower"

**Interfaces:**
- Consumes: `IconConcept.Empower` (Task 1); the `empower` tag name.
- Produces: the asset + entry the validation tests load, and conformant descriptions.

- [ ] **Step 1: Compile check.** Focus the Unity editor; confirm the console shows no compile errors from Tasks 1–2 and the "IconConcept does not exist" IDE warnings have cleared.
- [ ] **Step 2: Author the sprite.** Pick art distinct from the crystal (empower is the *act* — suggest an upward chevron / spark / "+" motif). Select its texture → Inspector: Texture Type **Sprite (2D and UI)**, Sprite Mode **Single** → Apply. Then **Assets → Create → TextMeshPro → Sprite Asset**, rename the created asset to exactly `empower`, and move it into `Assets/TextMesh Pro/Resources/Sprite Assets/`.
- [ ] **Step 3: Spot-check the tag.** Set any scene TMP text to `<sprite="empower" index=0>` — the glyph must render. If it shows literal text, the asset name or folder is wrong.
- [ ] **Step 4: Add the registry entry.** Select `Assets/Resources/IconRegistry.asset`. In the inspector, add one entry (now 17): concept **Empower**, sprite = the empower sprite (same texture backing the TMP asset).
- [ ] **Step 5: Sweep authored descriptions.** For each card/skill asset whose `cardDescription` contains the word "Empower" (the empowered-line header, e.g. `Empower <sprite="Sword" index=0>: 6`), edit the field to replace the leading `Empower ` with the glyph + a space:

```
<sprite="empower" index=0> <sprite="Sword" index=0>: 6
```

Find them via **Project search** for `Empower ` under `Assets/Scripts/ScriptableObjectData`, or open each card the earlier grep named. Leave any use of "Empower" that is prose in a full sentence (there are none in `cardDescription` today — they are all the header form).

- [ ] **Step 6: Run the validation tests.** Window → General → **Test Runner** → EditMode → run `IconMarkupTests` and `IconRegistryValidationTests`. `RegistryAssetIsComplete`, `EveryConceptTmpAssetResolves`, and `AuthoredDescriptionsUseKnownTags` must all pass. If `AuthoredDescriptionsUseKnownTags` names an asset, it has a typo'd tag — fix and re-run.
- [ ] **Step 7: Commit** (agent side after the user confirms): review `git status` first; if unrelated WIP is dirty under `Assets/`, stage these paths individually, otherwise:

```powershell
git add "Assets/TextMesh Pro/Resources/Sprite Assets" "Assets/Resources" "Assets/Scripts/ScriptableObjectData"
git commit -m "feat: empower sprite asset + registry entry + description sweep (empower-icon)"
```

---

### Task 4: Docs — authoring contract + decision log

**Files:**
- Modify: `.claude/skills/archons-rise-design/content-rules.md` (UI-language section)
- Modify: `.claude/skills/archons-rise-roadmap/decisions-log.md` (append entry)

**Interfaces:** none (documentation).

- [ ] **Step 1: content-rules.md** — in the "UI language — icons & costs" section, update the canonical-names bullet from 16 to 17 names and add `empower` to the list; add a sentence: the empowered-line header is `<sprite="empower" index=0> <stat>: N` (the `empower` glyph replaces the literal word "Empower").
- [ ] **Step 2: decisions-log.md** — append a dated (2026-07-16) entry: Empower got its own glyph (`IconConcept.Empower` / TMP name `empower`), replacing the word in card/skill descriptions and the ConvertBanner label; the CardInspector prose message was left as words (icon-as-verb reads awkwardly). Cite the spec path.
- [ ] **Step 3: Commit**

```powershell
git add ".claude/skills/archons-rise-design/content-rules.md" ".claude/skills/archons-rise-roadmap/decisions-log.md"
git commit -m "docs: empower-icon authoring contract + decision log (empower-icon)"
```

---

## Self-review notes

- **Spec coverage:** concept + TmpName → Task 1; ConvertBanner label → Task 2; sprite asset + registry entry + description sweep → Task 3; docs → Task 4; CardInspector explicitly out of scope (Global Constraints). Testing (IconMarkupTests mcs; IconRegistryValidationTests editor) → Tasks 1 and 3.
- **Type consistency:** `IconConcept.Empower` and TmpName `"empower"` used identically across Tasks 1–4; `IconMarkup.Tag(IconConcept.Empower)` produces `<sprite="empower" index=0>` as asserted in Task 1 and consumed in Task 2.
- **No new asmdef references:** `ConvertBanner` is in `Assembly-CSharp` (auto-references `ArchonsRise.UiLanguage`); the M2.11 test asmdef already references `ArchonsRise.UiLanguage` + `Unity.TextMeshPro`.
