using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV4Tests
{
    [Test]
    public void V3File_GainsEmptySpawnFields()
    {
        var file = new SaveFile { schemaVersion = 3 };
        file.run.spawnedEnemies = null; // JsonUtility yields null for a missing array on old files
        var migrated = SaveMigrator.Migrate(file);
        Assert.AreEqual(5, migrated.schemaVersion);
        Assert.IsNotNull(migrated.run.spawnedEnemies);
        Assert.AreEqual(0, migrated.run.spawnedEnemies.Length);
        Assert.AreEqual(0, migrated.run.doom);
        Assert.AreEqual(0, migrated.run.roundsSinceSpawn);
    }

    [Test]
    public void V4File_MigratesToV5_DoomAndSpawnsPreserved()
    {
        var file = new SaveFile { schemaVersion = 4 };
        file.run.doom = 12;
        file.run.roundsSinceSpawn = 1;
        file.run.spawnedEnemies = new[]
        {
            new SpawnedEnemy { x = 3, y = 4, enemyId = "enemy.dire-wolves", bonusHP = 1, bonusAttack = 1 }
        };
        var migrated = SaveMigrator.Migrate(file);
        Assert.AreEqual(5, migrated.schemaVersion);
        Assert.AreEqual(12, migrated.run.doom);
        Assert.AreEqual(1, migrated.run.spawnedEnemies.Length);
        Assert.AreEqual("enemy.dire-wolves", migrated.run.spawnedEnemies[0].enemyId);
    }
}
