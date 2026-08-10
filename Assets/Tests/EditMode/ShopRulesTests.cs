using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ShopRulesTests
{
    static Func<int, int> Seq(params int[] values)
    {
        int i = 0;
        return _ => values[Math.Min(i++, values.Length - 1)];
    }

    static List<string> Pool(params string[] entries) => new List<string>(entries);

    [Test]
    public void PicksRequestedCount()
    {
        var picked = ShopRules.PickUnique(Pool("A", "B", "C", "D"), 3, _ => 0);
        Assert.AreEqual(3, picked.Count);
    }

    [Test]
    public void ZeroRngWalksThePoolInOrder()
    {
        var picked = ShopRules.PickUnique(Pool("A", "B", "C", "D"), 3, _ => 0);
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, picked);
    }

    [Test]
    public void ScriptedRngProducesADeterministicSelection()
    {
        var picked = ShopRules.PickUnique(Pool("A", "B", "C", "D"), 3, Seq(3, 0, 0));
        CollectionAssert.AreEqual(new[] { "D", "B", "C" }, picked);
    }

    [Test]
    public void DuplicateEntriesAreCollapsed()
    {
        var picked = ShopRules.PickUnique(Pool("A", "A", "B"), 3, _ => 0);
        CollectionAssert.AreEqual(new[] { "A", "B" }, picked);
    }

    [Test]
    public void NeverRepeatsAnEntryAcrossManyRolls()
    {
        var rand = new System.Random(20260810);
        var pool = Pool("A", "B", "A", "C", "B", "D", "E");
        for (int i = 0; i < 200; i++)
        {
            var picked = ShopRules.PickUnique(pool, 3, max => rand.Next(max));
            Assert.AreEqual(3, picked.Count);
            CollectionAssert.AllItemsAreUnique(picked);
        }
    }

    [Test]
    public void ShortPoolReturnsWhatItHas()
    {
        var picked = ShopRules.PickUnique(Pool("A", "B"), 3, _ => 0);
        Assert.AreEqual(2, picked.Count);
    }

    [Test]
    public void NullEntriesAreDropped()
    {
        var picked = ShopRules.PickUnique(Pool("A", null, "B"), 3, _ => 0);
        CollectionAssert.AreEqual(new[] { "A", "B" }, picked);
    }

    [Test]
    public void EmptyPoolReturnsEmpty()
    {
        Assert.AreEqual(0, ShopRules.PickUnique(Pool(), 3, _ => 0).Count);
    }

    [Test]
    public void NullPoolReturnsEmpty()
    {
        Assert.AreEqual(0, ShopRules.PickUnique<string>(null, 3, _ => 0).Count);
    }

    [Test]
    public void ZeroCountReturnsEmpty()
    {
        Assert.AreEqual(0, ShopRules.PickUnique(Pool("A", "B"), 0, _ => 0).Count);
    }
}
