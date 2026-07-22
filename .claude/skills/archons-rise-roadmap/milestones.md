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

## M2.4 — Level-up rewards  ✅ DONE (2026-07-07)
**Goal:** make leveling pay out — the progression a win/lose loop needs to matter.
**Scope:**
- **Fixed reward table** (`LevelRewardsSO`): skill picks, card picks (existing choose-1-of-3
  screen), +HP (toughness), +hand size, +army size — all per-level counts, inspector-tunable.
- **Skills** (`SkillsSO` + skill bar): pick 1 of 3 on skill levels; exhaust/refresh per turn or
  per round; undoable activation.
- **Army cap** (starts 1) + disband-to-hire at cap; pure `LevelRules`/`ArmyRules` classes.
- **Save schema v3**: owned + exhausted skill ids (hand/army derived from level, not stored).
- Exp overflow carries over instead of being discarded.

Spec: `docs/superpowers/specs/2026-07-06-level-up-rewards-design.md`.
**Acceptance:** leveling to 2 offers a 3-skill pick usable on its cadence with undo; level 4
grants +1 hand and +1 army; recruit at cap forces disband-to-hire; skills survive save/load. ✅
(Authored table shifted some rows vs the plan during balancing — army cap now grows at 3/6/9;
level-up picks queue behind any open card reward canvas. Unified reward arbiter deferred.)

## M2.5 — Win/lose systems
**Goal:** make a run winnable and losable.
**Scope:**
- **Victory** — conquer **2 Castles** (`ConquestTracker.ConqueredCastleCount()`).
- **Doom Clock** — rises each round; reaching max loses the run.
- **Wound-out** — lose when Wounds ≥ threshold.
- **Game-over screen** for both outcomes.

**Acceptance:** a run can be won by conquering 2 Castles and lost by clock-max or wound-out.

## M2.75 — Unit gameplay & recruitment
**Goal:** units become meaningful, configurable board pieces and recruiting becomes a real choice.
**Scope:**
- **Units as option-lists** — `UnitsSO.options` (`UnitOption`) played through a card-style pop-out,
  including **crystal-costed options** (color-matched, wild counts); legacy flat unit stats retired.
- **Enemy influence** — pay a `canInfluence` enemy off (wound-free + rewards), or **recruit** it with
  the **Charismatic** passive (`recruitedUnit`).
- **Town recruit panel** — pick which unit at **per-unit influence prices** (`recruitLevel` retired).
- **Passive skills** — new `SkillCadence.Passive`; Charismatic is the first.
- **Save schema v5** — persist exhausted units (`unitExhausted`, parallel to `unitIds`).

**Acceptance:** units play through the option pop-out incl. crystal-costed options; enemies can be
paid off / recruited with Charismatic; towns recruit via panel at per-unit prices.
Spec: `docs/superpowers/specs/2026-07-09-unit-gameplay-and-recruitment-design.md`.

## M2.9 — Map Dungeons + RewardQueue ✅ (2026-07-14)
**Goal:** dungeons become **map places** — spaced hexes entered by standing on the cell — with
tiered delves, completion-gated bundles, doom-band flagging, and all reward modals serialized
through one unified `RewardQueue`. Replaces the never-wired card-based flow.
**Scope (shipped):**
- **6 spaced dungeon hexes** per map (`DungeonRuleTile`/`DungeonToken`, seeded placement, spawn
  blocking, never on towns or the start ring).
- **3 tiered delves** each: one Explore spend per delve, one authored enemy (tier 1/2/3) under
  normal field rules (wounds, flee = 1 wound); fights pay **experience only**.
- **Guaranteed completion bundle** (exp + `rewardCount` crystals + `rewardCount` card picks) and
  **Doom relief** on clearing the third delve.
- **Doom-band flags:** first entry into the mid/high band flags a random uncleared dungeon (+1
  doom/round until cleared; larger relief when a flagged dungeon is cleared).
- **Unified `RewardQueue`** — every card/skill/message modal opens one at a time; the M2.4 busy-wait
  is deleted. Save schema **v6** persists dungeon progress + flag state.

**Acceptance (met):** 6 spaced dungeons per map; stand-on-cell entry opens the panel; delves spend
Explore and pay exp only; completion pays the guaranteed bundle and drops doom; flags fire on band
entry and tick doom; progress saves/restores; no overlapping modals. **Precedes M3.**

## M2.10 — Stat Conversion, Refresh & Influence Options ✅ (2026-07-14)
**Goal:** three additive strategy mechanics on the existing apply/revert path, no save schema bump.
**Scope (shipped):**
- **1:1 stat conversion** — opt-in on cards (`ConvertBanner` inspector toggle) and via the
  `ConvertStat` skill; four action stats only; fully undoable. Pure `ConvertRules` (TDD).
  Content: Shield Bash, Rally to the Banner, Tactician skill.
- **Mid-round unit Refresh** — a budgeted `UnitPickerPanel` modal (opens directly, not via
  `RewardQueue`) driven by a `Refresh` card (`Mobilize`) or `RefreshUnits` skill; budget spent across
  units by recruit cost, unspent lost, fizzles when nothing affordable. Pure `RefreshRules` (TDD).
- **Influence-costed unit options** — one cost type per option (crystal OR influence OR free), with an
  undoable in-turn Influence spend and affordability lock.

**Acceptance (met):** converter cards/skills convert the whole pool and undo exactly; Refresh lists
spent units with costs, locks over-budget entries, fizzles with none spent, and undo re-exhausts the
picked units; influence options price/lock/spend/refund correctly; the full EditMode suite (incl.
`ConvertRulesTests`, `RefreshRulesTests`, `UnitOptionTextTests`) is green; save/load mid-run restores
units/skills/exhaust state. **Precedes M3.**

## M2.11 — UI language & iconography — ✅ code complete 2026-07-15 (editor acceptance pending)
**Status:** All code shipped and TDD-green (`IconMarkupTests`, `UnitOptionTextTests` via the mcs
harness). Remaining before full sign-off is **USER editor work**: authoring the 9 new TMP sprite
assets + the `IconRegistry.asset`, adding the `CanvasGroup`/`lockGroup` wiring, fixing any flagged
card descriptions, and the play-mode acceptance audit (`IconRegistryValidationTests` green).
**Deviation:** the spec's `CostRow` MonoBehaviour was replaced by a pure text-only `IconMarkup`
formatter (`ArchonsRise.UiLanguage`) — mcs/EditMode-testable, no new prefab, reuses the existing
`<sprite>`-in-description convention. See the 2026-07-15 decisions-log entry.
**Goal:** one icon per core concept and one layout dialect on every panel — make the game
legible to a stranger; prerequisite for M2.12's icon-inline teaching text.
**Scope:**
- **`IconRegistrySO`** — canonical concept → sprite + TMP tag mapping (8 stats, crystal
  colors + wild, Doom, XP, HP, army, place types, Dungeon); static `IconRegistry.Get`.
- **Layout language:** costs always `[icon][number]` (shared `CostRow` prefab); action
  buttons `[icon] Label`; fixed stat order (Atk/Def/Exp/Inf); one global locked/unaffordable
  treatment.
- **Audit sweep** of TownMenu, Recruit, Disband, Dungeon, UnitPicker, EnemyPreview,
  CardMenu, RewardCanvas, skill bar, HUD, run-end screen (surgical — conforming panels
  untouched).
- **Validation tests:** registry complete; every TMP tag resolves.

**Acceptance:** no bare-number cost on any audited panel; `<sprite name=...>` renders the
HUD's glyph for every concept; validation suite green.
Spec: `docs/superpowers/specs/2026-07-15-m2.11-ui-language-iconography-design.md`.

## M2.12 — Tutorial & help system — ✅ code complete 2026-07-16 (editor wiring + acceptance pending)
**Status:** All code shipped and TDD-green (`TutorialRulesTests` 13/13 + the starter-guarantee
additions to `SpawnRulesTests`, both via the mcs harness; `TutorialCopyValidationTests` pins authored
copy from the editor). Remaining before sign-off is **USER editor work** (plan Tasks 8 + 10): build
the TutorialCanvas (manager + banner + highlight frame + help popup), author the content assets
(8 rail steps, 7 one-shots, 11 help entries, 7 VoidEvents), wire the 12 listeners + `TutorialTarget`
drops + 11 `HelpIcon`s + MainMenu controls, then run the acceptance checklist.
**Deviations from the plan/spec:** the spec's step "GameEvent reference" ships as a stable **event-id
string** wired via Static-mode listeners (the typed generic bus can't be referenced polymorphically —
see the 2026-07-16 decisions-log entry); one-shots reuse the rail banner (Next relabeled "Got it")
rather than a second prefab; the doom one-shot fires on the **mid** band (`lowBandMax` crossing, where
flags first fire).
**Goal:** external playtesters learn unassisted — guided first round on the real first run,
contextual help everywhere, fully optional. Driver for the playtest handoff.
**Scope:**
- **TutorialCanvas** (Screen Space – Camera, above all) with step banner + Skip, pulsing
  highlight frame (`TutorialTarget` id lookup), shared help popup.
- **Guided rail** — ordered `TutorialStepSO`s advancing on real GameEvents (welcome/doom,
  play card, pools, move, enemy preview, fight, end turn, send-off); out-of-order tolerant;
  no input locking.
- **Reactive one-shots** (`TutorialOneShotSO`, once per profile, deferred past the rail):
  first wound/crystal/level-up/town/dungeon, deck-can't-refill, doom band.
- **`HelpEntrySO` + ? icon** on every major panel (pulses until first read).
- **PlayerPrefs persistence** (no save-schema bump); settings toggle + reset.
- **Starter-enemy spawn guarantee** — ≥1 tier-1 enemy near the start ring (pure rule + tests).

**Acceptance:** fresh profile → rail teaches round 1 hands-off and never stalls; skip/toggle/
reset/resume all work; every panel's ? opens icon-inline copy; one-shots fire exactly once;
20/20 maps pass the starter-enemy check; EditMode suites green.
Spec: `docs/superpowers/specs/2026-07-15-m2.12-tutorial-help-design.md`.

## M2.13 — Turn phases & shrinking rounds — ✅ shipped 2026-07-21 (final play-through acceptance = last gate)
**Status:** Code + editor wiring committed; pure logic TDD-green via the mcs harness
(`TurnPhaseRulesTests` 3/3, `RoundRulesTests` 4/4, `DoomRulesTests.TurnsForBand`, `TurnButtonGateTests`
2/2). Editor work done and committed: the two events + wired `TurnPhaseController`, `DoomTuning`
band-turn fields (6/4/3), End Round button removed, `PhaseHud` + phase label + its two listeners,
and the rail phase copy authored (move/fight/end-turn) — the `TutorialCopyValidationTests`
`PhaseRailStepsTeach…` pin is satisfied. **Post-wiring refinements:** the End Turn button caption
flips to **"End the Day"** when the next press ends the round (`EndTurnButton.UpdateLabel` off
`RoundRules.IsRoundOver`); the stale "End the Round" empty-deck message was removed (auto-rest
handles it); and the manual **click-the-deck-to-draw** path plus its now-dead code (`CardDrawCommand`,
`PlayerDeck.DataToDrawnCard`) were deleted. Remaining: the USER's full EditMode-suite run in Test
Runner + the play-through checklist (both inherently in-editor).
**Goal:** restructure a turn into a strict **Explore → Action → End** sequence with a one-encounter
cap, make the round a **Doom-band-scaled "day"** that auto-ends, make movement undoable, and surface
a phase + day-countdown HUD.
**Scope:**
- Pure rules: `TurnPhase`/`TurnPhaseRules` (move/interact/commit gating), `RoundRules` (day-budget
  math), `DoomRules.TurnsForBand` (per-band `turnsPerRound` 6/4/3).
- `TurnPhaseController` scene singleton owns phase / action-taken / turns-remaining; reuses the
  existing `endTheTurn` / `endTheRound` chains; raises `onPhaseChanged` / `onTurnsRemainingChanged`.
- Undoable `MoveCommand`; the fog-reveal branch commits the stack instead.
- One action per turn via `BeginAction` (combat / place visit / dungeon delve).
- End Turn routes through the controller and auto-ends the round; **End Round removed**.
- Event-driven day-countdown + phase HUD (`PhaseHud`); no per-frame Round/Turn text.
- Save/load rides the existing turn slot for the day budget; phase resets to Explore (no schema bump).

**Acceptance:** turn starts in Explore (move undoable; fog-reveal not); one action blocks a second +
further movement; a place visit allows all its services; End Turn decrements the day and the label
tracks phase; day auto-ends at 0 (reshuffle + Doom tick + refresh) with budget 6→4→3 across bands;
empty deck ends the round on End Turn; save/reload restores the day and lands in Explore; the rail
teaches the three phases + the day. EditMode suites green.
Spec: `docs/superpowers/specs/2026-07-21-turn-phase-system.md`; plan:
`docs/superpowers/plans/2026-07-21-turn-phase-system.md`. **Spec 2 — multi-enemy phased combat** is
queued next.

## M2.14 — Multi-enemy phased combat — ✅ code complete 2026-07-22 (editor wiring + play acceptance done)
**Status:** All C# committed; pure logic TDD-green via the mcs harness (`CombatPhaseRulesTests` 3/3,
`CombatRulesTests` 10/10 incl. `GroupWoundCount`, `DefeatFxMathTests` 4/4). Editor work (Task 10/11)
wired by the USER and play-confirmed: `CombatController` scene object, `onCombatPhaseChanged` event,
the Flee→multi-purpose button (`OnMultiButton`), the shared `PhaseHud` label driving combat
sub-phases, the `UI/EnemyCardDissolve` material + `EnemyCardDefeatFx` on the prefab, and the guardian
side-by-side layout.
**Goal:** replace single-enemy combat with a phased **Siege → Defend → Attack → auto-flee** engine
shared by field, dungeon, and guardian fights; spawn a guarded place's whole remaining roster at
once; and add Balatro-style defeat juice.
**Scope:**
- Pure rules: `CombatPhase`/`CombatPhaseRules` (phase gating + button label), `CombatRules.GroupWoundCount`
  (summed group counterattack), `DefeatFxMath` (shake envelope + dissolve ramp).
- `CombatController` scene singleton owns the phase machine, the logical live-enemy set, the per-fight
  context + source token, one multi-purpose button, and the deferred reward tally.
- `EnemyCardDefeatFx` component + hand-written `UI/EnemyCardDissolve` shader (procedural noise) for the
  two-track defeat FX; the fight holds open until the FX finishes.
- Simultaneous guardians via `OpenFight`; per-kill banking + 3-wound resumable retreat.
- Field/dungeon/guardian win-teardown folded into the controller; standalone `Flee`/`ResolveDefeat`
  retired; shared `PhaseHud` label reused for the sub-phases (returns to Action on resolve).
**Acceptance:** Siege phase gates Siege/Influence + Engage; Engage clears Siege and opens Defend; the
Defend press resolves the summed counterattack (wounds for the shortfall) → Attack; Fight kills
dissolve, Influence fades, canvas stays open through the FX then pays rewards; a Castle spawns both
guardians at once, Withdraw keeps progress, re-entry spawns only survivors, clearing both conquers
(+ victory on the 2nd); dungeon delves stay exp-only and complete on the last slot; field kills stay
dead across reload. Pure suites green.
Spec: `docs/superpowers/specs/2026-07-21-*` (Spec 2); plan:
`docs/superpowers/plans/2026-07-22-multi-enemy-phased-combat.md`.

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
