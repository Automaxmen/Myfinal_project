using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisualNovel.Data
{
    public enum SpeakerSide { None, Left, Right }

    [Serializable]
    public class DialogueLine
    {
        public CharacterProfile speaker;
        public SpeakerSide side = SpeakerSide.None;

        [TextArea(2, 5)]
        public string text;

        public List<DialogueChoice> choices = new List<DialogueChoice>();

        [Tooltip("If set, advancing past this line loads this Unity scene (by name, must be in Build Settings) instead of showing the next line. Use this to jump into a Turn-Based battle scene, or back to MainMenu. Leave empty for normal VN lines.")]
        public string unitySceneToLoad = "";
    }
}
