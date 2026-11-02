using System;
using Core.PlatformSystem.Configuration;
using Core.PlatformSystem.Interfaces;
using Core.PlatformSystem.Services;
using VContainer;
using VContainer.Unity;

namespace Core.PlatformSystem.Installers
{
    /// <summary>
    /// Registers the adapter chosen at build time by a scripting symbol (Editor/PlatformBuild); the editor plays on its own one.
    /// A build compiles exactly one adapter: the code of the others is not in it. A new platform: a new
    /// <see cref="IPlatform"/>, a jslib, a page template and a line in <see cref="CreatePlatform"/> and in the build profiles.
    /// </summary>
    public sealed class PlatformSystemInstaller : IInstaller
    {
        private readonly AdPolicyConfig _adPolicyConfig;

        public PlatformSystemInstaller(AdPolicyConfig adPolicyConfig)
        {
            _adPolicyConfig = adPolicyConfig;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(CreatePlatform()).As<IPlatform>();
            builder.RegisterInstance(_adPolicyConfig);
            builder.Register<PlaybackGate>(Lifetime.Singleton).As<IPlaybackGate, IStartable, IDisposable>();
            builder.Register<AdService>(Lifetime.Singleton).As<IAdService>();
        }

        private static IPlatform CreatePlatform()
        {
#if UNITY_EDITOR
            return new EditorPlatform();
#elif BB_PLATFORM_YOUTUBE
            return new YouTubePlatform(CreateBridge());
#elif BB_PLATFORM_CRAZYGAMES
            return new CrazyGamesPlatform(CreateBridge());
#elif BB_PLATFORM_YANDEX
            return new YandexPlatform(CreateBridge());
#else
#error Build through Tools/Build: a player build needs the symbol of its platform (Editor/PlatformBuild)
#endif
        }

#if !UNITY_EDITOR
        private static ISdkBridge CreateBridge()
        {
            var bridge = new JsSdkBridge();
            bridge.Listen();
            return bridge;
        }
#endif
    }
}
