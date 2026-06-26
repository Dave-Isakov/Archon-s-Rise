# Archon's Rise Design & Roadmap Skills Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build two committed, project-level Claude skills — `archons-rise-design` (the game-design bible) and `archons-rise-roadmap` (the living development plan) — under `.claude/skills/`.

**Architecture:** Eight markdown files across two skill folders. Each skill has a `SKILL.md` entry point with YAML frontmatter (`name` + `description`) that controls when the skill loads, plus supporting reference files. Content is seeded from the approved spec and grounded in the actual codebase (ScriptableObject field definitions, the code review).

**Tech Stack:** Markdown + YAML frontmatter. No build step. "Tests" are acceptance checks per task and a final fresh-subagent verification (no unit-test framework applies to prose skills).

## Global Constraints

- Skill files live under `.claude/skills/<skill-name>/` in the repo (committed), exact folder names `archons-rise-design` and `archons-rise-roadmap`.
- Every `SKILL.md` MUST begin with YAML frontmatter containing `name:` (matching the folder) and `description:` (verbatim as given in each task).
- Source-of-truth for game decisions is the spec: `docs/superpowers/specs/2026-06-25-archons-rise-skills-design.md`. Do not invent new game decisions; where the spec leaves a value as tuning, write it as a labelled placeholder range, never as "TBD".
- ScriptableObject authoring facts MUST match the real fields in `Assets/Scripts/GameScriptableObjectTypes/` (reference table embedded in Task 3). All SO types inherit `AllCards`: `cardName` (string) + `cardDescription` (TextArea string).
- Reference, don't duplicate, the code review: link to `docs/code-review.md` rather than restating it.
- Commit after each task with the message shown in that task's final step.

---

### Task 1: Scaffold + `archons-rise-design/SKILL.md`

**Files:**
- Create: `.claude/skills/archons-rise-design/SKILL.md`

**Interfaces:**
- Produces: the design skill entry point. Later design files (`mechanics.md`, `content-rules.md`, `balance.md`) are referenced from its index.

- [ ] **Step 1: Define the acceptance check**

This file passes when: it has valid frontmatter with `name: archons-rise-design`; the `description` is the exact string in Step 2; it states the canonical pitch; it lists the 4 design pillars; and it indexes the three sibling files by relative path.

- [ ] **Step 2: Write the file**

Create `.claude/skills/archons-rise-design/SKILL.md` with this exact frontmatter and structure:

```markdown
---
name: archons-rise-design
description: Game-design bible for Archon's Rise, a single-player roguelike deckbuilder. Use when designing or authoring game content (cards, enemies, towns, units, rewards, dungeons) or reasoning about mechanics, win/lose conditions, leveling, or balance.
---

# Archon's Rise — Game Design Bible

**Pitch:** A single-player roguelike deckbuilder where you explore a randomized hex realm, build a crystal-empowered deck, and race a doom clock to Rise to Archon before wounds or the falling land stop you.

## Design Pillars
1. **The Rise is domination, not a boss kill** — winning means accumulating territory + power (control towns, hit a Level/Influence threshold), so Influence, towns, and leveling are first-class, not side content.
2. **Two clocks of pressure** — every run is squeezed tactically by Wounds (deck pollution) and strategically by the Doom Clock (the land falling). Design must keep both live.
3. **Crystals are the spice** — the Empower/crystal economy is the main tactical lever; new content should create interesting crystal-spend decisions, not flat stat sticks.
4. **Runs are fresh; mastery grows the pool** — no power carryover. Meta-progression only unlocks new content into future runs.

## Index
- [mechanics.md](mechanics.md) — run loop, win/lose conditions, stats, empower economy, leveling.
- [content-rules.md](content-rules.md) — authoring contract for every ScriptableObject content type.
- [balance.md](balance.md) — number ranges, reward tiers, crystal costs, doom-clock pacing, unlock pool.

## Maintaining this skill
When a design decision changes, update the relevant file here AND append the decision to
`../archons-rise-roadmap/decisions-log.md` in the same change.
```

- [ ] **Step 3: Verify the acceptance check**

Read the file back. Confirm frontmatter parses (three `---`-delimited lines of YAML), `name` matches the folder, the four pillars and three index links are present. Confirm the description matches Step 2 verbatim.

- [ ] **Step 4: Commit**

```bash
git add .claude/skills/archons-rise-design/SKILL.md
git commit -m "feat: add archons-rise-design skill entry point"
```

---

### Task 2: `archons-rise-design/mechanics.md`

**Files:**
- Create: `.claude/skills/archons-rise-design/mechanics.md`

**Interfaces:**
- Consumes: pillars from `SKILL.md`.
- Produces: the locked mechanics other content references (win/lose definitions, stat list).

- [ ] **Step 1: Define the acceptance check**

Passes when the file documents, each in its own section: (a) the run loop; (b) win = Rise to Archon with its threshold shape; (c) lose = Wounds + Doom Clock, both explained; (d) the turn/round flow; (e) the four action stats + Heal/Wound/Crystal; (f) the empower/crystal economy; (g) the leveling curve. No "TBD" — tuning values are labelled and deferred to `balance.md`.

- [ ] **Step 2: Write the file**

Create `.claude/skills/archons-rise-design/mechanics.md` containing these sections with this required content (prose may be expanded but must include every point):

- **Run Loop:** start fresh deck → explore randomized hex map spending Explore → encounter towns/enemies/dungeons → play cards each turn to generate Attack/Defend/Explore/Influence → fight (Attack vs enemy HP), recruit (Influence at towns), level up from rewards → repeat under rising Doom until the Archon threshold is met (win) or a loss condition triggers.
- **Win — Rise to Archon:** a combined domination threshold: control N towns AND reach a Level/Influence target (exact N and targets live in `balance.md`). Met → run won.
- **Lose — Wounds (tactical):** defeat in combat shuffles Wound cards into the deck; they clog draws and (per `balance.md`) a count/HP threshold ends the run. Mend/Heal cards remove them.
- **Lose — Doom Clock (strategic):** a corruption/threat value rises each round (rate in `balance.md`); if it maxes before the Archon threshold is met, the run is lost. This is a NEW system (see roadmap M2).
- **Turn/Round Flow:** within a turn the player plays cards to build stats, acts, then ends the turn (stats reset to 0); rounds group turns and advance the Doom Clock. (Matches existing `GameManager` round/turn + `Player.TurnEnd`.)
- **Stats:** Attack, Defend, Explore, Influence are the four spendable actions; Heal removes Wounds; Wound is the penalty; Crystal feeds Empower. (Matches `StatType` flags.)
- **Empower / Crystal Economy:** cards may be Empowered by spending a colored Crystal (Red/Yellow/Green/Purple) for the card's stronger `empower*` values instead of base values. Crystals are a limited resource gained from rewards/cards.
- **Leveling:** experience fills toward `expToNextLevel`; on level-up apply the existing code intent — even level → +1 to a stat, odd level → +HP, every 3rd level → +hand size, every level → a new skill/option. (Curve numbers in `balance.md`.)

- [ ] **Step 3: Verify the acceptance check**

Read back; confirm all eight bullet topics are present as sections and no "TBD" appears.

- [ ] **Step 4: Commit**

```bash
git add .claude/skills/archons-rise-design/mechanics.md
git commit -m "feat: add Archon's Rise mechanics design doc"
```

---

### Task 3: `archons-rise-design/content-rules.md`

**Files:**
- Create: `.claude/skills/archons-rise-design/content-rules.md`

**Interfaces:**
- Consumes: mechanics definitions (stat meanings, empower economy).
- Produces: the authoring contract the verification task (Task 7) uses to author a valid card.

- [ ] **Step 1: Define the acceptance check**

Passes when the file gives, for each SO content type, its `CreateAssetMenu` path, its serialized fields with types, and authoring rules. The field facts MUST match the embedded reference (which mirrors `Assets/Scripts/GameScriptableObjectTypes/`).

- [ ] **Step 2: Write the file**

Create `.claude/skills/archons-rise-design/content-rules.md`. Open with: "All content types inherit `AllCards`: `cardName` (string) and `cardDescription` (TextArea string). Source of truth: `Assets/Scripts/GameScriptableObjectTypes/`." Then one section per type using exactly these fields:

**Card — `CardsSO`** (menu `ScriptableObjects/Cards/PlayerCards`):
`attack, defend, explore, influence, healAmount, numCrystals` (base ints); `empowerAttack, empowerDefend, empowerExplore, empowerInfluence, empowerHealAmount, empowerNumCrystals` (empowered ints); `cardType: StatType` (which stats this card provides — must be set or the stat returns 0); `empowerType: EmpowerType` (crystal color needed to empower); `isChoice: bool` (player picks which stat to apply).
Rule: only stats flagged in `cardType` are returned by `ReturnAttack/Defend/Influence/Explore`; set base AND empower values for each flagged stat. Empower values should exceed base (pillar 3).

**Enemy — `EnemiesSO`** (menu `ScriptableObjects/Cards/EnemyCards`):
`enemyHP: int`, `enemyAttack: int`, `reward: RewardLevel`, `defeatRewards: List<RewardsSO>`, `canInfluence: bool`, `influenceCost: int` (forced to 0 when `canInfluence` is false).
Rule: defeat needs player Attack ≥ `enemyHP`; player Defend < `enemyAttack` causes Wounds.

**Town — `TownsSO`** (menu `ScriptableObjects/Cards/TownCards`):
`townSize: TownSize` (Town/Village/Fortress/City); `activity: TownActivity [Flags]` (None/Recruit/Cards/Heal/Resources); `recruitableUnits: List<UnitsSO>`; `recruitLevel, cardLevel, resourceLevel, healLevel: int`.
Rule: towns are the domination win's currency — what you control toward Archon.

**Unit — `UnitsSO`** (menu `ScriptableObjects/Units`):
`attack, defend, explore, influence, healAmount, numCrystals: int`; `cardType: StatType`; `sprite: Sprite`; `color: Color`; `unitLetter: char`; `empowerType: EmpowerType`.
Rule: recruited at towns with Influence; played to add stats (no per-card empower toggle like cards).

**Reward — `RewardsSO`** (menu `ScriptableObjects/Cards/RewardCards`):
`rewardType: RewardType [Flags]` (Experience/Crystals/Cards); `rewardLevel: RewardLevel` (Beginner/Intermediate/Advanced/Master); `expAmount: int`; `numCrystals: int`.

**Dungeon — `DungeonsSO`** (menu `ScriptableObjects/Dungeons`):
`exploreCost: int`; `enemies: List<EnemiesSO>`; `rewards: List<RewardsSO>`.

**Location — `LocationsSO`** (menu `ScriptableObjects/LocationsSO`):
`exploreCost: int`; `enemies: List<EnemiesSO>`; `towns: List<TownsSO>`; `dungeons: List<DungeonsSO>`.

**Player — `PlayerSO`** (menu `ScriptableObjects/PlayerSO`):
`playerName: string`; `playerHandSize: int`; `startingHand: List<CardsSO>`.

- [ ] **Step 3: Verify the acceptance check**

Cross-check every field name/type against `Assets/Scripts/GameScriptableObjectTypes/` (open the files). Confirm no field is invented and every type has its `CreateAssetMenu` path.

- [ ] **Step 4: Commit**

```bash
git add .claude/skills/archons-rise-design/content-rules.md
git commit -m "feat: add Archon's Rise content authoring contract"
```

---

### Task 4: `archons-rise-design/balance.md`

**Files:**
- Create: `.claude/skills/archons-rise-design/balance.md`

**Interfaces:**
- Consumes: mechanics (win threshold, doom clock, leveling) and content-rules (stat fields).
- Produces: the tuning knobs referenced by mechanics.

- [ ] **Step 1: Define the acceptance check**

Passes when every tuning value the mechanics deferred has a labelled placeholder range here (not "TBD"): Archon win threshold, doom-clock rate/max, wound-out threshold, crystal costs, reward tiers, leveling curve, and the unlock pool concept.

- [ ] **Step 2: Write the file**

Create `.claude/skills/archons-rise-design/balance.md` with these sections, each giving a starting placeholder range explicitly labelled "starting value — tune in playtest":
- **Archon Win Threshold:** e.g. control 3 towns AND reach Level 8 (or Influence ≥ 30 accumulated). Starting values; tune.
- **Doom Clock:** starts 0, max e.g. 20; +1 per round, +extra on triggering events. Starting values; tune.
- **Wound-out:** lose if Wounds in deck ≥ e.g. 6, or HP reduced to 0. Starting values; tune.
- **Crystal Costs:** empower spends 1 crystal of the card's `empowerType`. Starting rule; tune per-card via `empowerNumCrystals`.
- **Reward Tiers:** Beginner/Intermediate/Advanced/Master map to rising `expAmount`/`numCrystals`/card rarity — give a starting number band per tier.
- **Leveling Curve:** `expToNextLevel` growth (existing code: `+= playerLevel + 12`). Note even=+stat, odd=+HP, every-3=+handsize, every-level=+skill. Starting values; tune.
- **Unlock Pool:** list the unlock categories (cards/units/enemies/events) and a starting unlock cadence (e.g. 1 unlock per run win). Tune.

- [ ] **Step 3: Verify the acceptance check**

Read back; confirm each deferred value from `mechanics.md` is resolved to a labelled starting range and no "TBD" remains.

- [ ] **Step 4: Commit**

```bash
git add .claude/skills/archons-rise-design/balance.md
git commit -m "feat: add Archon's Rise balance reference"
```

---

### Task 5: `archons-rise-roadmap/SKILL.md` + `status.md`

**Files:**
- Create: `.claude/skills/archons-rise-roadmap/SKILL.md`
- Create: `.claude/skills/archons-rise-roadmap/status.md`

**Interfaces:**
- Produces: the roadmap entry point + current status; `milestones.md` and `decisions-log.md` (Task 6) are referenced from `SKILL.md`.

- [ ] **Step 1: Define the acceptance check**

Passes when `SKILL.md` has valid frontmatter (`name: archons-rise-roadmap` + the exact description), a single **Current Focus** line, an index of `status.md`/`milestones.md`/`decisions-log.md`, and written maintenance rules; and `status.md` accurately lists existing vs. stubbed vs. missing systems seeded from the code review.

- [ ] **Step 2: Write `SKILL.md`**

Create `.claude/skills/archons-rise-roadmap/SKILL.md`:

```markdown
---
name: archons-rise-roadmap
description: Living development roadmap for the Archon's Rise game. Use when deciding what to build next, checking project status, reviewing or reordering milestones, or recording a design/development decision.
---

# Archon's Rise — Development Roadmap

**Current Focus:** M1 — Run-based save/load (see milestones.md).

## Index
- [status.md](status.md) — what exists vs. stubbed vs. missing.
- [milestones.md](milestones.md) — ordered milestones to a playable roguelike loop.
- [decisions-log.md](decisions-log.md) — append-only design/dev decisions with rationale.

## Maintaining this skill (living plan rules)
- Keep **Current Focus** above pointing at exactly one active milestone.
- When priorities shift, reorder `milestones.md`; do not delete history, mark items done.
- When a milestone completes, update `status.md` to reflect reality and advance Current Focus.
- Record every design/dev decision (and its why) in `decisions-log.md` — append-only.
- Game-design questions are answered by the `archons-rise-design` skill; this skill is the plan.
```

- [ ] **Step 3: Write `status.md`**

Create `.claude/skills/archons-rise-roadmap/status.md` with three lists (seed from `docs/code-review.md`):
- **Exists (in code):** stats/turn/round, undo (Command pattern), cards + empower/crystals, hex exploration (`GridGeneration`), combat, towns + unit recruiting, dungeons, rewards, leveling, ScriptableObject event bus, JSON save (scalar-only). Note the three fixed Critical bugs (listener unregister, LoadGame, autosave) — see `docs/code-review.md`.
- **Stubbed / partial:** save persists only scalar player stats (no deck/board/world); leveling rewards (the even/odd/every-3 rules are commented intent, not implemented); SaveButton prefab wired to LoadGame.
- **Missing:** run-based save schema, win check (Archon threshold), doom clock, wound-out loss, run setup/seed, meta-unlock pool, and the Important-tier refactors in `docs/code-review.md`.

- [ ] **Step 4: Verify the acceptance check**

Read both files back; confirm frontmatter, Current Focus, index, maintenance rules, and the three accurate status lists.

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/archons-rise-roadmap/SKILL.md .claude/skills/archons-rise-roadmap/status.md
git commit -m "feat: add archons-rise-roadmap skill entry point and status"
```

---

### Task 6: `archons-rise-roadmap/milestones.md` + `decisions-log.md`

**Files:**
- Create: `.claude/skills/archons-rise-roadmap/milestones.md`
- Create: `.claude/skills/archons-rise-roadmap/decisions-log.md`

**Interfaces:**
- Consumes: status (what's missing), the spec's milestone ordering.
- Produces: the ordered plan + seeded decision history.

- [ ] **Step 1: Define the acceptance check**

Passes when `milestones.md` lists M1→M2→M3 in order with goal + scope + acceptance for each, plus a "Later" bucket folding in content + the code-review refactors; and `decisions-log.md` contains the four brainstorming decisions, each with a one-line rationale and the date 2026-06-25.

- [ ] **Step 2: Write `milestones.md`**

Create `.claude/skills/archons-rise-roadmap/milestones.md`:
- **M1 — Run-based save/load** (Current Focus). Goal: persist a run in progress. Scope: serialize deck/hand/discard as card ids (stable id on `CardsSO`, not array index), map seed, doom-clock + run state; make save explicit + on quit; finishes deferred code-review Critical #3. Acceptance: quit mid-run and resume with deck/map/clock intact.
- **M2 — Win/lose systems.** Goal: make a run winnable/losable. Scope: Archon win check (threshold from `balance.md`), Doom Clock (rises per round, loss at max), wound-out loss. Acceptance: a run can be won by hitting the threshold and lost by clock max or wound-out.
- **M3 — Run setup & meta-unlocks.** Goal: framed runs + progression. Scope: run seed/initialization, content-unlock pool, between-runs unlock flow. Acceptance: starting a new run rolls a seeded map and uses unlocked content; winning grants an unlock.
- **Later (fold in after M1–M3):** content expansion per `../archons-rise-design/content-rules.md`; Important-tier refactors from `docs/code-review.md` (event-driven over `Update()`, decouple gameplay→UI via events, apply/revert toggle refactor, asmdefs + EditMode tests, modernization pass).

- [ ] **Step 3: Write `decisions-log.md`**

Create `.claude/skills/archons-rise-roadmap/decisions-log.md` as an append-only log, seeded:
- `2026-06-25` — **Structure: roguelike runs.** Why: fits randomized map + deckbuilding; replayability over one long campaign.
- `2026-06-25` — **Win: Rise to Archon (domination).** Why: leverages existing Influence/towns/leveling; avoids building a bespoke final-boss system.
- `2026-06-25` — **Lose: Wounds + Doom Clock.** Why: pairs tactical (existing Wound/Mend) and strategic pressure for run tension.
- `2026-06-25` — **Meta: content unlocks only.** Why: keeps runs fresh/balanceable; mastery grows the content pool, no power carryover.

- [ ] **Step 4: Verify the acceptance check**

Read both back; confirm M1→M3 ordering with goal/scope/acceptance each, the Later bucket, and the four dated decisions with rationale.

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/archons-rise-roadmap/milestones.md .claude/skills/archons-rise-roadmap/decisions-log.md
git commit -m "feat: add Archon's Rise milestones and decisions log"
```

---

### Task 7: Fresh-subagent verification of both skills

**Files:**
- Modify (only if gaps found): any of the eight skill files.

**Interfaces:**
- Consumes: all eight files from Tasks 1–6.

- [ ] **Step 1: Dispatch a fresh general-purpose subagent**

Dispatch a subagent with NO prior context and this prompt:

> You have access to two skills under `.claude/skills/`: `archons-rise-design` and `archons-rise-roadmap`. Read ONLY those skill files (do not read the rest of the codebase). Then: (1) Author a brand-new valid Card as it would be authored from the design — list every `CardsSO` field with a concrete value, choosing a `cardType` and matching base+empower stat values, and explain why it fits the design pillars. (2) State what the project should build next and why, citing the current milestone. Report any place where the skills were ambiguous, contradictory, or missing information you needed.

- [ ] **Step 2: Evaluate the result against success criteria**

The skills pass if the subagent (a) produced a card whose fields all exist in `content-rules.md` with `cardType` flags matching the stats it set, and (b) named M1 (run-based save/load) as next with a coherent reason. Note every ambiguity the subagent reported.

- [ ] **Step 3: Fix gaps inline**

For each ambiguity/contradiction/missing item the subagent reported, edit the relevant skill file to resolve it. If the subagent invented a field not in `content-rules.md`, make the authoring contract clearer. If it picked the wrong "next" milestone, sharpen `SKILL.md` Current Focus / `milestones.md`.

- [ ] **Step 4: Re-verify if changes were made**

If Step 3 changed any file, dispatch one more fresh subagent with the same prompt and confirm both criteria now pass cleanly.

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/
git commit -m "test: verify Archon's Rise skills via fresh-subagent acceptance check"
```

---

## Self-Review

**Spec coverage:**
- Two project-level skills under `.claude/skills/` → Tasks 1–6. ✓
- `archons-rise-design`: SKILL.md (T1), mechanics.md (T2), content-rules.md (T3), balance.md (T4). ✓
- `archons-rise-roadmap`: SKILL.md + status.md (T5), milestones.md + decisions-log.md (T6). ✓
- Four validated decisions seeded → mechanics.md (T2) + decisions-log.md (T6). ✓
- Authoring contract for every existing SO type → content-rules.md (T3), all 8 types. ✓
- M1→M3 milestone ordering → milestones.md (T6). ✓
- "Living" maintenance rules written into each SKILL.md → T1 + T5. ✓
- Success criterion "fresh session can author a card + state next step" → verification (T7). ✓
- Out-of-scope (game systems, code-review refactors) correctly deferred to roadmap milestones, not built here. ✓

**Placeholder scan:** Balance values are labelled "starting value — tune in playtest" (legitimate, not "TBD"); no "implement later"/"similar to Task N" present. ✓

**Type consistency:** SO field names/types in T3 match the verbatim source read from `Assets/Scripts/GameScriptableObjectTypes/`; skill folder names and `name:` frontmatter match across T1/T5 and the git-add paths. ✓
