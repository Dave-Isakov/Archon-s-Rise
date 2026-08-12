using System;
using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV14Tests
{
    [Test]
    public void V13_GetsEmptyUnitWounds()
    {
        var f = new SaveFile { schemaVersion = 13 };
        f.run.unitWounds = null;

        var m = SaveMigrator.Migrate(f);

        Assert.IsNotNull(m.run.unitWounds);
        Assert.AreEqual(0, m.run.unitWounds.Length);
        Assert.AreEqual(14, m.schemaVersion);
    }

    [Test]
    public void V14_IsUnchanged()
    {
        var f = new SaveFile { schemaVersion = 14 };
        f.run.unitWounds = new[] { 0, 2, 1 };

        var m = SaveMigrator.Migrate(f);

        CollectionAssert.AreEqual(new[] { 0, 2, 1 }, m.run.unitWounds);
        Assert.AreEqual(14, m.schemaVersion);
    }

    [Test]
    public void Migrate_IsIdempotent()
    {
        var f = new SaveFile { schemaVersion = 13 };
        var once = SaveMigrator.Migrate(f);
        var twice = SaveMigrator.Migrate(once);

        Assert.AreEqual(14, twice.schemaVersion);
        Assert.IsNotNull(twice.run.unitWounds);
    }
}
