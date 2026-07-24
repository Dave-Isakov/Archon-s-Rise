# Multi-Character Foundation, Toughness Rename & Avatar Animation — Design

**Date:** 2026-07-23
**Status:** Approved (pending final spec review), ready for implementation plan
**Scope note:** "Multi-character" is four subsystems, not one. This spec covers **A** (character as
data), **T** (the Toughness rename + readout), and **D** (avatar animation architecture). **B**
(character-select / run-setup screen) and **C** (unlock profile persistence) are deferred — see
[Deferred](#deferred-b-and-c). A is the keystone: B, C, and per-character content authoring are all
cheap once the character is data instead of literals. T rides along because A is what turns the
mis-named stat into an authored field.

## Problem

The game hardcodes a single, anonymous hero.

- `PlayerSO` carries only `playerName`, `playerHandSize`, `startingHand`, with two consumers
  (`Player`, `PlayerDeck`) holding **independent serialized references** that can silently disagree.
- `Player.cs` hardcodes `playerHP = 2` and four `improv*Value = 1` literals — exactly the knobs a
  character archetype needs to vary.
- `LevelRewardsSO` is a **scene reference** on `Player`, and it bundles the skill pool together with
  the level table, so per-character progression and per-character skill pools have no home.
- The save schema (v6) records no character identity, so a run is not self-describing.
- `PlayerPosition.controller` has **one state, one clip, zero parameters**. `PlayerWalk.anim` exists
  on disk but is wired into nothing. There is no state machine to extend.
- The stat named **HP** on the character side is not a health pool at all — it is a **divisor**. Every
  identifier (`playerHP`, `hpBonus`, `run.player.hp`) misdescribes the mechanic, and the value is
  **not shown anywhere in the UI**, so the player cannot see how tough they are.

## Goals

- A character is a **content bundle**: starting deck, skill pool, level table, starting HP, hand
  size, improvise values, and its animator. Authoring a new character is authoring assets, not code.
- **One** runtime source of truth for the active character, replacing the two independent refs.
- A run is **self-describing**: the save records which character it belongs to, so reloading can
  never drift onto the wrong archetype's derived values.
- An avatar **state machine** (Idle / Walk / Fight / Hurt) authored once and overridden per character,
  driven correctly today even where staging is deferred.
- Every knob that varies per character is **inspector-tunable**, per the project's standing pattern.
- The character's wound-resistance stat is **named for what it does** — Toughness — and is **visible
  in the HUD**.

**Non-goals:** a character-select screen; unlock/ownership persistence; any change to combat, turn,
or reward *rules*; a combat-canvas player avatar; per-character portraits; **any change to enemies**
(see Part T).

### Explicitly not a character concern

Rule-bending stays in the **skill** system. A future "improvise Siege once per turn" trait is a new
`SkillEffect` member queried by a gate — exactly the precedent `RecruitEnemies`/Charismatic already
set — not a character-effect hook system. **All characters start with zero skills.** Characters
differ only in starting cards, skill pool, and level-up bonuses. Nothing in this spec anticipates
trait architecture, and nothing here blocks it later.

## Part A — Character as data

### A1. `CharacterSO`

A rename of `PlayerSO.cs` — **file and class renamed together**, so the `.meta` guid is preserved and
the existing asset stays bound.

| Field | Type | Notes |
|---|---|---|
| `id` | string | Stable content id, matching the `CardsSO`/`UnitsSO`/`SkillsSO` pattern |
| `characterName` | string | was `playerName` |
| `startingToughness` | int | replaces the hardcoded `playerHP = 2` (see Part T); **seeds `Player.playerToughness` at run start only** — it then grows via level-up `toughnessBonus` and is restored from the save on load, never re-seeded. `OnValidate` clamps to **minimum 1** |
| `handSize` | int | was `playerHandSize` |
| `startingDeck` | List&lt;`CardsSO`&gt; | was `startingHand` — renamed because `PlayerDeck` feeds it into `deckList`, not the hand |
| `improvAttack` / `improvDefend` / `improvExplore` / `improvInfluence` | int | replace the four hardcoded `= 1` literals |
| `levelTable` | `LevelRewardsSO` | replaces the scene reference on `Player` |
| `skillPool` | `SkillPoolSO` | moved off `LevelRewardsSO` |
| `animatorController` | `RuntimeAnimatorController` | the Part D hook; accepts a base controller or an override |

`OnValidate` warns on: empty `id`, empty `startingDeck`, null `levelTable`, null `skillPool`, and
clamps `startingToughness` to at least 1 (see [T4](#t4-the-zero-toughness-hazard)).

**Menu:** `ScriptableObjects/CharacterSO`.

### A2. `SkillPoolSO`

New thin SO holding `List<SkillsSO> skills`. `LevelRewardsSO` loses its `skillPool` field and becomes
**entries-only**.

Splitting the pool from the table is deliberate: characters that share a progression curve reuse one
table asset while having entirely different pools, so retuning the exp curve is one asset edit rather
than N edits that can drift apart.

**Asset migration:** the current `LevelRewardsSO` asset's 10 skills move into a new
`CommonSkills.asset`.

### A3. Run identity

`DataManager` gains, mirroring the four existing registries:

- `allCharacters` (`CharacterSO[]`) and a `ContentRegistry<CharacterSO> Characters`,
- `ActiveCharacter` — **the** single source of truth,
- a serialized `defaultCharacter` fallback.

`NewGame()` sets `ActiveCharacter = defaultCharacter`. **That one line is the seam B replaces** with a
real selection. `LoadGame()` resolves `ActiveCharacter` from `run.characterId`; an unresolvable id
logs an error and falls back to `defaultCharacter` rather than hard-failing the load.

**Ordering guarantee:** `DataManager` is `DontDestroyOnLoad`, and both `NewGame()` and `LoadGame()`
set `ActiveCharacter` **before** `SceneManager.LoadScene(1)`. Every scene-1 `Awake` therefore sees a
resolved character. No initialization race exists to design around.

### A4. Save schema v7

Two additions to `RunState` / `PlayerState`:

- `RunState.characterId` (string) — a null or empty value marks a pre-v7 save and resolves to the
  default character.
- `PlayerState.toughness` (int) — replaces `hp`. **`hp` is kept in the model as a vestigial field**
  so `JsonUtility` still parses it out of v6 files; the migrator copies `hp → toughness` when
  `toughness == 0`. Renaming the field without this would silently read toughness as **0**, which is
  the hang described in [T4](#t4-the-zero-toughness-hazard).

Bump `schemaVersion` to 7.

### A5. Consumer changes

- **`Player`** — delete the `PlayerSO` and `LevelRewardsSO` serialized fields, the `playerHP = 2`
  literal (the field itself is *renamed* by [T3](#t3-the-rename-character-side-only), not deleted),
  and the four `improv*Value = 1` literals; read all of them from
  `DataManager.Instance.ActiveCharacter`. `PlayerHandSize` becomes
  `LevelRules.DerivedHandSize(character.HandSize, playerLevel, character.LevelTable.Entries)`.
  `HasCharismatic` is unchanged.
- **`PlayerDeck.Awake`** — `player.StartingHand` becomes `ActiveCharacter.StartingDeck`.
- **`LevelUpController`** — `player.LevelRewards.SkillPool` becomes the character's `SkillPool`.

**`LevelRules` requires no changes.** It is already fully parameterized: `DerivedHandSize` takes the
base size as an argument, and `DrawSkillChoices<T>` is generic over the pool. The pure-rules layer is
untouched by **A** — Part T does change `CombatRules` (a parameter rename plus the divisor clamp in
[T4](#t4-the-zero-toughness-hazard)), and Part D adds `AvatarStateRules`.

### A6. Tests and assemblies

Part A's only new pure-test surface is the **v6 → v7 migration** (both the character-id default and
the `hp → toughness` copy), which slots into the existing
`ArchonsRise.SaveData.Tests` asmdef. `CharacterSO` and `SkillPoolSO` are ScriptableObjects living in
`Assets/Scripts/GameScriptableObjectTypes/` alongside the other SOs in the main assembly — **Part A
needs no new asmdef**. (Part D does add one, for `AvatarStateRules` — see D6.) Verification runs
through the mcs harness.

## Part T — Toughness terminology & readout

### T1. What the stat actually is

The character stat is a **divisor, not a pool**. `CombatRules.WoundCount` walks the Defend shortfall
in stat-sized bites — `for (i = 0; i < shortfall; i += stat) wounds++`, i.e. `ceil(shortfall / stat)`.
A shortfall of 5 against a stat of 2 is **3 wounds**. It never depletes, and it is not a loss axis:
the only loss conditions are wound count and the Doom Clock. **Higher value = fewer wounds per bad
fight.** Its name is therefore **Toughness**.

### T2. Enemies are untouched

**Enemy HP is a genuine depleting pool and keeps its name, its fields, and its glyph.** `enemyHP`,
`EffectiveHP`, `IconConcept.Hp`, and the `"hp"` TMP tag are **out of scope and must not change.** No
new icon is added.

One documentation knock-on: [content-rules.md](../../../.claude/skills/archons-rise-design/content-rules.md)
currently reads *"Enemy toughness is `hp` everywhere"*, which would make "toughness" name both
concepts. It becomes *"Enemy **HP** is `hp` everywhere."*

### T3. The rename (character side only)

| Now | Becomes |
|---|---|
| `Player.playerHP` / `PlayerHP` | `playerToughness` / `PlayerToughness` |
| `LevelRewardEntry.hpBonus` | `toughnessBonus` |
| `CombatRules.WoundCount(..., int playerHP)` | `int playerToughness` (parameter name) |
| `CombatRules.GroupWoundCount(..., int playerHP)` | `int playerToughness` |
| `CharacterSO.startingHP` | `startingToughness` |
| `PlayerState.hp` | `toughness` (+ vestigial `hp`, see A4) |
| `LateGameSaveTool.PlayerHp` | `PlayerToughness` |

**`hpBonus` is a serialized field on the shipped `LevelRewards.asset`.** Renaming it plainly would
drop every authored value to 0 — silently removing all toughness progression. It **must** carry
`[FormerlySerializedAs("hpBonus")]`.

### T4. The zero-toughness hazard

`WoundCount` increments the loop counter by the stat. **A toughness of 0 makes `i += 0` loop
forever** and hangs Unity. This is unreachable today (the literal is 2 and only grows), but Part A
turns it into an authored field and Part T renames its save key — two new ways to reach 0.

Two guards, both required:

- `CombatRules` clamps the divisor to a minimum of 1 before looping. The pure rule must be safe on
  its own, independent of who calls it.
- `CharacterSO.OnValidate` clamps `startingToughness` to a minimum of 1, so the bad asset can't be
  authored in the first place.

`CombatRulesTests` gains a case pinning that a 0 divisor terminates and behaves as 1.

### T5. The HUD readout

A `ToughnessLabel` component modelled directly on `DoomMeter` — an `IntEvent` raised by `Player`,
consumed by an `IntListener`, **no per-frame polling**.

- **Renders as the word plus the number** (e.g. `Toughness 2`). It deliberately does **not** reuse
  the enemy `hp` glyph: borrowing that icon would re-assert exactly the pool/divisor equivalence
  this rename exists to break. No new sprite asset is needed.
- Raised on: run start, level-up (`toughnessBonus` applied), and save-load restore — the three
  places the value can change.

**Editor-wiring hazard:** the `IntListener` must be wired to the **Dynamic** method in the
UnityEvent dropdown, not the Static one. A Static binding always fires with a hardcoded 0 and the
label will read `Toughness 0` forever.

## Part D — Avatar animation

### D1. Hierarchy

Today `SpriteRenderer`, `Animator`, and `PlayerPosition.cs` all sit on the **root** of
`PlayerPosition.prefab` — **and the Main Camera is a child of that root**. Animating that root's
transform would drag the camera.

Add an `Avatar` child owning the `SpriteRenderer`, the `Animator`, and a new `PlayerAvatar.cs`. The
root keeps `PlayerPosition.cs`, the Main Camera, and the six move-arrow buttons.

This child node is also the seam that later allows Fight/Hurt to be re-staged into the combat canvas
by changing **where the avatar renders**, without touching how it is driven.

### D2. Controllers

One base `PlayerAvatar.controller`, authored once:

- **States:** `Idle`, `Walk`, `Fight`, `Hurt`. These names are the override-slot keys, so they are
  **contract — never rename after the first character ships.**
- **Parameters:** `isWalking` (bool), `fight` (trigger), `hurt` (trigger).
- **Transitions:** `Idle ↔ Walk` on `isWalking`; `Any State → Fight` and `Any State → Hurt` on their
  triggers, both with **Can Transition To Self off** so a repeat trigger cannot restart a clip
  mid-play; `Fight` and `Hurt` → `Idle` on exit time.

Each character supplies an `AnimatorOverrideController` filling those four slots with its own clips.
Because `CharacterSO.animatorController` is typed `RuntimeAnimatorController`, a base controller and
an override are interchangeable. **A null controller falls back to the base** — a half-authored
character renders rather than crashing.

Chosen over per-character full controllers (every state-machine fix would be repeated N times) and
over runtime sprite-swapping via Sprite Library (forces all characters to share frame counts and
timing — too constraining for hand-made art).

### D3. First character's clips

`PlayerAnim.anim` becomes **Idle**. `PlayerWalk.anim` — authored but currently wired into nothing —
becomes **Walk**. For the two clips that do not exist yet, the `Hero Knight - Pixel Art` pack already
in the project supplies `HeroKnight_Attack1.anim` and `HeroKnight_Hurt.anim` as stand-ins.

### D4. Drivers

`PlayerAvatar` is a scene singleton called directly, the same way `Player.cs` already calls
`CombatController.Instance`.

| State | Driven by |
|---|---|
| Walk | `ExplorationController.ApplyMove` on a forward move |
| Fight | `CombatController.NotifyDefeated(..., wasInfluence: false)` — influence removals stay on the fade track and play no attack animation |
| Hurt | the group counterattack at the Defend press, when `CombatRules.GroupWoundCount` returns > 0 |
| Idle | default, and the return target for Fight and Hurt |

Direct calls were chosen over new `GameEvent`s: events are cleaner decoupling but each costs manual
listener wiring in the editor, and the avatar is pure view-layer. Revisit alongside the roadmap's
"decouple gameplay→UI via events" refactor.

### D5. Movement is cosmetic only

`ApplyMove` currently snaps the transform, so Walk needs a short position tween to play against.

**The tween must never gate logic.** Explore spend, fog reveal, `ShouldCommitOnMove`, and the phase
transition all fire immediately exactly as they do today; the avatar catches up visually. **Undo
snaps without animating** — an undo is a correction, not a journey.

### D6. Pure rules and tests

`AvatarStateRules` — a Unity-free static class holding the interrupt/priority logic: Hurt outranks
Fight, neither is interruptible by Walk, and what each state returns to on exit. Tested via the mcs
harness like `CombatRules` and `DefeatFxMath`. The `Animator` calls stay a thin shell over it.

Per the project's asmdef rule, `AvatarStateRules` gets its own folder asmdef plus a reference from
the tests asmdef; `PlayerAvatar` (a MonoBehaviour) stays in the main assembly.

### D7. Accepted consequence

Because the combat canvas overlays the board, **Fight and Hurt will often play on a token the player
cannot see.** This is the accepted cost of "architecture now, staging later": the states are
correctly driven, and re-staging them into the combat canvas later changes only where the avatar
renders — none of D4 changes.

## Design-bible updates

Per the `archons-rise-design` maintenance rule, these land **in the same change** as the code, along
with a `decisions-log.md` entry for the Toughness rename:

- **mechanics.md** — the "Lose — Wounds" section's *"HP is toughness, not a health pool"* paragraph
  becomes the Toughness definition; the Leveling section's *"+1 HP at milestone levels"* becomes
  *+1 Toughness*.
- **balance.md** — the level table's *+1 HP* rows and the *HP **2*** baseline become Toughness.
- **content-rules.md** — the `PlayerSO` row becomes the `CharacterSO` table from A1; the
  *"Enemy toughness is `hp`"* line becomes *"Enemy HP is `hp`"* (T2); add `SkillPoolSO`, and drop
  `skillPool` from the `LevelRewardsSO` description.

## Editor work (USER)

Per the project's standing practice, scene and prefab wiring is done manually in the editor from
step-by-step instructions, never by hand-editing YAML:

1. Author the `CharacterSO` asset for the existing hero (id, stats, deck, table, pool, animator).
2. Author `CommonSkills.asset` and move the 10 skills off the current `LevelRewardsSO` asset.
3. Populate `DataManager.allCharacters` and `defaultCharacter`.
4. Add the `onToughnessChanged` `IntEvent` asset, the HUD label, and its `IntListener` — **wired to
   the Dynamic method** (T5).
5. Add the `Avatar` child to `PlayerPosition.prefab`; move `SpriteRenderer` + `Animator` onto it.
6. Build `PlayerAvatar.controller` — 4 states, 3 parameters, the transitions in D2.
7. Create the first `AnimatorOverrideController` and slot the four clips from D3.

Then verify the `LevelRewards.asset` toughness values survived the `hpBonus` rename (T3) — the
`FormerlySerializedAs` attribute should preserve them, but a zeroed column is the visible symptom if
it was missed.

## Acceptance

**Part A**
- A second `CharacterSO` with a different starting deck, HP, hand size, and skill pool produces a
  visibly different run with **no code change**.
- Starting HP, hand size, and improvise values all come from the character; no literals remain in
  `Player.cs`.
- Level-up skill picks draw from the **active character's** pool.
- A v6 save loads without error and resolves to the default character.
- A v7 save round-trips its `characterId`; reloading restores the same character and the same derived
  hand size.
- No component holds a serialized character reference except `DataManager`.

**Part T**
- A v6 save's `hp` value arrives as `toughness` — a leveled character does not lose progression on
  load.
- `LevelRewards.asset` still shows its authored toughness bonuses after the `hpBonus` rename.
- `CombatRules` with a 0 divisor terminates rather than hanging, and behaves as 1.
- `CharacterSO.OnValidate` refuses to leave `startingToughness` below 1.
- The HUD label reads the correct value at run start, changes on a toughness level-up, and survives
  save/reload — never `Toughness 0`.
- Nothing about enemies changed: `enemyHP`, `EffectiveHP`, `IconConcept.Hp`, and the `"hp"` tag are
  byte-identical, and `IconRegistryValidationTests` is green with no registry edits.

**Part D**
- The avatar idles on the map and plays Walk on a forward move, returning to Idle.
- Undo of a move snaps with no walk animation.
- A move's explore spend, fog reveal, and phase transition are unaffected by the tween.
- An Attack-phase kill triggers Fight; an Influence removal does not.
- A counterattack that inflicts ≥ 1 wound triggers Hurt.
- Swapping the character's `animatorController` swaps all four clips with no controller re-authoring.
- A character with a null `animatorController` renders on the base controller.
- `AvatarStateRules` suite green via the mcs harness.

## Deferred (B and C)

- **B — character select / run setup.** Replaces the one line in `NewGame()`. Pure UI once A ships;
  needs no save work, because A already made the run self-describing. Belongs with roadmap **M3**.
- **C — unlock profile.** Which characters the player owns, persisting *across* runs — a different
  persistence layer from the run save (the tutorial's device-level PlayerPrefs is the nearest
  precedent). Depends on M3's meta-unlock pool design, which is not yet decided.
- **Per-character content authoring.** Decks, pools, and tables for characters two onward — content
  work, not code, and unblocked the moment A lands.
- **A combat-canvas player avatar** — see D7.
