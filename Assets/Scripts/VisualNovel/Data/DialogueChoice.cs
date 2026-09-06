using System;

namespace VisualNovel.Data
{
    [Serializable]
    public class DialogueChoice
    {
        public string choiceText;
        public string affectedNpcId;
        public int relationshipDelta;
        public int nextLineIndexOverride = -1;
    }
}
