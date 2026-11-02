#if UNITY_EDITOR || BB_PLATFORM_YANDEX
using Core.PlatformSystem.Data;
using Core.PlatformSystem.Interfaces;

namespace Core.PlatformSystem.Services
{
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
}
#endif
