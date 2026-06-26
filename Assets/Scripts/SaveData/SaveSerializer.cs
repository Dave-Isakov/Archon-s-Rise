using UnityEngine;

namespace ArchonsRise.SaveData
{
    public static class SaveSerializer
    {
        public static string ToJson(SaveFile file) => JsonUtility.ToJson(file, prettyPrint: true);

        public static SaveFile FromJson(string json) => JsonUtility.FromJson<SaveFile>(json);
    }
}
