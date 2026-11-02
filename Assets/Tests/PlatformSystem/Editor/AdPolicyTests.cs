using Core.PlatformSystem.Configuration;
using Core.PlatformSystem.Enums;
using Core.PlatformSystem.Structs;
using Core.PlatformSystem.Utils;
using FluentAssertions;
using NUnit.Framework;
using UnityEditor;

namespace Tests.PlatformSystem.Editor
{
    /// <summary>The real assets of the platforms: these are the rules a build carries.</summary>
    public sealed class AdPolicyTests
    {
        private static readonly AdContext Late = new AdContext(10, 10000, false);

        private static AdPolicyConfig Load(string name) =>
            AssetDatabase.LoadAssetAtPath<AdPolicyConfig>("Assets/ScriptableObjects/Platform/AdPolicy_" + name + ".asset");

        [TestCase("YouTube")]
        [TestCase("CrazyGames")]
        [TestCase("Yandex")]
        public void CanShowInterstitial_ActiveBattle_NeverOnAnyPlatform(string platform)
        {
            AdPolicy.CanShowInterstitial(Load(platform), AdPlacement.ActiveBattle, Late).Should().BeFalse();
        }

        [TestCase("YouTube")]
        [TestCase("CrazyGames")]
        [TestCase("Yandex")]
        public void CanOfferRewarded_ActiveBattle_NeverOnAnyPlatform(string platform)
        {
            AdPolicy.CanOfferRewarded(Load(platform), AdPlacement.ActiveBattle).Should().BeFalse();
            AdPolicy.CanOfferRewarded(Load(platform), AdPlacement.Transition).Should().BeTrue();
        }

        [TestCase("YouTube", false)]
        [TestCase("CrazyGames", false)]
        [TestCase("Yandex", true)]
        public void CanShowInterstitial_NavigationButton_OnlyYandexAllowsIt(string platform, bool expected)
        {
            AdPolicy.CanShowInterstitial(Load(platform), AdPlacement.NavigationButton, Late).Should().Be(expected);
        }

        [TestCase("YouTube", true)]
        [TestCase("CrazyGames", true)]
        [TestCase("Yandex", false)]
        public void CanShowInterstitial_TransitionWithoutPress_YandexNeedsAnAction(string platform, bool expected)
        {
            AdPolicy.CanShowInterstitial(Load(platform), AdPlacement.Transition, Late).Should().Be(expected);
        }

        [TestCase("YouTube")]
        [TestCase("CrazyGames")]
        [TestCase("Yandex")]
        public void CanShowInterstitial_FirstBattles_NotYet(string platform)
        {
            var policy = Load(platform);
            var early = new AdContext(policy.FirstInterstitialAfterBattles - 1, 10000, false);

            AdPolicy.CanShowInterstitial(policy, AdPlacement.NextOrRetryButton, early).Should().BeFalse();
        }

        [Test]
        public void CanShowInterstitial_CrazyGamesAfterRewardedOffer_NotBoth()
        {
            var offered = new AdContext(10, 10000, true);

            AdPolicy.CanShowInterstitial(Load("CrazyGames"), AdPlacement.Transition, offered).Should().BeFalse();
            AdPolicy.CanShowInterstitial(Load("YouTube"), AdPlacement.Transition, offered).Should().BeTrue();
        }

        [Test]
        public void CanShowInterstitial_YouTubeTooSoon_WaitsOwnPause_SdkPlatformsLeaveItToTheSdk()
        {
            var soon = new AdContext(10, 1, false);

            AdPolicy.CanShowInterstitial(Load("YouTube"), AdPlacement.Transition, soon).Should().BeFalse();
            AdPolicy.CanShowInterstitial(Load("CrazyGames"), AdPlacement.Transition, soon).Should().BeTrue();
            AdPolicy.CanShowInterstitial(Load("Yandex"), AdPlacement.NextOrRetryButton, soon).Should().BeTrue();
        }
    }
}
