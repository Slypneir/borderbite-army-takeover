using System;
using Meta.FlowSystem.Services;
using Meta.FlowSystem.Views;
using VContainer;
using VContainer.Unity;

namespace Meta.FlowSystem.Installers
{
    /// <summary>
    /// The flow of the game: the boot, the main menu and the map editor, the battle, its results, the upgrades and the pause.
    /// </summary>
    public sealed class FlowSystemInstaller : IInstaller
    {
        private readonly ResultsView _results;
        private readonly UpgradesView _upgrades;
        private readonly PauseView _pause;
        private readonly MainMenuView _menu;

        public FlowSystemInstaller(ResultsView results, UpgradesView upgrades, PauseView pause, MainMenuView menu)
        {
            _results = results;
            _upgrades = upgrades;
            _pause = pause;
            _menu = menu;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_results);
            builder.RegisterInstance(_upgrades);
            builder.RegisterInstance(_pause);
            builder.RegisterInstance(_menu);
            builder.Register<MenuFlow>(Lifetime.Singleton).AsSelf().As<IDisposable>();
            builder.Register<BattleFlow>(Lifetime.Singleton).AsSelf().As<ITickable, IDisposable>();
            builder.Register<BootFlow>(Lifetime.Singleton).As<IAsyncStartable>();
        }
    }
}
