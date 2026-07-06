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
  rewards; Siege is scarce (advanced cards/units only, never improvisable),
- **Recruit** units at a town (spending Influence) — up to the **army cap** (starts at 1, raised
  by level-ups; at cap, hiring requires disbanding an existing unit),
- **Move/explore** further (spending Explore).

Gain rewards (experience, crystals, cards) from defeating enemies and clearing dungeons, level up,
and grow your deck — all while the **Doom Clock** rises each round. The run ends when you meet the
Archon threshold (**win**) or trigger a loss condition.

## Win — Conquer 2 Castles
Map places are typed: **Town / Keep / Castle** (+ existing Dungeons). Guarded places (Keep 1
guardian, Castle 2 — data-driven rosters) must have **all guardians defeated in order** to be
conquered; defeated guardians never respawn, so conquest is resumable. Retreating from an assault
in progress costs **3 wounds** (field-combat flee stays 1); closing a place's menu without
assaulting is free. Services gate by type (Town: Recruit+Heal; Keep: Recruit; Castle:
Recruit+Heal+Cards) and open only once the place is conquered (Towns, guardian-less, open
immediately). Places are entered by **standing on their cell** — adjacent interaction is not
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

## Turn / Round Flow
Within a **turn**, the player plays cards to build up the four stats, takes actions, then ends the
turn — at which point the action stats reset to 0 (matches existing `Player.TurnEnd`) and the hand
tops up to hand size from the deck. **Rounds** group turns and are the cadence on which the Doom
Clock advances (matches the existing `GameManager` round/turn counters). Ending a round is a **full
hand reset**: the discard pile and all unplayed hand cards return to the deck, the deck is shuffled,
and a fresh full hand is drawn (decision 2026-07-02). Units exhaust when used (turned sideways)
and all refresh when a new round starts. When the deck can't refill the hand, the turn cannot be
ended — the End Turn button disables and the player must end the round. Neither the turn nor the
round can end mid-combat: both buttons disable while a fight is active.

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

## Enemy Preview
- **Enemy preview** — hovering (later, controller-focusing) a map enemy token or a guarded place's
  Assault button previews the enemy's stats (name, Attack, HP, Influence cost) before you commit to
  combat; a guarded place previews all *remaining* guardians. Preview is read-only — it starts no
  fight. Visibility passes through a single blind gate (`PreviewRules.CanPreview`): today every enemy
  is visible, but a future mechanic can blind an encounter, replacing the whole panel with "You
  cannot see the enemy/enemies you are about to confront."

## Empower / Crystal Economy
A card may be **Empowered** by spending one **Crystal** of the card's color
(Red / Yellow / Green / Purple — the `EmpowerType`). When empowered, the card yields its stronger
`empower*` values instead of its base values. Crystals are limited and gained from rewards and
crystal-granting cards, so empowering is a deliberate tactical choice (pillar 3).

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
command stack, like card plays. Skills are acquired only via level-up picks; the pool is defined
on `LevelRewardsSO` (spec: `docs/superpowers/specs/2026-07-06-level-up-rewards-design.md`).
