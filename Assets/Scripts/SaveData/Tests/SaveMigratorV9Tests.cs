using NUnit.Framework;
using ArchonsRise.SaveData;

public class SaveMigratorV9Tests
{
    [Test]
    public void V8File_MigratesToV9_WithEmptyAggroedEnemies()
    {
        var file = new SaveFile { schemaVersion = 8 };
        file.run.map.aggroedEnemies = null; // a v8 file has no aggroedEnemies key

        SaveMigrator.Migrate(file);

        Assert.AreEqual(14, file.schemaVersion);
        Assert.IsNotNull(file.run.map.aggroedEnemies);
        Assert.AreEqual(0, file.run.map.aggroedEnemies.Length);
    }

    [Test]
    public void Migrate_PreservesExistingAggroedEnemies()
    {
        var file = new SaveFile { schemaVersion = 9 };
        file.run.map.aggroedEnemies = new[] { new Cell(3, -4) };

        SaveMigrator.Migrate(file);

        Assert.AreEqual(1, file.run.map.aggroedEnemies.Length);
        Assert.AreEqual(new Cell(3, -4), file.run.map.aggroedEnemies[0]);
    }

    // An armed cell and a defeated cell can name the same coordinates over a run's
    // lifetime; the two arrays are independent and neither may bleed into the other.
    [Test]
    public void Migrate_LeavesDefeatedEnemiesUntouched()
    {
        var file = new SaveFile { schemaVersion = 8 };
        file.run.map.defeatedEnemies = new[] { new Cell(1, 1) };
        file.run.map.aggroedEnemies = null;

        SaveMigrator.Migrate(file);

        Assert.AreEqual(1, file.run.map.defeatedEnemies.Length);
        Assert.AreEqual(new Cell(1, 1), file.run.map.defeatedEnemies[0]);
        Assert.AreEqual(0, file.run.map.aggroedEnemies.Length);
    }
}
