using NUnit.Framework;
using ArchonsRise.SaveData;

// v11 -> v12: the shrine guardian became shrine state instead of a map token
// (2026-07-31). The debt moves from SpawnedEnemy's legacy tag onto ShrineState.
public class SaveMigratorV12Tests
{
    [Test]
    public void PreV12Shrines_DefaultOwedRewardToMinusOne()
    {
        var file = new SaveFile { schemaVersion = 11 };
        // JsonUtility leaves the absent field at 0, which reads as
        // ShrineReward.CardPick â€” a dormant shrine would owe a card.
        file.run.shrines = new[]
        {
            new ShrineState { x = 1, y = 2, shrineId = "gate", state = 1, owedReward = 0 }
        };

        SaveMigrator.Migrate(file);

        Assert.AreEqual(14, file.schemaVersion);
        Assert.AreEqual(-1, file.run.shrines[0].owedReward);
    }

    [Test]
    public void GuardianSpawn_FoldsItsDebtOntoItsShrine()
    {
        var file = new SaveFile { schemaVersion = 11 };
        file.run.shrines = new[]
        {
            new ShrineState { x = 5, y = 6, shrineId = "gate", state = 2 }
        };
        file.run.spawnedEnemies = new[]
        {
            new SpawnedEnemy { x = 2, y = 2, enemyId = "wraith",
                               shrineRewardType = 1, shrineCellX = 5, shrineCellY = 6 }
        };

        SaveMigrator.Migrate(file);

        Assert.AreEqual(2, file.run.shrines[0].state);      // still Guarding
        Assert.AreEqual(1, file.run.shrines[0].owedReward); // debt collected on kill
        // The token is gone: a guardian is not a board entity any more.
        Assert.AreEqual(0, file.run.spawnedEnemies.Length);
    }

    [Test]
    public void OrdinarySpawns_SurviveTheFold()
    {
        var file = new SaveFile { schemaVersion = 11 };
        file.run.spawnedEnemies = new[]
        {
            new SpawnedEnemy { x = 1, y = 1, enemyId = "goblin", shrineRewardType = -1 },
            new SpawnedEnemy { x = 3, y = 3, enemyId = "wraith", shrineRewardType = 2,
                               shrineCellX = 9, shrineCellY = 9 }
        };

        SaveMigrator.Migrate(file);

        Assert.AreEqual(1, file.run.spawnedEnemies.Length);
        Assert.AreEqual("goblin", file.run.spawnedEnemies[0].enemyId);
    }

    [Test]
    public void GuardianWithNoMatchingShrine_IsStillDropped()
    {
        var file = new SaveFile { schemaVersion = 11 };
        file.run.spawnedEnemies = new[]
        {
            new SpawnedEnemy { x = 2, y = 2, enemyId = "wraith", shrineRewardType = 0,
                               shrineCellX = 7, shrineCellY = 7 }
        };

        SaveMigrator.Migrate(file);

        // Nothing to fold onto, but the token can't stay either â€” it would
        // restore as an ordinary enemy carrying a debt nothing can collect.
        Assert.AreEqual(0, file.run.spawnedEnemies.Length);
    }

    [Test]
    public void V12File_IsLeftAlone()
    {
        var file = new SaveFile { schemaVersion = 12 };
        file.run.shrines = new[]
        {
            new ShrineState { x = 1, y = 1, shrineId = "gate", state = 2, owedReward = 2 }
        };

        SaveMigrator.Migrate(file);

        Assert.AreEqual(14, file.schemaVersion);
        Assert.AreEqual(2, file.run.shrines[0].owedReward);
    }
}
