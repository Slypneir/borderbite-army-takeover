using System;
using Meta.FlowSystem.Services;
using Meta.FlowSystem.Views;
using VContainer;
using VContainer.Unity;

namespace Meta.FlowSystem.Installers
{
    /// <summary>
    /// The flow of the game: the boot, then straight into the battle, its results, the upgrades and the pause.
    /// </summary>
    public sealed class FlowSystemInstaller : IInstaller
    {
        private readonly ResultsView _results;
        private readonly UpgradesView _upgrades;
        private readonly PauseView _pause;

        public FlowSystemInstaller(ResultsView results, UpgradesView upgrades, PauseView pause)
        {
            _results = results;
            _upgrades = upgrades;
            _pause = pause;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_results);
            builder.RegisterInstance(_upgrades);
            builder.RegisterInstance(_pause);
            builder.Register<BattleFlow>(Lifetime.Singleton).AsSelf().As<ITickable, IDisposable>();
            builder.Register<BootFlow>(Lifetime.Singleton).As<IAsyncStartable>();
        }
    }
}
