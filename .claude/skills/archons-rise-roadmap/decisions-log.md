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

- **2026-07-04 — Enemy preview: information parity before combat.**
  Hovering a map enemy token or a guarded place's Assault button previews enemy stats (name, Attack,
  HP, Influence cost) without starting combat — the info-parity intent deferred out of M2
  (`m2-deferred-followups` #3) and left out of scope by the Siege spec. Pure info, not feasibility
  (no "can I beat it"). A guarded place previews all *remaining* guardians, and the panel is
  multi-enemy from the start to front-run simultaneous-guardian fights. The trigger is input-agnostic
  (`Focus`/`Unfocus`) so a gamepad can drive the same panel at the controller milestone. Visibility
  passes through a single pure `PreviewRules.CanPreview` blind gate (true today) with encounter-level
  aggregation — any blind enemy blinds the whole panel ("You cannot see the enemy/enemies you are
  about to confront"); no real blindness source is built yet. Spec:
  `docs/superpowers/specs/2026-07-04-enemy-preview-design.md`.
  _Why:_ closes the "see what you're up against before you spend cards / commit to an assault" gap
  that Siege assumed but never delivered, while leaving preview visibility itself gameable.

- **2026-07-06 — Level-up rewards: fixed table + skills + army cap (M2.4, before win/lose).**
  Level-ups pay out from a fixed, data-driven table (`LevelRewardsSO`), replacing the old
  even/odd/every-3rd comment scheme: skill picks (choose 1 of 3 unowned from a pool), +1 HP,
  +1 hand size, +1 army size at milestone levels. Skills (`SkillsSO`) live on an exhaustible
  skill bar — per-turn (weak stat gains) or per-round (crystals, heal 1 wound) cadence, undoable
  via the command stack. Army cap is new (starts 1); at cap, recruiting is disband-to-hire.
  Hand size and army cap are **derived from level + table, never saved**; schema v3 adds only
  owned/exhausted skill ids. Exp overflow now carries over instead of resetting to 0.
  Spec: `docs/superpowers/specs/2026-07-06-level-up-rewards-design.md`.
  _Why:_ progression must reward before a win/lose loop (M2.5) has stakes; a fixed table is the
  cheapest structure that still gives the player agency where it matters (which skill), and
  deriving sizes from level keeps saves lean and migration trivial.

- **2026-07-06 — HP is toughness, not a health pool.**
  HP's only role is the wound divisor in `CombatRules.WoundCount` (Defend shortfall is split into
  HP-sized bites, one Wound each). It never depletes; the "HP reduced to 0" loss clause is removed
  from mechanics/balance — the tactical loss is solely Wounds ≥ threshold.
  _Why:_ the HP-to-0 clause had no code behind it and a depletable pool would overlap confusingly
  with the Wound mechanic; keeping HP as pure toughness makes the level table's +1 HP a clear,
  already-working reward.

- **2026-07-06 — Level table amendment: card picks + all-count entries.**
  Card rewards join the level table (levels 3/6/10 to start), reusing the existing choose-1-of-3
  `RewardCanvas` screen and curated reward pool exactly as enemy-defeat card rewards do, queued
  after any skill modal. `LevelRewardEntry` fields are all per-level **counts** (skillPicks,
  cardPicks, hpBonus, handSizeBonus, armySizeBonus) rather than booleans.
  _Why:_ user wants level rewards freely tunable during playtesting — counts on one SO asset make
  every knob an inspector edit, and reusing the reward screen adds deck progression to leveling
  with near-zero new UI.

- **2026-07-09 — Units become option-lists (M2.75).**
  A unit (`UnitsSO`) is an authored list of `UnitOption`s (effect + amount + optional per-option
  crystal cost), played through a card-style pop-out; the legacy flat unit stats and unit
  `empowerType` are retired. Crystal costs match the card-empower rule (exact color or wild; an
  all-colors cost accepts any crystal), and `Crystallize` options grant a per-option authored color.
  Using any option applies it and exhausts the unit for the round; the whole use is one undoable
  command (reverts the effect and refunds any crystal). Spec:
  `docs/superpowers/specs/2026-07-09-unit-gameplay-and-recruitment-design.md`.
  _Why:_ fixed single-stat units were the least interesting board piece; option-lists give each unit
  a real in-combat decision and reuse the crystal economy without a new resource.

- **2026-07-09 — Unit use always opens the pop-out (single interaction model).**
  There is no "quick-play" path; every unit interaction routes through the pop-out.
  _Why:_ one model to learn, and it is the only place per-option costs and locked rows can read.

- **2026-07-09 — Enemy influence: pay-to-leave (rewards) / recruit (Charismatic).**
  Paying a `canInfluence` enemy's Influence cost ends the fight wound-free AND grants the normal
  defeat rewards (no counterattack runs). If the enemy has a `recruitedUnit` and the player owns the
  Charismatic passive, the same payment also adds the unit (rewards + unit). At the army cap the
  disband picker opens first; cancelling spends nothing.
  _Why:_ makes Influence a real third combat resolution alongside Attack/Siege, and gives Charismatic
  a concrete payoff without a bespoke capture minigame.

- **2026-07-09 — Town recruiting: choice panel at per-unit prices.**
  Recruiting opens a panel listing the town's units, each at its own `influenceCost`; `recruitLevel`
  is retired as the recruit price. Unaffordable entries show disabled; at cap the disband picker
  chains; cancel is free.
  _Why:_ the old flow silently hired `recruitableUnits[0]` at a flat town rate — players couldn't see
  or choose what they were buying.

- **2026-07-09 — Skills gain a passive cadence; Charismatic is the first passive.**
  `SkillCadence.Passive` skills are never clicked or exhausted — their effect is a queried gate
  (`SkillEffect.RecruitEnemies` → `Player.HasCharismatic`). Enum members are appended (serialized
  ints).
  _Why:_ some rewards should be always-on modifiers rather than activatable stat bursts; a queried
  passive needs no command-stack or refresh handling.

- **2026-07-09 — Save schema v5: persist exhausted units.**
  `RunState.unitExhausted` (a bool array parallel to `unitIds`) records which units were already used
  this round so a mid-round save reloads them still turned; migration from v4 defaults it to empty
  (all units fresh). Capture is single-source (both arrays from the same Unit-object iteration) so
  the parallel indexing can't drift.
  _Why:_ without it, saving mid-round and reloading refreshed every used unit for free.

- **2026-07-10 — Retired `EnemiesSO.reward`; kept `RewardsSO.rewardLevel` for dungeons.**
  `EnemiesSO.reward` (`RewardLevel`) was dead — no code read it; reward grants come from the
  `defeatRewards` list (flat-random) and difficulty from `tier`, so the field was removed.
  `RewardsSO.rewardLevel` is *also* currently unread, but it's kept: it's the natural hook for
  **dungeon-completion reward tiering** (higher-tier dungeons roll higher-tier rewards). The
  `RewardLevel` enum stays alive for it.
  _Why:_ removing genuinely-redundant clutter while preserving a deliberate design hook; the enemy
  field duplicated the reward list, the reward field is a property of the reward itself.

- **2026-07-10 — Dungeons (M2.9) prioritized ahead of meta-progression (M3).**
  Dungeon gameplay (enter via Explore, clear enemies in sequence, tiered completion rewards using
  `RewardsSO.rewardLevel`) is inserted as M2.9, before the M3 run-setup/meta-unlock work.
  _Why:_ dungeons are core-loop content the map already gestures at (`DungeonsSO`, Derelict Tower,
  DungeonEnemies all exist but aren't wired); meta-progression should layer on top of a complete
  core loop, not before it.

- **2026-07-10 — Combat rewards are tier-derived; `RewardsSO` retired.**
  Enemy/dungeon rewards no longer come from hand-authored `RewardsSO` bundles. Every defeat derives
  from a **tier** (1–3): Experience is always granted via a **bell-curve sample** of the tier's exp
  range (`RewardRules.SampleExp` — average of `expBellSamples` uniform draws, centre-weighted), then
  a **crystal** and a **card** are rolled **independently** against per-tier chances (crystals common,
  cards rare). Card rewards draw from **per-tier card pools** on `RewardTuningSO` — pool membership is
  a card's rarity, so "stronger rewards later" needs no upgrade system, just authored cards gated to
  higher tiers. Level-up card picks scale off player level the same way. `RewardsSO`, `RewardType`,
  `RewardLevel`, `EnemiesSO.defeatRewards`, and `DungeonsSO.rewards` are deleted; `DungeonsSO` gains
  `tier` + `rewardCount`. Pure math (`RewardRules`/`RewardTuning`) is TDD'd via the mcs harness in the
  new `ArchonsRise.Rewards` asmdef, mirroring `DoomRules`.
  _Why:_ the old path half-ignored its own data (crystal count unread, card pool flat, `rewardLevel`
  unconsumed) so reward magnitude didn't track enemy difficulty. Tier-derivation makes Experience the
  reliable growth spine with consumable bonuses layered on, and deletes a hollow indirection layer.
  _Supersedes_ the dungeon-reward mechanism in the 2026-07-10 M2.9 entry (which named
  `RewardsSO.rewardLevel`): dungeons now use `DungeonsSO.tier` + `rewardCount`.

- **2026-07-10 — Crystals are sold at every Place; town-menu buttons revive on open.**
  Two coupled changes to the place/town-menu system:
  1. **Crystal purchase is a Place service offered at all types** (Town/Keep/Castle), not gated by the
     legacy `TownsSO.activity` Resources flag. Added `PlaceService.Crystal`; `PlaceRules.AllowedServices`
     grants it to every `PlaceType`; `CrystalButton` now gates on `PlaceRules` + conquest like the other
     buttons. Price is the Place's `resourceLevel` (Influence per crystal, per-place). Influence — not
     place type — limits how many crystals a player can buy.
     _Why:_ crystals are the core tactical spice (pillar 3); restricting where you can buy them was an
     unintended limitation, and `CrystalButton` was the last holdout still reading the retired `activity`
     flags. Per-place pricing keeps stronger Places able to charge more.
  2. **Town buttons re-activate on menu open** via a new `TownMenu` lazy-singleton controller
     (`TownMenu.Instance.PrepareButtons()` from `TownToken.OnPointerClick`, before the open events).
     _Why:_ each button hides itself with `SetActive(false)`, which also disabled the `GameEventListener`
     on the same GameObject (`OnDisable` unregisters), so a button that hid once — e.g. Recruit on a
     not-yet-conquered Keep — never received `UpdateButtonText` again and the conquered menu came up empty.
     Reviving all buttons before raising the events re-registers their listeners. Zero scene wiring.

- **2026-07-14 — M2.9: dungeons are map places with tiered delves; unified `RewardQueue`.**
  Dungeons stopped being a card flow and became **map hexes** entered by standing on the cell
  (6 per map, spaced like spawn zones, never on towns or the start ring). Each dungeon is **three
  authored tiered delves**: one Explore spend per delve, one enemy (tier 1/2/3) under normal field
  rules (wounds, flee = 1 wound). **Fights pay experience only**; clearing the third delve pays a
  **guaranteed completion bundle** (1 exp roll at the dungeon `tier` + `rewardCount` crystals +
  `rewardCount` card picks) and **lowers the Doom Clock**. **Doom-band flags:** the first time doom
  enters the mid/high band, one random uncleared dungeon is **flagged** (once per band per run);
  each flagged dungeon adds **+1 doom/round** until cleared, and clearing a flagged dungeon gives a
  larger relief (−3 vs −1). `DoomRules.MaxTier` does **not** gate dungeon fights. Save schema
  bumped to **v6** (dungeon progress + once-per-run band-fire bools; positions/SO re-derive from
  the map seed). The legacy card-based dungeon flow (`Dungeon`, `DungeonDeck`, `DungeonEvent`) was
  deleted.
  _Why:_ the card flow was never wired and didn't fit the place-based map; map dungeons give
  explore a real payoff and a doom-management lever. A **unified `RewardQueue`** (pure
  `ModalQueueCore` + scene singleton) now serializes **every** reward/skill/message modal one at a
  time, replacing the M2.4 level-up busy-wait poll and guaranteeing no overlapping menus when a
  completion bundle, an enemy card drop, and a level-up all fire in the same frame.
  _Closes_ the reward-arbiter follow-up.

- **2026-07-14 — Stat conversion, unit refresh & influence-costed unit options.**
  Three additive strategy mechanics, no save schema bump (conversion/refresh are in-turn effects;
  refresh only flips the already-persisted `unitExhausted` state). Six locked decisions:
  1. **Conversion rate is always 1:1** — every point of source stat becomes one point of target.
  2. **Conversion is opt-in at play time** — cards show an inspector toggle (off by default, locked
     while improvising / until empowered when `convertRequiresEmpower`); skills are inherently opt-in
     (clicking is the choice).
  3. **Conversion touches the four action stats only** (Attack/Defend/Explore/Influence) — Siege is
     never a source or target (scarcity pillar); Heal/Crystal/Wound never participate. A converter is
     never also `isChoice`, and its target is never among its sources.
  4. **Refresh N is a budget across multiple units** — each pick deducts the unit's recruit
     `influenceCost` (min 1) from N until nothing affordable remains; unspent budget is lost; the
     effect fizzles when no spent unit is affordable at play time. The picker opens directly, never
     through `RewardQueue`.
  5. **One cost type per unit option** — crystal OR Influence OR free, never both; stronger variants
     are authored as separate option rows. The Influence spend is in-turn and undoable (never
     `Player.Influence()`, which clears the undo stack for permanent purchases).
  6. **No save schema bump.**
  _Why:_ conversion gives banked-but-wrong stats a use (a pillar-3 tactical lever); mid-round refresh
  turns Influence into a tempo resource; influence-costed options add a crystal-free unit spend. All
  three ride existing apply/revert symmetry, and pure rules (`ConvertRules`, `RefreshRules`) are
  TDD'd. Spec: `docs/superpowers/specs/2026-07-14-stat-conversion-refresh-influence-options-design.md`.

- **2026-07-15 — Tutorial for playtest handoff: contextual + event-driven, split M2.11/M2.12.**
  To hand the game to external playtesters, onboarding ships as **two milestones before M3**:
  **M2.11 UI language & iconography** (canonical `IconRegistrySO` — concept → sprite + TMP tag;
  costs always `[icon][number]` via a shared `CostRow`; action buttons `[icon] Label`; fixed
  Atk/Def/Exp/Inf order; one global locked treatment; full panel audit) and **M2.12 tutorial &
  help** (a dedicated TutorialCanvas above all others; a guided first-round rail of
  `TutorialStepSO`s advancing on real GameEvents with no input locking and out-of-order
  tolerance; once-per-profile reactive one-shots; a `HelpEntrySO` + ? icon on every major
  panel; PlayerPrefs-only persistence — no save-schema bump; Skip/toggle/reset; and a map-gen
  guarantee of ≥1 tier-1 enemy near the start ring). Locked decisions: the tutorial runs on the
  player's **real first run** (no authored tutorial map — scripted levels fight the randomized
  structure); teaching is **soft guidance** (steps wait, never block); banners/help are **not
  modals** and never enter `RewardQueue` (they hide while a modal is open); icons precede
  tutorial copy because inline `<sprite>` teaching only works when the real UI shows the same
  glyphs. Playtest shell (build packaging, feedback capture) is a separate later follow-up.
  _Why:_ external playtesters must learn unassisted; contextual event-driven teaching matches a
  game whose systems surface organically. Specs:
  `docs/superpowers/specs/2026-07-15-m2.11-ui-language-iconography-design.md`,
  `docs/superpowers/specs/2026-07-15-m2.12-tutorial-help-design.md`.

- **2026-07-15 — M2.11 implemented: `IconMarkup` (not `CostRow`) as the icon-language owner.**
  Shipped the canonical icon language as a pure `static IconMarkup` (new `ArchonsRise.UiLanguage`
  asmdef) owning all TMP sprite-tag names and cost strings, paired with an `IconRegistrySO`
  (`Assets/Resources/IconRegistry.asset`) mapping the 16 `IconConcept`s → Sprites for Image-based
  UI, and a `UiLock` static (alpha 0.4). Costs are `[icon][number]` (`<sprite="gem" index=0>3`);
  buttons `[icon] Label`; action order fixed Attack/Defend/Explore/Influence per line; crystal
  colors tint the one `crystal` glyph (Red `#E5484D`, Yellow `#F5D90A`, Green `#46A758`,
  Purple `#8E4EC6`); `shield` is Defend only and enemy toughness switched to a new `hp` glyph.
  **Amendment to the 2026-07-15 plan decision:** the spec's `CostRow` MonoBehaviour was replaced by
  the text-only `IconMarkup` formatter — a pure, mcs/EditMode-testable string owner reuses the
  existing "author descriptions with `<sprite>` tags" convention with no new prefab, and panels
  route their existing TMP labels through it. Three EditMode validation tests pin registry
  completeness, TMP-name resolution, and authored-description tag hygiene + canonical stat order.
  Panels swept: unit option text, town service buttons (Heal/Crystal/Recruit/Assault), recruit &
  unit-picker pickers, dungeon panel, card-inspector empower message, enemy preview + combat card,
  doom meter, xp bar, run-end screen. Spec:
  `docs/superpowers/specs/2026-07-15-m2.11-ui-language-iconography-design.md`; plan:
  `docs/superpowers/plans/2026-07-15-m2.11-ui-language-iconography.md`.

- **2026-07-16 — Empower gets its own glyph.**
  Added `IconConcept.Empower` (TMP name `empower`) to the M2.11 icon language so the literal word
  "Empower" reads as an icon: the empowered-line header in card/skill descriptions becomes
  `<sprite="empower" index=0> <stat>: N`, and the ConvertBanner locked-reason label reads
  `<empower-glyph> to unlock`. Empower is a **modifier** concept — not an action stat — so it is
  excluded from `IconMarkup.ActionStatOrder`/`TryForStat` and from per-line stat ordering. The
  CardInspector validation message ("…to empower this card.") was **left as prose**: "empower" there
  is a verb mid-sentence and a glyph reads as a verb awkwardly. This should have been part of M2.11
  (a missed concept); shipped as a small follow-on. Spec:
  `docs/superpowers/specs/2026-07-16-empower-icon-design.md`;
  plan: `docs/superpowers/plans/2026-07-16-empower-icon.md`.
