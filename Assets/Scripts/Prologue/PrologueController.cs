using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VisualNovel.UI;

namespace Prologue
{
    public class PrologueController : MonoBehaviour
    {
        public string nextSceneName = "VisualNovelDemo";
        public Button continueButton;
        public Button skipButton;

        void Awake()
        {
            foreach (var t in FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                t.font = t.fontStyle == FontStyle.Bold ? ThaiFont.GetBold() : ThaiFont.Get();
            }

            if (continueButton != null) continueButton.onClick.AddListener(Continue);
            if (skipButton != null) skipButton.onClick.AddListener(Continue);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) Continue();
        }

        void Continue()
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
