using System.Collections.Generic;
using NUnit.Framework;

public class FieldEncounterRulesTests
{
    [Test]
    public void LoneEnemy_FightsAlone()
    {
        var p = FieldEncounterRules.Participants(0,
            adjacentToPlayer: new[] { true },
            fogHidden: new[] { false });
        CollectionAssert.AreEqual(new List<int> { 0 }, p);
    }

    [Test]
    public void AdjacentNeighbour_JoinsTheFight()
    {
        var p = FieldEncounterRules.Participants(0,
            adjacentToPlayer: new[] { true, true },
            fogHidden: new[] { false, false });
        CollectionAssert.AreEqual(new List<int> { 0, 1 }, p);
    }

    [Test]
    public void TwoAdjacentNeighbours_BothJoin()
    {
        var p = FieldEncounterRules.Participants(0,
            adjacentToPlayer: new[] { true, true, true },
            fogHidden: new[] { false, false, false });
        CollectionAssert.AreEqual(new List<int> { 0, 1, 2 }, p);
    }

    [Test]
    public void DistantEnemy_StaysOut()
    {
        var p = FieldEncounterRules.Participants(0,
            adjacentToPlayer: new[] { true, false },
            fogHidden: new[] { false, false });
        CollectionAssert.AreEqual(new List<int> { 0 }, p);
    }

    [Test]
    public void FogHiddenNeighbour_StaysOut()
    {
        var p = FieldEncounterRules.Participants(0,
            adjacentToPlayer: new[] { true, true },
            fogHidden: new[] { false, true });
        CollectionAssert.AreEqual(new List<int> { 0 }, p);
    }

    // The source leads the ring no matter where it sits in the input, so the
    // clicked (or cornering) enemy is the one the fan is built around.
    [Test]
    public void Source_IsAlwaysFirst()
    {
        var p = FieldEncounterRules.Participants(2,
            adjacentToPlayer: new[] { true, true, true },
            fogHidden: new[] { false, false, false });
        CollectionAssert.AreEqual(new List<int> { 2, 0, 1 }, p);
    }

    // Defensive: the source is the fight's reason for existing, so it participates
    // even if the caller's adjacency snapshot disagrees.
    [Test]
    public void Source_ParticipatesEvenIfNotFlaggedAdjacent()
    {
        var p = FieldEncounterRules.Participants(0,
            adjacentToPlayer: new[] { false, true },
            fogHidden: new[] { false, false });
        CollectionAssert.AreEqual(new List<int> { 0, 1 }, p);
    }

    [Test]
    public void NoTokens_ReturnsEmpty()
    {
        var p = FieldEncounterRules.Participants(0,
            adjacentToPlayer: new bool[0],
            fogHidden: new bool[0]);
        CollectionAssert.IsEmpty(p);
    }
}
