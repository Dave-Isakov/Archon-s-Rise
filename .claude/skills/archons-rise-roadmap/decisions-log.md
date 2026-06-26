# Decisions Log

Append-only record of design/development decisions and their rationale, so the *why* survives
across sessions. Newest entries at the bottom. When a decision changes, add a new entry rather than
editing an old one.

---

- **2026-06-25 — Structure: roguelike runs.**
  Short, self-contained runs against escalating difficulty on a randomized hex map.
  _Why:_ fits the existing randomized map + deckbuilding systems; favors replayability over a single
  long campaign.

- **2026-06-25 — Win: Rise to Archon (domination).**
  A run is won by accumulating territory + power (control N towns AND a Level/Influence target),
  not by beating a single final boss.
  _Why:_ leverages the existing Influence / towns / leveling systems and avoids building a bespoke
  final-boss subsystem; matches the "Rise" theme.

- **2026-06-25 — Lose: Wounds + Doom Clock.**
  Two failure pressures: Wounds (deck pollution from combat losses) and a Doom Clock (rising threat
  each round).
  _Why:_ pairs tactical pressure (reusing existing Wound/Mend cards) with strategic urgency, so a
  run is tense both moment-to-moment and overall.

- **2026-06-25 — Meta: content unlocks only.**
  Runs start from identical conditions; winning permanently unlocks new content into the future-run
  pool. No power carryover.
  _Why:_ keeps runs fresh and balanceable; mastery grows variety rather than trivializing difficulty.

- **2026-06-25 — Deliverable: two project-level skills.**
  `archons-rise-design` (GDD) and `archons-rise-roadmap` (living plan) under `.claude/skills/`.
  _Why:_ separates stable design reference from the frequently-changing plan; committed so the
  context travels with the repo.
