using System.Collections.Generic;

// Who joins a field fight (spec 2026-08-01). A field encounter is never a duel by
// default: every enemy standing next to the player is dragged into it, so a pack
// can't be picked apart one at a time while the neighbours watch.
//
// Pure decision layer — the caller supplies the board facts (which tokens are on a
// player-adjacent cell, which are still under fog), so this has no UnityEngine
// dependency and is trivially unit-testable, matching HexActionRules.
public static class FieldEncounterRules
{
    // The participating token indices, source first, then the rest in input order.
    //
    // The source ALWAYS participates: it is the token that started the fight (the
    // one clicked, or the one the player got cornered by), and its own adjacency is
    // the caller's business, not this rule's.
    //
    // Everyone else joins only if they are adjacent to the player AND visible. Fog
    // is absolute here: you only ever fight what you can see, mirroring the existing
    // rule that a fog-hidden token neither glows nor answers a click.
    public static List<int> Participants(int sourceIndex, IReadOnlyList<bool> adjacentToPlayer,
                                         IReadOnlyList<bool> fogHidden)
    {
        var result = new List<int>();
        if (adjacentToPlayer == null || fogHidden == null) return result;

        int n = adjacentToPlayer.Count < fogHidden.Count ? adjacentToPlayer.Count : fogHidden.Count;
        if (sourceIndex >= 0 && sourceIndex < n) result.Add(sourceIndex);

        for (int i = 0; i < n; i++)
        {
            if (i == sourceIndex) continue;
            if (adjacentToPlayer[i] && !fogHidden[i]) result.Add(i);
        }
        return result;
    }
}
