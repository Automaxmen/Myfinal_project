using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VisualNovel.Data;

namespace VisualNovel.Runtime
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Tooltip("If set, this scene starts playing automatically on Start().")]
        public DialogueScene startingScene;

        public event Action<DialogueLine> OnLineShown;
        public event Action OnDialogueEnded;
        public event Action<DialogueScene> OnSceneStarted;

        private DialogueScene currentScene;
        private int lineIndex;

        public DialogueScene CurrentScene => currentScene;
        public int CurrentLineIndex => lineIndex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            if (GameBootstrapState.HasPendingEnding)
            {
                GameBootstrapState.HasPendingEnding = false;
                EndingManager.Instance?.TriggerEnding(GameBootstrapState.PendingEndingType);
            }
            else if (GameBootstrapState.HasPendingLoad && GameBootstrapState.PendingSaveData != null)
            {
                var data = GameBootstrapState.PendingSaveData;
                GameBootstrapState.HasPendingLoad = false;
                GameBootstrapState.PendingSaveData = null;
                ApplySaveData(data);
            }
            else if (startingScene != null)
            {
                PlayScene(startingScene);
            }
        }

        void ApplySaveData(SaveData data)
        {
            if (RelationshipManager.Instance != null)
            {
                var dict = new Dictionary<string, int>();
                foreach (var entry in data.relationships) dict[entry.npcId] = entry.value;
                RelationshipManager.Instance.LoadValues(dict);
            }

            var scene = DialogueSceneRegistry.FindById(data.currentSceneId);
            if (scene != null)
            {
                ResumeScene(scene, data.currentLineIndex);
            }
            else if (startingScene != null)
            {
                PlayScene(startingScene);
            }
        }

        public void PlayScene(DialogueScene scene)
        {
            currentScene = scene;
            lineIndex = 0;
            OnSceneStarted?.Invoke(scene);

            if (currentScene == null || currentScene.lines.Count == 0)
            {
                EndScene();
                return;
            }

            ShowCurrentLine();
        }

        public void ResumeScene(DialogueScene scene, int atLineIndex)
        {
            currentScene = scene;
            lineIndex = Mathf.Clamp(atLineIndex, 0, Mathf.Max(0, scene.lines.Count - 1));
            OnSceneStarted?.Invoke(scene);

            if (currentScene == null || currentScene.lines.Count == 0)
            {
                EndScene();
                return;
            }

            ShowCurrentLine();
        }

        void ShowCurrentLine()
        {
            var line = currentScene.lines[lineIndex];
            OnLineShown?.Invoke(line);
        }

        public void Advance()
        {
            if (currentScene != null && lineIndex < currentScene.lines.Count)
            {
                string sceneToLoad = currentScene.lines[lineIndex].unitySceneToLoad;
                if (!string.IsNullOrEmpty(sceneToLoad))
                {
                    SceneManager.LoadScene(sceneToLoad);
                    return;
                }
            }

            lineIndex++;
            if (currentScene == null || lineIndex >= currentScene.lines.Count)
            {
                EndScene();
            }
            else
            {
                ShowCurrentLine();
            }
        }

        public void SelectChoice(DialogueChoice choice)
        {
            if (choice == null) return;

            if (!string.IsNullOrEmpty(choice.affectedNpcId) && RelationshipManager.Instance != null)
            {
                RelationshipManager.Instance.ChangeValue(choice.affectedNpcId, choice.relationshipDelta);
            }

            lineIndex = choice.nextLineIndexOverride >= 0 ? choice.nextLineIndexOverride : lineIndex + 1;

            if (currentScene == null || lineIndex >= currentScene.lines.Count)
            {
                EndScene();
            }
            else
            {
                ShowCurrentLine();
            }
        }

        void EndScene()
        {
            var finishedScene = currentScene;
            if (finishedScene != null)
            {
                foreach (var rule in finishedScene.nextSceneRules)
                {
                    int value = RelationshipManager.Instance != null
                        ? RelationshipManager.Instance.GetValue(rule.requiredNpcId)
                        : 0;

                    if (value >= rule.minRelationship && rule.targetScene != null)
                    {
                        PlayScene(rule.targetScene);
                        return;
                    }
                }

                if (finishedScene.defaultNextScene != null)
                {
                    PlayScene(finishedScene.defaultNextScene);
                    return;
                }
            }

            currentScene = null;
            OnDialogueEnded?.Invoke();
        }
    }
}
