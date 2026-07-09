# Unit Gameplay & Recruitment — Design

**Date:** 2026-07-09
**Status:** Approved (brainstorm 2026-07-09)
**Why now:** M3's meta-unlocks need a robust gameplay surface to unlock content into. Units are
the weakest system today — recruited blindly (`recruitableUnits[0]`), played as an all-stats dump,
with `healAmount`/`numCrystals` and the enemy-card Influence button dead. This milestone makes
units a first-class configurable system and finishes recruit-via-influence.

## Goals
1. **Configurable unit options** — a unit is a list of authored options (like a Choice card's
   flags); the player picks exactly one per use via a card-style pop-out.
2. **Crystal-gated options** — an option may require spending 1 crystal of a color; units become
   both a crystal *source* (Crystallize options) and a crystal *sink* (costed options) — pillar 3.
3. **Recruit enemies with Influence** — pay a `canInfluence` enemy's cost to resolve the fight
   wound-free with rewards; with the **Charismatic** passive skill, a recruitable enemy instead
   joins your army (rewards + unit).
4. **Real town recruiting** — pick which unit to hire from a panel, at per-unit influence prices.

## Non-goals
- Controller navigation for the town menu / combat canvas (later phase per the controller roadmap;
  new UI is built nav-ready).
- The meta-unlock pool itself (M3 proper).
- Unit empower toggles mirroring cards (per-option crystal costs replace `empowerType`).

---

## 1. Data model

### `UnitOption` (new, `[Serializable]`)
| Field | Type | Meaning |
|---|---|---|
| `effect` | `UnitEffect` enum: `Attack, Defend, Explore, Influence, Siege, Heal, Crystallize` | What the option does |
| `amount` | int | Stat points / wounds healed / crystals granted |
| `grantColor` | `EmpowerType` | Crystallize only: color of the granted crystal |
| `crystalCost` | `EmpowerType` | `None` = free; a color = requires 1 crystal of that color (wild counts — same matching as card empower); all-colors = any crystal |

### `UnitsSO`
- **Add** `options : List<UnitOption>` — the pop-out renders exactly these.
- **Add** `influenceCost : int` — the unit's recruit price at towns.
- **Retire** the flat fields as gameplay inputs: `attack/defend/explore/influence/siege/
  healAmount/numCrystals`, `cardType`, `empowerType`, and `GetUnitStats()`. Existing assets are
  migrated to `options` in the content pass. (`sprite`, `color`, `unitLetter`, `cardName`,
  `cardDescription`, `id` stay.)
- Using a unit exhausts it for the round regardless of which option was used; round-end
  `RefreshUnits()` is unchanged.

### `EnemiesSO`
- **Add** `recruitedUnit : UnitsSO` (optional). Null = pay-to-leave only; set = recruitable when
  the player owns Charismatic. `canInfluence` / `influenceCost` keep gating the action.

### `SkillsSO`
- **Add** a **passive** kind alongside per-turn/per-round: always-on, never exhausts, not
  clickable; renders on the skill bar as a badge. First passive: **Charismatic** — "You can
  recruit influenced enemies into your army." Enters the level-up skill pick pool.

### Design-bible framing
Units are a round-cadence hand of configurable one-shot effects. `options` is to units what
`cardType` + `isChoice` are to cards. Crystal-costed options extend the crystal-spend decision
space to the army (pillar 3).

---

## 2. Unit pop-out (mirrors the card pop-out — approved Approach 1)

**Trigger:** clicking / gamepad-confirming an un-exhausted unit token opens the pop-out — always,
even for single-option free units (one interaction model). Exhausted units show the existing
"already been played" validation message. Close/Back/click-off never costs anything.

**Presentation:** board scrim + panel fade (same grammar as `CardInspector`), sets
`InputContextState.Current = InputContext.Inspector` on open, back to `Board` on close.

**Layout:** unit identity (letter, name, description) above a vertical list of option rows —
"Attack 2", "Defend 6 — 1 Red crystal". Costed rows show the cost inline; unaffordable rows are
visible but locked (dimmed + reason). Exactly one row selected at a time; the first affordable row
is preselected. A **Use** button applies the selection; disabled while a locked row is focused.

**Components (new, mirroring the card stack):**
- `UnitInspector` (MonoBehaviour) — owns the in-progress use; `Open(Unit)`, `Close()`,
  `SelectOption(int)`, `Use()`; fires `Changed` for views. Handles crystal reservation.
- `UnitPlaySelection` (pure) — option list snapshot, selected index, per-row affordability
  (given crystal counts), resolved effect preview, `Describe()`. Testable in the mcs harness.
- Option-list view + Use bar (thin views rendering from the inspector, like
  `ChoiceBanner`/`PlayBar`).

**Crystal reservation:** selecting a costed row reserves a matching crystal via
`CrystalInventory` exactly as `CardInspector.SetEmpowered` does; switching rows / closing releases
it; on Use, ownership passes to the command's consume/undo path.

**Command & undo:** `UnitCommand` upgraded from the blind toggle-Raise to carry
`(unit, resolvedOption)`.
- **Execute:** apply the effect — action stats (incl. Siege) through the existing
  `AssignPlayerStats` path; Heal through the same wound-removal flow cards use; Crystallize
  through the same crystal-grant flow — consume the reserved crystal, rotate the token, set
  `IsPlayed = true`.
- **Undo:** symmetric revert — stats unassigned, wound restored, granted crystal removed, cost
  crystal refunded, token upright, `IsPlayed = false`.
- Turn-end stat reset and round-end refresh semantics are unchanged (a used unit stays exhausted
  after its stats reset at turn end).

---

## 3. Recruiting flows

### Enemy influence (combat)
Wire the existing dead `influenceButton` on `EnemyCard`:
- **Gate:** `canInfluence` && fight live (same aggro gate as Fight/Siege) && current Influence ≥
  `influenceCost`.
- **Label:** "Pay X" normally; "Recruit X" when `recruitedUnit != null` && player owns
  Charismatic — the outcome is legible before clicking.
- **Resolve (pay):** spend influence via the existing `Player.Influence` path (which clears the
  undo stack, consistent with all influence spends); the enemy resolves **wound-free** (no
  counterattack); **defeat rewards are granted** exactly as if defeated.
- **Resolve (recruit):** everything above **plus** `recruitedUnit` joins the army
  (rewards + unit — approved decision). At the army cap the disband picker opens first;
  cancelling the disband cancels the whole action with nothing spent.
- **Guardians:** the code path is uniform — a guardian with `canInfluence` can be influenced
  mid-assault; content authoring keeps most guardians `canInfluence = false`.
- `DisbandPanel` is generalized to be openable from combat (callback/payload instead of its
  town-only assumptions).

### Town recruit panel
- `RecruitButton` opens a **Recruit panel** listing the town's `recruitableUnits`: name, option
  summary, per-unit `influenceCost`; unaffordable entries dimmed.
- Picking a unit hires **that** unit and spends **its** cost. The town-event pair that hardwired
  `recruitableUnits[0]` + `recruitLevel` is replaced by a payload carrying the chosen `UnitsSO`
  (`Player.RecruitUnit(UnitsSO)`).
- Disband-at-cap chains in front unchanged; cancel is free.
- `recruitLevel` is **retired as the price**; the Recruit button enables when at least one listed
  unit is affordable.

---

## 4. Controller support

- **Unit pop-out:** reuses `InputContext.Inspector`. New pure `UnitNavRules`: up/down cycles
  option rows (locked rows are focusable so their cost/reason can be read; Use disabled on them);
  down past the last row → Use; Confirm = Use; Cancel = close.
- **Reaching unit tokens:** the Gameplay map gets a **Units lane** adjacent to the hand —
  direction past the hand's edge crosses into the units row (extends `HandFocusController`'s
  focus model). Keeps the Gameplay map + `InputContext` extensible per the controller roadmap.
- **Town recruit panel / combat influence button:** mouse-first this milestone, but built from
  standard `Button`s with explicit navigation so the later towns/combat controller pass needs no
  rework.

## 5. Saves

- Unit ownership already persists as an id list (`RebuildUnits`). **Add** `exhaustedUnitIds`
  (mirrors the skills' exhausted-id set) so mid-round saves restore turned units. Save version
  bump; migrator default = none exhausted.
- Charismatic needs nothing new — owned skills already persist; passive kind is content-side.
- New SO fields are content-side and invisible to saves; new units/enemies get ids and a
  `ContentRegistryPopulator` run.

## 6. Content pass (numbers per balance.md — starting values, tune in playtest)

- **Rework the 4 existing units** into multi-option configs, e.g.:
  - Knight — Defend 3 / Defend 6 (1 Red)
  - Warrior — Attack 2 / Siege 2 (1 Red)
  - Scout — Explore 2 / Explore 4 (1 Green)
  - Merchant — Influence 2 / Crystallize Yellow
- **~4–6 recruitable enemies** across doom tiers with linked unit forms (e.g. Bandit Footsoldier
  → Footsoldier unit); `canInfluence` + `influenceCost` + `recruitedUnit` set; most guardians stay
  non-influenceable.
- **Charismatic** `SkillsSO` asset added to the level-up pool (pool grows to 10).
- **Towns** get curated `recruitableUnits` lists with per-unit costs (bands: cheap 2–3, standard
  3–4, premium 5+ influence).
- **Design-bible updates:** mechanics.md (unit options, enemy influence, passive skills),
  content-rules.md (new `UnitsSO`/`EnemiesSO`/`SkillsSO` schemas), balance.md (unit cost bands,
  option value guidance: costed options ≈ 2× their free sibling), decisions-log entries.

## 7. Testing & delivery split

- **Pure tests (mcs harness):** `UnitPlaySelection` (affordability, resolve, preview, describe),
  `UnitNavRules` (cycle/clamp/drop-to-Use), recruit-affordability rules; save round-trip /
  migrator tests for `exhaustedUnitIds`.
- **Manual wiring by the user in the editor** (per the established split — no hand-edited scene
  YAML): unit pop-out panel, recruit panel, units focus lane, influence button listener, skill-bar
  passive badge. Delivered as step-by-step instructions.
- **Playtest checklist:** pay-to-leave (rewards, wound-free), recruit with Charismatic (+cap
  disband, +cancel), costed option with/without matching crystal, undo each option type, exhausted
  state across turn/round ends and save/load, town panel per-unit pricing.

## Decisions log (to append to `decisions-log.md`)
- 2026-07-09 — Enemy influence: pay-to-leave is free of gates for `canInfluence` enemies and
  grants defeat rewards; recruiting requires the Charismatic passive and grants rewards + unit.
- 2026-07-09 — Units become option-lists (`UnitOption`); legacy flat unit stats and unit
  `empowerType` retired; per-option crystal costs (color-matched, wild counts).
- 2026-07-09 — Crystallize options grant a per-option authored color.
- 2026-07-09 — Unit use always opens the pop-out (single interaction model).
- 2026-07-09 — Town recruiting: choice panel with per-unit influence prices; `recruitLevel`
  retired as the price.
- 2026-07-09 — Skills gain a passive kind; Charismatic is the first passive.
