# Deck-Shortfall Buffer Turn — Design

**Date:** 2026-07-30
**Status:** Approved, ready for implementation plan
**Amends:** `2026-07-21-turn-phase-system-design.md` §"deck can't refill" secondary round-end trigger.

## Problem

The 2026-07-21 turn-phase spec gave the round two auto-end triggers: the turn budget running out, or
the deck being unable to refill the hand. The second trigger was implemented as an *immediate*
same-press check (`DrawGate.Evaluate` on the live hand/deck counts), which caused two problems:

1. **A real bug** ([EndTurnButton.cs](../../../Assets/Scripts/GameObjectScripts/DeckScripts/EndTurnButton.cs)):
   `TurnPhaseController.EndTurnPressed()` calls `ClearStack()` (which commits played cards out of the
   hand) *before* evaluating the draw verdict used for the round-over decision, while the button's
   label predicted the same verdict from the *live, pre-commit* hand state every frame. Whenever the
   deck was empty and the hand was "full" only because of an uncommitted play, the label read "End
   Turn" but the press actually force-ended the round.
2. **A gameplay-feel problem**, independent of the bug: forcing the round to end the instant a
   deck-starved refill would fall short means an efficient player who empties their hand fast can burn
   through a whole day in 2-3 turns, with no way to choose to spread remaining actions across more
   turns. Ending a turn should never feel like it's punishing the player for having played well.

## Goals

- The round auto-ends on turn budget exhaustion (unchanged) **or** on a **one-turn-delayed** deck
  shortfall: if a turn's end-of-turn hand top-up comes up short (the deck didn't have enough cards left
  to fully refill the hand), that turn ends normally, but the *next* turn's End Turn button reads "End
  Day" and pressing it ends the round no matter how much of the hand was played that turn.
- The button label always reflects reality — no live-state prediction that can diverge from what the
  press actually does.
- This state (`DeckShortfallPending`) survives save/reload mid-round.

## Non-goals

- No change to turn-budget math, Doom-band turn tables, or round-end behavior itself (discard rest of
  hand + reshuffle + fresh full hand + Doom tick + unit/skill refresh — already correct and unchanged).
- No change to `HandFullUnplayed()` (the existing "can't end a turn with a fully unplayed hand" gate) —
  orthogonal and untouched. A buffer turn where the player hasn't played anything yet still blocks on
  that gate first, same as today.
- No change to `DrawGate` — it keeps its existing role gating individual `TryDrawCard()` calls
  (max-hand-size message, silent no-op on an empty deck). This feature does not reuse `DrawGate`; it
  adds a separate, purpose-built check for "will this turn's top-up fully succeed."

## Mechanic

One new piece of round state, owned by `TurnPhaseController`:

```
DeckShortfallPending: bool
```

True for a turn means: **last turn's** end-of-turn hand top-up came up short. When true, ending *this*
turn always ends the round, regardless of remaining turn budget.

Worked example (5-card hand and deck, matching the reference walkthrough):

| Turn | State at press | Needed draws | Shortfall this turn? | `DeckShortfallPending` going in | Outcome |
|---|---|---|---|---|---|
| A | play all 5, deck=5 | 5 | No (5 ≥ 5) | false | draws 5, hand=5, deck=0; label stays "End Turn" |
| B | play 2, deck=0 | 2 | Yes (0 < 2) | false (from A) | turn ends normally (not the round); hand ends at 3; flag flips true |
| C | flag=true → label already "End Day"; play 2, deck=0 | 4 | Yes | **true (from B)** | round ends: discard rest of hand, reshuffle, fresh full hand, new round |

Note the "needed draws" formula already collapses the old `DrawGate` "HandFull wins" behavior for free:
if the hand is already full (needed ≤ 0), `deckCount >= neededDraws` is trivially true regardless of
deck size — a full hand never registers a shortfall, matching the original spec's stated intent that a
full hand needs no draw.

## Components

### `RoundRules.cs` (pure, existing file)

Add:

```csharp
// Whether the end-of-turn top-up can fully restore the hand from the deck.
// False means this turn's refill falls short, which carries into next turn as
// a forced round-end (see IsRoundOver's second argument).
public static bool CanFullyRefill(int deckCount, int neededDraws) => deckCount >= neededDraws;
```

`IsRoundOver(int turnsRemainingAfterDecrement, bool forceOver)` is **unchanged** in signature and
implementation — only the meaning of its second argument shifts, from "would *this* press strand the
player" to "did the *previous* turn already strand the player." Update its doc comment to reflect that.

Remove the now-dead `RoundRules.DeckCanRefill(DrawVerdict)` and its test — superseded by
`CanFullyRefill`.

### `PlayerHand.cs`

Extract the target-hand-size math already in `DrawCardsAtTurnEnd()` so it can be previewed without
side effects:

```csharp
int TargetHandSizeAtTurnEnd() => Mathf.Max(1, player.PlayerHandSize - player.PendingHandPenalty);
int NeededDrawsAtTurnEnd() => TargetHandSizeAtTurnEnd() - cardsInPlay.Count;

// Read-only preview of this turn's top-up: does NOT consume PendingHandPenalty
// (DrawCardsAtTurnEnd does that for real). Used by TurnPhaseController to decide
// whether this turn's refill will fall short.
public bool CanFullyRefillAtTurnEnd() => RoundRules.CanFullyRefill(deck.CardsInDeck.Count, NeededDrawsAtTurnEnd());

public void DrawCardsAtTurnEnd()
{
    int target = TargetHandSizeAtTurnEnd();
    player.PendingHandPenalty = 0;
    var cardDiff = target - cardsInPlay.Count;
    DrawCards(cardDiff);
    CheckWoundHand(target);
}
```

### `TurnPhaseController.cs`

```csharp
public bool DeckShortfallPending { get; private set; }
public bool NextPressEndsRound =>
    RoundRules.IsRoundOver(RoundRules.NextTurnsRemaining(TurnsRemaining), DeckShortfallPending);
```

`EndTurnPressed()` reorders around the same commit point that caused the original bug, but now the
commit happens before reading *this* turn's shortfall (which is correct and intentional — a played
card that's about to be committed out of the hand should count toward "needed draws"):

```csharp
public void EndTurnPressed()
{
    GameManager.Instance.commands.ClearStack();

    var hand = FindAnyObjectByType<PlayerHand>();
    bool refillFallsShortThisTurn = hand != null && !hand.CanFullyRefillAtTurnEnd();
    bool forceRoundOver = DeckShortfallPending; // decided by LAST turn, not this one

    HarvestHotspotIfParked();
    endTheTurn.Raise();

    int next = RoundRules.NextTurnsRemaining(TurnsRemaining);
    if (RoundRules.IsRoundOver(next, forceRoundOver))
    {
        DeckShortfallPending = false; // fresh round, fresh deck
        endTheRound.Raise();
        if (RunEndController.HasEnded) return;
        StartRound();
    }
    else
    {
        TurnsRemaining = next;
        onTurnsRemainingChanged.Raise(TurnsRemaining);
        DeckShortfallPending = refillFallsShortThisTurn; // carried into next turn
        BeginTurn();
    }
}
```

`LoadState(int turnsRemaining, bool deckShortfallPending)` gains the second parameter to restore this
flag on load. `CurrentDrawVerdict()` and its `DrawGate`/`DrawVerdict` usage are removed entirely — no
longer needed.

### `EndTurnButton.cs`

The label collapses to a single read, with no per-frame hand/deck/`DrawGate` polling at all:

```csharp
void UpdateLabel()
{
    if (label == null || TurnPhaseController.Instance == null) return;
    label.text = TurnPhaseController.Instance.NextPressEndsRound ? "End Day" : "End Turn";
}
```

The `deck` field and the old `verdict`/`lastVerdict` bookkeeping for the label are removed. `hand` is
kept only for the unchanged `HandFullUnplayed()` gate. The tutorial one-shot trigger is kept but
re-based: track `lastDeckShortfallPending` and fire `onDeckCannotRefillTutorial` on the false→true
transition of `TurnPhaseController.Instance.DeckShortfallPending` (i.e. exactly when the label is about
to read "End Day" for a deck reason, at the start of the buffer turn — turn C in the table above), not
on a budget-driven "End Day".

### Tutorial tip (`deck-empty.asset`)

Current copy ("Press End the Day, it reshuffles...") is wrong under this design — a dry deck no longer
means "end now." New copy:

> "Your deck couldn't fully refill your hand — End Turn will end the day next."

`highlightTargetId` stays `end-turn-button`. `triggerEventId` stays `deck-cant-refill` (still fired via
`onDeckCannotRefillTutorial`, just re-based as above).

## Save schema (v11)

- `RunState` (`SaveModels.cs`) gains `public bool deckShortfallPending;`.
- `SaveFile.schemaVersion` default 10 → 11, with a doc comment describing the new field.
- `SaveMigrator`: v10 → v11 bump. No data-fixup branch needed — a missing bool already defaults to
  `false` via `JsonUtility` (same as `dungeonMidFlagsFired` and other bool fields added in earlier
  versions), which is the correct "no forced-rest carried into this load" default. Add a comment noting
  this explicitly, then bump `schemaVersion`.
- `DataManager.CaptureRunState()`: `run.deckShortfallPending = TurnPhaseController.Instance != null && TurnPhaseController.Instance.DeckShortfallPending;`
- `DataManager.RestoreNow()`: `TurnPhaseController.Instance.LoadState(run.turn, run.deckShortfallPending);`
- `SaveMigratorTests.cs`: bump the two hard-coded `AreEqual(10, migrated.schemaVersion)` assertions to
  11.
- New `SaveMigratorV11Tests.cs`, mirroring the existing per-version test files, covering the version
  bump and the default-false behavior for a pre-v11 file.

## Testing

- `RoundRulesTests`: new cases for `CanFullyRefill` — full hand needs 0 draws (always true regardless
  of deck size), deck short of needed draws (false), deck sufficient (true), deck empty with 0 needed
  (true). Remove the `DeckCanRefill(DrawVerdict)` test.
- `SaveMigratorV11Tests`: version bump + default-false on a pre-v11 file.
- `TurnPhaseController` and `EndTurnButton` remain MonoBehaviours and are not unit-tested under the
  project's current editor-open constraint (see `unity-editmode-tests-while-editor-open` memory).
  Verification is manual: run the game and walk through the exact A/B/C example above, confirming the
  label and the actual press agree at every step, and confirming a save/reload between turns B and C
  preserves the forced End Day on turn C.

## Files touched

- `Assets/Scripts/TurnFlow/RoundRules.cs`
- `Assets/Scripts/GameObjectScripts/PlayerScripts/PlayerHand.cs`
- `Assets/Scripts/Managers/TurnPhaseController.cs`
- `Assets/Scripts/GameObjectScripts/DeckScripts/EndTurnButton.cs`
- `Assets/Scripts/Tutorial/Tips/deck-empty.asset`
- `Assets/Scripts/SaveData/SaveModels.cs`
- `Assets/Scripts/SaveData/SaveMigrator.cs`
- `Assets/Scripts/SaveData/DataManager.cs`
- `Assets/Scripts/SaveData/Tests/SaveMigratorTests.cs`
- `Assets/Scripts/SaveData/Tests/SaveMigratorV11Tests.cs` (new)
- `Assets/Tests/EditMode/RoundRulesTests.cs`
