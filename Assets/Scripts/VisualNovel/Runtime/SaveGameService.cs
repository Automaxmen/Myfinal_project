namespace VisualNovel.Runtime
{
    public static class SaveGameService
    {
        public static bool CanSave => DialogueManager.Instance != null && DialogueManager.Instance.CurrentScene != null;

        public static void SaveCurrentGame()
        {
            if (!CanSave) return;

            var dm = DialogueManager.Instance;
            var data = new SaveData
            {
                currentSceneId = dm.CurrentScene.sceneId,
                currentLineIndex = dm.CurrentLineIndex
            };

            if (RelationshipManager.Instance != null)
            {
                foreach (var kv in RelationshipManager.Instance.GetAllValues())
                {
                    data.relationships.Add(new RelationshipEntry { npcId = kv.Key, value = kv.Value });
                }
            }

            SaveSystem.Save(data);
        }
    }
}
