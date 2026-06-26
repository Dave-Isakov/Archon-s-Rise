---
name: archons-rise-design
description: Game-design bible for Archon's Rise, a single-player roguelike deckbuilder. Use when designing or authoring game content (cards, enemies, towns, units, rewards, dungeons) or reasoning about mechanics, win/lose conditions, leveling, or balance.
---

# Archon's Rise — Game Design Bible

**Pitch:** A single-player roguelike deckbuilder where you explore a randomized hex realm, build a crystal-empowered deck, and race a doom clock to Rise to Archon before wounds or the falling land stop you.

## Design Pillars
1. **The Rise is domination, not a boss kill** — winning means accumulating territory + power (control towns, hit a Level/Influence threshold), so Influence, towns, and leveling are first-class, not side content.
2. **Two clocks of pressure** — every run is squeezed tactically by Wounds (deck pollution) and strategically by the Doom Clock (the land falling). Design must keep both live.
3. **Crystals are the spice** — the Empower/crystal economy is the main tactical lever; new content should create interesting crystal-spend decisions, not flat stat sticks.
4. **Runs are fresh; mastery grows the pool** — no power carryover. Meta-progression only unlocks new content into future runs.

## Index
- [mechanics.md](mechanics.md) — run loop, win/lose conditions, stats, empower economy, leveling.
- [content-rules.md](content-rules.md) — authoring contract for every ScriptableObject content type.
- [balance.md](balance.md) — number ranges, reward tiers, crystal costs, doom-clock pacing, unlock pool.

## Maintaining this skill
When a design decision changes, update the relevant file here AND append the decision to
`../archons-rise-roadmap/decisions-log.md` in the same change.
