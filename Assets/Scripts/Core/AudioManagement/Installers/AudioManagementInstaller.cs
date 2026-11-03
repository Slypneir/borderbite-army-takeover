using System;
using Core.AudioManagement.Configuration;
using Core.AudioManagement.Interfaces;
using Core.AudioManagement.Services;
using VContainer;
using VContainer.Unity;

namespace Core.AudioManagement.Installers
{
    /// <summary>
    /// The sounds of the game through one table of volumes.
    /// </summary>
    public sealed class AudioManagementInstaller : IInstaller
    {
        private readonly SoundVolumes _volumes;

        public AudioManagementInstaller(SoundVolumes volumes)
        {
            _volumes = volumes;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_volumes);
            builder.Register<SfxPlayer>(Lifetime.Singleton).As<ISfxPlayer, IDisposable>();
        }
    }
}
