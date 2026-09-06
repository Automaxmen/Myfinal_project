using System.IO;
using UnityEngine;

namespace VisualNovel.Runtime
{
    public static class SaveSystem
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

        public static bool SaveExists() => File.Exists(SavePath);

        public static void Save(SaveData data)
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }

        public static SaveData Load()
        {
            if (!SaveExists()) return null;
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
        }

        public static void DeleteSave()
        {
            if (SaveExists()) File.Delete(SavePath);
        }
    }
}
