using System;
using System.Threading;
using Core.PlatformSystem.Configuration;
using Core.PlatformSystem.Enums;
using Core.PlatformSystem.Interfaces;
using Core.PlatformSystem.Structs;
using Core.PlatformSystem.Utils;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.PlatformSystem.Services
{
    /// <summary>
    /// Adverts by the rules of the platform (<see cref="AdPolicyConfig"/>); the game stands still from the request to the answer.
    /// </summary>
    public sealed class AdService : IAdService
    {
        private readonly IPlatform _platform;
        private readonly IPlaybackGate _gate;
        private readonly AdPolicyConfig _config;
        private int _battlesFinished;
        private double _lastInterstitial = double.NegativeInfinity;
        private bool _rewardedOfferedSinceBattle;
        private bool _showing;

        public AdService(IPlatform platform, IPlaybackGate gate, AdPolicyConfig config)
        {
            _platform = platform;
            _gate = gate;
            _config = config;
        }

        /// <summary>Seconds of real time; tests replace it.</summary>
        public Func<double> Clock { get; set; } = () => Time.realtimeSinceStartupAsDouble;

        public void NoteBattleFinished()
        {
            _battlesFinished++;
            _rewardedOfferedSinceBattle = false;
        }

        public void NoteRewardedOffered() => _rewardedOfferedSinceBattle = true;

        public bool CanOfferRewarded(AdPlacement placement) => AdPolicy.CanOfferRewarded(_config, placement);

        public async UniTask<bool> TryShowInterstitialAsync(AdPlacement placement, CancellationToken ct = default)
        {
            var now = Clock();
            var context = new AdContext(_battlesFinished, now - _lastInterstitial, _rewardedOfferedSinceBattle);
            if (_showing || !AdPolicy.CanShowInterstitial(_config, placement, context))
                return false;

            _showing = true;
            _gate.SetAdRequested(true);
            try
            {
                var shown = await _platform.ShowInterstitialAsync(ct);
                if (shown)
                    _lastInterstitial = Clock();
                return shown;
            }
            finally
            {
                _gate.SetAdRequested(false);
                _showing = false;
            }
        }

        public async UniTask<bool> ShowRewardedAsync(string rewardId, AdPlacement placement, CancellationToken ct = default)
        {
            if (_showing || !CanOfferRewarded(placement))
                return false;

            _showing = true;
            _gate.SetAdRequested(true);
            try
            {
                return await _platform.ShowRewardedAsync(rewardId, ct);
            }
            finally
            {
                _gate.SetAdRequested(false);
                _showing = false;
            }
        }
    }
}
