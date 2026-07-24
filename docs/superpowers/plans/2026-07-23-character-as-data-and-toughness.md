# Character-as-Data & Toughness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the hardcoded single hero into an authored `CharacterSO` content bundle with one runtime source of truth and a self-describing run save, and rename the mis-named character-side "HP" stat to **Toughness** with a HUD readout.

**Architecture:** `PlayerSO` is renamed in place to `CharacterSO` (file + class together, preserving the `.meta` guid) and grows the knobs currently hardcoded in `Player.cs`. `DataManager` gains a `ContentRegistry<CharacterSO>` and an `ActiveCharacter` property that is the single source of truth, resolved before `LoadScene(1)` in both `NewGame()` and `LoadGame()`. Save schema goes to v7 with `characterId` and `toughness`. The character-side HP identifiers become Toughness everywhere; enemy HP is untouched.

**Tech Stack:** Unity 6000.5.1f1, C# (Mono/mcs), NUnit EditMode + the mcs CLI pure-test harness, TextMeshPro, uGUI.

## Global Constraints

- **Pure classes live in an asmdef folder, MonoBehaviours in Assembly-CSharp.** `CombatRules` is in `Assets/Scripts/CardPlay/` (`ArchonsRise.CardPlay`); `LevelRewardEntry`/`LevelRules` are in `Assets/Scripts/Leveling/` (`ArchonsRise.Leveling`); save models are in `Assets/Scripts/SaveData/` (`ArchonsRise.SaveData`). ScriptableObjects (`CharacterSO`, `SkillPoolSO`, `LevelRewardsSO`) stay in `Assets/Scripts/GameScriptableObjectTypes/` in **Assembly-CSharp**.
- **Pure test harness = Mono mcs, not csc.** Compile with `"C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\MonoBleedingEdge\bin\mcs.bat"` and run the reflection runner under `mono.exe` (same dir). nunit ref: `Library\PackageCache\com.unity.ext.nunit*\net472\unity-custom\nunit.framework.dll`, **copied next to the built DLL + runner in the scratchpad**. csc is C# 5 and rejects `=>`.
- **No C# 8 switch expressions in pure code** — use `if`/ternary/classic `switch`, matching `CombatRules`/`TurnPhaseRules`.
- **Pure files must not reference UnityEngine.** `LevelRewardEntry.cs` is compiled by the mcs harness; any Unity attribute on it must be behind `#if UNITY_2017_1_OR_NEWER` (see Task 2).
- **Never hand-edit scene/prefab YAML.** All scene, prefab, and `.asset` authoring is USER editor work performed from Task 10. `.asmdef` files are plain JSON and **may** be edited directly.
- **Enemies are out of scope.** `EnemiesSO.enemyHP`, `EnemyCard.EffectiveHP`, `IconConcept.Hp`, and the `"hp"` TMP tag must not change. No new icon is added.
- **Toughness minimum is 1.** A 0 divisor makes `CombatRules.WoundCount` loop forever.
- **Save schema target: v7.** Adds `RunState.characterId` and `PlayerState.toughness`; `PlayerState.hp` is retained as a vestigial field for migration only.
- **Commit after every task.** End commit bodies with `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

**Spec:** `docs/superpowers/specs/2026-07-23-multi-character-and-avatar-animation-design.md` (Parts A and T).

## File Structure

**Modify (pure):**
- `Assets/Scripts/CardPlay/CombatRules.cs` — parameter rename + zero-divisor clamp.
- `Assets/Scripts/Leveling/LevelRewardEntry.cs` — `hpBonus` → `toughnessBonus`.
- `Assets/Scripts/SaveData/SaveModels.cs` — `characterId`, `toughness`, vestigial `hp`.
- `Assets/Scripts/SaveData/SaveMigrator.cs` — v6 → v7.

**Create (ScriptableObject, Assembly-CSharp):**
- `Assets/Scripts/GameScriptableObjectTypes/SkillPoolSO.cs` — the per-character skill pool.

**Rename (ScriptableObject, Assembly-CSharp):**
- `Assets/Scripts/GameScriptableObjectTypes/PlayerSO.cs` → `CharacterSO.cs`.

**Modify (Assembly-CSharp):**
- `Assets/Scripts/GameScriptableObjectTypes/LevelRewardsSO.cs` — drop `skillPool`.
- `Assets/Scripts/Managers/DataManager.cs` — character registry, `ActiveCharacter`, v7 capture/restore.
- `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` — read from `ActiveCharacter`; Toughness rename.
- `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs` — starting deck from `ActiveCharacter`.
- `Assets/Scripts/GameObjectScripts/Leveling/LevelUpController.cs` — skill pool from the character.
- `Assets/Scripts/Managers/CombatController.cs` — `PlayerHP` → `PlayerToughness` call site.
- `Assets/Scripts/Editor/LateGameSaveTool.cs` — `PlayerHp` → `PlayerToughness`.

**Create (MonoBehaviour, Assembly-CSharp):**
- `Assets/Scripts/GameObjectScripts/PlayerScripts/ToughnessLabel.cs` — HUD readout.

**Create (tests, pure):**
- `Assets/Scripts/SaveData/Tests/SaveMigratorV7Tests.cs`

**Modify (tests, pure):**
- `Assets/Tests/EditMode/CombatRulesTests.cs` — zero-divisor cases.

**Docs:**
- `.claude/skills/archons-rise-design/{mechanics,balance,content-rules}.md`
- `.claude/skills/archons-rise-roadmap/decisions-log.md`

---

### Task 1: Zero-toughness guard in CombatRules

The wound loop increments by the divisor. At 0 it never terminates and hangs Unity. Today the value is a literal `2`; this plan makes it authored data, so the guard must exist **before** anything can author it.

**Files:**
- Modify: `Assets/Scripts/CardPlay/CombatRules.cs:21-35`
- Test: `Assets/Tests/EditMode/CombatRulesTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `CombatRules.WoundCount(AttackKind kind, int defend, int enemyAttack, int playerToughness)` and `CombatRules.GroupWoundCount(int defend, int totalEnemyAttack, int playerToughness)` — both `static int`. Signatures are positionally unchanged, so existing call sites keep compiling.

- [ ] **Step 1: Write the failing tests**

Append to `Assets/Tests/EditMode/CombatRulesTests.cs` (inside the existing class):

```csharp
    [Test]
    public void WoundCount_ZeroToughness_TerminatesAndActsAsOne()
    {
        // A 0 divisor previously made `i += toughness` loop forever.
        Assert.AreEqual(5, CombatRules.WoundCount(AttackKind.Normal, 0, 5, 0));
    }

    [Test]
    public void WoundCount_NegativeToughness_TerminatesAndActsAsOne()
    {
        Assert.AreEqual(3, CombatRules.WoundCount(AttackKind.Normal, 2, 5, -4));
    }

    [Test]
    public void GroupWoundCount_ZeroToughness_TerminatesAndActsAsOne()
    {
        Assert.AreEqual(4, CombatRules.GroupWoundCount(1, 5, 0));
    }

    [Test]
    public void WoundCount_ToughnessTwo_DividesShortfallIntoBites()
    {
        // Shortfall 5, toughness 2 -> ceil(5/2) = 3. Unchanged behaviour.
        Assert.AreEqual(3, CombatRules.WoundCount(AttackKind.Normal, 0, 5, 2));
    }
```

- [ ] **Step 2: Run the pure harness to verify it fails (RED)**

```bash
MCS="/c/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mcs.bat"
MONO="/c/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mono.exe"
NUNIT=$(ls Library/PackageCache/com.unity.ext.nunit*/net472/unity-custom/nunit.framework.dll | head -1)
SCRATCH="/c/Users/DAVE'S~1/AppData/Local/Temp/claude/c--Users-Dave-s-Comp-source-repos-Archon-s-Rise/78663f39-8fe0-4f8a-a88d-5186df8613bb/scratchpad"
mkdir -p "$SCRATCH" && cp "$NUNIT" "$SCRATCH/"
"$MCS" -nologo -target:library "-out:$SCRATCH/CombatRulesTests.dll" "-r:$NUNIT" \
  Assets/Tests/EditMode/CombatRulesTests.cs Assets/Scripts/CardPlay/CombatRules.cs
"$MONO" "$SCRATCH/Runner.exe" "$SCRATCH/CombatRulesTests.dll"
```

Expected: the three zero/negative tests **hang or fail**. If the runner hangs, that IS the red state — kill it (Ctrl-C) and proceed; the infinite loop is precisely the bug being fixed.

> If `Runner.exe` is not yet in the scratchpad, build the reflection runner first per the `unity-pure-test-harness-mcs` memory, then re-run.

- [ ] **Step 3: Write the implementation**

Replace lines 21-35 of `Assets/Scripts/CardPlay/CombatRules.cs`:

```csharp
    // The counterattack wound the player takes on a defeat. Siege is always
    // wound-free. Normal wounds when Defend falls short of the enemy's Attack,
    // one wound per Toughness-sized bite of the shortfall.
    //
    // Toughness is a DIVISOR, not a pool (spec 2026-07-23, Part T): it never
    // depletes and is not a loss axis. Higher toughness = fewer wounds.
    // Clamped to >= 1 here because a 0 divisor makes `i += toughness` loop
    // forever; the rule must be safe on its own, whatever the caller passes.
    public static int WoundCount(AttackKind kind, int defend, int enemyAttack, int playerToughness)
    {
        if (kind == AttackKind.Siege) return 0;
        if (defend >= enemyAttack) return 0;
        int bite = playerToughness < 1 ? 1 : playerToughness;
        int wounds = 0;
        for (int i = 0; i < enemyAttack - defend; i += bite) wounds++;
        return wounds;
    }

    // The group counterattack: every surviving enemy hits at once, so their
    // Attack sums into ONE comparison against Defend, then the existing
    // Toughness-bite rule applies. Because Siege/Influence remove enemies
    // before Engage, a thinner survivor set means a smaller total and fewer
    // wounds.
    public static int GroupWoundCount(int defend, int totalEnemyAttack, int playerToughness)
        => WoundCount(AttackKind.Normal, defend, totalEnemyAttack, playerToughness);
```

- [ ] **Step 4: Run the harness to verify it passes (GREEN)**

```bash
"$MCS" -nologo -target:library "-out:$SCRATCH/CombatRulesTests.dll" "-r:$NUNIT" \
  Assets/Tests/EditMode/CombatRulesTests.cs Assets/Scripts/CardPlay/CombatRules.cs
"$MONO" "$SCRATCH/Runner.exe" "$SCRATCH/CombatRulesTests.dll"
```

Expected: all tests PASS (the 10 pre-existing plus the 4 added), no hang.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CardPlay/CombatRules.cs Assets/Tests/EditMode/CombatRulesTests.cs
git commit -m "fix: clamp toughness divisor to >=1 so WoundCount can never hang

Renames the parameter to playerToughness (Part T). A 0 divisor made
'i += toughness' loop forever; unreachable today, but the next commits
make it authored data.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Rename `hpBonus` → `toughnessBonus`

**Files:**
- Modify: `Assets/Scripts/Leveling/LevelRewardEntry.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs:693`

**Interfaces:**
- Consumes: nothing.
- Produces: `LevelRewardEntry.toughnessBonus` (public int field), replacing `hpBonus`.

**Critical:** `hpBonus` is a **serialized field on the shipped `LevelRewards.asset`**. A plain rename drops every authored value to 0, silently deleting all toughness progression. `[FormerlySerializedAs]` prevents that — but it lives in `UnityEngine.Serialization`, and this file is compiled by the mcs harness with no UnityEngine reference. The attribute therefore goes behind a Unity-only define.

- [ ] **Step 1: Write the implementation**

Replace the entire contents of `Assets/Scripts/Leveling/LevelRewardEntry.cs`:

```csharp
#if UNITY_2017_1_OR_NEWER
using UnityEngine.Serialization;
#endif

// One row of the level reward table. Plain serializable data — no UnityEngine —
// so LevelRules stays testable from the CLI mcs harness. All fields are counts,
// never booleans: every reward knob is tunable per level in the inspector.
//
// toughnessBonus was named hpBonus before 2026-07-23 (spec Part T). The
// FormerlySerializedAs is Unity-only so the mcs harness, which compiles this
// file with no UnityEngine reference, still builds it.
[System.Serializable]
public class LevelRewardEntry
{
    public int level;
#if UNITY_2017_1_OR_NEWER
    [FormerlySerializedAs("hpBonus")]
#endif
    public int toughnessBonus;
    public int handSizeBonus;
    public int armySizeBonus;
    public int skillPicks;
    public int cardPicks;
}
```

- [ ] **Step 2: Update the one consumer**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`, in `PlayerLevelUp()`, change:

```csharp
        if (entry != null) playerHP += entry.hpBonus;
```

to:

```csharp
        if (entry != null) playerHP += entry.toughnessBonus;
```

(`playerHP` itself is renamed in Task 7 — leaving it here keeps this task compiling on its own.)

- [ ] **Step 3: Verify the pure layer still compiles under mcs**

```bash
"$MCS" -nologo -target:library "-out:$SCRATCH/LevelRulesTests.dll" "-r:$NUNIT" \
  Assets/Tests/EditMode/LevelRulesTests.cs \
  Assets/Scripts/Leveling/LevelRules.cs Assets/Scripts/Leveling/LevelRewardEntry.cs
"$MONO" "$SCRATCH/Runner.exe" "$SCRATCH/LevelRulesTests.dll"
```

Expected: compiles clean (proving the `#if` kept UnityEngine out) and all existing `LevelRulesTests` PASS.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Leveling/LevelRewardEntry.cs Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs
git commit -m "refactor: LevelRewardEntry.hpBonus -> toughnessBonus

FormerlySerializedAs preserves authored LevelRewards.asset values. The
attribute is behind UNITY_2017_1_OR_NEWER so the mcs pure harness, which
compiles this file without UnityEngine, still builds it.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Create `SkillPoolSO`

Purely additive — `LevelRewardsSO` keeps its `skillPool` until Task 7, so nothing breaks.

**Files:**
- Create: `Assets/Scripts/GameScriptableObjectTypes/SkillPoolSO.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `SkillPoolSO.Skills` → `IReadOnlyList<SkillsSO>`.

- [ ] **Step 1: Write the implementation**

Create `Assets/Scripts/GameScriptableObjectTypes/SkillPoolSO.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

// The skills a character can be offered on a skill-pick level (spec 2026-07-23,
// A2). Split out of LevelRewardsSO so characters that share a progression curve
// can reuse one level table while drawing from different pools — retuning the
// exp curve stays a single-asset edit.
[CreateAssetMenu(fileName = "SkillPool", menuName = "ScriptableObjects/SkillPool")]
public class SkillPoolSO : ScriptableObject
{
    [SerializeField] List<SkillsSO> skills = new();

    public IReadOnlyList<SkillsSO> Skills => skills;
}
```

- [ ] **Step 2: Verify Unity compiles**

Let Unity recompile. Check the Editor Console shows no errors and that
`Assets > Create > ScriptableObjects > SkillPool` now exists.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/GameScriptableObjectTypes/SkillPoolSO.cs
git commit -m "feat: SkillPoolSO — per-character skill pool asset

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Rename `PlayerSO` → `CharacterSO` and add the character knobs

**Files:**
- Rename: `Assets/Scripts/GameScriptableObjectTypes/PlayerSO.cs` → `CharacterSO.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs:7`
- Modify: `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs:10,23`

**Interfaces:**
- Consumes: `SkillPoolSO.Skills` (Task 3).
- Produces: `CharacterSO` with `Id` (string), `CharacterName` (string), `StartingToughness` (int), `HandSize` (int), `StartingDeck` (`List<CardsSO>`), `ImprovAttack`/`ImprovDefend`/`ImprovExplore`/`ImprovInfluence` (int), `LevelTable` (`LevelRewardsSO`), `SkillPool` (`SkillPoolSO`), `AnimatorController` (`RuntimeAnimatorController`).

**Critical:** rename the **file and the class together** with `git mv`, keeping `PlayerSO.cs.meta` renamed alongside it. Unity binds assets to scripts by the `.meta` guid, so the existing `Player.asset` stays wired.

- [ ] **Step 1: Rename the files**

```bash
git mv Assets/Scripts/GameScriptableObjectTypes/PlayerSO.cs \
       Assets/Scripts/GameScriptableObjectTypes/CharacterSO.cs
git mv Assets/Scripts/GameScriptableObjectTypes/PlayerSO.cs.meta \
       Assets/Scripts/GameScriptableObjectTypes/CharacterSO.cs.meta
```

- [ ] **Step 2: Write the implementation**

Replace the entire contents of `Assets/Scripts/GameScriptableObjectTypes/CharacterSO.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

// A playable character: the content bundle that makes one hero different from
// another (spec 2026-07-23, Part A). Characters differ ONLY in starting cards,
// skill pool, level table, and these scalars — never in rules. Rule-bending
// belongs in SkillEffect (the Charismatic/RecruitEnemies precedent), and every
// character starts with zero skills.
//
// Renamed from PlayerSO 2026-07-23; the file+class were renamed together so the
// .meta guid — and therefore the authored asset binding — survived.
[CreateAssetMenu(fileName = "Character", menuName = "ScriptableObjects/CharacterSO")]
public class CharacterSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] string id;
    [SerializeField] string characterName;

    [Header("Starting stats")]
    // Toughness is a DIVISOR of the Defend shortfall, not a health pool: higher
    // = fewer wounds per bad fight. Seeds Player at run start only; it then
    // grows via level-up toughnessBonus and is restored from the save on load.
    [SerializeField] int startingToughness = 2;
    [SerializeField] int handSize = 5;
    [SerializeField] int improvAttack = 1;
    [SerializeField] int improvDefend = 1;
    [SerializeField] int improvExplore = 1;
    [SerializeField] int improvInfluence = 1;

    [Header("Content")]
    [SerializeField] List<CardsSO> startingDeck = new();
    [SerializeField] LevelRewardsSO levelTable;
    [SerializeField] SkillPoolSO skillPool;

    [Header("Presentation")]
    [SerializeField] RuntimeAnimatorController animatorController;

    public string Id => id;
    public string CharacterName => characterName;
    public int StartingToughness => startingToughness;
    public int HandSize => handSize;
    public int ImprovAttack => improvAttack;
    public int ImprovDefend => improvDefend;
    public int ImprovExplore => improvExplore;
    public int ImprovInfluence => improvInfluence;
    public List<CardsSO> StartingDeck => startingDeck;
    public LevelRewardsSO LevelTable => levelTable;
    public SkillPoolSO SkillPool => skillPool;
    public RuntimeAnimatorController AnimatorController => animatorController;

    void OnValidate()
    {
        // A 0 toughness would be a divide-by-zero-shaped hang in
        // CombatRules.WoundCount. The rule clamps too, but refuse to author it.
        if (startingToughness < 1) startingToughness = 1;

        if (string.IsNullOrEmpty(id))
            Debug.LogWarning($"{name}: CharacterSO needs a stable id (used by the save file).", this);
        if (startingDeck == null || startingDeck.Count == 0)
            Debug.LogWarning($"{name}: CharacterSO has an empty startingDeck.", this);
        if (levelTable == null)
            Debug.LogWarning($"{name}: CharacterSO has no levelTable.", this);
        if (skillPool == null)
            Debug.LogWarning($"{name}: CharacterSO has no skillPool.", this);
    }
}
```

- [ ] **Step 3: Update the two consumers so the project compiles**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` line 7:

```csharp
    [SerializeField] CharacterSO player;
```

In `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs` line 10:

```csharp
    [SerializeField] CharacterSO player;
```

and line 23, inside `Awake()`:

```csharp
        foreach(var card in player.StartingDeck)
```

(Both fields are removed entirely in Tasks 5/7 — this step only keeps the project compiling.)

- [ ] **Step 4: Verify Unity compiles**

Let Unity recompile. Expected: no console errors, and the existing `Player.asset` still shows its fields populated in the inspector (proving the guid survived the rename).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/GameScriptableObjectTypes/CharacterSO.cs \
        Assets/Scripts/GameScriptableObjectTypes/CharacterSO.cs.meta \
        Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs \
        Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs
git commit -m "refactor: PlayerSO -> CharacterSO with the per-character knobs

File+class renamed together so the .meta guid and the authored asset
binding survive. Adds startingToughness, improv values, levelTable,
skillPool, and the animator hook.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: `DataManager` character registry + `ActiveCharacter`

**Files:**
- Modify: `Assets/Scripts/Managers/DataManager.cs:33-41,59-74,126-157`

**Interfaces:**
- Consumes: `CharacterSO.Id` (Task 4).
- Produces: `DataManager.Instance.ActiveCharacter` → `CharacterSO`; `DataManager.Instance.Characters` → `ContentRegistry<CharacterSO>`.

- [ ] **Step 1: Add the registry fields**

In `Assets/Scripts/Managers/DataManager.cs`, after `public EnemiesSO[] allEnemies;` (line 36):

```csharp
    public CharacterSO[] allCharacters;
    // Fallback when a run has no recorded character (pre-v7 save) and the seed
    // for NewGame until the select screen (spec Part B) replaces it.
    public CharacterSO defaultCharacter;
```

After `public ContentRegistry<EnemiesSO> Enemies { get; private set; }` (line 41):

```csharp
    public ContentRegistry<CharacterSO> Characters { get; private set; }

    // THE single source of truth for which character this run is (spec A3).
    // Resolved before LoadScene(1) in both NewGame and LoadGame, so every
    // scene-1 Awake sees it — no initialization race.
    //
    // Falls back to defaultCharacter when unset so that opening GameBoard.unity
    // DIRECTLY in the editor (bypassing the MainMenu scene, as you do constantly
    // while working) still runs instead of null-referencing.
    CharacterSO activeCharacter;
    public CharacterSO ActiveCharacter
    {
        get => activeCharacter != null ? activeCharacter : defaultCharacter;
        private set => activeCharacter = value;
    }
```

- [ ] **Step 2: Register characters in `BuildRegistries`**

In `BuildRegistries()`, after the `Enemies = ...` line:

```csharp
            Characters = new ContentRegistry<CharacterSO>(allCharacters, c => c.Id);
```

- [ ] **Step 3: Set `ActiveCharacter` in `NewGame`**

In `NewGame()`, after `CurrentSeed = new System.Random()...`:

```csharp
        // Part B (the select screen) replaces exactly this line.
        ActiveCharacter = defaultCharacter;
```

- [ ] **Step 4: Resolve `ActiveCharacter` in `LoadGame`**

In `LoadGame()`, after `DefeatedEnemies = new HashSet<Cell>(...)`:

```csharp
        // An unresolvable id must not block a load: fall back to the default so
        // the run still opens, and say so loudly.
        ActiveCharacter = defaultCharacter;
        if (!string.IsNullOrEmpty(current.run.characterId)
            && Characters != null
            && Characters.TryGet(current.run.characterId, out var loaded))
        {
            ActiveCharacter = loaded;
        }
        else if (!string.IsNullOrEmpty(current.run.characterId))
        {
            Debug.LogError($"Save names unknown character '{current.run.characterId}'; using default.");
        }
```

> `current.run.characterId` does not exist yet — Task 6 adds it. Expect a compile error until then; that is why Tasks 5 and 6 land back-to-back.

- [ ] **Step 5: Commit (after Task 6 compiles)**

This task's commit is folded into Task 6's, because `characterId` must exist for the project to build.

---

### Task 6: Save schema v7 (TDD)

**Files:**
- Modify: `Assets/Scripts/SaveData/SaveModels.cs:6-12,47-61`
- Modify: `Assets/Scripts/SaveData/SaveMigrator.cs:31-38`
- Modify: `Assets/Scripts/Managers/DataManager.cs` (capture + restore)
- Test: `Assets/Scripts/SaveData/Tests/SaveMigratorV7Tests.cs`

**Interfaces:**
- Consumes: `DataManager.ActiveCharacter` (Task 5).
- Produces: `RunState.characterId` (string), `PlayerState.toughness` (int), `PlayerState.hp` (int, vestigial).

- [ ] **Step 1: Write the failing test**

Create `Assets/Scripts/SaveData/Tests/SaveMigratorV7Tests.cs`:

```csharp
using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV7Tests
{
    [Test]
    public void V6File_GetsEmptyCharacterId()
    {
        var f = new SaveFile { schemaVersion = 6 };
        var m = SaveMigrator.Migrate(f);
        Assert.AreEqual(7, m.schemaVersion);
        // Empty means "pre-v7": DataManager resolves it to defaultCharacter.
        Assert.IsTrue(string.IsNullOrEmpty(m.run.characterId));
    }

    [Test]
    public void V6File_CopiesLegacyHpIntoToughness()
    {
        var f = new SaveFile { schemaVersion = 6 };
        f.run.player.hp = 4;          // v6 JSON key
        f.run.player.toughness = 0;   // absent in v6
        var m = SaveMigrator.Migrate(f);
        Assert.AreEqual(4, m.run.player.toughness);
    }

    [Test]
    public void V7File_KeepsItsToughnessAndCharacter()
    {
        var f = new SaveFile { schemaVersion = 7 };
        f.run.characterId = "warlord";
        f.run.player.toughness = 3;
        f.run.player.hp = 99;   // stale vestigial value must not win
        var m = SaveMigrator.Migrate(f);
        Assert.AreEqual(7, m.schemaVersion);
        Assert.AreEqual("warlord", m.run.characterId);
        Assert.AreEqual(3, m.run.player.toughness);
    }
}
```

- [ ] **Step 2: Run the pure harness to verify it fails (RED)**

```bash
"$MCS" -nologo -target:library "-out:$SCRATCH/SaveV7Tests.dll" "-r:$NUNIT" \
  Assets/Scripts/SaveData/Tests/SaveMigratorV7Tests.cs \
  Assets/Scripts/SaveData/SaveModels.cs Assets/Scripts/SaveData/SaveMigrator.cs
```

Expected: FAIL — `error CS1061: 'RunState' does not contain a definition for 'characterId'` and the same for `toughness`.

- [ ] **Step 3: Add the model fields**

In `Assets/Scripts/SaveData/SaveModels.cs`, change the `SaveFile` header comment and version:

```csharp
    [Serializable]
    public class SaveFile
    {
        // v7: adds RunState.characterId (which character this run is) and
        // PlayerState.toughness (renamed from hp — a divisor, not a pool).
        public int schemaVersion = 7;
        public RunState run = new RunState();
    }
```

In `RunState`, add after `public PlayerState player = new PlayerState();`:

```csharp
        // Which CharacterSO this run belongs to (v7). Empty = pre-v7 save;
        // DataManager resolves that to defaultCharacter.
        public string characterId = "";
```

In `PlayerState`, replace `public int hp;` with:

```csharp
        // Toughness: the Defend-shortfall divisor (never a pool). Renamed from
        // `hp` in v7.
        public int toughness;
        // VESTIGIAL — v6 files carry this JSON key and JsonUtility only parses
        // fields that exist on the model. SaveMigrator copies it into
        // `toughness`; nothing else reads or writes it. Do not delete.
        public int hp;
```

- [ ] **Step 4: Add the migration**

In `Assets/Scripts/SaveData/SaveMigrator.cs`, replace the closing block:

```csharp
            // v6 -> v7: characterId did not exist; empty means "pre-v7", which
            // DataManager resolves to the default character.
            if (file.run.characterId == null)
                file.run.characterId = "";

            // v6 -> v7: `hp` became `toughness`. Copy the legacy value across —
            // without this a v6 save loads at toughness 0, which is both wrong
            // and the CombatRules hang case.
            if (file.run.player.toughness == 0 && file.run.player.hp > 0)
                file.run.player.toughness = file.run.player.hp;

            if (file.schemaVersion < 7)
                file.schemaVersion = 7;
            return file;
```

- [ ] **Step 5: Run the harness to verify it passes (GREEN)**

```bash
"$MCS" -nologo -target:library "-out:$SCRATCH/SaveV7Tests.dll" "-r:$NUNIT" \
  Assets/Scripts/SaveData/Tests/SaveMigratorV7Tests.cs \
  Assets/Scripts/SaveData/SaveModels.cs Assets/Scripts/SaveData/SaveMigrator.cs
"$MONO" "$SCRATCH/Runner.exe" "$SCRATCH/SaveV7Tests.dll"
```

Expected: 3/3 PASS.

- [ ] **Step 6: Wire capture and restore**

In `Assets/Scripts/Managers/DataManager.cs`, in `RestoreNow()`, replace:

```csharp
        player.PlayerHP        = run.player.hp;
```

with:

```csharp
        player.PlayerToughness = run.player.toughness;
```

In `CaptureRunState()`, change `var file = new SaveFile { schemaVersion = 6 };` to `7`, replace:

```csharp
        run.player.hp            = player.PlayerHP;
```

with:

```csharp
        run.player.toughness     = player.PlayerToughness;
```

and add, next to `run.map.seed = CurrentSeed;`:

```csharp
        run.characterId = ActiveCharacter != null ? ActiveCharacter.Id : "";
```

> `Player.PlayerToughness` does not exist until Task 7. Tasks 6 and 7 therefore land as one compiling unit — do Task 7 before building in Unity.

- [ ] **Step 7: Commit (after Task 7 compiles)**

Folded into Task 7's commit.

---

### Task 7: `Player` reads the active character; Toughness rename completes

This is the task that deletes the hardcoded literals. It also completes the compile unit begun in Tasks 5 and 6.

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs:7,15-19,27-32,693`
- Modify: `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs:10,23`
- Modify: `Assets/Scripts/GameObjectScripts/Leveling/LevelUpController.cs:37`
- Modify: `Assets/Scripts/GameScriptableObjectTypes/LevelRewardsSO.cs`
- Modify: `Assets/Scripts/Managers/CombatController.cs:125`
- Modify: `Assets/Scripts/Editor/LateGameSaveTool.cs:55`

**Interfaces:**
- Consumes: `DataManager.ActiveCharacter` (Task 5), `CharacterSO` accessors (Task 4), `SkillPoolSO.Skills` (Task 3).
- Produces: `Player.PlayerToughness` (int, get/set), `Player.SkillChoices` (`IReadOnlyList<SkillsSO>`), `Player.LevelRewards` (`LevelRewardsSO`, now derived from the character).

- [ ] **Step 1: Replace `Player`'s fields and derived accessors**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs`, delete line 7 (`[SerializeField] PlayerSO player;` — now `CharacterSO player;`) and line 27 (`[SerializeField] LevelRewardsSO levelRewards;`). Replace lines 15-19:

```csharp
    private int playerToughness;
```

(the four `improv*Value` fields and `playerHP` all go). Add, near the other properties:

```csharp
    // THE character this run is. Resolved by DataManager before the scene
    // loads, so it is safe to read from Awake onward (spec A3).
    CharacterSO Character => DataManager.Instance.ActiveCharacter;

    // The SkillsSO list a level-up pick draws from. Named *Choices* to keep it
    // distinct from CharacterSO.SkillPool, which is the SkillPoolSO asset — the
    // two would otherwise read as the same thing with different types.
    public IReadOnlyList<SkillsSO> SkillChoices => Character.SkillPool.Skills;

    // Toughness divides the Defend shortfall into bites, one Wound each. It
    // never depletes and is not a loss axis (spec Part T).
    public int PlayerToughness { get => playerToughness; set => playerToughness = value; }
```

Replace the `PlayerHandSize` property (line 32):

```csharp
    public int PlayerHandSize =>
        LevelRules.DerivedHandSize(Character.HandSize, playerLevel, Character.LevelTable.Entries);
```

Replace `ArmyCap` and `LevelRewards`:

```csharp
    public int ArmyCap => LevelRules.DerivedArmyCap(playerLevel, Character.LevelTable.Entries);
    public LevelRewardsSO LevelRewards => Character.LevelTable;
```

- [ ] **Step 2: Seed toughness from the character**

Add an `Awake` to `Player` (it currently has only `Start`):

```csharp
    void Awake()
    {
        // Seed from the character on a fresh run only. A load overwrites this
        // from the save in DataManager.RestoreNow — never re-seed, or a leveled
        // character silently loses its earned toughness.
        if (DataManager.Instance != null && DataManager.Instance.IsLoading) return;
        playerToughness = Character.StartingToughness;
    }
```

- [ ] **Step 3: Point the improvise methods at the character**

In `ImprovAttack`, `ImprovDefend`, `ImprovInfluence`, and `ImprovExplore`, replace each `improv*Value` with the character accessor. For example, `ImprovAttack` becomes:

```csharp
    public void ImprovAttack(Card card)
    {
        if(!card.IsPlayed)
        {
            playerAttack += Character.ImprovAttack;
            card.IsPlayed = true;
        }
        else if(card.IsPlayed)
        {
            playerAttack -= Character.ImprovAttack;
            card.IsPlayed = false;
        }
    }
```

Apply the same substitution in the other three: `improvDefendValue` → `Character.ImprovDefend`, `improvInfluenceValue` → `Character.ImprovInfluence`, `improvExploreValue` → `Character.ImprovExplore`.

- [ ] **Step 4: Finish the toughness rename in `Player`**

In `PlayerLevelUp()`:

```csharp
        if (entry != null) playerToughness += entry.toughnessBonus;
```

(The HUD event raise is added to this same method in Task 8, once the event field exists.)

- [ ] **Step 5: Update `PlayerDeck`**

In `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs`, delete line 10 (`[SerializeField] CharacterSO player;`) and change `Awake()`:

```csharp
        foreach(var card in DataManager.Instance.ActiveCharacter.StartingDeck)
```

- [ ] **Step 6: Update `LevelUpController`**

Line 37 becomes:

```csharp
        var choices = LevelRules.DrawSkillChoices(player.SkillChoices,
            new List<SkillsSO>(player.Skills), rng, 3);
```

- [ ] **Step 7: Strip `skillPool` from `LevelRewardsSO`**

Replace the contents of `Assets/Scripts/GameScriptableObjectTypes/LevelRewardsSO.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

// THE level reward table: one asset drives all level-up payouts, so every
// balance change during playtesting is an inspector edit on this asset.
// The skill pool moved to SkillPoolSO on 2026-07-23 (spec A2) so characters
// can share one curve while drawing different skills.
[CreateAssetMenu(fileName = "LevelRewards", menuName = "ScriptableObjects/LevelRewards")]
public class LevelRewardsSO : ScriptableObject
{
    [SerializeField] List<LevelRewardEntry> entries = new();

    public IReadOnlyList<LevelRewardEntry> Entries => entries;
}
```

- [ ] **Step 8: Update the two remaining call sites**

`Assets/Scripts/Managers/CombatController.cs` line 125:

```csharp
        int wounds = CombatRules.GroupWoundCount(player.PlayerDefend, total, player.PlayerToughness);
```

`Assets/Scripts/Editor/LateGameSaveTool.cs` line 55 — rename the tool's field `PlayerHp` to `PlayerToughness` (including its declaration and any inspector label above), and:

```csharp
        player.PlayerToughness = PlayerToughness;
```

- [ ] **Step 9: Verify Unity compiles**

Let Unity recompile. Expected: **zero** console errors. Then confirm no stale identifiers remain:

```bash
grep -rn "PlayerHP\|playerHP\|hpBonus\|improvAttackValue\|LevelRewards.SkillPool" Assets/Scripts --include=*.cs
```

Expected: **no output**. (`enemyHP` and `EffectiveHP` are enemy-side and are excluded by the pattern — they must still exist.)

- [ ] **Step 10: Commit**

```bash
git add Assets/Scripts/Managers/DataManager.cs Assets/Scripts/SaveData/ \
        Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs \
        Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs \
        Assets/Scripts/GameObjectScripts/Leveling/LevelUpController.cs \
        Assets/Scripts/GameScriptableObjectTypes/LevelRewardsSO.cs \
        Assets/Scripts/Managers/CombatController.cs \
        Assets/Scripts/Editor/LateGameSaveTool.cs
git commit -m "feat: character-as-data + save v7 + Toughness rename

Player and PlayerDeck now read DataManager.ActiveCharacter instead of
holding independent PlayerSO refs. Deletes the hardcoded playerHP=2 and
the four improv*Value=1 literals. Save v7 records characterId and
toughness; v6 files migrate hp -> toughness.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 8: Toughness HUD readout

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/ToughnessLabel.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs` (event field + `RaiseToughness`)

**Interfaces:**
- Consumes: `Player.PlayerToughness` (Task 7).
- Produces: `ToughnessLabel.OnToughnessChanged(int)` — the `IntListener` target; `Player.RaiseToughness()`.

- [ ] **Step 1: Write the implementation**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/ToughnessLabel.cs`:

```csharp
using TMPro;
using UnityEngine;

// HUD toughness readout, modelled on DoomMeter: driven by an IntEvent via an
// IntListener, never per-frame polling.
//
// Renders the WORD plus the number, deliberately not the `hp` glyph — that icon
// means enemy HP, a depleting pool, and borrowing it would re-assert exactly the
// equivalence the Toughness rename exists to break (spec Part T).
public class ToughnessLabel : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;

    public void OnToughnessChanged(int toughness)
    {
        label.text = $"Toughness {toughness}";
    }
}
```

- [ ] **Step 2: Add the event to `Player`**

In the `[Header("Events")]` block of `Player.cs`:

```csharp
    [SerializeField] IntEvent onToughnessChanged;
```

Add the raiser next to `GetCurrentInfluence`:

```csharp
    // Raised at the three points toughness can change: run start, a level-up
    // that granted toughnessBonus, and a save-load restore.
    public void RaiseToughness()
    {
        if (onToughnessChanged != null) onToughnessChanged.Raise(playerToughness);
    }
```

Call it from `Start` (which runs after both the `Awake` seed and, for a load, after
`RestoreAfterSceneInit`'s frame wait has not yet run — so `RestoreNow` raises it again itself in
Step 3):

```csharp
    void Start()
    {
        OnExploreEvent_GetCurrentExplore.Raise(playerExplore);
        RaiseToughness();   // fresh-run seed
    }
```

And from `PlayerLevelUp()`, immediately after the toughness line added in Task 7:

```csharp
        if (entry != null) playerToughness += entry.toughnessBonus;
        if (entry != null && entry.toughnessBonus > 0) RaiseToughness();
```

- [ ] **Step 3: Raise it after a load restore**

In `DataManager.RestoreNow()`, after `player.PlayerToughness = run.player.toughness;`:

```csharp
        player.RaiseToughness();
```

- [ ] **Step 4: Verify Unity compiles**

Let Unity recompile. Expected: no console errors. (The label will not display until the USER wires it in Task 10.)

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/GameObjectScripts/PlayerScripts/ToughnessLabel.cs \
        Assets/Scripts/GameObjectScripts/PlayerScripts/Player.cs \
        Assets/Scripts/Managers/DataManager.cs
git commit -m "feat: Toughness HUD label driven by an IntEvent

Word + number, not the hp glyph — that icon means enemy HP (a pool).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 9: Design-bible and decisions-log updates

Per the `archons-rise-design` maintenance rule, doc updates land in the same change as the code.

**Files:**
- Modify: `.claude/skills/archons-rise-design/mechanics.md`
- Modify: `.claude/skills/archons-rise-design/balance.md`
- Modify: `.claude/skills/archons-rise-design/content-rules.md`
- Modify: `.claude/skills/archons-rise-roadmap/decisions-log.md`

- [ ] **Step 1: `mechanics.md`**

In "Lose — Wounds (tactical)", replace the `**HP is toughness, not a health pool**` paragraph:

```markdown
**Toughness is a divisor, not a health pool** (decision 2026-07-06, renamed 2026-07-23): Toughness
divides the Defend shortfall into Toughness-sized bites, one Wound per bite (`CombatRules.WoundCount`).
It never depletes and is not a loss axis — raising it via level-ups means each bad fight inflicts
fewer Wounds. The character has no HP; the only loss axes are Wound count and the Doom Clock.
```

In "Leveling", change `**+1 HP** at milestone levels (toughness — fewer Wounds per bad fight)` to:

```markdown
- **+1 Toughness** at milestone levels (fewer Wounds per bad fight).
```

- [ ] **Step 2: `balance.md`**

In the Leveling Curve table, change the three `+1 HP` cells to `+1 Toughness`, and change the
baselines line to:

```markdown
- Baselines: hand size **5**, army cap **1**, Toughness **2**.
```

- [ ] **Step 3: `content-rules.md`**

Replace the `## Player — PlayerSO` section with the `CharacterSO` table from spec section A1, add a
`SkillPoolSO` section, drop `skillPool` from the `LevelRewardsSO` description, and — in the UI
language section — change:

```markdown
- **`shield` means Defend only.** Enemy HP is `hp` everywhere — never the Defend shield.
```

(was "Enemy toughness is `hp`" — "toughness" now names the character stat only).

- [ ] **Step 4: `decisions-log.md`**

Append:

```markdown
## 2026-07-23 — Character-side HP renamed to Toughness; characters become data

**Decision:** The character stat formerly called HP is renamed **Toughness** everywhere on the
character side. Enemy HP keeps its name, fields, and `hp` glyph — enemy HP genuinely is a depleting
pool. No new icon; the HUD label reads the word plus the number.

**Why:** The stat is a *divisor* of the Defend shortfall (`ceil(shortfall / toughness)`), not a pool.
It never depletes, and the loss axes are Wound count and the Doom Clock — characters have no HP at
all. Calling it HP misdescribed the mechanic and invited confusion with the enemy stat.

**Also:** `PlayerSO` becomes `CharacterSO`, a content bundle (starting deck, skill pool, level table,
toughness, hand size, improvise values, animator). `DataManager.ActiveCharacter` is the single source
of truth, replacing two independent serialized refs that could disagree. Save v7 records
`characterId` so a run is self-describing — the select screen (Part B) then needs no save work.

**Guard:** a 0 toughness makes `CombatRules.WoundCount` loop forever. Clamped in the rule *and*
in `CharacterSO.OnValidate`.
```

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/archons-rise-design/ .claude/skills/archons-rise-roadmap/decisions-log.md
git commit -m "docs: Toughness rename + CharacterSO in the design bible

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 10: USER editor work + acceptance

**This task is performed by the USER in the Unity editor**, per the project's standing practice — never by hand-editing YAML.

- [ ] **Step 1: Author `CommonSkills.asset`**

`Assets > Create > ScriptableObjects > SkillPool`, name it `CommonSkills`. Open the current
`LevelRewards.asset`, note its 10 skills, and add the same 10 to `CommonSkills`.

- [ ] **Step 2: Verify the level table survived the rename**

Open `LevelRewards.asset`. The column now reads **Toughness Bonus**. Confirm levels 3, 6, and 9 still
show `1` — if they read `0`, the `FormerlySerializedAs` in Task 2 was missed; fix it before
continuing rather than re-typing the values.

- [ ] **Step 3: Author the character asset**

`Assets > Create > ScriptableObjects > CharacterSO`, name it `Warlord` (or your hero's name). Set:
`id` = a stable slug (e.g. `warlord`), `characterName`, `startingToughness` = 2, `handSize` = 5,
all four improv values = 1, `startingDeck` = the 7 cards from the old `Player.asset`,
`levelTable` = `LevelRewards.asset`, `skillPool` = `CommonSkills.asset`. Leave `animatorController`
empty (the D plan fills it).

- [ ] **Step 4: Populate `DataManager`**

On the `DataManager` object, add the new character to `allCharacters` and set `defaultCharacter` to
it.

- [ ] **Step 5: Create the toughness event + HUD label**

Create an `IntEvent` asset named `OnToughnessChanged`. Assign it to `Player`'s `onToughnessChanged`
field. Add a TMP text to the HUD, put `ToughnessLabel` on it, assign its `label`, and add an
`IntListener` pointing at `OnToughnessChanged` whose response calls `ToughnessLabel.OnToughnessChanged`.

**Pick the DYNAMIC method in the UnityEvent dropdown, not the Static one.** A Static binding always
fires with a hardcoded 0 and the label will read `Toughness 0` forever.

- [ ] **Step 6: Acceptance checklist**

- [ ] New Game starts with the character's deck; the HUD reads `Toughness 2`.
- [ ] Level to a toughness level; the label increments.
- [ ] Save, quit, reload: same character, same toughness, same derived hand size.
- [ ] Load the pre-existing v6 `Save.json`: it opens on the default character with its old HP value
      showing as Toughness — **not** `Toughness 0`.
- [ ] A fight whose Defend falls short by 5 at Toughness 2 inflicts **3** wounds.
- [ ] Level-up skill picks are drawn from `CommonSkills`.
- [ ] Duplicate a `CharacterSO` with a different deck and toughness, set it as `defaultCharacter`,
      start a new run: the run visibly differs with **no code change**.
- [ ] Enemy previews still show enemy HP unchanged; `IconRegistryValidationTests` green.
- [ ] Full EditMode suite green in Test Runner.

- [ ] **Step 7: Commit any asset changes**

```bash
git add Assets/
git commit -m "content: Warlord CharacterSO, CommonSkills pool, toughness HUD wiring

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
