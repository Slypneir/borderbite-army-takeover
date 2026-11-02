using System.Threading;
using Core.PlatformSystem.Enums;
using Cysharp.Threading.Tasks;

namespace Core.PlatformSystem.Interfaces
{
    /// <summary>
    /// Adverts by the rules of the platform: the game asks everywhere it would be natural, the rules decide.
    /// </summary>
    public interface IAdService
    {
        /// <summary>
        /// A battle ended: counts toward the first interstitial and starts a new interval between two battles
        /// </summary>
        void NoteBattleFinished();

        /// <summary>
        /// Shows an interstitial if the rules allow it here; false when it was not shown
        /// </summary>
        UniTask<bool> TryShowInterstitialAsync(AdPlacement placement, CancellationToken ct = default);

        /// <summary>
        /// The rewarded button may be shown here
        /// </summary>
        bool CanOfferRewarded(AdPlacement placement);

        /// <summary>
        /// A rewarded offer is on screen between two battles: on some platforms it excludes the interstitial
        /// </summary>
        void NoteRewardedOffered();

        /// <summary>
        /// Shows a rewarded advert on the press of the player; true only when the reward is earned
        /// </summary>
        UniTask<bool> ShowRewardedAsync(string rewardId, AdPlacement placement, CancellationToken ct = default);
    }
}
