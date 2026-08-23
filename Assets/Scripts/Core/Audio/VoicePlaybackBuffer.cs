using System.Collections.Concurrent;
using UnityEngine;

namespace BangBang.Core.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class VoicePlaybackBuffer : MonoBehaviour
    {
        private readonly ConcurrentQueue<float> _samples = new ConcurrentQueue<float>();
        private const int MaxBufferedSamples = 16000;
        private const float InputSampleRate = 16000f;
        private float _phase = 1f;
        private float _currentSample;

        public void Enqueue(short[] pcm)
        {
            while (_samples.Count > MaxBufferedSamples && _samples.TryDequeue(out _)) { }
            for (int i = 0; i < pcm.Length; i++) _samples.Enqueue(pcm[i] / 32768f);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float step = InputSampleRate / Mathf.Max(1, AudioSettings.outputSampleRate);
            for (int frame = 0; frame < data.Length / channels; frame++)
            {
                if (_phase >= 1f)
                {
                    _currentSample = _samples.TryDequeue(out var value) ? value : 0f;
                    _phase -= 1f;
                }
                for (int channel = 0; channel < channels; channel++) data[frame * channels + channel] = _currentSample;
                _phase += step;
            }
        }
    }
}
