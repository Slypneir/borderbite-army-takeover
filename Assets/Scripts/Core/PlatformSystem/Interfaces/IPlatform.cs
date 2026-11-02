using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.PlatformSystem.Interfaces
{
    /// <summary>
    /// The one adapter between the game and a platform: the game never calls a platform SDK directly. The adapter is chosen
    /// at build time, a build holds only its own (PlatformSystemInstaller, Editor/PlatformBuild).
    /// </summary>
    public interface IPlatform
    {
        /// <summary>
        /// Two-letter language of the player from the SDK; the device language until the SDK answers
        /// </summary>
        string Language { get; }

        /// <summary>
        /// The platform allows sound right now (its mute button and settings)
        /// </summary>
        bool AudioEnabled { get; }

        /// <summary>
        /// The platform stopped the game (true) or let it go on (false)
        /// </summary>
        event Action<bool> OnPauseChanged;

        /// <summary>
        /// The platform turned the sound on (true) or off (false), apart from the pause
        /// </summary>
        event Action<bool> OnAudioChanged;

        /// <summary>
        /// The page lost (false) or got back (true) the focus; platforms that forbid watching the page never raise it
        /// </summary>
        event Action<bool> OnFocusChanged;

        /// <summary>
        /// An advert started (true) or ended (false) playing on screen
        /// </summary>
        event Action<bool> OnAdPlayingChanged;

        /// <summary>
        /// Starts the SDK; never throws: without the SDK the game plays as a guest with no ads
        /// </summary>
        UniTask InitializeAsync(CancellationToken ct = default);

        /// <summary>
        /// The first frame of the loading screen is drawn
        /// </summary>
        void SignalFirstFrame();

        /// <summary>
        /// The game loaded and the player can act; no loading screen is left
        /// </summary>
        void SignalReady();

        /// <summary>
        /// A battle runs (true) or stopped (false): menus, results, adverts
        /// </summary>
        void SignalGameplay(bool playing);

        /// <summary>
        /// Shows an interstitial; true when the platform took the request. Ask only through IAdService: it knows the rules
        /// </summary>
        UniTask<bool> ShowInterstitialAsync(CancellationToken ct = default);

        /// <summary>
        /// Shows a rewarded advert; true only when the reward is earned. The id holds no user data
        /// </summary>
        UniTask<bool> ShowRewardedAsync(string rewardId, CancellationToken ct = default);

        /// <summary>
        /// Reads the save of the player; null when there is none or it failed
        /// </summary>
        UniTask<string> LoadAsync(CancellationToken ct = default);

        /// <summary>
        /// Writes the save of the player; false when it failed or is over the limit of the platform
        /// </summary>
        UniTask<bool> SaveAsync(string data, CancellationToken ct = default);
    }
}
