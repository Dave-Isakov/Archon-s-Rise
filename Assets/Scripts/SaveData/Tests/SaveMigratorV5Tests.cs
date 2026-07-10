using System;
using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV5Tests
{
    static SaveFile V4File()
    {
        var f = new SaveFile { schemaVersion = 4 };
        f.run.unitIds = new[] { "unit_knight", "unit_scout" };
        f.run.unitExhausted = null; // absent in v4 JSON
        return f;
    }

    [Test]
    public void V4_File_Gets_Empty_UnitExhausted()
    {
        var m = SaveMigrator.Migrate(V4File());
        Assert.IsNotNull(m.run.unitExhausted);
        Assert.AreEqual(0, m.run.unitExhausted.Length);
    }

    [Test]
    public void V4_File_Version_Bumps_To_5()
    {
        Assert.AreEqual(5, SaveMigrator.Migrate(V4File()).schemaVersion);
    }

    [Test]
    public void V5_File_Is_Untouched()
    {
        var f = new SaveFile { schemaVersion = 5 };
        f.run.unitExhausted = new[] { true, false };
        var m = SaveMigrator.Migrate(f);
        Assert.AreEqual(5, m.schemaVersion);
        CollectionAssert.AreEqual(new[] { true, false }, m.run.unitExhausted);
    }
}
