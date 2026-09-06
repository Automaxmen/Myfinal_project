using UnityEngine;
using UnityEngine.UI;
using VisualNovel.Data;
using VisualNovel.Runtime;

namespace VisualNovel.UI
{
    [RequireComponent(typeof(Button))]
    public class ChoiceButtonUI : MonoBehaviour
    {
        public Text label;

        private static readonly Color BaseColor = new Color(0.12f, 0.12f, 0.18f, 0.92f);
        private static readonly Color FocusedColor = new Color(0.25f, 0.25f, 0.35f, 0.95f);

        private DialogueChoice choice;
        private Button button;
        private Image background;

        void Awake()
        {
            button = GetComponent<Button>();
            background = GetComponent<Image>();
            button.onClick.AddListener(HandleClick);
            if (label != null) label.font = ThaiFont.Get();
        }

        public void Setup(DialogueChoice dialogueChoice)
        {
            choice = dialogueChoice;
            if (label != null) label.text = choice.choiceText;
        }

        public void SetFocused(bool focused)
        {
            if (background != null) background.color = focused ? FocusedColor : BaseColor;
        }

        void HandleClick()
        {
            DialogueManager.Instance?.SelectChoice(choice);
        }
    }
}
