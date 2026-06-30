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

## M2 — Win/lose systems  _(Current Focus)_
**Goal:** make a run winnable and losable.
**Scope:**
- **Archon win check** — control N towns AND reach the Level/Influence target (`archons-rise-design/balance.md`).
- **Doom Clock** — rises each round; reaching max loses the run.
- **Wound-out** — lose when Wounds ≥ threshold or HP hits 0.

**Acceptance:** a run can be won by hitting the Archon threshold and lost by clock-max or wound-out.

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
