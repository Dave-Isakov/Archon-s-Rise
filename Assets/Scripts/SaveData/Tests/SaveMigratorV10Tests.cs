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

        Assert.AreEqual(10, file.schemaVersion);
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

    [Test]
    public void V10Spawns_PreserveTheirTag()
    {
        var file = new SaveFile { schemaVersion = 10 };
        file.run.spawnedEnemies = new[]
        {
            new SpawnedEnemy { x = 2, y = 2, enemyId = "wraith", shrineRewardType = 1, shrineCellX = 5, shrineCellY = 6 }
        };

        SaveMigrator.Migrate(file);

        Assert.AreEqual(1, file.run.spawnedEnemies[0].shrineRewardType);
        Assert.AreEqual(5, file.run.spawnedEnemies[0].shrineCellX);
    }
}
