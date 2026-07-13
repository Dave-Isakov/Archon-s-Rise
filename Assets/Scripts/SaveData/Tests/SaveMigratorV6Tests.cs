using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV6Tests
{
    [Test]
    public void V5File_GainsEmptyDungeonFields()
    {
        var f = new SaveFile { schemaVersion = 5 };
        f.run.dungeons = null; // absent in v5 JSON
        var m = SaveMigrator.Migrate(f);
        Assert.AreEqual(6, m.schemaVersion);
        Assert.IsNotNull(m.run.dungeons);
        Assert.AreEqual(0, m.run.dungeons.Length);
        Assert.IsFalse(m.run.dungeonMidFlagsFired);
        Assert.IsFalse(m.run.dungeonHighFlagsFired);
    }

    [Test]
    public void V6File_IsUntouched()
    {
        var f = new SaveFile { schemaVersion = 6 };
        f.run.dungeons = new[] { new DungeonState
            { x = 4, y = 5, dungeonId = "dungeon-sunken-crypt", defeatedCount = 2, flagged = true } };
        f.run.dungeonMidFlagsFired = true;
        var m = SaveMigrator.Migrate(f);
        Assert.AreEqual(6, m.schemaVersion);
        Assert.AreEqual(1, m.run.dungeons.Length);
        Assert.AreEqual(2, m.run.dungeons[0].defeatedCount);
        Assert.IsTrue(m.run.dungeons[0].flagged);
        Assert.IsTrue(m.run.dungeonMidFlagsFired);
    }
}
