# Status

Where Archon's Rise stands today. Seeded from the code review (`docs/code-review.md`,
2026-06-25). Update this as milestones complete.

## Exists (in code)
- Four action stats (Attack/Defend/Explore/Influence) + Heal/Wound/Crystal; turn/round structure;
  stat reset on turn end.
- Full **undo** via Command pattern (`PlayManager`, `PlayCommand`, `CardDrawCommand`).
- Cards with **Empower / crystal** economy; 7 starting + ~9 acquirable cards.
- Hex-map **exploration** via `GridGeneration` (randomized).
- **Combat** (player Attack vs enemy HP; enemy Attack vs Defend → Wounds); field + dungeon enemies.
- **Towns** (Town/Village/Fortress/City) and **Unit** recruiting (Knight/Scout/Warrior/Merchant).
- **Dungeons**; **Rewards** (Experience/Crystals/Cards at Beginner→Master).
- **Leveling** counters (exp, expToNextLevel, level).
- ScriptableObject **GameEvent/Listener** bus.
- JSON **save** (scalar player stats only).
- **Three Critical bugs fixed** (2026-06-25): listener-unregister inverted condition, `LoadGame`
  stale-field + scene-load race, and unsafe `OnDisable` autosave. See `docs/code-review.md`.

## Stubbed / partial
- **Save persists only scalar player stats** — no deck, hand, discard, crystals, map, or world state.
- **Leveling rewards** — the even/odd/every-3 rules are commented intent in `Player.cs`, not implemented.
- **SaveButton prefab** is wired to call `LoadGame`, not `SaveGame` (likely a wiring bug).

## Missing
- **Run-based save schema** (deck/map/clock state) — milestone M1.
- **Win check** (Archon threshold) — milestone M2.
- **Doom Clock** (strategic loss) — milestone M2.
- **Wound-out** loss condition — milestone M2.
- **Run setup / seed** and **meta-unlock pool** — milestone M3.
- **Important-tier refactors** from `docs/code-review.md`: event-driven updates over per-frame
  `Update()`, decoupling gameplay→UI via events, the apply/revert toggle refactor, assembly
  definitions + EditMode tests, and the modernization pass.
