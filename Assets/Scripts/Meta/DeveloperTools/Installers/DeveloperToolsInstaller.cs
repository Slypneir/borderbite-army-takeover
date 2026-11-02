using VContainer;
using VContainer.Unity;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Meta.DeveloperTools.Services;
#endif

namespace Meta.DeveloperTools.Installers
{
    /// <summary>
    /// The tools of the developer; nothing is registered in a build for players.
    /// </summary>
    public sealed class DeveloperToolsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.Register<DevTools>(Lifetime.Singleton).As<IStartable>();
#endif
        }
    }
}
