using UnityEngine;

namespace VisualNovel.Data
{
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "Visual Novel/Character Profile")]
    public class CharacterProfile : ScriptableObject
    {
        public string npcId;
        public string displayName;
        public Sprite portrait;
        public Color nameColor = Color.white;
    }
}
