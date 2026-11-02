using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using VContainer;
using VContainer.Unity;

namespace Meta.BattleSystem.Installers
{
    /// <summary>
    /// The battle on the frames of the engine; the battle itself lives in the engine-free assembly Core.BattleSystem.
    /// </summary>
    public sealed class BattleSystemInstaller : IInstaller
    {
        private readonly BattleConfig _config;

        public BattleSystemInstaller(BattleConfig config)
        {
            _config = config;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_config);
            builder.Register<BattleDriver>(Lifetime.Singleton).As<IBattleDriver, ITickable>();
        }
    }
}
