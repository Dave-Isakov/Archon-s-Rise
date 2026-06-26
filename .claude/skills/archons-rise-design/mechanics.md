# Mechanics

The locked mechanics of Archon's Rise. Tuning values (counts, rates, curves) live in
[balance.md](balance.md); this file defines *how the systems work*, not their numbers.

## Run Loop
Start a run with a fresh starting deck. Explore the randomized hex map by spending **Explore**,
revealing locations (towns, enemies, dungeons). Each turn, play cards from hand to generate the
four action stats, then act:
- **Fight** an enemy (your Attack vs the enemy's HP),
- **Recruit** units at a town (spending Influence),
- **Move/explore** further (spending Explore).

Gain rewards (experience, crystals, cards) from defeating enemies and clearing dungeons, level up,
and grow your deck — all while the **Doom Clock** rises each round. The run ends when you meet the
Archon threshold (**win**) or trigger a loss condition.

## Win — Rise to Archon
A run is won by a **domination threshold**, not a single boss kill: control **N towns** AND reach a
**Level / Influence target**. The moment both are satisfied, the player is crowned Archon and the
run is won. Exact N and targets are tuning — see [balance.md](balance.md). This pillar is why
Influence, towns, and leveling are first-class systems.

## Lose — Wounds (tactical)
Losing a fight (insufficient Defend vs the enemy's Attack) shuffles **Wound** cards into the deck.
Wounds are dead draws that clog the deck; accumulating too many — a count threshold or HP reduced
to zero (see [balance.md](balance.md)) — ends the run. **Heal/Mend** cards remove Wounds, so wound
management is an ongoing tactical cost.

## Lose — Doom Clock (strategic)
A corruption/threat value rises every round (rate in [balance.md](balance.md)); some events push it
further. If it reaches its maximum before the Archon threshold is met, the land falls and the run is
lost. The Doom Clock is the strategic pressure that forces the player to *rise fast*. This is a
**new system to build** (roadmap milestone M2).

## Turn / Round Flow
Within a **turn**, the player plays cards to build up the four stats, takes actions, then ends the
turn — at which point the action stats reset to 0 (matches existing `Player.TurnEnd`). **Rounds**
group turns and are the cadence on which the Doom Clock advances (matches the existing
`GameManager` round/turn counters).

## Stats
Seven stat types (the `StatType` flags in code):
- **Attack** — defeats enemies (Attack ≥ enemy HP).
- **Defend** — absorbs enemy Attack; shortfall becomes Wounds.
- **Explore** — moves across the hex map and into dungeons.
- **Influence** — recruits units and is part of the Archon win threshold.
- **Heal** — removes Wounds.
- **Wound** — the penalty card type added on combat loss.
- **Crystal** — the resource that fuels Empower.

## Empower / Crystal Economy
A card may be **Empowered** by spending one **Crystal** of the card's color
(Red / Yellow / Green / Purple — the `EmpowerType`). When empowered, the card yields its stronger
`empower*` values instead of its base values. Crystals are limited and gained from rewards and
crystal-granting cards, so empowering is a deliberate tactical choice (pillar 3).

## Leveling
Experience accrues toward `expToNextLevel`. On level-up, apply the existing code intent:
- **Even** level → +1 to a chosen stat,
- **Odd** level → +HP,
- every **3rd** level → +hand size,
- every level → a new skill/option.

The experience curve and per-level values are tuning — see [balance.md](balance.md).
