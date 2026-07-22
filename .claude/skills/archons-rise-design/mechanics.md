# Mechanics

The locked mechanics of Archon's Rise. Tuning values (counts, rates, curves) live in
[balance.md](balance.md); this file defines *how the systems work*, not their numbers.

## Run Loop
Start a run with a fresh starting deck. Explore the randomized hex map by spending **Explore**,
revealing locations (towns, enemies, dungeons). Each turn, play cards from hand to generate the
four action stats, then act:
- **Fight** an enemy — two forms: **Normal** (spends the Attack pool vs the enemy's HP; the enemy's
  counterattack can wound you when Defend falls short) or **Siege** (spends the separate Siege pool
  vs the enemy's HP; always wound-free — the counterattack is skipped). Both grant identical
  rewards; Siege is scarce (advanced cards/units only, never improvisable). A third resolution,
  **Influence**, applies to enemies flagged `canInfluence`: paying the enemy's Influence cost ends
  the fight **wound-free and still grants the defeat rewards** (no counterattack runs). If the enemy
  has a `recruitedUnit` **and** the player owns the **Charismatic** passive, the same payment also
  adds that unit to the army (rewards + unit); otherwise influence is pay-to-leave only. At the army
  cap a recruit-influence opens the disband picker first; cancelling spends nothing. These resolve
  through the phased, multi-enemy combat engine (Siege → Defend → Attack) — see **Combat** below,
- **Recruit** units at a town (spending Influence) — a **recruit panel** lists the town's units,
  each at **its own Influence price** (per-unit, not a flat town rate); unaffordable entries show
  disabled. Recruiting is capped by the **army cap** (starts at 1, raised by level-ups; at cap,
  hiring first opens the disband picker to make room — cancelling it spends nothing),
- **Move/explore** further (spending Explore),
- **Enter a dungeon** by **standing on its cell** (place-like, not adjacent) — see [Dungeons](#dungeons).

Gain rewards (experience, crystals, cards) from defeating enemies and clearing dungeons, level up,
and grow your deck — all while the **Doom Clock** rises each round. The run ends when you meet the
Archon threshold (**win**) or trigger a loss condition.

## Combat — phased, multi-enemy (spec 2026-07-21, Spec 2)
Field, dungeon, and guardian fights all run through one phased engine (`CombatController`) with a
**single multi-purpose button** whose caption tracks the phase. A guarded place spawns its **whole
remaining roster at once** (simultaneous guardians), not one at a time; field and dungeon fights are
single-enemy. Combat stays in the board scene.

The phases (button caption in **bold**):
- **Siege** (**Engage**) — the pre-commit phase. Per-enemy **Siege** (spends the Siege pool, wound-free)
  and **Influence** (pay `canInfluence` cost, wound-free, still grants rewards; recruits with
  Charismatic + `recruitedUnit`) remove enemies *before* the counterattack. Thinning the roster here
  shrinks the coming counterattack. Pressing **Engage** commits: the **Siege pool is cleared** (a
  Siege-phase-only currency) and combat enters Defend. No wounds yet.
- **Defend** (**Defend**) — a window to play defense cards and build the Defend pool. Pressing
  **Defend** resolves the **group counterattack**: every surviving enemy's Attack is **summed into
  one comparison** against Defend, and the shortfall becomes Wounds in HP-sized bites
  (`CombatRules.GroupWoundCount`). Then combat enters Attack.
- **Attack** (**Withdraw**) — spend the **Attack** pool to defeat remaining enemies (the counterattack
  already happened, so normal kills here are wound-free at point of use). **Withdraw** flees: field/
  dungeon costs **1 wound**, a guardian assault costs **3 wounds** with conquest progress kept.
- **Resolved** — the fight is over; the roster is cleared (win) or the player withdrew.

Each defeat is **banked immediately** (logical removal, guardian conquest record, field save-cell/
token teardown, dungeon depth tracking, reward captured) but reward **messages + card picks are paid
at fight-end**, serialized through `RewardQueue`, so a mid-fight kill never pops a modal that
interrupts a Siege/Attack decision. Defeats play a **two-track FX** — a shake→dissolve for Siege/
Attack kills, a fade-and-drift for Influence — and the fight **holds the canvas open until the FX
finishes** before closing. The shared HUD phase label doubles as the combat sub-phase readout
(**Siege/Defend/Attack**), returning to **Action** when combat resolves.

## Win — Conquer 2 Castles
Map places are typed: **Town / Keep / Castle** (+ existing Dungeons). Guarded places (Keep 1
guardian, Castle 2 — data-driven rosters) must have **all guardians defeated in order** to be
conquered; defeated guardians never respawn, so conquest is resumable. Retreating from an assault
in progress costs **3 wounds** (field-combat flee stays 1); closing a place's menu without
assaulting is free. Services gate by type (Town: Recruit+Heal; Keep: Recruit; Castle:
Recruit+Heal+Cards) plus **Crystal purchase, offered at every Place** (decision 2026-07-10 —
Influence, not place type, limits how many crystals you buy), and open only once the place is
conquered (Towns, guardian-less, open immediately). Places are entered by **standing on their cell** — adjacent interaction is not
allowed (unlike enemies). **Conquer 2 Castles to win** — territory is the sole win axis, no
Level/Influence gate. Exact rosters and castle count are tuning — see [balance.md](balance.md).

## Lose — Wounds (tactical)
Losing a fight (insufficient Defend vs the enemy's Attack) shuffles **Wound** cards into the deck.
Wounds are dead draws that clog the deck; accumulating too many — a count threshold (see
[balance.md](balance.md)) — ends the run. **Heal/Mend** cards remove Wounds, so wound management is
an ongoing tactical cost.

**HP is toughness, not a health pool** (decision 2026-07-06): HP divides the Defend shortfall into
HP-sized bites, one Wound per bite (`CombatRules.WoundCount`). HP never depletes and is not a loss
axis — raising it via level-ups means each bad fight inflicts fewer Wounds.

## Lose — Doom Clock (strategic)
A corruption/threat value rises every round (rate in [balance.md](balance.md)); some events push it
further. If it reaches its maximum before the Archon threshold is met, the land falls and the run is
lost. The Doom Clock is the strategic pressure that forces the player to *rise fast*. This is a
**new system to build** (roadmap milestone M2).

## Turn / Round Flow (spec 2026-07-21, M2.13)
A **turn** runs a strict **Explore → Action → End** sequence (`TurnPhase`, `TurnPhaseRules`):

- **Explore:** the player plays cards to build stats and spends Explore to move and uncover the map.
  Movement is **undoable** (`MoveCommand` on the undo stack) — *except* a step that reveals new fog,
  which commits the stack (revealed knowledge can't be un-known; `ShouldCommitOnMove`).
- **Action:** the player takes **exactly one** encounter per turn — a fight, a place visit, or a
  dungeon delve. Taking the action is the **implicit Explore→Action transition** (`BeginAction`):
  it commits the movement stack and locks further movement for the turn. **Opening a place or
  dungeon menu is a free peek** (spec 2026-07-22) — the action is spent only by the first service
  *committed* inside (an assault, a heal/recruit/crystal purchase, or pressing Delve), so you can
  open a menu to see what's on offer and close it without cost. A whole place visit still counts as
  the one action: once the first service commits, the rest of that same open menu is free; reopening
  after the action is spent is peek-only (its service buttons lock). A second interaction is refused.
- **End:** **End Turn** is the only turn-flow control (End Round is gone). It resets action stats to
  0, tops up the hand, and advances the day.

A **round is a "day"** whose length scales with the Doom band (`DoomRules.TurnsForBand`,
`RoundRules`): **6** turns in the low band, **4** mid, **3** high — the day shrinks as Doom climbs.
The day **auto-ends** when its turn budget is spent **or** the deck can no longer refill the hand
(a forced rest, so a short deck can't strand the player mid-day). A day's end is a **full hand
reset**: discard + unplayed hand cards return to the deck, the deck is shuffled, a fresh hand is
drawn (decision 2026-07-02), the Doom Clock ticks, and units/skills refresh. Combat still blocks
End Turn while a fight is active. Phase is **not** saved — a loaded run always resumes at Explore
(the remaining day budget rides the existing turn save slot; no schema bump).

## Units
Recruited units are **configurable option-lists**, not fixed stat blocks. Each unit (`UnitsSO`)
carries an authored list of **options** (`UnitOption`); clicking or focusing a unit opens a
card-style **pop-out** rendering exactly those options. Each option is one effect — Attack, Defend,
Explore, Influence, Siege, Heal, or Crystallize — at an authored amount, and may carry a **crystal
cost**. A costed option requires spending one crystal that satisfies the cost (exact color, or a
wild crystal — the same matching rule as card Empower; an all-colors cost accepts any crystal). The
pop-out shows unaffordable options as **locked** (dimmed): they are still focusable so the player
can read the cost, but Use refuses them. Using **any** option applies its effect and **exhausts**
the unit for the round (turned sideways). Costed options reserve/spend the crystal exactly like card
empower, and the whole use is a single undoable command (undo reverts the stat/heal/crystallize and
refunds the crystal). Units all refresh at round start.

An option may instead carry an **Influence cost** (spec 2026-07-14): an in-turn tactical spend
(deducted from the banked Influence pool, undoable — not a permanent purchase). An option costs a
**crystal OR Influence OR is free — never both**; stronger variants are authored as separate option
rows. Unaffordable-by-Influence options lock exactly like crystal-costed ones (Use shows "Needs
influence").

**Mid-round Refresh** (spec 2026-07-14): a `Refresh` card or a `RefreshUnits` skill can **ready spent
units mid-round**, budgeted by recruit value. Refresh N is a **budget spent across multiple units**:
a modal picker lists only spent units, each showing its cost (the unit's recruit `influenceCost`,
minimum 1); picking one readies it immediately and deducts its cost from the budget; entries over the
remaining budget show disabled. Unspent budget is **lost** (no banking). If nothing spent is
affordable at play time the effect **fizzles** (so refresh cards pair a small secondary stat to never
be a dead play). The picker is **not** a reward modal — it opens directly, never through the
`RewardQueue`. Undo of the play re-exhausts exactly the units it readied.

## Stats
Eight stat types (the `StatType` flags in code):
- **Attack** — defeats enemies (Attack ≥ enemy HP).
- **Defend** — absorbs enemy Attack; shortfall becomes Wounds.
- **Explore** — moves across the hex map and into dungeons.
- **Influence** — recruits units and is part of the Archon win threshold.
- **Heal** — removes Wounds.
- **Wound** — the penalty card type added on combat loss.
- **Crystal** — the resource that fuels Empower.
- **Siege** — a wound-free attack; defeats an enemy (Siege ≥ enemy HP) without taking the
  counterattack. Cannot be improvised; comes only from advanced cards and units (advanced Siege
  cards always also carry the Attack flag, so Siege only matters in combat).

### Conversion (spec 2026-07-14)
Some cards and skills can **convert** banked stat pools **1:1** into another stat: every point of a
source stat becomes one point of the target. Conversion touches the **four action stats only**
(Attack/Defend/Explore/Influence) — Siege/Heal/Crystal/Wound never participate (Siege's scarcity is a
pillar). It is **opt-in at play time**: a converter card shows an inspector toggle (off by default,
and locked while improvising or, for empower-gated converters, until the play is empowered); a
convert **skill** is opt-in simply because clicking it is the choice. A conversion drains the
**entire current pool** of each flagged source into the target (4 Defend banked + convert Defend→Attack
= +4 Attack, Defend 0), and is fully undoable — undo restores the exact pools moved. A card is never
both a converter and an `isChoice` card, and a converter's target is never one of its own sources.

## Enemy Preview
- **Enemy preview** — hovering (later, controller-focusing) a map enemy token or a guarded place's
  Assault button previews the enemy's stats (name, Attack, HP, Influence cost) before you commit to
  combat; a guarded place previews all *remaining* guardians. Preview is read-only — it starts no
  fight. Visibility passes through a single blind gate (`PreviewRules.CanPreview`): today every enemy
  is visible, but a future mechanic can blind an encounter, replacing the whole panel with "You
  cannot see the enemy/enemies you are about to confront."

## Dungeons
Map places (6 per map — count is tuning, see [balance.md](balance.md)), entered by **standing
on the cell** like other places. A dungeon is **three tiered delves**: each delve spends the
dungeon's flat `exploreCost` and fights one authored enemy — tier 1, then 2, then 3 — under
normal field-combat rules (wounds, flee = 1 Wound). Defeated slots never respawn; progress
persists and saves. Fights inside grant **experience only**; completing the third delve pays a
**guaranteed bundle** (exp roll + `rewardCount` crystals + `rewardCount` card picks at the
dungeon's `tier`) and **lowers the Doom Clock**. When doom first enters the mid/high band, a
random uncleared dungeon becomes **flagged** (once per band per run): each flagged dungeon adds
**+1 doom per round** until cleared, and clearing a flagged dungeon grants a larger doom
reduction. All reward/message modals resolve one at a time through the **RewardQueue**.

## Empower / Crystal Economy
A card may be **Empowered** by spending one **Crystal** of the card's color
(Red / Yellow / Green / Purple — the `EmpowerType`). When empowered, the card yields its stronger
`empower*` values instead of its base values. Crystals are limited and gained from rewards,
crystal-granting cards, and **purchase at any conquered Place** (spend Influence, pick the color;
per-crystal price is the Place's `resourceLevel` — decision 2026-07-10), so empowering is a
deliberate tactical choice (pillar 3).

## Leveling
Experience accrues toward `expToNextLevel`. On level-up, rewards come from a **fixed, data-driven
reward table** (`LevelRewardsSO` — decision 2026-07-06, replacing the older even/odd scheme).
Three reward kinds:
- **Skill pick** — choose 1 of 3 random unowned skills from the pool (see Skills below).
- **+1 hand size** at milestone levels.
- **+1 army size** at milestone levels (raises the recruit cap).
- **+1 HP** at milestone levels (toughness — fewer Wounds per bad fight).

Hand size and army cap are **derived from level + table**, never stored in saves. The experience
curve and the table itself are tuning — see [balance.md](balance.md).

## Skills
Level-up rewards distinct from cards: activatable abilities on a persistent **skill bar**. A skill
is clicked to apply its effect (gain a stat, a crystal, or heal a wound), then **exhausts** until
its cadence refreshes it — **per-turn** skills refresh at turn end (weak effects), **per-round**
skills at round end (strong effects, e.g. crystals and healing). Activation is undoable via the
command stack, like card plays. A third cadence, **passive**, has no activatable effect: passive
skills are never clicked or exhausted — their state is simply *queried* by the systems they gate
(e.g. **Charismatic** = `SkillEffect.RecruitEnemies`, which lets influenced enemies be recruited).
Skills are acquired only via level-up picks; the pool is defined on `LevelRewardsSO`
(spec: `docs/superpowers/specs/2026-07-06-level-up-rewards-design.md`).

Two additional effects (spec 2026-07-14): **`ConvertStat`** converts banked pools 1:1 from the
skill's `convertFrom` to `convertTo` (see [Conversion](#conversion-spec-2026-07-14)); **`RefreshUnits`**
opens the refresh picker with `magnitude` as the budget (see mid-round Refresh under [Units](#units)).
Both are undoable like any skill activation.
