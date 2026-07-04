using System.Collections.Generic;

// Moves cards between zone lists (hand/discard -> deck). The round-end reshuffle
// bug was zones clearing their lists without handing the cards to the deck; every
// zone transfer must go through here so cards are never dropped. Pure, no scene
// dependency.
public static class CardZonePlan
{
    // Appends everything in `from` onto `to`, then empties `from`.
    public static void TransferAll<T>(List<T> from, List<T> to)
    {
        to.AddRange(from);
        from.Clear();
    }
}
