using System;
using System.Collections.Generic;
using System.Collections;
using BangBang.Core.Network;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.Core.Audio
{
    public sealed class VoiceChatManager : MonoBehaviour
    {
        public static VoiceChatManager Instance { get; private set; }
        public Button micButton;
        public Text micButtonText;

        private const int SampleRate = 16000;
        private const int PacketSamples = 320;
        private readonly Dictionary<string, VoicePlaybackBuffer> _playbacks = new Dictionary<string, VoicePlaybackBuffer>();
        private BangLiveGateway _gateway;
        private BangCloudflareGateway _cloudflareGateway;
        private AudioClip _micClip;
        private int _readPosition;
        private bool _enabled;
        private float _nextPacketAt;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Initialize(Button button)
        {
            micButton = button;
            micButtonText = button != null ? button.GetComponentInChildren<Text>() : null;
            if (micButton != null)
            {
                micButton.onClick.RemoveAllListeners();
                micButton.onClick.AddListener(ToggleMicrophone);
            }
            FindGateway();
            RefreshButton();
        }

        private void FindGateway()
        {
            var found = FindAnyObjectByType<BangLiveGateway>();
            if (_gateway == found) return;
            if (_gateway != null) _gateway.OnVoiceFrame -= HandleVoiceFrame;
            _gateway = found;
            if (_gateway != null) _gateway.OnVoiceFrame += HandleVoiceFrame;
            var cloudflare = FindAnyObjectByType<BangCloudflareGateway>();
            if (_cloudflareGateway != cloudflare)
            {
                if (_cloudflareGateway != null) _cloudflareGateway.OnVoiceFrame -= HandleVoiceFrame;
                _cloudflareGateway = cloudflare;
                if (_cloudflareGateway != null) _cloudflareGateway.OnVoiceFrame += HandleVoiceFrame;
            }
        }

        private void Update()
        {
            if (_gateway == null) FindGateway();
            if (!_enabled || _micClip == null || Time.unscaledTime < _nextPacketAt) return;
            _nextPacketAt = Time.unscaledTime + 0.02f;
            CapturePacket();
        }

        private void ToggleMicrophone()
        {
            if (_enabled) StopMicrophone();
            else StartCoroutine(RequestAndStartMicrophone());
        }

        private IEnumerator RequestAndStartMicrophone()
        {
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                if (micButtonText != null) micButtonText.text = "MIC BỊ TỪ CHỐI";
                yield break;
            }
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                if (micButtonText != null) micButtonText.text = "KHÔNG CÓ MIC";
                yield break;
            }
            try
            {
                _micClip = Microphone.Start(null, true, 1, SampleRate);
                _readPosition = 0;
                _enabled = _micClip != null;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning("[VoiceChat] Lỗi khi bật Mic: " + ex.Message);
                if (micButtonText != null) micButtonText.text = "LỖI MIC";
                _enabled = false;
            }
            RefreshButton();
        }

        private void StopMicrophone()
        {
            if (Microphone.IsRecording(null)) Microphone.End(null);
            _micClip = null;
            _enabled = false;
            RefreshButton();
        }

        private async void CapturePacket()
        {
            int position = Microphone.GetPosition(null);
            if (position < 0) return;
            int available = position >= _readPosition ? position - _readPosition : _micClip.samples - _readPosition + position;
            if (available < PacketSamples) return;

            var floats = new float[PacketSamples];
            _micClip.GetData(floats, _readPosition);
            _readPosition = (_readPosition + PacketSamples) % _micClip.samples;
            var bytes = new byte[PacketSamples * 2];
            float peak = 0f;
            for (int i = 0; i < floats.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(floats[i]));
                short value = (short)Mathf.Clamp(Mathf.RoundToInt(floats[i] * 32767f), short.MinValue, short.MaxValue);
                bytes[i * 2] = (byte)(value & 0xff);
                bytes[i * 2 + 1] = (byte)((value >> 8) & 0xff);
            }
            string payload = Convert.ToBase64String(bytes);
            if (_cloudflareGateway != null && !string.IsNullOrEmpty(_cloudflareGateway.CurrentRoomId))
                await _cloudflareGateway.SendVoiceFrameAsync(payload, peak);
            else if (_gateway != null)
                await _gateway.SendVoiceFrameAsync(payload, peak);
        }

        private void HandleVoiceFrame(string playerId, string payload, float level)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(payload);
                var pcm = new short[bytes.Length / 2];
                for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
                if (!_playbacks.TryGetValue(playerId, out var playback))
                {
                    var speaker = new GameObject("Voice_" + playerId, typeof(AudioSource), typeof(VoicePlaybackBuffer));
                    speaker.transform.SetParent(transform, false);
                    var source = speaker.GetComponent<AudioSource>();
                    source.loop = true;
                    source.spatialBlend = 0f;
                    source.clip = AudioClip.Create("VoiceStream", SampleRate, 1, SampleRate, false);
                    source.Play();
                    playback = speaker.GetComponent<VoicePlaybackBuffer>();
                    _playbacks[playerId] = playback;
                }
                playback.Enqueue(pcm);
            }
            catch (FormatException) { }
        }

        private void RefreshButton()
        {
            if (micButtonText != null) micButtonText.text = _enabled ? "MIC: ĐANG BẬT" : "MIC: TẮT";
            if (micButton != null && micButton.targetGraphic is Image image)
                image.color = _enabled ? new Color(0.25f, 0.65f, 0.32f) : new Color(0.22f, 0.16f, 0.12f);
        }

        private void OnDestroy()
        {
            if (_gateway != null) _gateway.OnVoiceFrame -= HandleVoiceFrame;
            if (_cloudflareGateway != null) _cloudflareGateway.OnVoiceFrame -= HandleVoiceFrame;
            if (_enabled) StopMicrophone();
            if (Instance == this) Instance = null;
        }
    }
}
