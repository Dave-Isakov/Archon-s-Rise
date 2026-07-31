using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV11Tests
{
    [Test]
    public void V10File_MigratesToV11_WithNoShortfallPending()
    {
        var file = new SaveFile { schemaVersion = 10 };

        SaveMigrator.Migrate(file);

        Assert.AreEqual(12, file.schemaVersion);
        // A pre-v11 save predates the deck-shortfall buffer entirely, so the
        // missing field must default to false (no forced rest carried into load).
        Assert.IsFalse(file.run.deckShortfallPending);
    }

    [Test]
    public void V11File_PreservesShortfallPending()
    {
        var file = new SaveFile { schemaVersion = 11 };
        file.run.deckShortfallPending = true;

        SaveMigrator.Migrate(file);

        Assert.AreEqual(12, file.schemaVersion);
        Assert.IsTrue(file.run.deckShortfallPending);
    }
}
