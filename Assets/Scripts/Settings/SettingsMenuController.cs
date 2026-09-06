using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GameAudio;
using VisualNovel.Runtime;
using VisualNovel.UI;

namespace Settings
{
    public class SettingsMenuController : MonoBehaviour
    {
        [Header("Panel")]
        public GameObject panelRoot;
        public Button closeButton;

        [Header("Sliders")]
        public Slider soundSlider;
        public Slider musicSlider;

        [Header("Buttons")]
        public Button saveGameButton;
        public Button loadGameButton;
        public Button titleButton;

        [Tooltip("Unity scene that hosts the VN gameplay loop, used when Load Game / New Game resumes into a fresh scene load.")]
        public string gameplaySceneName = "VisualNovelDemo";
        public string mainMenuSceneName = "MainMenu";

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Text>(true))
            {
                t.font = t.fontStyle == FontStyle.Bold ? ThaiFont.GetBold() : ThaiFont.Get();
            }

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (saveGameButton != null) saveGameButton.onClick.AddListener(OnSaveGame);
            if (loadGameButton != null) loadGameButton.onClick.AddListener(OnLoadGame);
            if (titleButton != null) titleButton.onClick.AddListener(OnTitle);

            if (soundSlider != null) soundSlider.onValueChanged.AddListener(OnSoundSliderChanged);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (panelRoot == null) return;
            if (panelRoot.activeSelf) Close();
            else Open();
        }

        public void Open()
        {
            if (panelRoot == null) return;

            if (AudioManager.Instance != null)
            {
                if (soundSlider != null) soundSlider.SetValueWithoutNotify(AudioManager.Instance.SoundVolume);
                if (musicSlider != null) musicSlider.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);
            }

            if (saveGameButton != null) saveGameButton.interactable = DialogueManager.Instance != null;
            if (loadGameButton != null) loadGameButton.interactable = SaveSystem.SaveExists();

            panelRoot.SetActive(true);
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        void OnSoundSliderChanged(float value)
        {
            AudioManager.Instance?.SetSoundVolume(value);
        }

        void OnMusicSliderChanged(float value)
        {
            AudioManager.Instance?.SetMusicVolume(value);
        }

        void OnSaveGame()
        {
            SaveGameService.SaveCurrentGame();
        }

        void OnLoadGame()
        {
            var data = SaveSystem.Load();
            if (data == null) return;

            GameBootstrapState.PendingSaveData = data;
            GameBootstrapState.HasPendingLoad = true;
            SceneManager.LoadScene(gameplaySceneName);
        }

        void OnTitle()
        {
            GameBootstrapState.HasPendingLoad = false;
            GameBootstrapState.PendingSaveData = null;
            GameBootstrapState.HasPendingEnding = false;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
