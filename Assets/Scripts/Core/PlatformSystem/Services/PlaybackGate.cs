using System;
using Core.PlatformSystem.Interfaces;
using UnityEngine;
using VContainer.Unity;

namespace Core.PlatformSystem.Services
{
    /// <summary>
    /// Applies <see cref="PlaybackState"/> to the engine: the time scale and the listener. A frozen game draws its last picture;
    /// the jslib of a platform that demands a full stop stops the main loop itself after this has run.
    /// </summary>
    public sealed class PlaybackGate : IPlaybackGate, IStartable, IDisposable
    {
        private readonly IPlatform _platform;
        private readonly PlaybackState _state = new PlaybackState();

        public PlaybackGate(IPlatform platform)
        {
            _platform = platform;
        }

        public bool Frozen => _state.Frozen;

        public bool Muted => _state.Muted;

        public event Action OnChanged;

        public void Start()
        {
            _platform.OnPauseChanged += HandlePause;
            _platform.OnAudioChanged += HandleAudio;
            _platform.OnFocusChanged += HandleFocus;
            _platform.OnAdPlayingChanged += HandleAdPlaying;
            HandleAudio(_platform.AudioEnabled);
        }

        public void Dispose()
        {
            _platform.OnPauseChanged -= HandlePause;
            _platform.OnAudioChanged -= HandleAudio;
            _platform.OnFocusChanged -= HandleFocus;
            _platform.OnAdPlayingChanged -= HandleAdPlaying;
        }

        public void SetAdRequested(bool requested)
        {
            _state.AdRequested = requested;
            Apply();
        }

        private void HandlePause(bool paused)
        {
            _state.PlatformPaused = paused;
            Apply();
        }

        private void HandleAudio(bool enabled)
        {
            _state.PlatformMuted = !enabled;
            Apply();
        }

        private void HandleFocus(bool focused)
        {
            _state.FocusLost = !focused;
            Apply();
        }

        private void HandleAdPlaying(bool playing)
        {
            _state.AdPlaying = playing;
            Apply();
        }

        private void Apply()
        {
            Time.timeScale = _state.Frozen ? 0f : 1f;
            AudioListener.pause = _state.Muted;
            Debug.Log($"[PlaybackGate] frozen {_state.Frozen}, muted {_state.Muted}");
            OnChanged?.Invoke();
        }
    }
}
