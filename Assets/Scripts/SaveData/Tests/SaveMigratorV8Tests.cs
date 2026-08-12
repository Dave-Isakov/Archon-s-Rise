using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV8Tests
{
    [Test]
    public void V7File_MigratesToV8_WithEmptyHotspots()
    {
        var file = new SaveFile { schemaVersion = 7 };
        file.run.hotspots = null; // a v7 file has no hotspots key

        SaveMigrator.Migrate(file);

        Assert.AreEqual(14, file.schemaVersion);
        Assert.IsNotNull(file.run.hotspots);
        Assert.AreEqual(0, file.run.hotspots.Length);
    }

    [Test]
    public void Migrate_PreservesExistingHotspots()
    {
        var file = new SaveFile { schemaVersion = 8 };
        file.run.hotspots = new[] { new HotspotState { x = 1, y = 2, hotspotId = "red", remainingCharges = 1 } };

        SaveMigrator.Migrate(file);

        Assert.AreEqual(1, file.run.hotspots.Length);
        Assert.AreEqual("red", file.run.hotspots[0].hotspotId);
    }
}
