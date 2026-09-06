using UnityEngine;
using VisualNovel.Data;

namespace VisualNovel.Runtime
{
    /// <summary>
    /// Lives alongside DialogueManager in the VN scene. Holds the three ending
    /// DialogueScene assets and plays the right one when triggered.
    /// </summary>
    public class EndingManager : MonoBehaviour
    {
        public static EndingManager Instance { get; private set; }

        public DialogueScene trueEndingScene;
        public DialogueScene goodEndingScene;
        public DialogueScene badEndingScene;

        void Awake()
        {
            Instance = this;
        }

        public void TriggerEnding(EndingType type)
        {
            DialogueScene target = type switch
            {
                EndingType.True => trueEndingScene,
                EndingType.Good => goodEndingScene,
                _ => badEndingScene
            };

            if (target != null && DialogueManager.Instance != null)
            {
                DialogueManager.Instance.PlayScene(target);
            }
        }
    }
}
