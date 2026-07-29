// Where a wound card lands (spec 2026-07-29 §6.1). APPEND-ONLY — Unit = 2 is
// reserved for the unit-wounds phase.
//
// Destinations are COUNTING or NON-COUNTING for the wound-out loss. Hand and
// Discard both count (PlayerHand.TotalWoundCount enumerates them); Unit will
// NOT. Adding a zone to the loss axis must always be a deliberate edit in
// TotalWoundCount, never a side effect of adding a destination here.
public enum WoundDestination
{
    Hand = 0,
    Discard = 1,
}
