using System;
using System.Collections.Generic;

namespace VisualNovel.Runtime
{
    [Serializable]
    public struct RelationshipEntry
    {
        public string npcId;
        public int value;
    }

    [Serializable]
    public class SaveData
    {
        public string currentSceneId;
        public int currentLineIndex;
        public List<RelationshipEntry> relationships = new List<RelationshipEntry>();
    }
}
