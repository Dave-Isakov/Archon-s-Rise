# Card List Redesign — Read-Only Deck Viewer

**Date:** 2026-07-14
**Status:** Approved

## Purpose

The card list canvas exists to show the player every card in their run deck in a
visible, easy-to-scan way, with scrolling once the deck outgrows one screen. The
current implementation instead physically reparents the *live* Card GameObjects
out of the hand, draw pile, and discard into `cardListParent`, then moves them
back on close. Since the hand rework (HandFanLayout), hand cards carry fan tilt,
focus lift/scale, and dim alpha into the list, and the list is coupled to every
zone's internals — any zone change can break it.

This redesign makes the card list a pure read-only display built from card
*data*, fully decoupled from the live zones.

## Decisions (from brainstorming)

- **Scope:** all owned cards — hand + draw pile + discard combined.
- **Ordering:** grouped by card type, then name. Every copy appears as its own
  card (no ×N stacking). Draw-pile order is deliberately hidden.
- **Interaction:** read-only. Scroll to browse; clicks do nothing (the existing
  `ToggleInspect` guard already blocks inspect while the list is open).

## Architecture

### New: `CardListController` (MonoBehaviour, on the card list canvas)

Serialized refs: `PlayerHand`, `PlayerDeck`, `DiscardPile`, Card prefab,
ScrollRect content `Transform`.

- `Open()`:
  1. Destroy any leftover clones under the content (defensive — covers the
     run-end path, where `RunEndController` force-disables the canvas without
     going through `Close()`).
  2. Gather `CardsSO` from `PlayerHand.cardsInPlay`, `PlayerDeck.CardsInDeck`,
     `DiscardPile.Cards`.
  3. Order them via `CardListPlan`.
  4. Instantiate one Card-prefab clone per entry under the content, assigning
     only `cardSO` and name. `Card.Start()` populates text, wound styling, and
     empower colors. Clones are inert: `isPlayed` false, zone flags false.
  5. Enable `cardListCanvas`.
- `Close()`: disable the canvas, destroy the clones.

Live cards never move, reparent, or change state.

### New: `CardListPlan` (pure static class)

Follows the repo's pure-class TDD pattern (`DrawGate`, `HandNavRules`,
`FanMath`). `CardsSO` is a ScriptableObject and can't be referenced from the
pure harness, so the interface is data-only: takes per-card `(StatType type,
string name)` entries and returns the display order as a list of indices into
the input (sorted by type, then name; stable for equal keys). The controller
maps indices back to `CardsSO`. Unit-tested via the mcs CLI harness.

### Deletions

- `PlayerDeck.DeckToCardList()`
- `PlayerHand.HandToCardList()`
- `DiscardPile.SetCardList()`
- `GameManager.cardListParent` (only those three methods used it)

All other `cardListCanvas.enabled` guards (save blocking in DataManager,
TurnFlowShortcuts, HandFocusController, `Card.ToggleInspect`) remain valid and
unchanged.

### Scene changes (manual, in-editor, from step-by-step instructions)

- Under the card list canvas: a Scroll View — viewport + content, vertical
  scrolling only. Content gets `GridLayoutGroup` + `ContentSizeFitter`
  (vertical preferred size), so rows wrap and the content grows downward.
- Open button: replace the current persistent calls (`set_enabled` + the three
  zone methods) with `CardListController.Open`.
- Close button: same, with `CardListController.Close`.
- Assign `CardListController` refs (hand, deck, discard, card prefab, content).

## Data Flow

Open button → `CardListController.Open()` → zones → `List<CardsSO>` →
`CardListPlan` order → clone instantiation → `GridLayoutGroup` → ScrollRect.

## Error Handling

- Null zone refs: skip that zone (log a warning) rather than throw.
- Re-entrant `Open()` (e.g. after run-end force-close): step 1's defensive
  clear makes it idempotent.

## Testing

- `CardListPlan`: TDD via the mcs pure-test harness (type-then-name ordering,
  duplicate preservation, empty input, single zone empty).
- Manual in-editor verification: open list mid-round with cards in all three
  zones — cards appear upright, unscaled, undimmed, grouped by type; hand fan
  is intact after closing; scrolling works with an oversized deck; save is
  still blocked while open; run-end force-close followed by reopen shows no
  duplicates.
