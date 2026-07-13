# Map Dungeons (M2.9) — Design (2026-07-13)

## Problem

Dungeons exist as data (`DungeonsSO`, Derelict Tower, DungeonEnemies) but not as gameplay.
The only runtime flow is a legacy card UI: `DungeonDeck` instantiates a `Dungeon` card, and
each click rolls a 50/50 "reward or next enemy" event. Dungeons are not on the hex map, have
no progress model, no completion state, no save representation, and no relationship to the
Doom Clock. The roadmap slots this as **M2.9 — Dungeons**; this spec replaces the older
M2.9 sketch (enter once, fight the authored list in order).

## Goals

1. **Dungeons live on the map** — a dungeon hex tile + token, placed at map generation:
   configurable count per map (start: **6**), randomized but spaced apart.
2. **Three tiered delves.** Each delve costs the dungeon's `exploreCost` (flat per delve).
   Delve 1 fights a tier-1 enemy, delve 2 a tier-2, delve 3 a tier-3 — all authored per
   dungeon. Defeated slots never respawn; progress persists across visits and saves.
3. **Completion-gated rewards.** Fights inside grant **experience only**; the dungeon's
   reward is a **guaranteed bundle** on completion (no dice).
4. **Doom interplay.** Completion always lowers the Doom Clock (configurable). Dungeons can
   become **flagged** (doom-band driven): each flagged dungeon adds **+1 doom per round**
   until cleared, and completing a flagged dungeon grants a **larger** doom reduction.
5. Follow the established **pure-rules + tuning + mcs-TDD** pattern (`DoomRules`,
   `RewardRules`, `SpawnRules`).
6. **Reward modals never overlap.** Level-ups, enemy-defeat card picks, validation
   messages, and the new dungeon bundle must resolve strictly one at a time. The dungeon
   bundle is the third independent reward producer, so this spec **pulls the deferred
   M2.4 "unified modal queue" follow-up into scope** instead of adding another ad-hoc
   chaining mechanism.

## Non-Goals (explicitly deferred)

- **Multi-room / branching dungeon interiors.** A dungeon is exactly 3 sequential fights.
- **Flag triggers beyond doom bands** (events, spawner pressure). The flag *state* is
  general; only the band trigger ships now.
- **Flag effects on the dungeon's enemies** (stat bonuses, corruption variants). Flags are
  doom-economy only for now.
- **Doom-gating dungeon fight tiers** (`DoomRules.MaxTier` does not apply inside dungeons —
  they are opt-in challenges; the tier-3 finale is available from round 1).

## The Model

A dungeon on the map is a **place-like location**: entered by **standing on its cell**
(same rule as Towns/Keeps/Castles), which opens a **dungeon panel**. The panel's **Delve**
button spends the dungeon's `exploreCost` from the Explore pool and starts a **normal field
combat** against the current depth's enemy — same combat UI and resolutions (Attack /
Siege / Influence where applicable), same wound math, flee costs 1 Wound.

- **Win** → depth advances permanently (`defeatedCount` 0→3); the fight grants
  **experience only** at that enemy's tier (no crystal/card rolls).
- **Lose / flee** → Wounds per normal field rules; the slot's enemy is unchanged; delving
  again costs `exploreCost` again. No extra doom penalty — failure costs time, Wounds, and
  wasted Explore.
- **Third win = completion**: the guaranteed bundle fires, the doom relief applies, the
  token switches to a permanent "cleared" look, and any flag on the dungeon ends.

### Completion bundle (guaranteed, no rolls)

At the dungeon's `tier` (the 6 starting dungeons author tier 3):

- **1 exp roll** — `RewardRules.SampleExp(tier, …)` (the usual bell sample).
- **`rewardCount` crystals** — random color each (existing crystal grant).
- **`rewardCount` card picks** — the existing choose-1-of-3 screen from that tier's pool.
  Exp and crystals apply instantly (non-modal); the card picks are **enqueued on the
  `RewardQueue`** (below) and resolve one at a time. If the bundle's exp levels the
  player, the level-up's message/skill/card picks enqueue *behind* the bundle's picks —
  deterministic order, no overlap.

### Flagging (doom-band driven)

- When the Doom Clock **first enters the mid band** (`doom > lowBandMax`),
  `flagsOnMidBand` random uncleared, unflagged dungeons become flagged; first entering the
  high band (`doom > midBandMax`) flags `flagsOnHighBand` more.
- Each band fires **once per run** (two saved bools). Doom relief dropping the clock back
  below a band edge never un-fires or re-fires a band — no flag churn.
- If no eligible dungeon exists when a band fires, the firing is consumed with no effect.
- The **round doom tick** becomes `1 + flaggedCount` (was flat +1). This is a deliberate
  soft death-spiral: ignored corruption accelerates the clock.
- Flags never clear except by completing the dungeon.

### Doom relief

- Complete an unflagged dungeon: `DoomClock.Add(-dungeonCompleteRelief)`.
- Complete a flagged dungeon: `DoomClock.Add(-flaggedCompleteRelief)` (larger).
- `DoomRules.Add` already clamps to `[0, doomMax]`, so negative pushes are safe as-is.

## Components

### `DungeonsSO` — reshaped (no new SO type)

| Field | Meaning under this spec |
|---|---|
| `exploreCost` | Cost of **each** delve (flat × up to 3). |
| `enemies` | **Exactly 3 entries**: slot 0 = tier-1 fight, slot 1 = tier-2, slot 2 = tier-3. |
| `tier` | Tier the **completion bundle** pays at. |
| `rewardCount` | Bundle scale: this many crystals **and** this many card picks. |

An editor-time validation (OnValidate or authoring check) warns when `enemies.Count != 3`
or an enemy's `tier` doesn't match its slot.

### Map placement — `GridGeneration`

- New serialized fields: `dungeonTile` (a **`DungeonRuleTile`**, same pattern as
  `TownRuleTile`, walkable ground), `dungeonCount = 6`, `dungeonMinSpacing = 4`
  (Chebyshev), `dungeonPool` (List&lt;DungeonsSO&gt;).
- After town placement: candidates = land cells that are not towns, not inside
  `startSafeRadius`, not already claimed. Placement reuses
  **`SpawnRules.SeedZones(candidates, dungeonCount, dungeonMinSpacing, Rng)`** verbatim.
- Each placed cell gets the tile + a **`DungeonToken`** (instantiated like town tokens,
  carrying `gridPos` + its assigned `DungeonsSO`). Assignment draws seed-randomly from
  `dungeonPool` **without replacement** until the pool exhausts, then with replacement.
- **Deterministic over the map seed → positions and SO assignment are never saved**; only
  progress is (same trick as towns/`PlaceConquest`).
- Dungeon cells join the **blocked sets** for enemy spawning: the initial-pack loop in
  `GridGeneration` and `EnemySpawner.BuildBlockedSet` treat dungeon cells like town cells
  (no enemy may spawn on a dungeon).

### `DungeonToken` (MonoBehaviour) — new

Map-side identity: `gridPos`, assigned `DungeonsSO`, visual states (normal / flagged
marker / cleared), and the stand-on-cell open hook (same mechanism as `TownToken` →
town menu).

### `DungeonLedger` (scene singleton) — new

Owns all runtime dungeon state, mirroring the conquest-ledger role for places:

- Per-cell state: `defeatedCount` (0–3), `flagged`. Completed ⇔ `defeatedCount == 3`.
- `FlaggedCount` — read by the round tick.
- `OnBandEntered(band)` — picks flag targets via `DungeonRules.PickFlagTargets`, sets
  flags, updates token visuals; tracks the two once-per-run fired bools.
- Completion handling: fires the reward bundle, applies doom relief, marks the token
  cleared.
- Save export/restore (below).

### `DungeonRules` (pure static class) — new, TDD'd via mcs harness

Lives in `Assets/Scripts/Doom/` under the existing `ArchonsRise.Doom` asmdef (it consumes
`DoomTuning`, and the flag/relief math is doom math). Unity-free, mcs-testable.

- `NextTier(int defeatedCount) → int` — 0→1, 1→2, 2→3; ≥3 → none/complete.
- `IsComplete(int defeatedCount) → bool`.
- `RoundTick(int flaggedCount) → int` — `1 + flaggedCount`.
- `Relief(bool flagged, DoomTuning t) → int` — picks the right relief knob.
- `PickFlagTargets(IList<candidate> uncompletedUnflagged, int count, Func<int,int> rng)`
  — random distinct picks; returns fewer when candidates run short.
- `BandsEntered(int before, int after, DoomTuning t) → (enteredMid, enteredHigh)` —
  crossing detection used by `DoomClock.Add`.

### `DoomTuning` — extended

New knobs (starting values, tune in playtest):

```
flagsOnMidBand = 1
flagsOnHighBand = 1
dungeonCompleteRelief = 1
flaggedCompleteRelief = 3
```

### `DoomClock` / `GameManager` — touched

- `GameManager.RoundPlus` doom tick passes `DungeonRules.RoundTick(ledger.FlaggedCount)`
  instead of the flat 1.
- `DoomClock.Add` detects band entry (`DungeonRules.BandsEntered` on before/after) and
  notifies the ledger **after** the doom change and loss check resolve.

### Dungeon panel (UI) — new

Opens on standing on the dungeon cell (town-menu open pattern, including the
revive-before-raise listener rule). Shows:

- Name, description, **progress pips** (3 slots: cleared / next / locked).
- **Delve** button with the explore cost; disabled when the Explore pool < cost, when the
  dungeon is complete, or mid-combat.
- **Next-enemy preview** through the existing `PreviewRules.CanPreview` gate (same
  read-only stat panel as field/guardian previews).
- **Flagged banner** when flagged: "Corrupted — +1 Doom each round until cleared."
- Cleared state: no Delve, a "Cleared" marker.

### `RewardQueue` (scene singleton) — new: the unified modal arbiter

Today three mechanisms coexist: `LevelUpController`'s private queue with a 0.25s
`Invoke` poll against `messageCanvas`/`cardRewardCanvas` (the M2.4 targeted busy-wait),
`Rewards.Grant`'s unqueued immediate card pick, and `RewardCanvas.Offer`'s documented
double-Offer hole (a second Offer clobbers the first — its callbacks never fire, which
would strand any chained `onClosed`). `GameManager.ValidationMessage` has the same
clobber shape (a second message overwrites the text), and `ReturnButton` closes with no
callback, which is why the level-up queue polls instead of chaining. The dungeon bundle
adds a third reward producer, so the ad-hoc net gets replaced:

- `RewardQueue.Enqueue(Action<Action> job)` — FIFO, one job in flight. A job opens its
  modal and invokes the supplied `done` exactly once when the modal resolves. Empty
  queue + idle → the job runs immediately (solo rewards keep today's instant feel).
- `Busy => inFlight || pending > 0` — consumed by the `DataManager` save gate (replacing
  the `LevelUpController.Busy` check) and anything else that must wait for a settled
  screen.
- **Run-end flush**: `RunEndController` already force-closes every canvas; the queue
  drops all pending jobs when the run ends (no modal may open over the terminal screen).

**Everything modal routes through it:**

- `Rewards.OfferCardChoice(tier, onClosed)` — enqueues the `RewardCanvas` offer; `done`
  fires on chosen *or* skip, then `onClosed`.
- `GameManager.ValidationMessage(msg)` — enqueues the message; `done` fires on dismiss
  (`ReturnButton`). Callers are unchanged. Two messages in one frame now show
  sequentially instead of the second overwriting the first.
- `LevelUpController` — keeps its ordering responsibility (message, then skill picks,
  then card picks) but **loses its private queue and the `Invoke` poll**: it enqueues
  each item on the `RewardQueue` and `Busy` delegates to the queue.
- Dungeon completion bundle — enqueues its `rewardCount` card picks (above).
- `RewardCanvas.Offer` / `ValidationMessage` gain a **defensive error log** if invoked
  while their canvas is already up — impossible once the queue is the sole caller, so
  any hit is a routing bug surfaced loudly instead of a silent clobber.

Non-modal grants (exp, crystals, doom changes) never enter the queue.

### `Rewards` — two additions

- `GrantExpOnly(int tier)` — the bell exp sample, no crystal/card rolls (per-fight grant).
- `GrantDungeonCompletion(int tier, int rewardCount)` — the guaranteed bundle (exp roll +
  `rewardCount` crystals instantly, then `rewardCount` card picks via the `RewardQueue`).

### Save — schema v6

- `RunState.dungeons : DungeonState[]` — `{ int x, int y, string dungeonId,
  int defeatedCount, bool flagged }`. Positions/assignment re-derive from the seed; the
  saved `dungeonId` is a sanity check (mismatch → warn + skip, like `RestoreSpawned`).
- `RunState.dungeonMidFlagsFired : bool`, `RunState.dungeonHighFlagsFired : bool`.
- Migrator v5→v6: absent array → empty, bools → false. Existing runs get fresh, unflagged
  dungeons (acceptable — no live run has dungeon progress).

## Retirement / Cleanup

- **Delete** the legacy card flow: `Dungeon.cs`, `DungeonDeck.cs`, their prefabs/scene
  objects, and the `DungeonEvent`/`DungeonListener`/`UnityDungeonEvent`/
  `onDungeonReward_RewardPlayer` event plumbing if nothing else consumes it.
- **Delete** the `LevelUpController` busy-wait (`Invoke(nameof(TryNext), 0.25f)` poll and
  its private `pending` queue) — subsumed by `RewardQueue`.
- `LocationsSO.dungeons` is already-legacy; leave untouched (LocationsSO retirement is its
  own cleanup).
- The implementation plan must verify a clean compile and that no scene/prefab still wires
  a deleted component.

## Starting Tuning Values (tune in playtest)

| Knob | Value | Lives on |
|---|---|---|
| Dungeons per map | 6 | `GridGeneration.dungeonCount` |
| Min spacing | 4 (Chebyshev) | `GridGeneration.dungeonMinSpacing` |
| Explore cost per delve | 2–3 | per `DungeonsSO` |
| Completion tier | 3 | per `DungeonsSO` |
| `rewardCount` | 1 (one showpiece dungeon at 2) | per `DungeonsSO` |
| Flags on mid-band entry | 1 | `DoomTuning` |
| Flags on high-band entry | 1 | `DoomTuning` |
| Completion doom relief | −1 | `DoomTuning` |
| Flagged completion relief | −3 | `DoomTuning` |

Content pass: **6 authored dungeons**, each with 3 enemies at tiers 1/2/3 (reuse existing
DungeonEnemies where they fit, author the rest).

## Testing

- **Pure TDD (mcs harness)** for `DungeonRules`: tier progression and completion
  boundaries; `RoundTick` with 0/1/3 flags; relief selection; `PickFlagTargets`
  distinctness, short-candidate behaviour, determinism under injected rng; `BandsEntered`
  crossing matrix (no-cross, mid, high, both-in-one-add, relief re-cross → no re-fire is
  ledger logic but the detector must report only crossings).
- **`DoomRules.Add` negative-amount clamp** — the clamp exists; add a negative-push test
  if the suite lacks one.
- **Save tests**: v5→v6 migration defaults; round-trip of `DungeonState[]` + fired bools.
- **`RewardQueue` ordering** — extract the FIFO/in-flight bookkeeping into a small pure
  class (Unity-free) so mcs tests cover: FIFO order, one-in-flight, immediate run on
  idle+empty, `done` called twice is ignored, flush drops pending jobs.
- **Manual Unity verification**: generate several seeds → 6 dungeons, spaced, never on
  towns/start; delve flow end-to-end (cost spend, combat, exp-only, persistence across
  save/load mid-progress); completion bundle + doom relief; band crossing flags a dungeon,
  round tick accelerates, flagged completion applies the larger relief and stops the tick;
  cleared token/panel states.
- **Overlap stress scenario** (the reason `RewardQueue` exists): author a test dungeon
  whose completion bundle exp is guaranteed to level the player with `rewardCount = 2`.
  Complete it → expect, strictly in order with no clobbered screens: card pick 1, card
  pick 2, "You reached level N!" message, skill pick, level-up card pick. Also re-verify
  the M2.4 case (enemy defeat whose card reward and level-up land in the same frame).
- Scene/prefab wiring (tiles, tokens, panel, listeners) delivered as **step-by-step editor
  instructions** for the user — no hand-edited scene YAML.

## Docs to update alongside implementation

- `.claude/skills/archons-rise-design/mechanics.md` — replace the dungeon mention in the
  run loop with the delve model; add a Dungeons section (3 tiered delves, completion
  rewards, flags, doom relief).
- `.claude/skills/archons-rise-design/content-rules.md` — `DungeonsSO` row updates
  (3-slot enemies convention, bundle semantics).
- `.claude/skills/archons-rise-design/balance.md` — dungeon knob table above; note the
  round tick is now `1 + flaggedCount`.
- `.claude/skills/archons-rise-roadmap/milestones.md` — rewrite M2.9 scope/acceptance to
  this spec; `decisions-log.md` — append the decision (map dungeons, tiered delves,
  completion-gated rewards, band-driven flags, doom relief, and the unified `RewardQueue`
  replacing the M2.4 busy-wait).

## Open Follow-ups (not this spec)

- Additional flag triggers (events system, spawner pressure) — M3+.
- Flag effects on dungeon enemies (corruption stat bonuses / variants).
- Per-map dungeon-count variation (map presets) — the knob exists; presets are M3
  run-setup territory.
- ~~Unified modal reward queue~~ — **now in scope** (the `RewardQueue` section); the
  M2.4 arbiter follow-up closes with this spec.
