using System;

namespace VisualNovel.Data
{
    [Serializable]
    public class SceneTransitionRule
    {
        public string requiredNpcId;
        public int minRelationship;
        public DialogueScene targetScene;
    }
}
