using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV10Tests
{
    [Test]
    public void V9File_MigratesToV10_WithEmptyShrines()
    {
        var file = new SaveFile { schemaVersion = 9 };
        file.run.shrines = null;

        SaveMigrator.Migrate(file);

        Assert.AreEqual(12, file.schemaVersion);
        Assert.IsNotNull(file.run.shrines);
        Assert.AreEqual(0, file.run.shrines.Length);
    }

    [Test]
    public void PreV10Spawns_DefaultShrineRewardTypeToMinusOne()
    {
        var file = new SaveFile { schemaVersion = 9 };
        // A pre-v10 spawn has no shrine tag; JsonUtility would leave the new int at 0,
        // which must be corrected to -1 (0 == ShrineReward.CardPick).
        file.run.spawnedEnemies = new[]
        {
            new SpawnedEnemy { x = 1, y = 1, enemyId = "goblin", shrineRewardType = 0 }
        };

        SaveMigrator.Migrate(file);

        Assert.AreEqual(-1, file.run.spawnedEnemies[0].shrineRewardType);
    }

    // A TAGGED v10 spawn no longer survives migration at all: v12 folds its debt
    // onto its shrine and drops the token (SaveMigratorV12Tests). Only the
    // untagged default above is still v10's business.
}
