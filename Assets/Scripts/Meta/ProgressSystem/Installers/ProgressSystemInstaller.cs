using System;
using Meta.ProgressSystem.Configuration;
using Meta.ProgressSystem.Services;
using VContainer;
using VContainer.Unity;

namespace Meta.ProgressSystem.Installers
{
    /// <summary>The progress of the player between levels: crowns, upgrades, the save.</summary>
    public sealed class ProgressSystemInstaller : IInstaller
    {
        private readonly ProgressConfig _config;

        public ProgressSystemInstaller(ProgressConfig config)
        {
            _config = config;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_config);
            builder.Register<ProgressService>(Lifetime.Singleton).AsSelf().As<IDisposable>();
        }
    }
}
