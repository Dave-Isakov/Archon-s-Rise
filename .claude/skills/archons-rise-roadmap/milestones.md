# Milestones

Ordered path to a playable roguelike loop. Each milestone is specced and planned individually when
it becomes the Current Focus. Mark items done rather than deleting them.

## M1 — Run-based save/load  ✅ DONE (2026-06-29)
**Goal:** persist a run in progress so the player can quit and resume.
**Scope:**
- Serialize deck / hand / discard as **card ids** (a stable id on `CardsSO`, NOT array index —
  current reward/deck code indexes `allCards` by position, which is fragile).
- Serialize the **map seed**, the **Doom Clock**, and run state (round/turn, crystals, level/exp).
- Make save **explicit** (a real Save action) plus on `OnApplicationQuit`.
- Completes deferred code-review Critical #3 (`docs/code-review.md`).

**Acceptance:** quit mid-run and resume with deck, map, crystals, and clock intact.

## M2 — Place-type system  ✅ DONE (2026-07-03)
**Goal:** typed places (Town/Keep/Castle) + resumable guardian conquest + service gating.
**Scope:** `PlaceType` taxonomy; data-driven guardian rosters; assault/retreat (3 wounds);
conquest persistence (save schema v2); services gate by type + conquest; Cards stub.
Spec: `docs/superpowers/specs/2026-06-30-m2-place-type-system-design.md`.
**Acceptance:** a Keep/Castle can be assaulted, conquered across sessions, and gates its
services; retreat costs 3 wounds and keeps progress. ✅

## M2.4 — Level-up rewards  _(Current Focus)_
**Goal:** make leveling pay out — the progression a win/lose loop needs to matter.
**Scope:**
- **Fixed reward table** (`LevelRewardsSO`): skill picks, +HP (toughness), +hand size, +army size.
- **Skills** (`SkillsSO` + skill bar): pick 1 of 3 on skill levels; exhaust/refresh per turn or
  per round; undoable activation.
- **Army cap** (starts 1) + disband-to-hire at cap; pure `LevelRules`/`ArmyRules` classes.
- **Save schema v3**: owned + exhausted skill ids (hand/army derived from level, not stored).
- Exp overflow carries over instead of being discarded.

Spec: `docs/superpowers/specs/2026-07-06-level-up-rewards-design.md`.
**Acceptance:** leveling to 2 offers a 3-skill pick usable on its cadence with undo; level 4
grants +1 hand and +1 army; recruit at cap forces disband-to-hire; skills survive save/load.

## M2.5 — Win/lose systems
**Goal:** make a run winnable and losable.
**Scope:**
- **Victory** — conquer **2 Castles** (`ConquestTracker.ConqueredCastleCount()`).
- **Doom Clock** — rises each round; reaching max loses the run.
- **Wound-out** — lose when Wounds ≥ threshold.
- **Game-over screen** for both outcomes.

**Acceptance:** a run can be won by conquering 2 Castles and lost by clock-max or wound-out.

## M3 — Run setup & meta-unlocks
**Goal:** framed runs plus between-run progression.
**Scope:**
- Run **seed / initialization** (new run rolls a seeded map and starting deck).
- **Content-unlock pool** + the between-runs unlock flow (1 unlock per win — `balance.md`).

**Acceptance:** starting a new run rolls a seeded map using unlocked content; winning grants an unlock.

## Later (fold in after M1–M3)
- **Content expansion** — new cards/enemies/towns/units/dungeons per
  `../archons-rise-design/content-rules.md`.
- **Code-review Important-tier refactors** (`docs/code-review.md`): event-driven updates over
  per-frame `Update()`; decouple gameplay→UI via the event bus; refactor the duplicated
  apply/revert toggle into explicit Apply/Revert wired to Command Execute/Undo; add assembly
  definitions + EditMode tests around combat/stat math; modernization pass (deprecated finds,
  `[field: SerializeField]` props, switch expressions, file-scoped namespaces).
