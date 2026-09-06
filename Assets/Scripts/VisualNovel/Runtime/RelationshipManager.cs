using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisualNovel.Runtime
{
    public class RelationshipManager : MonoBehaviour
    {
        public static RelationshipManager Instance { get; private set; }

        public const int MinValue = -100;
        public const int MaxValue = 100;

        public event Action<string, int> OnRelationshipChanged;

        [Header("Ending Thresholds (generic defaults — tune once the real character roster/story is in)")]
        [Tooltip("NPC ids whose relationship values are averaged to decide the ending.")]
        public string[] endingTrackedNpcIds;
        public int trueEndingThreshold = 60;
        public int goodEndingThreshold = 0;

        private readonly Dictionary<string, int> values = new Dictionary<string, int>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public int GetValue(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return 0;
            return values.TryGetValue(npcId, out var v) ? v : 0;
        }

        public void ChangeValue(string npcId, int delta)
        {
            if (string.IsNullOrEmpty(npcId) || delta == 0) return;
            int newValue = Mathf.Clamp(GetValue(npcId) + delta, MinValue, MaxValue);
            values[npcId] = newValue;
            OnRelationshipChanged?.Invoke(npcId, newValue);
        }

        public IReadOnlyDictionary<string, int> GetAllValues() => values;

        public void LoadValues(IEnumerable<KeyValuePair<string, int>> data)
        {
            values.Clear();
            if (data == null) return;
            foreach (var kv in data) values[kv.Key] = Mathf.Clamp(kv.Value, MinValue, MaxValue);
        }

        public void ResetAll()
        {
            values.Clear();
        }

        /// <summary>
        /// Averages the tracked NPCs' relationship values against the configured thresholds.
        /// Placeholder logic — replace trackedNpcIds/thresholds with real story data once available.
        /// </summary>
        public EndingType DetermineEnding()
        {
            if (endingTrackedNpcIds == null || endingTrackedNpcIds.Length == 0) return EndingType.Good;

            int total = 0;
            foreach (var id in endingTrackedNpcIds) total += GetValue(id);
            int average = total / endingTrackedNpcIds.Length;

            if (average >= trueEndingThreshold) return EndingType.True;
            if (average >= goodEndingThreshold) return EndingType.Good;
            return EndingType.Bad;
        }
    }
}
