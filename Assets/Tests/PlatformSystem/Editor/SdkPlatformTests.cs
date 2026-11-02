using System;
using System.Collections.Generic;
using System.Threading;
using Core.PlatformSystem.Interfaces;
using Core.PlatformSystem.Services;
using Cysharp.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.PlatformSystem.Editor
{
    public sealed class SdkPlatformTests
    {
        private sealed class FakeBridge : ISdkBridge
        {
            public readonly List<string> Calls = new List<string>();
            public readonly Dictionary<string, string> Answers = new Dictionary<string, string>();

            public event Action<string> OnEvent;

            public UniTask<string> CallAsync(string op, string arg, CancellationToken ct = default)
            {
                Calls.Add(op);
                return UniTask.FromResult(Answers.TryGetValue(op, out var answer) ? answer : null);
            }

            public void Raise(string type) => OnEvent?.Invoke(type);
        }

        private static FakeBridge Up(string init = "{\"lang\":\"ru-RU\",\"audio\":true}")
        {
            var bridge = new FakeBridge();
            bridge.Answers["init"] = init;
            bridge.Answers["firstFrame"] = "";
            bridge.Answers["ready"] = "";
            return bridge;
        }

        [Test]
        public void Initialize_SdkAnswers_LanguageFromTheTag()
        {
            var platform = new CrazyGamesPlatform(Up());

            platform.InitializeAsync().GetAwaiter().GetResult();

            platform.Language.Should().Be("ru");
        }

        [Test]
        public void Initialize_MutedByPlatform_RaisesNothingButAudioIsOff()
        {
            var platform = new YouTubePlatform(Up("{\"lang\":\"en-US\",\"audio\":false}"));

            platform.InitializeAsync().GetAwaiter().GetResult();

            platform.AudioEnabled.Should().BeFalse();
        }

        [Test]
        public void Calls_NoSdk_FailSoftlyAndSignalNothing()
        {
            var bridge = new FakeBridge();
            var platform = new YandexPlatform(bridge);

            platform.InitializeAsync().GetAwaiter().GetResult();
            platform.SignalFirstFrame();
            platform.SignalReady();
            platform.SignalGameplay(true);

            platform.ShowInterstitialAsync().GetAwaiter().GetResult().Should().BeFalse();
            platform.ShowRewardedAsync("r").GetAwaiter().GetResult().Should().BeFalse();
            platform.LoadAsync().GetAwaiter().GetResult().Should().BeNull();
            platform.SaveAsync("x").GetAwaiter().GetResult().Should().BeFalse();
            bridge.Calls.Should().Equal("init");
        }

        [Test]
        public void ShowRewarded_ClosedEarly_NoReward()
        {
            var bridge = Up();
            var platform = new CrazyGamesPlatform(bridge);
            platform.InitializeAsync().GetAwaiter().GetResult();

            bridge.Answers["rewarded"] = "closed";
            platform.ShowRewardedAsync("r").GetAwaiter().GetResult().Should().BeFalse();

            bridge.Answers["rewarded"] = "rewarded";
            platform.ShowRewardedAsync("r").GetAwaiter().GetResult().Should().BeTrue();
        }

        [Test]
        public void Save_YouTubeBeforeLoad_ReadsTheSaveFirst()
        {
            var bridge = Up();
            bridge.Answers["load"] = "";
            bridge.Answers["save"] = "ok";
            var platform = new YouTubePlatform(bridge);
            platform.InitializeAsync().GetAwaiter().GetResult();

            platform.SaveAsync("x").GetAwaiter().GetResult().Should().BeTrue();

            bridge.Calls.Should().ContainInOrder("load", "save");
        }

        [Test]
        public void Save_YouTubeLoadFailed_DoesNotWrite()
        {
            var bridge = Up();
            bridge.Answers["save"] = "ok";
            var platform = new YouTubePlatform(bridge);
            platform.InitializeAsync().GetAwaiter().GetResult();

            platform.SaveAsync("x").GetAwaiter().GetResult().Should().BeFalse();

            bridge.Calls.Should().NotContain("save");
        }

        [Test]
        public void Save_OverTheLimit_Refused()
        {
            var bridge = Up();
            bridge.Answers["save"] = "ok";
            var platform = new YandexPlatform(bridge);
            platform.InitializeAsync().GetAwaiter().GetResult();

            platform.SaveAsync(new string('x', 200 * 1000)).GetAwaiter().GetResult().Should().BeFalse();

            bridge.Calls.Should().NotContain("save");
        }

        [Test]
        public void PauseEvent_DuringBattle_StopsAndStartsTheGameplayMarkup()
        {
            var bridge = Up();
            var platform = new YandexPlatform(bridge);
            platform.InitializeAsync().GetAwaiter().GetResult();
            var pauses = new List<bool>();
            platform.OnPauseChanged += pauses.Add;
            platform.SignalGameplay(true);
            bridge.Calls.Clear();

            bridge.Raise("pause");
            bridge.Raise("resume");

            pauses.Should().Equal(true, false);
            bridge.Calls.Should().Equal("gameplayStop", "gameplayStart");
        }

        [Test]
        public void PlaybackState_AdRequested_FrozenButNotMuted_AdPlaying_Both()
        {
            var state = new PlaybackState { AdRequested = true };

            state.Frozen.Should().BeTrue();
            state.Muted.Should().BeFalse();

            state.AdPlaying = true;
            state.Muted.Should().BeTrue();
        }

        [Test]
        public void PlaybackState_PlatformMute_SilentButRunning()
        {
            var state = new PlaybackState { PlatformMuted = true };

            state.Frozen.Should().BeFalse();
            state.Muted.Should().BeTrue();
        }

        [Test]
        public void PlaybackState_ResumeWhileFocusLost_StaysStopped()
        {
            var state = new PlaybackState { PlatformPaused = true, FocusLost = true };

            state.PlatformPaused = false;

            state.Frozen.Should().BeTrue();
            state.Muted.Should().BeTrue();
        }
    }
}
