using System;
using Meta.FlowSystem.Services;
using Meta.FlowSystem.Views;
using VContainer;
using VContainer.Unity;

namespace Meta.FlowSystem.Installers
{
    /// <summary>
    /// The flow of the game: the boot, then straight into the battle and its results.
    /// </summary>
    public sealed class FlowSystemInstaller : IInstaller
    {
        private readonly ResultsView _results;

        public FlowSystemInstaller(ResultsView results)
        {
            _results = results;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_results);
            builder.Register<BattleFlow>(Lifetime.Singleton).AsSelf().As<IDisposable>();
            builder.Register<BootFlow>(Lifetime.Singleton).As<IAsyncStartable>();
        }
    }
}
