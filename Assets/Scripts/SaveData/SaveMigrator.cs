using System;

namespace ArchonsRise.SaveData
{
    // Upgrades old save files in place. v1 -> v2: the places array did not
    // exist; absent means nothing conquered. Idempotent; UnityEngine-free.
    public static class SaveMigrator
    {
        public static SaveFile Migrate(SaveFile file)
        {
            if (file.run.places == null)
                file.run.places = Array.Empty<PlaceConquest>();
            if (file.schemaVersion < 2)
                file.schemaVersion = 2;
            return file;
        }
    }
}
