using System.Collections.Generic;
using UnityEngine;

namespace VisualNovel.Data
{
    [CreateAssetMenu(fileName = "NewDialogueScene", menuName = "Visual Novel/Dialogue Scene")]
    public class DialogueScene : ScriptableObject
    {
        public string sceneId;
        public List<DialogueLine> lines = new List<DialogueLine>();

        [Tooltip("Checked in order when the scene ends. The first rule whose NPC relationship meets the threshold wins.")]
        public List<SceneTransitionRule> nextSceneRules = new List<SceneTransitionRule>();

        [Tooltip("Used when no rule matches. Leave empty to just end the dialogue.")]
        public DialogueScene defaultNextScene;
    }
}
