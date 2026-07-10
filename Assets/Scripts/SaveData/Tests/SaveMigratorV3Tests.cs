using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV3Tests
{
    [Test]
    public void MigratesV2_DefaultsSkillArraysAndBumpsVersion()
    {
        var file = new SaveFile { schemaVersion = 2 };
        file.run.player.ownedSkillIds = null;      // v2 json has no such keys
        file.run.player.exhaustedSkillIds = null;

        var migrated = SaveMigrator.Migrate(file);

        // v2 files migrate all the way to the current schema (now v5).
        Assert.AreEqual(5, migrated.schemaVersion);
        Assert.IsNotNull(migrated.run.player.ownedSkillIds);
        Assert.IsEmpty(migrated.run.player.ownedSkillIds);
        Assert.IsNotNull(migrated.run.player.exhaustedSkillIds);
        Assert.IsEmpty(migrated.run.player.exhaustedSkillIds);
    }

    // new SaveFile() defaults to the current schema version, so migrating it is a
    // no-op: the version is unchanged and existing fields are preserved.
    [Test]
    public void MigrationIsIdempotentOnCurrent()
    {
        var file = new SaveFile();
        file.run.player.ownedSkillIds = new[] { "skill-envoy" };
        var migrated = SaveMigrator.Migrate(file);
        Assert.AreEqual(5, migrated.schemaVersion);
        Assert.AreEqual(new[] { "skill-envoy" }, migrated.run.player.ownedSkillIds);
    }
}
