using System.Collections.Generic;
using NUnit.Framework;

public class CardZonePlanTests
{
    [Test]
    public void TransferAll_MovesEveryItemToDestination_AndEmptiesSource()
    {
        var deck = new List<string> { "a" };
        var discard = new List<string> { "b", "c" };

        CardZonePlan.TransferAll(discard, deck);

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, deck);
        Assert.IsEmpty(discard);
    }

    [Test]
    public void TransferAll_EmptySource_LeavesDestinationUnchanged()
    {
        var deck = new List<string> { "a" };
        var discard = new List<string>();

        CardZonePlan.TransferAll(discard, deck);

        CollectionAssert.AreEqual(new[] { "a" }, deck);
    }

    [Test]
    public void TransferAll_HandThenDiscard_AccumulatesBothIntoDeck()
    {
        var deck = new List<string>();
        var hand = new List<string> { "h1", "h2" };
        var discard = new List<string> { "d1" };

        CardZonePlan.TransferAll(hand, deck);
        CardZonePlan.TransferAll(discard, deck);

        CollectionAssert.AreEqual(new[] { "h1", "h2", "d1" }, deck);
        Assert.IsEmpty(hand);
        Assert.IsEmpty(discard);
    }

    [Test]
    public void TransferAll_TransferringTwice_SecondCallIsNoOp()
    {
        var deck = new List<string>();
        var discard = new List<string> { "d1" };

        CardZonePlan.TransferAll(discard, deck);
        CardZonePlan.TransferAll(discard, deck);

        CollectionAssert.AreEqual(new[] { "d1" }, deck);
    }
}
