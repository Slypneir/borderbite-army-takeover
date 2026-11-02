using UnityEditor;

namespace Core.EditorTools
{
    /// <summary>
    /// What makes the build of one platform: its symbol (the C# adapter), jslib, page template, ad policy and compression.
    /// Compression differs by the rules of the platforms: Docs/PLATFORM_REQUIREMENTS.md.
    /// </summary>
    public sealed class PlatformBuildProfile
    {
        public string Id;
        public string Symbol;
        public string Jslib;
        public string Template;
        public string AdPolicy;
        public string OutputDir;
        public WebGLCompressionFormat Compression;
        public bool DataCaching;

        /// <summary>Text that must not be found in a build of this platform; checked after the build.</summary>
        public string[] Forbidden;

        private const string JslibFolder = "Assets/Plugins/WebGL/Platforms/";
        private const string PolicyFolder = "Assets/ScriptableObjects/Platform/";
        private const string YouTubeId = "youtube";
        private const string CrazyGamesId = "crazygames";
        private const string YandexId = "yandex";

        public static readonly PlatformBuildProfile YouTube = new PlatformBuildProfile
        {
            Id = YouTubeId,
            Symbol = "BB_PLATFORM_YOUTUBE",
            Jslib = JslibFolder + "platform_youtube.jslib",
            Template = "PROJECT:Playables",
            AdPolicy = PolicyFolder + "AdPolicy_YouTube.asset",
            OutputDir = "Build/Playables",
            Compression = WebGLCompressionFormat.Disabled,
            DataCaching = false,
            Forbidden = new[] { "crazygames", "CrazyGames", "yandex", "Yandex", "YaGames", "ysdk", "sdk.js" },
        };

        public static readonly PlatformBuildProfile CrazyGames = new PlatformBuildProfile
        {
            Id = CrazyGamesId,
            Symbol = "BB_PLATFORM_CRAZYGAMES",
            Jslib = JslibFolder + "platform_crazygames.jslib",
            Template = "PROJECT:CrazyGames",
            AdPolicy = PolicyFolder + "AdPolicy_CrazyGames.asset",
            OutputDir = "Build/CrazyGames",
            Compression = WebGLCompressionFormat.Brotli,
            DataCaching = true,
            Forbidden = new[] { "ytgame", "game_api/v1", "yandex", "Yandex", "YaGames", "ysdk" },
        };

        public static readonly PlatformBuildProfile Yandex = new PlatformBuildProfile
        {
            Id = YandexId,
            Symbol = "BB_PLATFORM_YANDEX",
            Jslib = JslibFolder + "platform_yandex.jslib",
            Template = "PROJECT:Yandex",
            AdPolicy = PolicyFolder + "AdPolicy_Yandex.asset",
            OutputDir = "Build/Yandex",
            Compression = WebGLCompressionFormat.Brotli,
            DataCaching = true,
            Forbidden = new[] { "ytgame", "game_api/v1", "crazygames", "CrazyGames" },
        };

        public static readonly PlatformBuildProfile[] All = { YouTube, CrazyGames, Yandex };

        public static PlatformBuildProfile Find(string id)
        {
            foreach (var profile in All)
            {
                if (profile.Id == id)
                    return profile;
            }

            throw new System.ArgumentException("no platform " + id);
        }
    }
}
