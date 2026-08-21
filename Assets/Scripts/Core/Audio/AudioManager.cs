using System.Collections.Generic;
using BangBang.Core.Data;
using UnityEngine;

namespace BangBang.Core.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource _bgmSource;
        private AudioSource _sfxSource;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                SetupAudioSources();
                PreloadClips();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SetupAudioSources()
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.volume = 0.5f;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.volume = 0.8f;
        }

        private void PreloadClips()
        {
            string[] clipNames = {
                "bang_shot", "damage", "dodge", "card_draw", "card_play",
                "button_tap", "splash_intro", "western_theme", "win", "lose"
            };

            foreach (var name in clipNames)
            {
                var clip = CardCatalogDatabase.LoadAudio(name);
                if (clip != null) _clips[name] = clip;
            }
        }

        public void PlayBGM(string clipName = "western_theme")
        {
            if (_clips.TryGetValue(clipName, out var clip) && clip != null)
            {
                if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;
                _bgmSource.clip = clip;
                _bgmSource.Play();
            }
        }

        public void PlaySFX(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return;

            if (_clips.TryGetValue(clipName, out var clip) && clip != null)
            {
                _sfxSource.PlayOneShot(clip);
            }
            else
            {
                var loaded = CardCatalogDatabase.LoadAudio(clipName);
                if (loaded != null)
                {
                    _clips[clipName] = loaded;
                    _sfxSource.PlayOneShot(loaded);
                }
            }
        }

        public void SetVolume(float bgm, float sfx)
        {
            if (_bgmSource != null) _bgmSource.volume = bgm;
            if (_sfxSource != null) _sfxSource.volume = sfx;
        }
    }
}
