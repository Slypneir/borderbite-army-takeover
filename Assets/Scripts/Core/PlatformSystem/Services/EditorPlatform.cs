#if UNITY_EDITOR
using System;
using System.Threading;
using Core.PlatformSystem.Interfaces;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.PlatformSystem.Services
{
    /// <summary>
    /// The editor plays without an SDK: saves in PlayerPrefs, adverts are a log line, the pause and the language come from DevBridge.
    /// </summary>
    public sealed class EditorPlatform : IPlatform
    {
        private const string SaveKey = "borderbite.save";

        public string Language { get; set; } = Application.systemLanguage == SystemLanguage.Russian ? "ru" : "en";

        public bool AudioEnabled { get; private set; } = true;

        public event Action<bool> OnPauseChanged;

        public event Action<bool> OnAudioChanged;

        public event Action<bool> OnFocusChanged;

        public event Action<bool> OnAdPlayingChanged;

        public UniTask InitializeAsync(CancellationToken ct = default) => UniTask.CompletedTask;

        public void SignalFirstFrame() => Debug.Log("[EditorPlatform] first frame");

        public void SignalReady() => Debug.Log("[EditorPlatform] ready");

        public void SignalGameplay(bool playing) => Debug.Log($"[EditorPlatform] gameplay {playing}");

        public async UniTask<bool> ShowInterstitialAsync(CancellationToken ct = default)
        {
            Debug.Log("[EditorPlatform] interstitial");
            OnAdPlayingChanged?.Invoke(true);
            await UniTask.Yield(ct);
            OnAdPlayingChanged?.Invoke(false);
            return true;
        }

        public async UniTask<bool> ShowRewardedAsync(string rewardId, CancellationToken ct = default)
        {
            Debug.Log($"[EditorPlatform] rewarded {rewardId}");
            OnAdPlayingChanged?.Invoke(true);
            await UniTask.Yield(ct);
            OnAdPlayingChanged?.Invoke(false);
            return true;
        }

        public UniTask<string> LoadAsync(CancellationToken ct = default) =>
            UniTask.FromResult(PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null);

        public UniTask<bool> SaveAsync(string data, CancellationToken ct = default)
        {
            PlayerPrefs.SetString(SaveKey, data ?? "");
            PlayerPrefs.Save();
            return UniTask.FromResult(true);
        }

        public void SimulatePause(bool paused) => OnPauseChanged?.Invoke(paused);

        public void SimulateAudio(bool enabled)
        {
            AudioEnabled = enabled;
            OnAudioChanged?.Invoke(enabled);
        }

        public void SimulateFocus(bool focused) => OnFocusChanged?.Invoke(focused);
    }
}
#endif
