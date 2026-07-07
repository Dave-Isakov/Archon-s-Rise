using System;

namespace ArchonsRise.SaveData
{
    // Upgrades old save files in place. Idempotent; UnityEngine-free.
    public static class SaveMigrator
    {
        public static SaveFile Migrate(SaveFile file)
        {
            // v1 -> v2: places array did not exist; absent means nothing conquered.
            if (file.run.places == null)
                file.run.places = Array.Empty<PlaceConquest>();

            // v2 -> v3: skill arrays did not exist; absent means no skills owned.
            // (handSize was dropped from the model: JsonUtility ignores the stale
            // key in old files, and the value is derived from level on load.)
            if (file.run.player.ownedSkillIds == null)
                file.run.player.ownedSkillIds = Array.Empty<string>();
            if (file.run.player.exhaustedSkillIds == null)
                file.run.player.exhaustedSkillIds = Array.Empty<string>();

            if (file.schemaVersion < 3)
                file.schemaVersion = 3;
            return file;
        }
    }
}
