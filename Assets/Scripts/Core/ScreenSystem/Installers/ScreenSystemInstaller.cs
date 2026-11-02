using Core.ScreenSystem.Configuration;
using Core.ScreenSystem.Interfaces;
using Core.ScreenSystem.Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.ScreenSystem.Installers
{
    /// <summary>
    /// The adaptive screen: the camera scale under the window. The interface follows by anchors and <c>SafeAreaFitter</c>.
    /// </summary>
    public sealed class ScreenSystemInstaller : IInstaller
    {
        private readonly ScreenLayoutConfig _config;
        private readonly Camera _camera;

        public ScreenSystemInstaller(ScreenLayoutConfig config, Camera camera)
        {
            _config = config;
            _camera = camera;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_config);
            builder.RegisterInstance(_camera);
            builder.Register<ScreenLayoutService>(Lifetime.Singleton).As<IScreenLayout, IStartable, ITickable>();
        }
    }
}
