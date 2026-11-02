using System;
using System.Threading;
using Core.PlatformSystem.Data;
using Core.PlatformSystem.Interfaces;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.PlatformSystem.Services
{
    /// <summary>
    /// A platform whose SDK lives in the page. Every call fails softly: without the SDK the game plays as a guest with no ads.
    /// The stop and start of a battle follow the pause of the platform, as its gameplay markup asks.
    /// </summary>
    public abstract class SdkPlatform : IPlatform
    {
        private readonly ISdkBridge _bridge;
        private readonly PlatformCapabilities _capabilities;
        private bool _sdkUp;
        private bool _playing;
        private bool _saveRead;

        protected SdkPlatform(ISdkBridge bridge, PlatformCapabilities capabilities)
        {
            _bridge = bridge;
            _capabilities = capabilities;
            _bridge.OnEvent += HandleEvent;
            Language = Application.systemLanguage == SystemLanguage.Russian ? "ru" : "en";
        }

        public string Language { get; private set; }

        public bool AudioEnabled { get; private set; } = true;

        public event Action<bool> OnPauseChanged;

        public event Action<bool> OnAudioChanged;

        public event Action<bool> OnFocusChanged;

        public event Action<bool> OnAdPlayingChanged;

        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            var answer = await _bridge.CallAsync("init", "", ct);
            _sdkUp = !string.IsNullOrEmpty(answer);
            if (_sdkUp)
            {
                var init = JsonUtility.FromJson<SdkInitAnswer>(answer);
                if (!string.IsNullOrEmpty(init.lang))
                    Language = init.lang.Substring(0, Math.Min(2, init.lang.Length)).ToLowerInvariant();
                SetAudio(init.audio);
            }

            Debug.Log($"[{GetType().Name}] SDK {(_sdkUp ? "up" : "absent")}, language {Language}, audio {AudioEnabled}");
        }

        public void SignalFirstFrame() => Signal("firstFrame");

        public void SignalReady() => Signal("ready");

        public void SignalGameplay(bool playing)
        {
            _playing = playing;
            Signal(playing ? "gameplayStart" : "gameplayStop");
        }

        public async UniTask<bool> ShowInterstitialAsync(CancellationToken ct = default)
        {
            if (!_sdkUp)
                return false;

            return await _bridge.CallAsync("interstitial", "", ct) == "shown";
        }

        public async UniTask<bool> ShowRewardedAsync(string rewardId, CancellationToken ct = default)
        {
            if (!_sdkUp)
                return false;

            return await _bridge.CallAsync("rewarded", rewardId, ct) == "rewarded";
        }

        public async UniTask<string> LoadAsync(CancellationToken ct = default)
        {
            if (!_sdkUp)
                return null;

            var data = await _bridge.CallAsync("load", "", ct);
            _saveRead = data != null;
            return string.IsNullOrEmpty(data) ? null : data;
        }

        public async UniTask<bool> SaveAsync(string data, CancellationToken ct = default)
        {
            if (!_sdkUp)
                return false;

            if (data != null && data.Length > _capabilities.SaveLimit)
            {
                Debug.LogWarning($"[{GetType().Name}] save of {data.Length} is over the limit of {_capabilities.SaveLimit}");
                return false;
            }

            if (_capabilities.LoadBeforeSave && !_saveRead)
            {
                await LoadAsync(ct);
                if (!_saveRead)
                    return false;
            }

            return await _bridge.CallAsync("save", data ?? "", ct) != null;
        }

        private void Signal(string op)
        {
            if (_sdkUp)
                _bridge.CallAsync(op, "").Forget();
        }

        private void SetAudio(bool enabled)
        {
            if (AudioEnabled == enabled)
                return;

            AudioEnabled = enabled;
            OnAudioChanged?.Invoke(enabled);
        }

        private void HandleEvent(string type)
        {
            switch (type)
            {
                case "pause":
                    if (_playing)
                        Signal("gameplayStop");
                    OnPauseChanged?.Invoke(true);
                    break;
                case "resume":
                    OnPauseChanged?.Invoke(false);
                    if (_playing)
                        Signal("gameplayStart");
                    break;
                case "audioOn":
                    SetAudio(true);
                    break;
                case "audioOff":
                    SetAudio(false);
                    break;
                case "focusLost":
                    OnFocusChanged?.Invoke(false);
                    break;
                case "focusBack":
                    OnFocusChanged?.Invoke(true);
                    break;
                case "adStart":
                    OnAdPlayingChanged?.Invoke(true);
                    break;
                case "adEnd":
                    OnAdPlayingChanged?.Invoke(false);
                    break;
            }
        }
    }
}
