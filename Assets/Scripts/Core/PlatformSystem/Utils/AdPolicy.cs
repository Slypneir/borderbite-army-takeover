using Core.PlatformSystem.Configuration;
using Core.PlatformSystem.Enums;
using Core.PlatformSystem.Structs;

namespace Core.PlatformSystem.Utils
{
    /// <summary>Decides whether an advert may be shown here. The differences between the platforms are the assets, not this code.</summary>
    public static class AdPolicy
    {
        public static bool CanShowInterstitial(AdPolicyConfig config, AdPlacement placement, AdContext context)
        {
            if (!config.AdsEnabled || !IsPlacementAllowed(config, placement))
                return false;
            if (context.BattlesFinished < config.FirstInterstitialAfterBattles)
                return false;
            if (config.RewardedExcludesInterstitial && context.RewardedOfferedSinceBattle)
                return false;

            return context.SecondsSinceInterstitial >= config.MinSecondsBetweenInterstitials;
        }

        /// <summary>The rewarded button may be on screen: never in a running battle.</summary>
        public static bool CanOfferRewarded(AdPolicyConfig config, AdPlacement placement) =>
            config.AdsEnabled && config.RewardedEnabled && placement != AdPlacement.ActiveBattle;

        private static bool IsPlacementAllowed(AdPolicyConfig config, AdPlacement placement)
        {
            switch (placement)
            {
                case AdPlacement.Transition: return config.InterstitialOnTransition;
                case AdPlacement.NextOrRetryButton: return config.InterstitialOnNextOrRetry;
                case AdPlacement.NavigationButton: return config.InterstitialOnNavigation;
                default: return false;
            }
        }
    }
}
