# Status

Where Archon's Rise stands today. Seeded from the code review (`docs/code-review.md`,
2026-06-25). Update this as milestones complete.

## Exists (in code)
- Four action stats (Attack/Defend/Explore/Influence) + Heal/Wound/Crystal; stat reset on turn end.
- **Turn phases** (M2.13, 2026-07-21 — code complete, editor wiring pending): strict
  Explore → Action → End; one encounter/place-visit per turn; the round is a Doom-band-scaled "day"
  (`turnsPerRound` 6/4/3) that auto-ends (budget spent or deck can't refill); End Round removed;
  movement undoable except on fog reveal; event-driven phase + day-countdown HUD; day budget saved in
  the existing turn slot (phase resets to Explore on load, no schema bump).
- Full **undo** via Command pattern (`PlayManager`, `PlayCommand`, `CardDrawCommand`).
- Cards with **Empower / crystal** economy; 7 starting + ~9 acquirable cards.
- Hex-map **exploration** via `GridGeneration` (randomized).
- **Combat** — phased **Siege → Defend → Attack → auto-flee** engine (M2.14, 2026-07-22), one
  multi-purpose button, shared by field, dungeon, and guardian fights. Siege/Influence remove enemies
  before a single summed group counterattack (Attack vs Defend → Wounds); guarded places spawn their
  whole remaining roster at once with per-kill banking + 3-wound resumable retreat; deferred
  fight-end rewards; two-track defeat FX (dissolve/fade) via `UI/EnemyCardDissolve`.
- **Towns** (Town/Village/Fortress/City) and **Unit** recruiting (Knight/Scout/Warrior/Merchant).
- **Map dungeons** (M2.9, 2026-07-14) — 6 spaced hexes/map, stand-on-cell entry, 3 tiered delves
  (exp-only fights), guaranteed completion bundle + doom relief, doom-band flagging, save v6.
- **Crystal hotspots** (M2.15 Plan 1, 2026-07-24 — code complete, editor authoring + play acceptance
  pending): scattered tiles that grant 1 fixed-color crystal when the player ends a turn parked on
  them; charge-limited (`-1` = unlimited rich vein), a **free** passive (not the Action) with no
  popup — the HUD count + token pip are the feedback. Save **v8** (`RunState.hotspots`). Ships an
  extensible **hex-tooltip occupant** model (`IHexOccupant` + `HexOccupantRegistry` + pure
  `TileDescriptor`): towns/keeps/castles/dungeons/hotspots each describe their own tooltip line, and a
  new tile type integrates with zero `HexInteractor` edits.
- **Shrines** (M2.16 Plan 2, 2026-07-27 — code complete, editor authoring + play acceptance pending):
  one-shot gamble tiles. Stand on one and a fan of 4 slots arcs overhead; each click cycles a slot
  through the crystals you can still spare (and back to empty), so you choose **which** crystals to
  pay. Nothing is spent until the checkmark, which costs the turn's action and the crystals
  regardless of outcome. Then a coin flip: **safe** pays 1× a rolled reward (card pick / unit / large
  exp — never skills, never crystals) and the shrine goes spent; **bad** summons a persistent tier-3
  guardian that owes **2×** that reward plus its defeat exp and nothing else. Flee it and it stands;
  kill it and the shrine retires. Save **v10** (`RunState.shrines` + a `SpawnedEnemy` reward tag so the
  guardian's debt survives reload). Built on M2.15's `IHexOccupant` / `TileDescriptor` foundation.
- **Rewards** (Experience/Crystals/Cards at Beginner→Master), all modals serialized through the
  unified **`RewardQueue`** (replaces the M2.4 busy-wait).
- **Leveling** counters (exp, expToNextLevel, level).
- ScriptableObject **GameEvent/Listener** bus.
- JSON **save** — full run state: deck/hand/discard (by stable content id), fog-of-war reveal, crystals, scalar player stats. ✅ M1 complete (2026-06-29).
- **Three Critical bugs fixed** (2026-06-25): listener-unregister inverted condition, `LoadGame`
  stale-field + scene-load race, and unsafe `OnDisable` autosave. See `docs/code-review.md`.
- **Stable content ids** on card/unit/reward SO assets.
- **Single `PlayerDeck.AddCard` path**; card plays committed to discard when undo stack clears.
- **Fog-of-war reveal** persisted across save/load.
- **SaveButton** correctly wired to `SaveGame`.
- **Place-type system** — Town/Keep/Castle taxonomy, guardian-conquest assaults (resumable,
  3-wound retreat), services gated by type + conquest, conquest persisted (schema v2). ✅ M2.

## Stubbed / partial
- **Leveling rewards** — the even/odd/every-3 rules are commented intent in `Player.cs`, not implemented.

## Missing
- **Win check** (conquer 2 Castles) — milestone M2.5.
- **Doom Clock** (strategic loss) — milestone M2.5.
- **Wound-out** loss condition — milestone M2.5.
- **Run setup / seed** and **meta-unlock pool** — milestone M3.
- **Important-tier refactors** from `docs/code-review.md`: event-driven updates over per-frame
  `Update()`, decoupling gameplay→UI via events, the apply/revert toggle refactor, assembly
  definitions + EditMode tests, and the modernization pass.
