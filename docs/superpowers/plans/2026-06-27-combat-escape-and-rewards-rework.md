# Combat Escape & Rewards Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Flee button so players can escape unwinnable combat, and rebuild the card-reward flow so picking a reward adds exactly one card (no duplicates), with a Skip option — both so save/load can be tested with applied rewards.

**Architecture:** Two independent features. (A) Combat escape: `GameManager` tracks the active `EnemyToken` and exposes `FleeCombat()` (wound + de-aggro + teardown), wired to a new canvas button. (B) Rewards: a single `PlayerDeck.AddCard(CardsSO, toTop)` materialization path, a display-only `CardPreview`, a guarded `RewardCanvas.Offer(...)` selection UI with Skip, and a `Rewards` service with one `Grant(RewardsSO)` path. The interactive `Card` loses its reward mode.

**Tech Stack:** Unity 6 (6000.5.1f1), C#, ScriptableObject event channels, uGUI (Canvas/Button), TextMeshPro.

## Global Constraints

- **Unity version:** 6000.5.1f1. Do not change project/package versions.
- **Do NOT rename existing MonoBehaviour classes** (`Rewards`, `RewardCanvas`, `Card`, `PlayerDeck`, `GameManager`, `EnemyToken`). Unity binds scene/prefab components by class name + GUID; renaming silently breaks every wired reference. Refactor internals only.
- **Preserve the influence mechanic.** Do not touch `EnemyCard.influenceButton`, `canInfluence`, `influenceCost`, or `enemyInfluence`. They are reserved for a future feature.
- **No automated gameplay tests exist.** The only test assembly (`ArchonsRise.SaveData.Tests`) covers pure save serialization and does not reference `Assembly-CSharp`. Gameplay verification in this plan is: let Unity recompile (Console shows 0 errors), then Play Mode manual checks. Do not invent pytest/NUnit steps for Unity-coupled code.
- **Editor steps are manual.** Steps marked **🖱️ UNITY EDITOR (manual)** require the Unity Editor GUI (adding buttons to scenes, wiring Inspector references, creating prefabs). The coding agent cannot perform these; they are instructions for the human running Unity. Code-only steps can be done by the agent.
- **Commit after each task.** Do not skip hooks. Co-author trailer as configured.

---

## File Structure

**Feature A — Combat Escape**
- Modify: `Assets/Scripts/Managers/GameManager.cs` — add `activeCombatant` field + `FleeCombat()`.
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs` — register self as active combatant in `StartCombat()`.
- Editor: `Assets/Scenes/GameBoard.unity` (combat canvas) — add Flee `Button`.

**Feature B — Rewards Rework**
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/CardVisuals.cs` — shared empower-color helper.
- Create: `Assets/Scripts/GameObjectScripts/CardMenuScripts/RewardCanvas/CardPreview.cs` — display-only reward card.
- Modify: `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs` — add `AddCard(...)`, remove `AddRewardToDeck`.
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/RewardCanvas/RewardCanvas.cs` — `Offer/Choose/SkipReward` guarded flow.
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs` — single `Grant(RewardsSO)`, fix bugs, Cards branch on enemy defeat.
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs` — remove `isReward`/`IsReward`/reward branch/`onRewardSelect_AddCardToDeck`, use `CardVisuals`.
- Editor: reward-card prefab, `RewardCanvas` Inspector wiring, Skip button, remove dead event listener.

---

# Feature A — Escape Combat

### Task A1: FleeCombat infrastructure (code only)

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs:87-92`

**Interfaces:**
- Produces: `GameManager.activeCombatant` (public `EnemyToken`); `GameManager.FleeCombat()` (public, parameterless, void). Task A2 wires a button to `FleeCombat()`.
- Consumes: `PlayerHand.AddWound()` (exists), `GameManager.playerHand` (public `GameObject`), `GameManager.enemyCardCombatPosition` (public `GameObject`), `GameManager.combatCanvas` (public `Canvas`), `EnemyToken.player` (public `PlayerPosition`), `PlayerPosition.inCombat` (public `bool`), `EnemyCard` component on combat cards.

- [ ] **Step 1: Add the active-combatant field to GameManager**

In `GameManager.cs`, add the field next to the other combat fields (after `public GameObject enemyCardCombatPosition;` on line 18):

```csharp
    // The enemy token whose combat is currently open. Set when combat starts,
    // read by FleeCombat() to de-aggro the right token, cleared on teardown.
    [HideInInspector] public EnemyToken activeCombatant;
```

- [ ] **Step 2: Add FleeCombat() to GameManager**

In `GameManager.cs`, add this method right after `CheckCombatants()` (after line 105):

```csharp
    // Player gives up the current fight: take one wound, clear the enemy
    // cards, drop the player out of combat, de-aggro the engaged token so it
    // does not instantly re-engage, then tear down the combat canvas.
    public void FleeCombat()
    {
        if (!combatCanvas.enabled) return;

        playerHand.GetComponent<PlayerHand>().AddWound();

        foreach (var card in enemyCardCombatPosition.GetComponentsInChildren<EnemyCard>())
            Destroy(card.gameObject);

        if (activeCombatant != null)
        {
            activeCombatant.isAggro = false;
            if (activeCombatant.player != null)
                activeCombatant.player.inCombat = false;
            activeCombatant = null;
        }

        combatCanvas.enabled = false;
        combatCanvas.GetComponentInChildren<Animator>().enabled = false;

        ValidationMessage("You flee the battle and suffer a wound!");
    }
```

- [ ] **Step 3: Register the active combatant when combat starts**

In `EnemyToken.cs`, update `StartCombat()` (lines 87-92) to record this token on the GameManager:

```csharp
    IEnumerator StartCombat()
    {
        GameManager.Instance.activeCombatant = this;
        GameManager.Instance.CombatCanvasActive();
        yield return new WaitUntil(() => GameManager.Instance.combatCanvas.GetComponentInChildren<TextMeshProUGUI>().enabled == false);
        deck.GetNewEnemyCard(this);
    }
```

- [ ] **Step 4: Verify it compiles**

🖱️ UNITY EDITOR (manual): Switch to Unity, let it recompile.
Expected: Console shows **0 compile errors**. `FleeCombat` is defined but not yet called, so no behavior change.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Managers/GameManager.cs" "Assets/Scripts/GameObjectScripts/GameBoardObjects/EnemyToken.cs"
git commit -m "feat: add FleeCombat infrastructure and active-combatant tracking"
```

---

### Task A2: Flee button wiring + verification

**Files:**
- Editor: `Assets/Scenes/GameBoard.unity` (the combat canvas object referenced by `GameManager.combatCanvas`)

**Interfaces:**
- Consumes: `GameManager.FleeCombat()` from Task A1.

- [ ] **Step 1: Add the Flee button to the combat canvas**

🖱️ UNITY EDITOR (manual):
1. Open `Assets/Scenes/GameBoard.unity`.
2. In the Hierarchy, find the combat canvas object (the one assigned to `GameManager.combatCanvas` — select the GameManager object to confirm which Canvas it points to).
3. Right-click that canvas → UI → Button - TextMeshPro. Name it `FleeButton`.
4. Set its label text to `Flee`. Position it on the combat canvas so it does not overlap the per-enemy Fight button (e.g. bottom-center of the canvas).

- [ ] **Step 2: Wire the button to FleeCombat()**

🖱️ UNITY EDITOR (manual):
1. Select `FleeButton`. In the Inspector → Button → **On Click ()**, press `+`.
2. Drag the **GameManager** object into the object slot.
3. In the function dropdown choose **GameManager → FleeCombat ()** (the no-argument runtime method).

- [ ] **Step 3: Play Mode verification — flee an unwinnable fight**

🖱️ UNITY EDITOR (manual): Enter Play Mode and:
1. Move the player adjacent to an enemy so combat opens (combat canvas shows an enemy card).
2. With low/zero player attack, click **Fight** → confirm the "need N Attack" message appears (you cannot win).
3. Click **Flee**.

Expected:
- Combat canvas closes.
- A **Wound** card is now in the player's hand.
- The validation message "You flee the battle and suffer a wound!" appears.
- Moving away and back near the enemy does not *instantly* re-open combat on the first adjacent step (the token was de-aggro'd).

- [ ] **Step 4: Play Mode verification — flee then save/load**

🖱️ UNITY EDITOR (manual): After fleeing (and once the board is in a settled state), use the in-game Save, then Load.
Expected: The wound card persists in the restored deck/hand; the enemy token is still on the board; no console errors.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scenes/GameBoard.unity"
git commit -m "feat: add Flee button to combat canvas wired to FleeCombat"
```

---

# Feature B — Rewards Rework

### Task B1: Single deck-add path (`PlayerDeck.AddCard`)

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs:81-86`

**Interfaces:**
- Produces: `PlayerDeck.AddCard(CardsSO so, bool toTop = false)` returns `Card`. Tasks B4 calls `deck.AddCard(so, toTop: true)`.
- Consumes: existing private `AddCardToDecklist(CardsSO)` returning `Card`, `CardsInDeck` (`List<Card>`), `deckList` (`List<CardsSO>`).

- [ ] **Step 1: Replace `AddRewardToDeck` with `AddCard`**

In `PlayerDeck.cs`, delete the `AddRewardToDeck` method (lines 81-86) and add in its place:

```csharp
    // The single path for granting a card into the deck from card data.
    // Used by rewards (and any future grant). toTop=true makes it the next draw.
    public Card AddCard(CardsSO so, bool toTop = false)
    {
        var card = AddCardToDecklist(so); // instantiates, appends to CardsInDeck, sets flags, inactive
        deckList.Add(so);
        if (toTop)
        {
            CardsInDeck.Remove(card);
            CardsInDeck.Insert(0, card);
        }
        return card;
    }
```

- [ ] **Step 2: Verify it compiles**

🖱️ UNITY EDITOR (manual): Let Unity recompile.
Expected: **Compile error** in `Card.cs`/scene wiring referencing the now-removed `AddRewardToDeck` IS acceptable here ONLY if it appears — but `AddRewardToDeck` was invoked via a ScriptableObject event listener (Inspector), not via code, so there should be **0 code compile errors**. If the Console reports a missing-method on an event listener at runtime later, it is cleaned up in Task B5. Confirm 0 *compile* errors now.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/DeckScripts/PlayerDeck.cs"
git commit -m "feat: add single PlayerDeck.AddCard path, remove AddRewardToDeck"
```

---

### Task B2: Display-only `CardPreview` + shared color helper + reward prefab

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/CardVisuals.cs`
- Create: `Assets/Scripts/GameObjectScripts/CardMenuScripts/RewardCanvas/CardPreview.cs`
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs:127-138`
- Editor: new `RewardCard` prefab

**Interfaces:**
- Produces: `CardVisuals.ApplyEmpowerColor(GameObject, EmpowerType, Color green, Color red, Color purple, Color yellow)` (static, void). `CardPreview.Bind(CardsSO so, System.Action<CardsSO> onSelected)` (public, void). Task B3 instantiates the `RewardCard` prefab and calls `Bind`.
- Consumes: `EmpowerType` enum, `CardsSO.cardName`/`cardDescription`/`empowerType`, `UnityEngine.UI.Image`, TMP.

- [ ] **Step 1: Create the shared color helper**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/CardVisuals.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

// Shared card-display helpers so Card (interactive) and CardPreview (display-only)
// don't duplicate the empower-color logic.
public static class CardVisuals
{
    public static void ApplyEmpowerColor(GameObject card, EmpowerType type,
        Color green, Color red, Color purple, Color yellow)
    {
        var frontImage = card.GetComponentsInChildren<Image>()[0];
        switch (type)
        {
            case EmpowerType.Green:  frontImage.color = green;  break;
            case EmpowerType.Red:    frontImage.color = red;    break;
            case EmpowerType.Purple: frontImage.color = purple; break;
            case EmpowerType.Yellow: frontImage.color = yellow; break;
        }
    }
}
```

- [ ] **Step 2: Route `Card` through the helper (behavior-preserving)**

In `Card.cs`, replace `GetEmpowerTypeColor` (lines 127-138) with:

```csharp
    private void GetEmpowerTypeColor(GameObject card)
    {
        CardVisuals.ApplyEmpowerColor(card, cardSO.empowerType,
            greenColor, redColor, purpleColor, yellowColor);
    }
```

- [ ] **Step 3: Create the CardPreview component**

Create `Assets/Scripts/GameObjectScripts/CardMenuScripts/RewardCanvas/CardPreview.cs`:

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Display-only presentation of a CardsSO for selection contexts (reward screen).
// It renders card data and reports a click back via a callback. It never touches
// the deck or any game state itself.
public class CardPreview : MonoBehaviour, IPointerClickHandler
{
    public CardsSO cardSO;
    [SerializeField] private TextMeshProUGUI cardName;
    [SerializeField] private TextMeshProUGUI cardDescription;
    [Header("Empower Type Colors")]
    [SerializeField] private Color redColor;
    [SerializeField] private Color yellowColor;
    [SerializeField] private Color purpleColor;
    [SerializeField] private Color greenColor;

    private Action<CardsSO> onSelected;

    public void Bind(CardsSO so, Action<CardsSO> onSelected)
    {
        cardSO = so;
        this.onSelected = onSelected;
        cardName.text = so.cardName;
        cardDescription.text = so.cardDescription;
        CardVisuals.ApplyEmpowerColor(gameObject, so.empowerType,
            greenColor, redColor, purpleColor, yellowColor);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onSelected?.Invoke(cardSO);
    }
}
```

- [ ] **Step 4: Verify it compiles**

🖱️ UNITY EDITOR (manual): Let Unity recompile.
Expected: **0 compile errors**. Existing card colors still render in Play Mode (Card uses the same helper).

- [ ] **Step 5: Create the `RewardCard` prefab**

🖱️ UNITY EDITOR (manual):
1. In the Project window, find the existing interactive card prefab (the one assigned to `RewardCanvas.cardPrefab` / `PlayerDeck.cardPrefab`). Duplicate it (Ctrl+D) and rename the copy `RewardCard`. Move it next to the other reward UI prefabs.
2. Open `RewardCard` for editing. **Remove the `Card` component** from the root.
3. **Add the `CardPreview` component** to the root.
4. In `CardPreview`, assign the **Card Name** and **Card Description** TMP fields to the same child text objects the original `Card` used, and set the four empower colors to the same values the card prefab used (copy them from the original prefab's `Card` component before/while editing).
5. Confirm the front `Image` is still the first Image in `GetComponentsInChildren<Image>()` order (it should be, since structure is unchanged).
6. Save the prefab.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/PlayerScripts/CardVisuals.cs" "Assets/Scripts/GameObjectScripts/CardMenuScripts/RewardCanvas/CardPreview.cs" "Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs"
git add -A -- "*RewardCard*"
git commit -m "feat: add CardPreview display component, CardVisuals helper, and RewardCard prefab"
```

---

### Task B3: Guarded reward selection (`RewardCanvas.Offer`)

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/RewardCanvas/RewardCanvas.cs` (full rewrite of body)
- Editor: `RewardCanvas` Inspector wiring + Skip button

**Interfaces:**
- Produces: `RewardCanvas.Offer(System.Collections.Generic.IReadOnlyList<CardsSO> candidates, System.Action<CardsSO> onChosen, System.Action onSkip)` (public, void); `RewardCanvas.SkipReward()` (public, void — wired to Skip button). Task B4 calls `Offer(...)`.
- Consumes: `CardPreview.Bind` (Task B2), `GameManager.cardRewardCanvas` (public `Canvas`), the `RewardCard` prefab (Task B2).

- [ ] **Step 1: Rewrite RewardCanvas**

Replace the entire contents of `RewardCanvas.cs` with:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

// Owns the lifecycle of the "pick one of N cards" reward screen. It spawns
// display-only previews, guards against double-resolution, and reports the
// player's choice (or skip) via callbacks. It never mutates the deck itself.
public class RewardCanvas : MonoBehaviour
{
    [SerializeField] GameObject[] cardLocations = new GameObject[3];
    [SerializeField] GameObject rewardCardPrefab; // root has a CardPreview
    private readonly List<GameObject> spawned = new();
    private bool resolved;
    private Action<CardsSO> onChosen;
    private Action onSkip;

    public void Offer(IReadOnlyList<CardsSO> candidates, Action<CardsSO> onChosen, Action onSkip)
    {
        Clear();
        resolved = false;
        this.onChosen = onChosen;
        this.onSkip = onSkip;

        GameManager.Instance.cardRewardCanvas.enabled = true;

        for (int i = 0; i < cardLocations.Length && i < candidates.Count; i++)
        {
            var preview = Instantiate(rewardCardPrefab, cardLocations[i].transform, false);
            preview.transform.localScale = new Vector3(3, 3, 3);
            preview.GetComponent<CardPreview>().Bind(candidates[i], Choose);
            spawned.Add(preview);
        }
    }

    private void Choose(CardsSO chosen)
    {
        if (resolved) return;
        resolved = true;
        onChosen?.Invoke(chosen);
        Close();
    }

    // Wired to the Skip button's OnClick.
    public void SkipReward()
    {
        if (resolved) return;
        resolved = true;
        onSkip?.Invoke();
        Close();
    }

    private void Close()
    {
        Clear();
        GameManager.Instance.cardRewardCanvas.enabled = false;
    }

    private void Clear()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();
    }
}
```

- [ ] **Step 2: Verify it compiles**

🖱️ UNITY EDITOR (manual): Let Unity recompile.
Expected: **0 compile errors**. The `RewardCanvas` component on the reward canvas object now shows missing serialized fields (`rewardCardPrefab`) and an unused old `cardPrefab` slot — wired next.

- [ ] **Step 3: Wire RewardCanvas in the Inspector**

🖱️ UNITY EDITOR (manual):
1. Select the reward canvas object that has the `RewardCanvas` component (assigned to `GameManager.cardRewardCanvas`).
2. Confirm **Card Locations** (size 3) still point to the three slot objects (they were serialized before; re-assign if cleared).
3. Assign **Reward Card Prefab** = the `RewardCard` prefab from Task B2.

- [ ] **Step 4: Add the Skip button**

🖱️ UNITY EDITOR (manual):
1. Under the reward canvas, add UI → Button - TextMeshPro named `SkipButton`, label `Skip`. Place it below the three card slots.
2. In its **On Click ()**: drag the reward canvas object (the one with `RewardCanvas`) into the slot, choose **RewardCanvas → SkipReward ()**.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/CardMenuScripts/RewardCanvas/RewardCanvas.cs"
git add -A -- "Assets/Scenes/GameBoard.unity"
git commit -m "feat: guarded reward selection (Offer/Choose/SkipReward) with Skip button"
```

---

### Task B4: Reward granting service (`Rewards.Grant`)

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs` (full rewrite of body)
- Editor: `Rewards` Inspector wiring (PlayerDeck + RewardCanvas refs)

**Interfaces:**
- Produces: `Rewards.GetReward()`, `Rewards.GetReward(EnemyCard)`, `Rewards.GetReward(Dungeon)` (public, void — context selectors). `GetReward(EnemyCard)` stays wired to the `OnEnemyDefeat_GetRewards` event.
- Consumes: `PlayerDeck.AddCard(CardsSO, bool)` (Task B1), `RewardCanvas.Offer(...)` (Task B3), `DataManager.Instance.Cards.Items` (`List<CardsSO>`), `CrystalInventory.CreateCrystal(EmpowerType)`, `Player.PlayerExp`, `EnemiesSO.defeatRewards`, `Dungeon.rewards`, `RewardsSO.rewardType/expAmount/cardDescription`.

- [ ] **Step 1: Rewrite Rewards**

Replace the entire contents of `Rewards.cs` with:

```csharp
using System.Collections.Generic;
using UnityEngine;

// Reward-granting service. Context methods pick which RewardsSO applies, then
// everything funnels through one Grant() that applies each reward flag once.
// Card rewards offer a choice through RewardCanvas and grant the chosen card
// via the single PlayerDeck.AddCard path.
public class Rewards : Deck<RewardsSO>
{
    public List<RewardsSO> rewards = new List<RewardsSO>();
    public CrystalInventory crystals;
    [SerializeField] Player player;
    [SerializeField] PlayerDeck deck;
    [SerializeField] RewardCanvas rewardCanvas;

    private void Start()
    {
        Shuffle(rewards);
    }

    // No-context reward (legacy entry point).
    public void GetReward()
    {
        Grant(rewards[0]);
        Shuffle(rewards);
    }

    // Wired to OnEnemyDefeat_GetRewards.
    public void GetReward(EnemyCard enemy)
    {
        var reward = enemy.enemySO.defeatRewards[Random.Range(0, enemy.enemySO.defeatRewards.Count)];
        Grant(reward);
    }

    public void GetReward(Dungeon dungeon)
    {
        var reward = dungeon.rewards[Random.Range(0, dungeon.rewards.Count)];
        Grant(reward);
        dungeon.rewards.Remove(reward);
    }

    private void Grant(RewardsSO reward)
    {
        if (reward.rewardType.HasFlag(RewardType.Experience))
            player.PlayerExp += reward.expAmount;

        if (reward.rewardType.HasFlag(RewardType.Crystals))
        {
            var types = new[] { EmpowerType.Green, EmpowerType.Yellow, EmpowerType.Red, EmpowerType.Purple, EmpowerType.None };
            crystals.CreateCrystal(types[Random.Range(0, types.Length)]);
        }

        if (reward.rewardType.HasFlag(RewardType.Cards))
            OfferCardChoice();

        Debug.Log($"Your reward is: {reward.cardDescription}");
    }

    private void OfferCardChoice()
    {
        var pool = DataManager.Instance.Cards.Items;
        if (pool == null || pool.Count == 0) return;

        var candidates = new List<CardsSO>();
        for (int i = 0; i < 3; i++)
            candidates.Add(pool[Random.Range(0, pool.Count)]);

        rewardCanvas.Offer(candidates, so => deck.AddCard(so, toTop: true), () => { });
    }
}
```

- [ ] **Step 2: Verify it compiles**

🖱️ UNITY EDITOR (manual): Let Unity recompile.
Expected: **0 compile errors**. The `Rewards` component now shows two new empty serialized slots (`Deck`, `Reward Canvas`).

- [ ] **Step 3: Wire Rewards in the Inspector**

🖱️ UNITY EDITOR (manual):
1. Select the object with the `Rewards` component.
2. Confirm `Player` and `Crystals` are still assigned.
3. Assign **Deck** = the PlayerDeck object.
4. Assign **Reward Canvas** = the reward canvas object (the one with `RewardCanvas`).

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/Rewards.cs"
git add -A -- "Assets/Scenes/GameBoard.unity"
git commit -m "feat: single Grant path for rewards; fix flag bugs; enemy card-reward branch"
```

---

### Task B5: Remove reward mode from `Card` + dead event wiring

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs`
- Editor: remove the old `GetCardRewards → RewardCanvas.SetCardRewards` event listener and the old `onRewardSelect_AddCardToDeck → PlayerDeck.AddRewardToDeck` listener if present.

**Interfaces:**
- Produces: a `Card` with no reward responsibilities. No new public surface.

- [ ] **Step 1: Remove the reward field and property**

In `Card.cs`, delete the `isReward` field (line 14: `private bool isReward;`) and the entire `IsReward` property (lines 59-69).

- [ ] **Step 2: Remove the reward event field**

In `Card.cs`, delete the line:

```csharp
    [SerializeField] CardEvent onRewardSelect_AddCardToDeck;
```

- [ ] **Step 3: Simplify OnPointerClick**

In `Card.cs`, replace the whole `OnPointerClick` method (lines 140-165) with the non-reward version:

```csharp
    public void OnPointerClick(PointerEventData eventData)
    {
        if(GameManager.Instance.cardListCanvas.enabled)
            return;

        if(isMaximized)
        {
            GameManager.Instance.cardCanvas.enabled = false;
            onCloseCardMenu_MinimizeCard.Raise(this);
            onClick_CloseCardMenu.Raise(this);
        }
        else if(!isMaximized && !isPlayed)
        {
            GameManager.Instance.cardCanvas.enabled = true;
            onClick_OpenCardMenu.Raise(this);
            onOpenCardMenu_MaximizeCard.Raise(this);
        }
        else if (isPlayed)
        {
            GameManager.Instance.ValidationMessage($"{cardSO.name} has already been played. Click Undo on the Gameboard to undo previous plays.");
        }
    }
```

- [ ] **Step 4: Verify it compiles**

🖱️ UNITY EDITOR (manual): Let Unity recompile.
Expected: **0 compile errors**. The `Card` component no longer shows the `On Reward Select_Add Card To Deck` field on prefabs/instances.

- [ ] **Step 5: Remove dead event listeners**

🖱️ UNITY EDITOR (manual):
1. Inspect the reward canvas object and the `GetCardRewards` `VoidEvent` listeners. Remove any `GameEventListener` response still pointing to `RewardCanvas.SetCardRewards` or `RemovePreviousRewards` (those methods no longer exist).
2. If any object had a listener calling `PlayerDeck.AddRewardToDeck`, remove that response.
3. Play Mode smoke test: open a normal (non-reward) card in hand → it still maximizes/opens the card menu as before (reward path removal didn't affect normal cards).

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/PlayerScripts/Card.cs"
git add -A -- "Assets/Scenes/GameBoard.unity"
git commit -m "refactor: remove reward mode from Card and dead reward event wiring"
```

---

### Task B6: Full reward flow verification (incl. save/load)

**Files:** none (verification only).

- [ ] **Step 1: Card reward — pick one**

🖱️ UNITY EDITOR (manual): Enter Play Mode. Defeat an enemy whose `defeatRewards` includes a `Cards` reward (or trigger a dungeon card reward).
Expected: the reward canvas opens with **3** card previews.
- Click one preview → exactly **one** matching card is added to the deck and is the **next** card drawn; the other two previews are destroyed; the canvas closes.
- Click where a preview used to be → nothing happens (no extra cards). The deck count increased by exactly 1.

- [ ] **Step 2: Card reward — skip**

🖱️ UNITY EDITOR (manual): Trigger another card reward and click **Skip**.
Expected: canvas closes, **no** card added (deck count unchanged).

- [ ] **Step 3: Non-card rewards still apply once**

🖱️ UNITY EDITOR (manual): Defeat an enemy with an Experience and/or Crystals reward.
Expected: player exp increases by the reward's `expAmount` exactly once; a crystal is created exactly once; no card canvas opens (unless the reward also has the Cards flag).

- [ ] **Step 4: Reward applies on save/load**

🖱️ UNITY EDITOR (manual): Take a card reward (pick one), let the board settle, **Save**, then **Load**.
Expected: the chosen card is present in the restored deck (validated via deck contents / `deckCardIds`); no console errors. This is the core goal: a reward applied before saving survives the round-trip.

- [ ] **Step 5: Commit (verification notes, if any)**

No code change expected. If a verification revealed a defect, fix it in the relevant task's file and commit with a `fix:` message describing the observed-vs-expected behavior.

---

## Self-Review Notes

- **Spec coverage:** Flee button (A2), wound penalty (A1 step 2), de-aggro via active combatant (A1 steps 1-3) ✓. Pick-1-of-3 + Skip (B3, B6) ✓. Top-of-deck placement (B1 `toTop`, B4 `AddCard(..., toTop:true)`) ✓. Single materialization path (B1) ✓. CardPreview display-only (B2) ✓. Centralized Grant + flag-bug fixes + enemy Cards branch (B4) ✓. Remove `isReward` from Card (B5) ✓. Influence mechanic untouched (Global Constraints; no task modifies EnemyCard) ✓. Save/load capture (B6 step 4) ✓.
- **Deviation from spec naming:** Spec named `RewardService`/`RewardChoiceUI`; plan keeps `Rewards`/`RewardCanvas` class names to avoid breaking Unity component links (see Global Constraints). Same responsibilities, same single-grant/guarded-selection design.
- **Type consistency:** `AddCard(CardsSO, bool)` defined in B1, consumed in B4. `Offer(IReadOnlyList<CardsSO>, Action<CardsSO>, Action)` defined B3, consumed B4. `Bind(CardsSO, Action<CardsSO>)` defined B2, consumed B3. `FleeCombat()` defined A1, consumed A2. `activeCombatant` set in A1 step 3, read in A1 step 2. Consistent.
- **Testing approach:** Unity recompile + Play Mode (no gameplay test harness exists). Flagged in Global Constraints.
