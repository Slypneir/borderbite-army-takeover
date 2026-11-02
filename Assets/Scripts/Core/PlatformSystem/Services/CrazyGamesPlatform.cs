#if UNITY_EDITOR || BB_PLATFORM_CRAZYGAMES
using Core.PlatformSystem.Data;
using Core.PlatformSystem.Interfaces;

namespace Core.PlatformSystem.Services
{
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
}
#endif
