using System;
using System.Threading;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using Core.PlatformSystem.Interfaces;
using Cysharp.Threading.Tasks;
using FluentAssertions;
using Meta.ProgressSystem.Configuration;
using Meta.ProgressSystem.Enums;
using Meta.ProgressSystem.Services;
using NUnit.Framework;
using UnityEngine;

namespace Tests.ProgressSystem.Editor
{
    /// <summary>Crowns, upgrades and the save of layer 4 (stage 1 spec 5.2, 5.3; GDD 6).</summary>
    public sealed class ProgressTests
    {
        private ProgressConfig _config;
        private FakePlatform _platform;
        private ProgressService _progress;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProgressConfig>();
            _platform = new FakePlatform();
            _progress = new ProgressService(_platform, _config);
        }

        [TearDown]
        public void TearDown()
        {
            _progress.Dispose();
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void Prices_TwentyTimesOneAndAHalfPerStep_Rounded()
        {
            var prices = new int[10];
            for (var step = 1; step <= 10; step++)
                prices[step - 1] = _config.PriceOf(step);

            prices.Should().Equal(20, 30, 45, 68, 101, 152, 228, 342, 513, 769);
        }

        [Test]
        public void Award_VictoryOnLevelOne_TwelveCrowns_DefeatThree()
        {
            _progress.Award(BattleResult.Victory).Should().Be(12);
            _progress.Award(BattleResult.Defeat).Should().Be(3);
            _progress.Data.Crowns.Should().Be(15);
        }

        [Test]
        public void Buy_EnoughCrowns_StepUpAndSaved()
        {
            _progress.Data.Crowns = 50;

            _progress.Buy(Upgrade.HeroBrush).Should().BeTrue();
            _progress.Buy(Upgrade.HeroBrush).Should().BeTrue();
            _progress.Buy(Upgrade.HeroBrush).Should().BeFalse();

            _progress.StepOf(Upgrade.HeroBrush).Should().Be(2);
            _progress.Data.Crowns.Should().Be(0);
            _platform.Saved.Should().Contain("\"Upgrades\":[0,2,0,0]");
        }

        [Test]
        public void Apply_TwoStepsOfBrush_BrushHalfACellWider()
        {
            _progress.Data.Upgrades[(int)Upgrade.HeroBrush] = 2;
            var settings = new BattleSettings();

            _progress.Apply(settings);

            settings.PlayerBrushBonusUnits.Should().Be(BattleSettings.UnitsPerCell / 2);
        }

        [Test]
        public void Apply_AllUpgrades_OnlyThePlayerGrows()
        {
            _progress.Data.Upgrades = new[] { 3, 0, 5, 2 };
            var settings = new BattleSettings();

            _progress.Apply(settings);
            var simulation = new BattleSimulation(settings);

            // T14: +1 point of health a step, a blow stays 1
            settings.HeroHealthOf(Side.Player).Should().Be(13);
            settings.HeroHealthOf(Side.Enemy).Should().Be(7);
            settings.SoldierHealthOf(Side.Player).Should().Be(10);
            settings.SoldierHealthOf(Side.Enemy).Should().Be(5);
            settings.MeleeDamage.Should().Be(1);
            simulation.SideOf(Side.Player).Gold.Should().Be(10);
            simulation.SideOf(Side.Enemy).Gold.Should().Be(0);
        }

        [Test]
        public void Load_SavedJson_ComesBackTheSame()
        {
            _progress.Data.Crowns = 77;
            _progress.Data.Upgrades = new[] { 1, 2, 3, 4 };
            _progress.Data.HintStep = 2;
            _progress.Save();

            var again = new ProgressService(_platform, _config);
            again.Load(_platform.Saved);

            again.Data.Crowns.Should().Be(77);
            again.Data.Upgrades.Should().Equal(1, 2, 3, 4);
            again.Data.HintStep.Should().Be(2);
            again.Dispose();
        }

        [Test]
        public void Load_Nothing_FreshStart()
        {
            _progress.Load(null);

            _progress.Data.Level.Should().Be(1);
            _progress.Data.Crowns.Should().Be(0);
            _progress.Data.Upgrades.Should().HaveCount(4);
        }

        private sealed class FakePlatform : IPlatform
        {
            public string Saved;

            public string Language => "en";
            public bool AudioEnabled => true;
#pragma warning disable 67
            public event Action<bool> OnPauseChanged;
            public event Action<bool> OnAudioChanged;
            public event Action<bool> OnFocusChanged;
            public event Action<bool> OnAdPlayingChanged;
#pragma warning restore 67
            public UniTask InitializeAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public void SignalFirstFrame() { }
            public void SignalReady() { }
            public void SignalGameplay(bool playing) { }
            public UniTask<bool> ShowInterstitialAsync(CancellationToken ct = default) => UniTask.FromResult(false);
            public UniTask<bool> ShowRewardedAsync(string rewardId, CancellationToken ct = default) => UniTask.FromResult(false);
            public UniTask<string> LoadAsync(CancellationToken ct = default) => UniTask.FromResult(Saved);

            public UniTask<bool> SaveAsync(string data, CancellationToken ct = default)
            {
                Saved = data;
                return UniTask.FromResult(true);
            }
        }
    }
}
