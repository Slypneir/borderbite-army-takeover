using Core.InputManagement.Configuration;
using Core.InputManagement.Interfaces;
using Core.InputManagement.Services;
using VContainer;
using VContainer.Unity;

namespace Core.InputManagement.Installers
{
    /// <summary>
    /// The input of the battle: where the hero goes, by the floating stick, the held mouse or the keys.
    /// </summary>
    public sealed class InputManagementInstaller : IInstaller
    {
        private readonly MoveStickConfig _config;

        public InputManagementInstaller(MoveStickConfig config)
        {
            _config = config;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_config);
            builder.Register<MoveInput>(Lifetime.Singleton).As<IMoveInput, ITickable>();
        }
    }
}
