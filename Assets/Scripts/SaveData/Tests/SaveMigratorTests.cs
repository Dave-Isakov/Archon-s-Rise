using NUnit.Framework;
using ArchonsRise.SaveData;

namespace ArchonsRise.SaveData.Tests
{
    public class SaveMigratorTests
    {
        [Test]
        public void V1_MigratesToCurrent_WithNothingConquered()
        {
            var v1 = new SaveFile { schemaVersion = 1 };
            v1.run.places = null; // simulate a field absent from old JSON

            var migrated = SaveMigrator.Migrate(v1);

            Assert.AreEqual(6, migrated.schemaVersion);
            Assert.IsNotNull(migrated.run.places);
            Assert.AreEqual(0, migrated.run.places.Length);
        }

        [Test]
        public void V2_PlacesUntouched_VersionBumped()
        {
            var v2 = new SaveFile { schemaVersion = 2 };
            v2.run.places = new[] { new PlaceConquest { x = 3, y = 4, defeatedCount = 1 } };

            var migrated = SaveMigrator.Migrate(v2);

            Assert.AreEqual(6, migrated.schemaVersion);
            Assert.AreEqual(1, migrated.run.places.Length);
            Assert.AreEqual(1, migrated.run.places[0].defeatedCount);
        }
    }
}
