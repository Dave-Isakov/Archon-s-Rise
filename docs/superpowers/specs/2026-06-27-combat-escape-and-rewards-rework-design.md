# Combat Escape & Rewards Rework — Design

**Date:** 2026-06-27
**Branch context:** `feature/m1-save-load-spec`
**Goal:** Resolve two gameplay blockers that make save/load hard to test: (1) you can get trapped in an unwinnable combat with no way out, and (2) the card-reward system duplicates cards and never closes, so rewards can't be applied cleanly before a save.

These are two independent features. They share the goal of making a run completable/testable, but touch different systems and can be implemented and verified separately.

---

## Feature 1 — Escape Combat (Flee button)

### Problem
Combat is driven by `EnemyToken` ([EnemyToken.cs:48-92](../../../Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs)). Once an enemy is aggro'd and the player is adjacent, `StartCombat()` enables `combatCanvas`. The only exits are defeating the enemy (`fightButton` → `DefeatMonster`) or the non-functional `influenceButton` (hardcoded to "Impossible"). `CheckCombatants()` only closes the canvas when no enemy cards remain. An unwinnable fight therefore traps the player.

### Decisions
- **Mechanism:** A **Flee button** on the combat canvas (canvas-level, not per enemy card).
- **Cost:** Fleeing inflicts **one wound** (reusing the existing `PlayerHand.AddWound()`).
- **Aftermath:** The engaged enemy is **de-aggro'd** so re-entering its tile runs a fresh aggro check instead of instantly re-triggering combat.

### Design
Add a `FleeCombat()` flow, owned by `GameManager` (mirrors the existing `CombatCanvasActive()` / `CheckCombatants()` combat methods that already live there).

`FleeCombat()` does, in order:
1. `PlayerHand.AddWound()` — adds one wound card to the player's hand.
2. Destroy all enemy cards parented under `enemyCardCombatPosition`.
3. Set the player's `inCombat = false`.
4. De-aggro the enemy token(s) that started this combat (`isAggro = false`).
5. Disable `combatCanvas` and its animator (same teardown as `CheckCombatants()`).
6. `ValidationMessage("You flee the battle and suffer a wound!")`.

### Open implementation detail (resolve in planning)
`FleeCombat()` must map the open combat back to the originating board `EnemyToken` to de-aggro it. **Chosen approach:** when `EnemyToken.StartCombat()` runs, it registers itself as the GameManager's "active combatant" (e.g. `GameManager.activeCombatant = this`). `FleeCombat()` reads that reference, sets `isAggro = false`, then clears it. This avoids brittle board scans and keeps a single source of truth for "who am I fighting."

### UI
- Add a Flee `Button` to the combat canvas prefab/scene, wired to `GameManager.FleeCombat()`.

### Testing
- Enter combat with an enemy whose HP exceeds the player's attack; confirm Fight shows the "need more attack" message and Flee exits.
- After Flee: combat canvas closed, one wound card present in hand, `inCombat == false`, enemy token present but no longer instantly re-engaging when adjacent.
- Save after fleeing, reload: wound card persists in the deck/hand serialization, enemy token still on board.

---

## Feature 2 — Rewards Clean Redesign

### Problem
The current reward system overloads the interactive `Card` MonoBehaviour with an `isReward` mode flag that rewrites its click behavior:
1. A reward card raises `onRewardSelect_AddCardToDeck` on **every** click with no guard, and nothing closes the reward canvas → infinite duplicate cards ([Card.cs:161-164](../../../Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs)).
2. `PlayerDeck.AddRewardToDeck` triple-adds: it inserts the reward `Card` object **and** adds the SO to `deckList` **and** calls `AddCardToDecklist` which instantiates yet another card ([PlayerDeck.cs:81-86](../../../Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs)).
3. The two unpicked reward cards are never cleaned up.
4. Missing braces in `Rewards.GetReward()` ([Rewards.cs:28-30](../../../Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs)) and `GetReward(Dungeon)` ([Rewards.cs:58-60](../../../Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs)) make `GetCardRewards.Raise()` fire unconditionally; `GetReward(Dungeon)` also reads `rewards[0]` instead of the dungeon's chosen reward.
5. `GetReward(EnemyCard)` has **no Cards branch** — defeating an enemy never opens the card-reward screen, so there is currently no reliable trigger to test card rewards.

### Core principle
A card reward is not a new kind of object — it is the **same data** (`CardsSO`) shown in a different context. Reward systems for "items identical to usable objects" should **share the data layer, not the behavior layer**. Reuse `CardsSO`; give it a distinct, thin presenter per context; never reuse the interactive component in a mode.

### Target architecture (clean redesign)
```
CardsSO                          data: single definition of a card
  |- Card (MonoBehaviour)        interactive card in hand/deck
  |- CardPreview (MonoBehaviour) display-only; used by the reward UI

RewardsSO                        data: what a reward is (xp / crystals / card choice) — flags kept as-is

RewardService.Grant(reward)      ONE grant entry point (refactor of Rewards):
   Experience -> player.PlayerExp += amount
   Crystals   -> crystals.CreateCrystal(...)
   Cards      -> RewardChoiceUI.Offer(candidateSOs, chosen => deck.AddCard(chosen, toTop:true))

PlayerDeck.AddCard(CardsSO so, bool toTop)   the ONLY place an SO becomes a live Card
```

### Decisions
- **Selection flow:** Show 3 candidate cards; player picks **one**, or presses a **Skip** button to decline all. Exactly one resolution; no re-clicking.
- **Placement:** The chosen card goes to the **top of the deck** (drawn next) — `AddCard(so, toTop: true)`.
- **Reward type data:** Keep `RewardsSO` and its `[Flags] RewardType` as authored (no SO class hierarchy / asset re-authoring).
- **Card-reward trigger for testing:** Add a Cards branch to the enemy-defeat path so defeating an enemy with a card reward opens the choice UI — the common, reliable trigger.

### Components

**1. `CardPreview` (new, display-only)**
- Renders a `CardsSO`'s name, description, and empower color. Reuses the display logic currently inline in `Card.Start()` / `GetEmpowerTypeColor` (extract a shared helper so `Card` and `CardPreview` don't duplicate it).
- No play logic, no deck mutation. Optionally raises a "this preview was clicked" signal to its owner (the reward UI), carrying its `CardsSO`.
- Lives on a dedicated **reward-card prefab** (a `CardPreview` + clickable area), separate from the interactive player-card prefab.

**2. `RewardChoiceUI` (refactor of `RewardCanvas`)**
- `Offer(IReadOnlyList<CardsSO> candidates, Action<CardsSO> onChosen, Action onSkip)`:
  - Spawns one reward-card preview per candidate into its slots, tracks them.
  - Holds a `resolved` guard so only the first selection/skip takes effect.
  - On a preview click → set `resolved`, destroy **all** spawned previews, disable the canvas, invoke `onChosen(chosenSO)`.
  - On **Skip** button → set `resolved`, destroy all previews, disable canvas, invoke `onSkip()`.
- A new **Skip** `Button` is added to the reward canvas, wired to the skip path.
- `RewardChoiceUI` never touches the deck directly — it only returns the choice.

**3. `PlayerDeck.AddCard(CardsSO so, bool toTop = false)` (new single path)**
- Materializes one `Card` from the SO via the existing `AddCardToDecklist`, then if `toTop` moves it to index 0 of `CardsInDeck`.
- This is the only SO→Card materialization entry for grants. `AddRewardToDeck` is removed (its three responsibilities collapse into this one method).
- Save/load already serializes from `CardsInDeck` in order ([DataManager.cs:248](../../../Assets/Scripts/Managers/DataManager.cs)) and restores via `RebuildDeck` ([DataManager.cs:206](../../../Assets/Scripts/Managers/DataManager.cs)); writing to `CardsInDeck` is therefore automatically captured on save.

**4. `RewardService` (refactor of `Rewards`)**
- Collapse the three `GetReward*` overloads into thin context selectors (enemy → `enemySO.defeatRewards`, dungeon → `dungeon.rewards`) that each pick a `RewardsSO`, then funnel into one `Grant(RewardsSO reward)`.
- `Grant` applies each `RewardType` flag exactly once: Experience, Crystals, and Cards. For Cards it builds the candidate set (3 random `CardsSO` from `DataManager.Cards.Items`, as today) and calls `RewardChoiceUI.Offer(...)` with `onChosen = so => deck.AddCard(so, toTop:true)` and a no-op `onSkip`.
- Fix the brace/logic bugs; ensure `GetReward(Dungeon)` uses the dungeon's chosen reward, not `rewards[0]`.
- Add the Cards branch to the enemy-defeat path.

**5. `Card` cleanup**
- Remove the `isReward` field, the `IsReward` property, the reward branch in `OnPointerClick`, and the `onRewardSelect_AddCardToDeck` event. `Card` is now solely the interactive in-hand/in-deck card.

### Error / edge handling
- Double-click / rapid clicks: prevented by the `resolved` guard in `RewardChoiceUI`.
- Empty candidate pool: if no candidate `CardsSO` exist, skip opening the UI (grant nothing) rather than showing empty slots.
- Reward canvas left open across a save: with the guard + auto-close on resolve, the canvas closes before the player can save; `IsSettledState` already blocks saving while `cardRewardCanvas.enabled` ([DataManager.cs:312](../../../Assets/Scripts/Managers/DataManager.cs)).

### Testing
- Defeat an enemy whose `defeatRewards` include a Cards reward → choice UI opens with 3 previews.
- Click one preview → exactly one matching card added to the **top** of the deck; the other two previews destroyed; canvas closes; clicking where a preview was does nothing.
- Press Skip → no card added; canvas closes.
- Experience and Crystal rewards still apply (and only once).
- Save after taking a card reward, reload → the chosen card is present in the restored deck (validates reward-applies-on-save).

---

## Out of scope
- Implementing the influence/recruit-enemy mechanic (the old influence button concept).
- Converting `RewardsSO` to a polymorphic SO hierarchy.
- Rebalancing reward contents or drop tables.
