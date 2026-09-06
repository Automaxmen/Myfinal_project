using System;
using UnityEngine;

namespace GameAudio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        const string SoundKey = "audio_sound_volume";
        const string MusicKey = "audio_music_volume";

        public float SoundVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;

        public event Action<float> OnSoundVolumeChanged;
        public event Action<float> OnMusicVolumeChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SoundVolume = PlayerPrefs.GetFloat(SoundKey, 1f);
            MusicVolume = PlayerPrefs.GetFloat(MusicKey, 1f);
        }

        public void SetSoundVolume(float value)
        {
            SoundVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SoundKey, SoundVolume);
            OnSoundVolumeChanged?.Invoke(SoundVolume);
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            OnMusicVolumeChanged?.Invoke(MusicVolume);
        }
    }
}
