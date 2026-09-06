using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VisualNovel.Data;
using VisualNovel.Runtime;
using Settings;

namespace VisualNovel.UI
{
    // Must subscribe to DialogueManager's events before DialogueManager.Start() fires them.
    [DefaultExecutionOrder(-100)]
    public class VNUIController : MonoBehaviour
    {
        [Header("Dialogue")]
        public Text nameText;
        public Text bodyText;
        public Renderer leftPortrait;
        public Renderer rightPortrait;
        public GameObject continueIndicator;
        public Button dialogueClickCatcher;

        [Header("Portrait Highlight")]
        [Range(0f, 1f)] public float dimFactor = 0.35f;

        [Header("Portrait Entrance")]
        public float entranceDuration = 0.6f;
        public float entranceOffscreenDistance = 6f;
        [Range(0.05f, 1f)] public float entranceStartScale = 0.4f;

        [Header("Choices")]
        public Transform choiceContainer;
        public ChoiceButtonUI choiceButtonPrefab;

        [Header("Menu / Settings")]
        public Button menuButton;
        public Button settingsButton;
        public GameObject menuPanel;
        public GameObject settingsPanel;
        public Button saveButton;
        public SettingsMenuController settingsMenu;

        private readonly List<ChoiceButtonUI> spawnedButtons = new List<ChoiceButtonUI>();
        private bool waitingForChoice;
        private int focusedChoiceIndex;
        private Color leftBaseColor;
        private Color rightBaseColor;
        private Vector3 leftHomePosition;
        private Vector3 rightHomePosition;
        private Vector3 leftHomeScale;
        private Vector3 rightHomeScale;
        private Coroutine leftEntranceRoutine;
        private Coroutine rightEntranceRoutine;

        void Awake()
        {
            if (dialogueClickCatcher != null) dialogueClickCatcher.onClick.AddListener(HandleDialogueClick);

            if (settingsMenu != null)
            {
                if (menuButton != null) menuButton.onClick.AddListener(settingsMenu.Toggle);
                if (settingsButton != null) settingsButton.onClick.AddListener(settingsMenu.Toggle);
            }
            else
            {
                if (menuButton != null) menuButton.onClick.AddListener(ToggleMenu);
                if (settingsButton != null) settingsButton.onClick.AddListener(ToggleSettings);
            }
            if (menuPanel != null) menuPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (saveButton != null) saveButton.onClick.AddListener(SaveGameService.SaveCurrentGame);

            if (leftPortrait != null)
            {
                leftBaseColor = leftPortrait.material.color;
                leftHomePosition = leftPortrait.transform.position;
                leftHomeScale = leftPortrait.transform.localScale;
            }
            if (rightPortrait != null)
            {
                rightBaseColor = rightPortrait.material.color;
                rightHomePosition = rightPortrait.transform.position;
                rightHomeScale = rightPortrait.transform.localScale;
            }

            foreach (var t in FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                t.font = t.fontStyle == FontStyle.Bold ? ThaiFont.GetBold() : ThaiFont.Get();
            }
        }

        void Start()
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.OnLineShown += ShowLine;
                DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;
                DialogueManager.Instance.OnSceneStarted += HandleSceneStarted;
            }
        }

        void OnDestroy()
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.OnLineShown -= ShowLine;
                DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;
                DialogueManager.Instance.OnSceneStarted -= HandleSceneStarted;
            }
        }

        void HandleSceneStarted(DialogueScene scene)
        {
            if (this == null || !isActiveAndEnabled) return;

            if (leftPortrait != null)
            {
                if (leftEntranceRoutine != null) StopCoroutine(leftEntranceRoutine);
                leftEntranceRoutine = StartCoroutine(AnimateEntrance(leftPortrait.transform, leftHomePosition, leftHomeScale, Vector3.left * entranceOffscreenDistance));
            }
            if (rightPortrait != null)
            {
                if (rightEntranceRoutine != null) StopCoroutine(rightEntranceRoutine);
                rightEntranceRoutine = StartCoroutine(AnimateEntrance(rightPortrait.transform, rightHomePosition, rightHomeScale, Vector3.right * entranceOffscreenDistance));
            }
        }

        IEnumerator AnimateEntrance(Transform t, Vector3 homePosition, Vector3 homeScale, Vector3 offscreenOffset)
        {
            t.position = homePosition + offscreenOffset;
            t.localScale = homeScale * entranceStartScale;

            float elapsed = 0f;
            while (elapsed < entranceDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / entranceDuration);
                float eased = 1f - (1f - progress) * (1f - progress) * (1f - progress);

                t.position = Vector3.Lerp(homePosition + offscreenOffset, homePosition, eased);
                t.localScale = Vector3.Lerp(homeScale * entranceStartScale, homeScale, eased);
                yield return null;
            }

            t.position = homePosition;
            t.localScale = homeScale;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (waitingForChoice && spawnedButtons.Count > 0)
            {
                if (kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) MoveChoiceFocus(-1);
                else if (kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) MoveChoiceFocus(1);

                if (kb.spaceKey.wasPressedThisFrame)
                {
                    var chosen = spawnedButtons[focusedChoiceIndex];
                    if (chosen != null) chosen.GetComponent<Button>().onClick.Invoke();
                }
            }
            else if (!waitingForChoice)
            {
                if (kb.spaceKey.wasPressedThisFrame) DialogueManager.Instance?.Advance();
            }
        }

        void MoveChoiceFocus(int delta)
        {
            if (spawnedButtons.Count == 0) return;
            SetChoiceFocus(focusedChoiceIndex, false);
            focusedChoiceIndex = Mathf.Clamp(focusedChoiceIndex + delta, 0, spawnedButtons.Count - 1);
            SetChoiceFocus(focusedChoiceIndex, true);
        }

        void SetChoiceFocus(int index, bool focused)
        {
            if (index < 0 || index >= spawnedButtons.Count) return;
            var btn = spawnedButtons[index];
            if (btn != null) btn.SetFocused(focused);
        }

        void HandleDialogueClick()
        {
            if (waitingForChoice) return;
            DialogueManager.Instance?.Advance();
        }

        void ToggleMenu()
        {
            if (menuPanel != null) menuPanel.SetActive(!menuPanel.activeSelf);
        }

        void ToggleSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        void ShowLine(DialogueLine line)
        {
            nameText.text = line.speaker != null ? line.speaker.displayName : string.Empty;
            nameText.color = line.speaker != null ? line.speaker.nameColor : Color.white;
            bodyText.text = line.text;

            UpdatePortraitHighlight(line.side);
            ClearChoices();

            waitingForChoice = line.choices != null && line.choices.Count > 0;
            if (continueIndicator != null) continueIndicator.SetActive(!waitingForChoice);

            if (waitingForChoice)
            {
                foreach (var choice in line.choices)
                {
                    var button = Instantiate(choiceButtonPrefab, choiceContainer);
                    button.Setup(choice);
                    spawnedButtons.Add(button);
                }
                focusedChoiceIndex = 0;
                SetChoiceFocus(0, true);
            }
        }

        void UpdatePortraitHighlight(SpeakerSide side)
        {
            if (leftPortrait != null)
            {
                bool active = side == SpeakerSide.Left || side == SpeakerSide.None;
                leftPortrait.material.color = active ? leftBaseColor : leftBaseColor * dimFactor;
            }
            if (rightPortrait != null)
            {
                bool active = side == SpeakerSide.Right || side == SpeakerSide.None;
                rightPortrait.material.color = active ? rightBaseColor : rightBaseColor * dimFactor;
            }
        }

        void ClearChoices()
        {
            foreach (var button in spawnedButtons)
            {
                if (button != null) Destroy(button.gameObject);
            }
            spawnedButtons.Clear();
        }

        void HandleDialogueEnded()
        {
            nameText.text = string.Empty;
            bodyText.text = "-- End of Scene --";
            ClearChoices();
            if (continueIndicator != null) continueIndicator.SetActive(false);
        }
    }
}
