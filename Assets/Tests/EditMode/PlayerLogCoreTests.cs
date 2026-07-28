using NUnit.Framework;

public class PlayerLogCoreTests
{
    [Test]
    public void AppendingPastCapacityEvictsOldest()
    {
        var log = new PlayerLogCore();
        for (int i = 0; i < PlayerLogCore.Capacity + 5; i++) log.Append(1, "e" + i);
        Assert.AreEqual(PlayerLogCore.Capacity, log.Count);
        Assert.AreEqual("e104", log.Entries[0].Text, "newest survives");
        Assert.AreEqual("e5", log.Entries[PlayerLogCore.Capacity - 1].Text, "oldest five evicted");
    }

    [Test]
    public void EntriesAreNewestFirst()
    {
        var log = new PlayerLogCore();
        log.Append(1, "first");
        log.Append(1, "second");
        Assert.AreEqual("second", log.Entries[0].Text);
        Assert.AreEqual("first", log.Entries[1].Text);
    }

    [Test]
    public void DividerMarksTheFirstEntryAndEveryDayChange()
    {
        var log = new PlayerLogCore();
        log.Append(1, "d1a");
        log.Append(1, "d1b");
        log.Append(2, "d2a");
        Assert.IsTrue(log.NeedsDayDivider(0), "newest entry always opens a day header");
        Assert.IsTrue(log.NeedsDayDivider(1), "the day 2 -> day 1 boundary");
        Assert.IsFalse(log.NeedsDayDivider(2), "same day as the entry above it");
    }

    [Test]
    public void DividerSurvivesEvictionOfADaysFirstEntry()
    {
        var log = new PlayerLogCore();
        log.Append(1, "oldest");
        for (int i = 0; i < PlayerLogCore.Capacity; i++) log.Append(2, "d2-" + i);
        Assert.AreEqual(PlayerLogCore.Capacity, log.Count);
        Assert.IsTrue(log.NeedsDayDivider(0));
        for (int i = 1; i < log.Count; i++)
            Assert.IsFalse(log.NeedsDayDivider(i), "only one day remains after eviction");
    }

    [Test]
    public void ClearEmptiesTheBuffer()
    {
        var log = new PlayerLogCore();
        log.Append(1, "x");
        log.Clear();
        Assert.AreEqual(0, log.Count);
    }
}
