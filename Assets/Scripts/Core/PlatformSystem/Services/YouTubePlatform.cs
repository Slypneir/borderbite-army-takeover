#if UNITY_EDITOR || BB_PLATFORM_YOUTUBE
using Core.PlatformSystem.Data;
using Core.PlatformSystem.Interfaces;

namespace Core.PlatformSystem.Services
{
    /// <summary>
    /// YouTube Playables: a closed platform, not a single request anywhere but the SDK. Progress only through saveData, which
    /// YouTube accepts after a successful loadData; a save is at most 3 MiB.
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
}
#endif
