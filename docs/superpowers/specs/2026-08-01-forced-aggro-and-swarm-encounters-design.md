# Forced aggro and swarm encounters — design (2026-08-01)

## 1. The problem

The combat-preview work (spec 2026-07-30) made opening a fight a free look: `OpenFight` starts a
fight with `Committed == false`, and the canvas's click-off catch calls `CombatController.Decline()`,
which closes it for nothing. That is right for a fight the player *chose* to open by clicking a token.

It is wrong for the one fight the player never chose. `EnemyToken.CheckAggro` already implements the
intended rule — the first step into an enemy's ring arms it (`isAggro = true`), a second step from one
adjacent hex to another adjacent hex starts the fight — but that fight now opens through the same
uncommitted path as a click, so the player just clicks off and walks away. The aggro rule has no teeth.

## 2. The rules

### 2.1 Forced entry

Stepping from one hex adjacent to an armed enemy to *another* hex adjacent to that same enemy commits
the player to the fight. The combat canvas opens already committed: the turn's action is spent at open,
and clicking off does nothing.

Nothing else about combat changes. Inside the fight the player still chooses how to engage — stage
Siege, spend Influence, then press Engage — and Withdraw is still available in the Attack phase at the
usual cost (1 wound, Harrying if any enemy has it, and de-aggro of the tokens involved). Being forced
into the encounter is not being forced to fight it to the death; it is losing the free "never mind".

Walking *out* of adjacency still disarms the token, exactly as today, so stepping away and back re-arms
rather than triggers.

A log line at open names what happened: `"Cornered! You cannot avoid this fight."` — a single
`GameLog` post, per memory `map-feedback-tooltip-and-log`. There is no pre-move telegraph on the hex;
that was considered and explicitly deferred (§7).

### 2.2 The swarm pull-in

Any **field** fight — forced or voluntarily clicked — drags in every enemy token standing on a hex
adjacent to the player, not just the one that started it. Standing next to two enemies means fighting
two. This is one rule the player can learn, and it closes the hole where a player picks a pack apart
one at a time while the neighbours watch.

No cap; hex geometry caps it at six. Guardian, Dungeon and Shrine fights are untouched — the pull-in
is `CombatContext.Field` only.

### 2.3 Fog

You only ever fight what you can see. A fog-hidden token never joins another enemy's fight, and — new
here — a fog-hidden token can no longer arm aggro or force a fight of its own. This matches the
existing rule that fog-hidden tokens are neither clickable nor glowing (`EnemyToken.OnPointerClick`,
`EnemyToken.Update`); today `CheckAggro` is the one place that ignores fog, which would otherwise let
an invisible enemy force an inescapable fight.

Consequence, accepted: a token that armed while visible and is then re-fogged stops participating.
Fog does not otherwise re-cover explored ground, so this is not reachable in practice.

## 3. Forced entry — implementation

The trigger already exists at `EnemyToken.CheckAggro` (the `isAggro` branch). What changes:

- `EnemyToken.StartCombat` gains a `bool forced` parameter, passed through to `OpenFight`. The
  `CheckAggro` branch passes `true`; `OnPointerClick` passes `false`.
- `CombatController.OpenFight` gains `bool forced = false`. Once the fight is built (after the card
  spawn and `SetPhase(CombatPhase.Siege)`), a forced fight calls the existing private `Commit()`.
  That invokes `pendingOnCommit` — which for a field fight is `TurnPhaseController.BeginAction()`,
  spending the turn's action and clearing the undo stack — and sets `Committed = true`.
- **No new escape gate is written.** `Decline()` already opens with `if (Committed) return;`, so the
  click-off catch wired in the scene is dead for a forced fight from its first frame. This is the
  whole reason to express "forced" as "committed at open" rather than as a separate flag.
- A forced fight is never `previewOnly`. A preview-only fight cannot be committed to and cannot be
  escaped from except by `Decline()`; forcing one would soft-lock the canvas.
- The `CanInteract` guard at the top of `StartCombat` stays as it is. If the turn's action is already
  spent the fight does not open and the token stays armed. Movement is Explore-phase only and
  `BeginAction` leaves the Explore phase, so the only way to reach that branch is repositioning via a
  teleport card played during the Action phase.

### 3.1 Two guards the trigger needs

**The arming cell.** `CheckAggro` records the hex the player stood on when the token armed, and forces
only when the current cell differs from it. That makes "moved from one adjacent hex to another" literal
rather than inferred from the event firing, so a repeat raise at the same cell can't manufacture a
fight. It is deliberately not saved: a restored save's default cell can only differ from wherever the
player actually is, which is the reading we want anyway.

**One fight at a time.** Every armed token receives the same move event, so a swarm trigger runs
`StartCombat` on two or three tokens at once — and the combat intro beat means `InCombat` is still
false while they queue behind it. A static latch on `EnemyToken` lets the first one through and drops
the rest; they are in its roster regardless.

### 3.2 Fix carried in the same change

`CheckAggro` sets `player.inCombat = true` *before* calling `StartCombat`, which can then bail on the
`CanInteract` check — leaving the flag set with nothing to clear it. The write moves to where the fight
actually opens, and the clear moves to every field-fight close (decline, flee, win) instead of the flee
path alone, since a won fight used to leave it latched on. `PlayerPosition.inCombat` has no readers in
code today; it is corrected rather than removed because it is a public field and may be bound in the
scene.

## 4. The swarm pull-in — implementation

### 4.1 The pure rule

New `FieldEncounterRules` in `Assets/Scripts/Exploration/` — the folder already carries
`ArchonsRise.Exploration.asmdef` and a `Tests` folder, so no new asmdef is needed (memory
`pure-class-asmdef-placement`). Primitives only, no `UnityEngine` types, matching `HexActionRules`:

```csharp
public static List<int> Participants(int sourceIndex, IReadOnlyList<bool> adjacentToPlayer,
                                     IReadOnlyList<bool> fogHidden)
```

Returns the source first, then every other index that is adjacent to the player and not fog-hidden,
in input order. The source is always included regardless of its own adjacency flag: it is the token
that started the fight. The Unity-side facts (who is adjacent, who is fogged) are gathered by the
caller, so the rule stays trivially testable.

### 4.2 Gathering

`EnemyToken` builds the roster for both entry paths (click and forced) from
`ExplorationController.PlayerNeighbors()` and `MapFog.IsHidden`, over `FindObjectsByType<EnemyToken>()`,
and hands `OpenFight` a spawn list plus the matching ordered token list. Source-first ordering means
the enemy the player clicked, or the one that cornered them, leads the ring.

### 4.3 CombatController

`fieldToken` (a single `EnemyToken`) becomes a card→token map built at open, with the source token
retained for `OriginWorld()`'s fallback. It drives what is single-token bookkeeping today:

- `OpenFight` — `SetBoardVisible(false)` on every participant, not just the source.
- Morph — each card flies out of and back to *its own* icon. `OriginLocalPoint(OriginWorld())` is
  replaced by a per-card lookup in `MorphIn`, `MorphAwayThenClose` and `MorphSurvivorsBackThenFinish`,
  falling back to the source token when a card has no mapping.
- `NotifyDefeated` — `RecordFieldDefeat` runs against the token that card came from.
- `Decline` / `MorphAwayThenClose` — restore board visibility on every participant.
- `FinishEnd` — on a flee, de-aggro every surviving participant token, not just the source.

Layout needs nothing: `PlaceFight` already switches from the token fan to the ring at `live.Count >= 2`.

## 5. The swarm test save

`Assets/Scripts/Editor/SwarmSaveTool.cs`, menu **Tools ▸ Archon's Rise ▸ Create Swarm Test Save**.
Editor-only, `#if UNITY_EDITOR`, modelled directly on `LateGameSaveTool`: run it in Play Mode on a
fresh New Game in the GameBoard scene, and it mutates the live session and writes `Save.json`, which
Load Game replays any number of times.

**The geometry.** Two enemies adjacent to each other share exactly two common neighbours. The tool
searches outward from the player's current cell for a quad — enemy cells `E1`, `E2` adjacent to each
other, and their two shared neighbours `A` and `B` — where all four cells are walkable, not town, not
dungeon, and hold no existing token, and where no *third* enemy token is adjacent to `A` or `B`. That
last condition keeps the expected outcome exactly two enemies rather than a surprise three. If no quad
is found, the tool logs an error and writes nothing.

**What it writes.**

- Player parked on `A`; fog cleared in a radius-3 box around it, since fog-hidden tokens no longer
  participate (§2.3) and the test needs clean sight.
- Two lowest-tier enemies from the `EnemyDeck` pool spawned on `E1`/`E2` via
  `EnemyDeck.GetNewEnemyToken(..., isMidRunSpawn: true)` with zero stat bonuses, so they save and
  restore explicitly (schema v4 mid-run spawns) rather than depending on the map seed.
- `gridPos` assigned directly on each spawned token instead of waiting for its `Start()` — otherwise
  `DataManager.AggroedEnemyCells` captures a default cell and the restored save comes back unarmed.
- `isAggro = true` on both, so the *first* step the player takes is the trigger.
- A hand stacked with Explore cards (to afford the step) plus Attack/Defend/Siege cards (so the fight
  is playable), drawn from the baked registry the way `LateGameSaveTool` does, matched on
  `(cardType & StatType.X) != 0` since `StatType` is a flags enum.
- `Debug.Log` of all four cells and the instruction: load the save, step `A → B`.

**What it tests.** One step from `A` to `B` — both adjacent to both enemies — forces the fight and
pulls both in, exercising the whole feature at once. Clicking off the canvas and finding that nothing
closes is the forced-entry half.

## 6. Testing

- **EditMode**, via the existing mcs/nunit CLI harness (memory `unity-pure-test-harness-mcs`,
  `unity-editmode-tests-while-editor-open`), in `Assets/Scripts/Exploration/Tests/`:
  `FieldEncounterRules.Participants` with — a lone source and no neighbours; one adjacent neighbour;
  two adjacent neighbours; an adjacent but fog-hidden neighbour excluded; a non-adjacent token
  excluded; the source always first and always present.
- **Manual**, from the swarm save: step `A → B` and confirm two enemies ring the player, the turn's
  action reads as spent, and clicking off does nothing. Then Engage through to the Attack phase and
  Withdraw; confirm one wound, both tokens back on the board, both de-aggroed, and each card morphing
  back to its own icon. Reload and instead kill both; confirm both tokens are gone and both rewards
  pay through the queue in kill order.
- **Manual, single-enemy regression**: click a lone armed token with no neighbours and confirm the
  fight still opens uncommitted and still closes on click-off.

**No Unity editor wiring.** No new serialized fields, no scene or prefab changes — `CheckAggro` is
already hooked to the player-position event on `EnemyToken.prefab`, and the click-off catch is already
wired to `Decline()`.

## 7. Deferred

- **Pre-move telegraph.** A hex-tooltip warning on hover ("Moving here forces combat with 2 enemies"),
  with the enemies that would join highlighted. Considered and deliberately cut to keep this change
  tight; the aggro glow plus the at-trigger log line are the only feedback for now. This is the first
  thing to revisit if the swarm rule reads as a gotcha in play.
- **Swarm reward incentive.** Nothing extra is paid for taking on several enemies at once beyond the
  per-kill rewards that already stack.
