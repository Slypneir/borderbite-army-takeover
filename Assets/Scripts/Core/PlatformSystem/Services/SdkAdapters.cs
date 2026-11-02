using Core.PlatformSystem.Data;
using Core.PlatformSystem.Interfaces;

// All adapters live in this one file on purpose. A build compiles only the adapter of its symbol, but Unity also stores the name of
// every script file in the build: a file named after a platform would leave that name in the builds of the others.
namespace Core.PlatformSystem.Services
{
#if UNITY_EDITOR || BB_PLATFORM_YOUTUBE
    /// <summary>
    /// YouTube Playables: a closed platform, not a single request anywhere but the SDK. Progress only through saveData, which
    /// YouTube accepts after a successful loadData; a save is at most 3 MiB of UTF-16.
    /// </summary>
    public sealed class YouTubePlatform : SdkPlatform
    {
        public YouTubePlatform(ISdkBridge bridge) : base(bridge, new PlatformCapabilities
        {
            LoadBeforeSave = true,
            SaveLimit = 3 * 1024 * 1024 / 2,
        })
        {
        }
    }
#endif

#if UNITY_EDITOR || BB_PLATFORM_CRAZYGAMES
    /// <summary>
    /// CrazyGames: progress in the Data module of the SDK, the mute of the site over the sound of the game.
    /// </summary>
    public sealed class CrazyGamesPlatform : SdkPlatform
    {
        public CrazyGamesPlatform(ISdkBridge bridge) : base(bridge, new PlatformCapabilities
        {
            SaveLimit = 1024 * 1024,
        })
        {
        }
    }
#endif

#if UNITY_EDITOR || BB_PLATFORM_YANDEX
    /// <summary>
    /// Yandex Games: progress in the player data of the SDK, 200 KB at most, written at once after an action of the player.
    /// </summary>
    public sealed class YandexPlatform : SdkPlatform
    {
        public YandexPlatform(ISdkBridge bridge) : base(bridge, new PlatformCapabilities
        {
            SaveLimit = 200 * 1000 / 2,
        })
        {
        }
    }
#endif
}
