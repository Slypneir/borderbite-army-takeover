using UnityEngine;

namespace Core.PlatformSystem.Configuration
{
    /// <summary>The ad rules of one platform. One asset per platform; a build carries only the asset of its own platform.</summary>
    [CreateAssetMenu(fileName = "AdPolicy", menuName = "Borderbite/Platform/Ad Policy")]
    public sealed class AdPolicyConfig : ScriptableObject
    {
        [Tooltip("Ads at all. Off for a launch without ads.")]
        [SerializeField] private bool adsEnabled = true;

        [Tooltip("Interstitial when the game itself moves between two different screens, with no press of the player.")]
        [SerializeField] private bool interstitialOnTransition = true;

        [Tooltip("Interstitial after «Next» or «Again» on the results.")]
        [SerializeField] private bool interstitialOnNextOrRetry = true;

        [Tooltip("Interstitial after a navigation button: menu, settings, shop.")]
        [SerializeField] private bool interstitialOnNavigation;

        [Tooltip("Battles to finish before the first interstitial, count. Starting value: 4.")]
        [Min(0)] [SerializeField] private int firstInterstitialAfterBattles = 4;

        [Tooltip("Own pause between interstitials, seconds. 0 — the frequency is left to the SDK. Starting value: 180.")]
        [Min(0f)] [SerializeField] private float minSecondsBetweenInterstitials = 180f;

        [Tooltip("Rewarded ads by the button of the player.")]
        [SerializeField] private bool rewardedEnabled = true;

        [Tooltip("Between two battles: an interstitial or a rewarded offer to continue, not both.")]
        [SerializeField] private bool rewardedExcludesInterstitial;

        public bool AdsEnabled => adsEnabled;
        public bool InterstitialOnTransition => interstitialOnTransition;
        public bool InterstitialOnNextOrRetry => interstitialOnNextOrRetry;
        public bool InterstitialOnNavigation => interstitialOnNavigation;
        public int FirstInterstitialAfterBattles => firstInterstitialAfterBattles;
        public float MinSecondsBetweenInterstitials => minSecondsBetweenInterstitials;
        public bool RewardedEnabled => rewardedEnabled;
        public bool RewardedExcludesInterstitial => rewardedExcludesInterstitial;

#if UNITY_EDITOR
        /// <summary>Starting values of a generated asset (Tools/Setup); the owner tunes the asset afterwards.</summary>
        public void InitForEditor(bool transition, bool nextOrRetry, bool navigation, float minSeconds, bool rewardedExcludes)
        {
            interstitialOnTransition = transition;
            interstitialOnNextOrRetry = nextOrRetry;
            interstitialOnNavigation = navigation;
            minSecondsBetweenInterstitials = minSeconds;
            rewardedExcludesInterstitial = rewardedExcludes;
        }
#endif
    }
}
