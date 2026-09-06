using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VisualNovel.Runtime;
using VisualNovel.UI;
using Settings;

namespace MainMenu
{
    public class MainMenuController : MonoBehaviour
    {
        public Button newGameButton;
        public Button loadGameButton;
        public Button optionsButton;
        public Button creditButton;
        public Button exitButton;
        public SettingsMenuController settingsMenu;
        public CreditController creditPanel;

        public string gameplaySceneName = "VisualNovelDemo";
        public string prologueSceneName = "Prologue";

        void Awake()
        {
            foreach (var t in FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                t.font = t.fontStyle == FontStyle.Bold ? ThaiFont.GetBold() : ThaiFont.Get();
            }

            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGame);

            if (loadGameButton != null)
            {
                loadGameButton.onClick.AddListener(OnLoadGame);
                loadGameButton.interactable = SaveSystem.SaveExists();
            }

            if (exitButton != null) exitButton.onClick.AddListener(OnExit);

            if (optionsButton != null && settingsMenu != null) optionsButton.onClick.AddListener(settingsMenu.Toggle);
            if (creditButton != null && creditPanel != null) creditButton.onClick.AddListener(creditPanel.Toggle);
        }

        void OnNewGame()
        {
            GameBootstrapState.HasPendingLoad = false;
            GameBootstrapState.PendingSaveData = null;
            RelationshipManager.Instance?.ResetAll();
            SceneManager.LoadScene(prologueSceneName);
        }

        void OnLoadGame()
        {
            var data = SaveSystem.Load();
            if (data == null) return;

            GameBootstrapState.PendingSaveData = data;
            GameBootstrapState.HasPendingLoad = true;
            SceneManager.LoadScene(gameplaySceneName);
        }

        void OnExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
