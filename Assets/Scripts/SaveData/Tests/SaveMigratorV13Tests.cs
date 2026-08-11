using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV13Tests
{
    [Test]
    public void V12_CountBecomesLeadingIndices()
    {
        var file = new SaveFile { schemaVersion = 12 };
        file.run.places = new[]
        {
            new PlaceConquest { x = 3, y = 4, defeatedCount = 2 },
            new PlaceConquest { x = 7, y = 1, defeatedCount = 1 },
        };

        var m = SaveMigrator.Migrate(file);

        Assert.AreEqual(13, m.schemaVersion);
        CollectionAssert.AreEqual(new[] { 0, 1 }, m.run.places[0].defeatedIndices);
        CollectionAssert.AreEqual(new[] { 0 }, m.run.places[1].defeatedIndices);
    }

    [Test]
    public void V13_LeavesExplicitIndicesAlone()
    {
        var file = new SaveFile { schemaVersion = 13 };
        file.run.places = new[]
        {
            new PlaceConquest { x = 3, y = 4, defeatedCount = 1, defeatedIndices = new[] { 1 } },
        };

        var m = SaveMigrator.Migrate(file);

        Assert.AreEqual(13, m.schemaVersion);
        CollectionAssert.AreEqual(new[] { 1 }, m.run.places[0].defeatedIndices);
    }

    [Test]
    public void Migrate_IsIdempotent()
    {
        var file = new SaveFile { schemaVersion = 12 };
        file.run.places = new[] { new PlaceConquest { x = 0, y = 0, defeatedCount = 2 } };

        var once = SaveMigrator.Migrate(file);
        var twice = SaveMigrator.Migrate(once);

        Assert.AreEqual(13, twice.schemaVersion);
        CollectionAssert.AreEqual(new[] { 0, 1 }, twice.run.places[0].defeatedIndices);
    }
}
