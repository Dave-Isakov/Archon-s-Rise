using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV7Tests
{
    [Test]
    public void V6File_GetsEmptyCharacterId()
    {
        var f = new SaveFile { schemaVersion = 6 };
        var m = SaveMigrator.Migrate(f);
        Assert.AreEqual(7, m.schemaVersion);
        // Empty means "pre-v7": DataManager resolves it to defaultCharacter.
        Assert.IsTrue(string.IsNullOrEmpty(m.run.characterId));
    }

    [Test]
    public void V6File_CopiesLegacyHpIntoToughness()
    {
        var f = new SaveFile { schemaVersion = 6 };
        f.run.player.hp = 4;          // v6 JSON key
        f.run.player.toughness = 0;   // absent in v6
        var m = SaveMigrator.Migrate(f);
        Assert.AreEqual(4, m.run.player.toughness);
    }

    [Test]
    public void V7File_KeepsItsToughnessAndCharacter()
    {
        var f = new SaveFile { schemaVersion = 7 };
        f.run.characterId = "warlord";
        f.run.player.toughness = 3;
        f.run.player.hp = 99;   // stale vestigial value must not win
        var m = SaveMigrator.Migrate(f);
        Assert.AreEqual(7, m.schemaVersion);
        Assert.AreEqual("warlord", m.run.characterId);
        Assert.AreEqual(3, m.run.player.toughness);
    }
}
