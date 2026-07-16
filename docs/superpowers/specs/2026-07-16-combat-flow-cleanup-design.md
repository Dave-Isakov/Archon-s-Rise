# Combat Flow Cleanup — Design

**Date:** 2026-07-16
**Status:** Approved, ready for implementation plan

## Problem

Combat has accumulated messy, partly-dead code and three concrete UX defects:

1. **Intro transition only plays once.** `EnemyToken.StartCombat()` enables the
   combat canvas plus a child `Animator` (a "Combat!" banner flash), then waits
   with `WaitUntil(... TextMeshProUGUI.enabled == false)` before spawning the
   enemy card. The clip flips the banner TMP on, then off at its end, and the
   coroutine reads that `false` as "intro finished." After the first fight
   nothing resets the TMP back to `enabled`, so on every later fight the flag is
   already `false`, the `WaitUntil` passes instantly, and the enemy card just
   pops in with no transition.

2. **XP-only defeats give no feedback.** On defeat, `Player.ResolveAttack` raises
   `OnEnemyDefeat_GetRewards` → `Rewards.GetReward`. If the reward roll opens a
   card/crystal modal the player gets feedback, but if only experience is
   granted nothing opens. The "*X has been destroyed!*" message and
   `CheckCombatants()` (which tears the canvas down) only fire from
   `EnemyCard.OnPointerClick` — i.e. when the player clicks the already-defeated
   card. So an XP-only win leaves the player staring at the enemy card with no
   indication the fight is over until they click it.

3. **No affordance that a token is fightable.** `EnemyToken.CheckAggro()` already
   flips `isAggro` true when the player stands on an adjacent hex — exactly the
   "you can start combat here" state — but nothing visual is tied to it, so it is
   not clear a token can be clicked to fight.

Plus dead code in `EnemyCard` (commented-out `CheckWounds`, `OnPointerEnter/Exit`,
reward stubs) and defeat logic smeared across `Player`, a multi-listener
`OnEnemyDefeat_GetRewards` event, and `EnemyCard.OnPointerClick`.

## Goals

- Combat intro plays reliably every fight, driven from code with a real duration.
- Defeats end automatically with a clear, player-dismissed message that **names
  the reward received**; no need to click the enemy card.
- Adjacent enemy tokens visibly glow so it's obvious combat can be started.
- Remove the dead code and consolidate the scattered defeat orchestration.

Non-goals: changing combat math (`CombatRules` is untouched), reworking the
reward tiers/odds, or the guardian-assault / dungeon-delve fight structures.

## Design

### 1. Intro transition — fixed, code-driven

Stop using the banner TMP's `enabled` flag as the "intro finished" signal; drive
the existing Animator clip deterministically instead.

- `GameManager` gains `[SerializeField] string combatIntroState`,
  `[SerializeField] float combatIntroDuration` (default = clip length), and a
  reference to the banner `TextMeshProUGUI`.
- New `IEnumerator PlayCombatIntro()`: enable the banner text,
  `animator.Play(combatIntroState, 0, 0f)` to force a replay from frame 0,
  `yield return new WaitForSeconds(combatIntroDuration)`, then finish.
- `EnemyToken.StartCombat()` yields on that coroutine, then spawns the enemy card
  (`deck.GetNewEnemyCard(this)`).

The authored Animator clip is kept as-is; only the trigger/timing is moved into
C#, so it replays every fight with deterministic timing.

### 2. Automatic defeat sequence

A single orchestrator, `GameManager.ResolveDefeat(EnemyCard enemy)`, replaces the
`OnEnemyDefeat_GetRewards` event fan-out and the click-the-card teardown path. It
sequences everything through the existing `RewardQueue` modal arbiter so ordering
is correct by construction.

**Reward summary refactor.** `Rewards.GetReward(enemy)` is changed to **return a
`RewardSummary`** and to **no longer self-enqueue** the card-pick modal:

```
public struct RewardSummary {
    public int exp;
    public EmpowerType? crystal; // null when no crystal rolled
    public bool cardPick;        // a card choice is pending
    public int tier;
}
```

`GetReward` still applies experience and any rolled crystal instantly; it only
reports whether a card pick is pending rather than opening it. Dungeon fights
(`DungeonDelve.AnyInProgress`) return an exp-only summary (unchanged exp-only
rule). The card modal is opened separately by the orchestrator via the existing
`Rewards.OfferCardChoice(tier)` so it lands *after* the reward message.

**`ResolveDefeat(enemy)` sequence:**

1. `var summary = rewards.GetReward(enemy);` — applies exp/crystal, returns the
   summary.
2. Enqueue the **reward message** on `RewardQueue`: a standard
   `ValidationMessage`-style popup the player dismisses with **Return** when
   ready (no auto-timeout, no clicking the enemy card). Text names the reward
   using the icon language, e.g.
   *"{enemy.enemySO.cardName} has been defeated. You receive
   {IconMarkup.Cost(IconConcept.Experience, exp)}[, {IconConcept.Crystal}]
   [ and a new card to choose]."*
3. If `summary.cardPick`, call `rewards.OfferCardChoice(summary.tier)` — its modal
   enqueues **after** the message, so the pick appears once the player clears the
   reward text.
4. Enqueue **teardown** last, run as a coroutine so watcher `Update`s can react
   between steps (ordering matters for the guardian chain):
   - `enemy.isDefeated = true` — existing `EnemyToken.Update`,
     `DungeonDelve.Update`, and `GuardianAssault.Update` watchers react off this
     flag (token destroy + defeated-cell recording, dungeon depth recording, and
     — for an unfinished guardian roster — spawning the *next* guardian card).
   - `yield return null` — let those `Update`s run, so any next-guardian card is
     already parented before the close check.
   - `CheckCombatants()` **while the defeated card is still present** — it closes
     the canvas only when `enemyCardCombatPosition` holds a single enemy card
     (the just-defeated one = truly the last). If a next guardian spawned,
     child count is 2 and the canvas stays open. This preserves the exact
     `childCount == 1` semantics the current click-to-dismiss path relies on.
   - Destroy the enemy card object, then `commands.ClearStack()`.
   - `CheckCombatants()` calls `EndCombat()` on close (clears `activeCombatant`,
     hides the Flee button).

**Callers.** `Player.ResolveAttack` and `Player.CompleteInfluence` call
`GameManager.Instance.ResolveDefeat(enemy)` instead of raising
`OnEnemyDefeat_GetRewards`. The wound message (`wounds > 0`) still enqueues first
as its own `ValidationMessage`, naturally landing before the reward message.

This works uniformly for field, dungeon, and guardian fights because teardown
keys off the same `isDefeated` flag and `CheckCombatants()` those systems already
watch. It removes the ambiguous "click the enemy card to end the fight" path.

### 3. Adjacency glow — pulsing halo child sprite

- The `EnemyToken` prefab gets a child glow `SpriteRenderer` (a soft halo behind
  the token), wired once in the editor. `EnemyToken` gains
  `[SerializeField] SpriteRenderer glow`.
- Drive it from the adjacency state `CheckAggro()` already computes into
  `isAggro`: when `isAggro` is true, enable the glow and pulse its alpha (a sine
  over time in `Update`, e.g. 0.3↔1.0); when false, disable it. Fog-hidden tokens
  (`MapFog.IsHidden(gridPos)`) keep the glow off.
- The alpha pulse is factored into a small pure function
  (`GlowPulse(time, min, max, speed) → float`) so it is unit-testable via the CLI
  pure-test harness.
- Purely visual; no change to combat-start logic.

### 4. Dead-code cleanup

- Remove commented-out `CheckWounds`, `OnPointerEnter/OnPointerExit`, and reward
  stubs in `EnemyCard`.
- Simplify `EnemyCard.OnPointerClick`: the `isDefeated == true` branch is now dead
  (teardown is automatic); the out-of-range preview dismiss branch stays.
- Retire the `OnEnemyDefeat_GetRewards` event and the superseded
  `DefeatMonster` / `DestroyEnemyObject` scatter now that `ResolveDefeat` owns the
  flow. `DefeatMonster`/`SiegeMonster` button hooks that route into
  `Player.ResolveAttack` are kept.

## Testing

Most of this is Unity/scene glue, consistent with the EditMode-while-editor-open
constraint. Plan:

- `CombatRules` is unchanged.
- New pure helpers are unit-tested via the CLI pure-test harness: the glow alpha
  `GlowPulse` function, and any reward-summary string/formatting helper worth
  isolating.
- The intro replay, the full defeat message → rewards → teardown sequence, and
  the token glow are verified manually in-editor via step-by-step instructions
  (the usual manual-scene-edit flow), since they depend on scene wiring
  (Animator state name, banner TMP, glow child sprite).

## Risks / Notes

- **Scene wiring the user performs manually:** the glow child `SpriteRenderer` on
  the `EnemyToken` prefab, and the `combatIntroState` / `combatIntroDuration` /
  banner-TMP fields on `GameManager`. Provide step-by-step editor instructions;
  never hand-edit scene/prefab YAML.
- **Reward modal serialization:** all reward/message modals must continue to go
  through `RewardQueue`; the card pick is enqueued via `Rewards.OfferCardChoice`
  (which self-enqueues) *after* the reward message, never opened directly.
- **Dungeon/guardian teardown** relies on `isDefeated` being set in the teardown
  step; because teardown runs last (after the message and any card pick), dungeon
  depth recording happens after the player dismisses the reward message, which is
  acceptable.
