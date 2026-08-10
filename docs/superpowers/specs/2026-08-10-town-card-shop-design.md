# Town Card Shop

**Date:** 2026-08-10
**Status:** Design approved — ready for implementation plan.
**Closes:** the M2 deferral "Castle card-purchase shop" (`2026-06-30-m2-place-type-system-design.md`).

## Summary

Places can sell cards. A place that lists cards for sale shows a **Cards** action; pressing it spends
the place's influence price, spends the visit's action, and opens the existing card-reward picker with
**three unique cards** drawn from that place's list. The price is a property of the **place**, not of
the card — every card at a given place costs the same. Skipping the pick forfeits the influence.

The same unique-selection rule retroactively applies to every other card offer in the game (defeats,
level-ups, dungeon completions, shrines), which today can show the same card twice.

## Design Decisions (locked)

- **The list is the gate.** A place sells cards when `TownsSO.purchasableCards` is non-empty —
  regardless of place type. `PlaceRules` stops granting `PlaceService.Cards` to Castle; nothing reads
  that flag afterwards. Authoring the list *is* the decision, so a Town or Keep can be a card seller
  with no rules change.
- **Price is `TownsSO.cardLevel`**, the field already authored on the town assets. No new cost field.
- **Fewer than 3 cards offers fewer options.** A 2-card place shows 2, a 1-card place shows 1, at the
  same full price. `RewardCanvas.Offer` already loops `min(locations, candidates)`.
- **Pay first, then pick.** Influence and the visit's action are spent on the button press, before the
  picker opens. That is what makes skipping non-refundable, and it means a mis-click costs the same as
  a purchase. This deliberately differs from the crystal pop-out, where the charge waits for a colour.
- **One purchase per visit.** Buying commits the visit's action (`TurnPhaseController.CommitVisitAction`),
  exactly like Heal, Recruit and Crystal.
- **No depletion.** The list is a menu, not stock. Re-visiting on a later turn rolls a fresh three, can
  offer a previously-bought card again, and can hand out a second copy. No per-place run state, so no
  save-schema bump.
- **Options are always unique** within one offer, and this becomes the single path for all card offers.

## Data & Rules

### `TownsSO`

One new field:

```csharp
// Cards this place sells. Non-empty = it offers the Cards service (the list IS
// the gate; place type no longer decides). Price is cardLevel, per purchase.
public List<CardsSO> purchasableCards = new List<CardsSO>();
```

### `PlaceRules`

`AllowedServices` drops `PlaceService.Cards` from the Castle line. The enum member stays (removing it
churns the flags value), but no rule reads it after this change.

### `ShopRules` (new pure class)

`Assets/Scripts/Rewards/ShopRules.cs`, inside the existing `ArchonsRise.Rewards` asmdef — no new
asmdef, and the EditMode tests already reference that assembly.

```csharp
public static List<T> PickUnique<T>(IReadOnlyList<T> pool, int count, Func<int,int> rng)
    where T : class
```

Drops nulls, dedupes by reference, then partial Fisher-Yates over the distinct set for `count` draws.
Returns fewer than `count` when the distinct pool is short, and an empty list for a null or empty pool.
`rng(exclusiveMax)` returns `[0, exclusiveMax)`, matching `RewardRules`' convention, so tests inject a
deterministic sequence. Generic and Unity-free, so tests exercise it with `string` under the mcs harness.

## Purchase Flow

Both routes — the place fan (primary) and the town canvas (secondary) — funnel through one method, so
the spend/commit/offer sequence exists exactly once.

### Fan route

`TownActionSnapshot` gains `SellsCards` (bool) and `CardCost` (int).

`PlaceActionRules.ForTown` stops emitting the M2 stub and gates on the list instead. The slot keeps its
position (after Heal, before Crystal) so the fan never reshuffles between opens:

```csharp
if (s.SellsCards)
    list.Add(new PlaceAction(PlaceActionId.Cards, IconConcept.Card,
        IconConcept.Influence, s.CardCost,
        s.Influence >= s.CardCost && s.VisitCanAct));
```

`TownToken.BuildActions` feeds it `townSO.purchasableCards.Count > 0` and `townSO.cardLevel`.
`TownToken.Dispatch(PlaceActionId.Cards)` calls `BuyCards()`.

### The purchase method

```csharp
// Influence and the turn's action are spent HERE, before the picker opens —
// which is why skipping the pick refunds nothing (unlike the crystal pop-out,
// where the charge waits for a colour). Spending straight through
// Player.Influence mirrors RecruitPanel.Hire and sidesteps the two-IntEvent trap
// (AdjustPlayerInfluence spends; GetCurrentInfluence only rebroadcasts).
public void BuyCards()
{
    if (PlayerStats != null) PlayerStats.Influence(townSO.cardLevel);
    if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
    FindAnyObjectByType<Rewards>().OfferTownCards(townSO);
}
```

`Rewards` is resolved at click time rather than serialized, for the same reason `RecruitPanel` is:
`TownToken` is spawned from a prefab by `GridGeneration` while `Rewards` lives only in the scene, so
the reference is unauthorable.

### Canvas route

`CardButton.UpdateButtonText` replaces its "Cards (soon)" stub:

- **Visible** when `purchasableCards` is non-empty **and** `ConquestTracker.IsConquered(gridPos)`;
  otherwise `SetActive(false)`, as today.
- **Text**: `{Cards icon} Cards — {Influence icon}{cardLevel}`, via `IconMarkup.Cost`, matching
  Heal and Crystal.
- **Interactable** when `currentPlayerInfluence >= cardLevel && CanActThisVisit`, with `SyncLock()`
  and the same `Update()` re-check Heal and Crystal use.
- **On click**: `_town.BuyCards()` — nothing else. No `townEvent`, no `influenceCostEvent`, no
  separate commit call, because `BuyCards` owns all of it.

## The Offer Path

New on `Rewards`:

```csharp
// Town card shop: pay-then-pick. The purchase is already charged by the caller,
// so a skip is a forfeit, not a refund.
public void OfferTownCards(TownsSO town)
{
    var pool = town.purchasableCards;
    if (pool == null || pool.Count == 0) return;
    RewardQueue.Instance.Enqueue(done =>
    {
        var candidates = ShopRules.PickUnique(pool, 3, max => Random.Range(0, max));
        rewardCanvas.Offer(candidates,
            so => { deck.AddCard(so, toTop: true); done(); },
            () => done());
    });
}
```

The purchased card goes to the top of the draw pile through `PlayerDeck.AddCard(so, toTop: true)` —
the same path every other card reward uses.

Self-enqueueing on `RewardQueue` is mandatory (standing rule: never open a reward canvas directly), and
callers must not wrap this in their own `Enqueue`, matching `OfferCardChoice`'s contract.

`OfferCardChoice` swaps its with-replacement sampling loop for the same
`ShopRules.PickUnique(pool, 3, ...)` call. After this change every card offer in the game — defeats,
level-ups, dungeon bundles, shrines, shops — yields unique options.

## Testing

**`ShopRulesTests`** (new, `ArchonsRise.Rewards` asmdef, `string`-typed so it runs under the mcs
pure-test harness):

- a pool containing repeats never yields a duplicate
- returns `min(count, distinctPoolSize)` entries
- nulls are dropped
- null pool and empty pool both return an empty list
- a scripted rng produces a deterministic, asserted selection

**`PlaceActionRulesTests`** (existing, extended):

- conquered town, non-empty list, affordable → enabled `Cards` slot carrying an Influence badge of
  `CardCost`
- empty list → no `Cards` slot
- influence below `CardCost` → slot present but locked
- visit action already spent → slot present but locked
- a `PlaceType.Town` (not just Castle) with a list gets the slot
- the slot sits between Heal and Crystal in the returned order

**`PlaceRulesTests`** (existing, updated): Castle no longer reports `PlaceService.Cards`.

## Manual Editor Work

Per the standing convention, scene and asset wiring is done by hand from step-by-step instructions:

- Author `purchasableCards` on each place that should sell cards.
- Confirm `cardLevel` on those assets — it is now the purchase price. Currently authored:
  CastleBrune 5, Garth Barracks 6, SirensGateKeep 5, Rags Town 7, and **Merchant Village 0**, which
  would make it a free shop if it is given a card list.
- No new scene wiring: the fan slot is prefab-driven and `IconConcept.Card` already resolves in
  `IconRegistry` (the stub slot renders today), and `CardButton` needs only its existing `_town`
  reference.

## Out of Scope

- Per-place stock depletion and restocking.
- Card-specific pricing (a place has one price for everything it sells).
- Selling or removing cards from the deck at a place.
- Renaming `cardLevel`, or fixing `TownCard.cs:23`, which renders `cardLevel` as a legacy
  "raze amount" on the town card.
