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

- **2026-07-02 — Round end is a full hand reset.**
  Ending a round returns the discard pile AND all unplayed hand cards to the deck, shuffles, and
  draws a fresh full hand. Turn end (unchanged) only tops the hand up to hand size from the deck.
  _Why:_ matches the original code intent (round-end draw deals a full hand size) and keeps Wounds
  cycling through the deck as deck pollution rather than sitting stuck in hand. Decided while fixing
  the round-end bug where the discard was cleared without ever re-entering the deck.

- **2026-07-02 — Round cadence rules: units refresh, End Turn gates on the deck.**
  Units exhausted during a round all refresh when the new round starts. When the deck can't refill
  the hand, End Turn is disabled (clicking it only ticked the turn counter) and the blocked-draw
  message tells the player to end the round instead of wrongly claiming the hand is at max size.
  _Why:_ ends of turns exist to top the hand up; with an empty deck that's impossible, so the round
  end (reshuffle + doom-clock tick) is the only meaningful action.

- **2026-07-02 — End Turn / Round End disabled during combat.**
  Both buttons gate on an active fight (`GameManager.activeCombatant != null`) in addition to the
  End Turn deck gate.
  _Why:_ ending the turn or round mid-fight would reset stats or reshuffle the hand out from under
  the combat; a fight must resolve (win or flee) first.

- **2026-07-02 — M2 retargeted to the place-type system; win/lose becomes M2.5.**
  M2 now builds Town/Keep/Castle taxonomy, data-driven guardian conquest (rosters: Town 0,
  Keep 1, Castle 2), 3-wound assault retreat, type+conquest service gating, and schema-v2
  persistence. Victory changes to **conquer 2 Castles** (no Level/Influence gate).
  _Why:_ the old "control 3 towns" win had no control mechanic behind it; typed places make
  territory meaningful and tie conquest to the existing combat system. Spec:
  `docs/superpowers/specs/2026-06-30-m2-place-type-system-design.md`.

- **2026-07-02 — M2 implementation decisions.**
  (1) The **Crystal/Resources service keeps its legacy `activity`-flag gate** (plus conquest) —
  the spec's service table omits it, and silently deleting a working service was worse; fold it
  into `PlaceService` when the design decides its place. (2) **Seeded maps guarantee ≥ 2
  Castles** (last-placed tokens upgrade if random picks came up short) so the M2.5 victory is
  always reachable. (3) **Retreat penalty applies only to an assault in progress** (user-confirmed
  2026-07-02) — clicking a guarded place opens the menu with all services locked and an Assault
  button; closing the menu without pressing Assault is free; the combat Flee button doubles as
  Retreat (3 wounds) during assaults. (4) **Places are entered by standing on their cell** —
  adjacent clicks are rejected with a message (enemies keep their adjacency interaction).
  (5) `GridGeneration` now draws towns from the full pool (`Rng(0, towns.Count)` instead of the
  hardcoded `Rng(0,3)`); RNG draw count is unchanged, so old seeds keep their tile layout — only
  town identities shift (v1 saves carry no conquest state, so this is cosmetic).

- **2026-07-03 — M2 guardian-assault polish deferred to a follow-up.**
  During acceptance the user proposed (a) spawning all of a place's guardians simultaneously,
  (b) reusing the usual `EnemyDeck.GetNewEnemyCard` combat-start path for guardians, and
  (c) a hover preview of enemies before combat. All three deferred rather than folded into M2.
  _Why:_ M2 acceptance was passing; (a) contradicts the spec's "fought in order" resumable model
  and would rewrite the assault driver + its retreat/resume guarantee mid-acceptance, (b) is a
  cleanliness refactor (the Fight button already works), and (c) is a genuinely new UI feature
  wanting its own design pass. Revisit as a focused follow-up.

- **2026-07-04 — Siege: a wound-free attack type.**
  A second attack, Siege, defeats an enemy on its own `Siege` stat pool (StatType flag 128) and
  skips the counterattack entirely — always wound-free — for the same rewards as a normal kill.
  Siege cannot be improvised (Improvise still only offers the four basic stats); it comes only from
  advanced cards (base or empower line) and units, so scarcity is its cost. Advanced Siege cards
  always also carry the Attack flag (so they pass the `CardPlaySelection.IsPlayable` gate and Siege
  only matters in combat); Siege units need no co-flag. Resolution logic lives in the pure
  `CombatRules` class. Spec: `docs/superpowers/specs/2026-07-04-siege-attack-type-design.md`.
  _Why:_ turns "how do I attack this enemy" into a real decision (read the enemy's Attack, then
  spend a scarce Siege vs risk the wound) without a separate preview screen; keeps the Wound clock
  live because Siege is deliberately rare. Supersedes the deferred hover-preview item for this need.
