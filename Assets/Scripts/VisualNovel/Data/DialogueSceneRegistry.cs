using UnityEngine;

namespace VisualNovel.Data
{
    /// <summary>
    /// DialogueScene assets must live under a Resources/ folder for this lookup to find them.
    /// </summary>
    public static class DialogueSceneRegistry
    {
        public static DialogueScene FindById(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId)) return null;

            var all = Resources.LoadAll<DialogueScene>("VisualNovel/Dialogues");
            foreach (var s in all)
            {
                if (s.sceneId == sceneId) return s;
            }
            return null;
        }
    }
}
