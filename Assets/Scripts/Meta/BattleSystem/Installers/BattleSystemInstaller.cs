using System;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using Meta.BattleSystem.Views;
using VContainer;
using VContainer.Unity;

namespace Meta.BattleSystem.Installers
{
    /// <summary>
    /// The battle on the frames of the engine and its drawing; the battle itself lives in the engine-free assembly
    /// Core.BattleSystem. The views on the canvas are injected through the auto-inject list of the root installer.
    /// </summary>
    public sealed class BattleSystemInstaller : IInstaller
    {
        private readonly BattleConfig _config;
        private readonly BattleViewConfig _viewConfig;
        private readonly BattleWorld _world;

        public BattleSystemInstaller(BattleConfig config, BattleViewConfig viewConfig, BattleWorld world)
        {
            _config = config;
            _viewConfig = viewConfig;
            _world = world;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_config);
            builder.RegisterInstance(_viewConfig);
            builder.RegisterInstance(_world);
            builder.Register<BattleDebug>(Lifetime.Singleton);
            builder.Register<BattleDriver>(Lifetime.Singleton).As<IBattleDriver, ITickable>();
            builder.Register<ViewMaterials>(Lifetime.Singleton).AsSelf().As<IDisposable>();
            builder.Register<EffectsView>(Lifetime.Singleton).AsSelf().As<ILateTickable>();
            builder.Register<MapView>(Lifetime.Singleton).As<ILateTickable, IDisposable>();
            builder.Register<HeroView>(Lifetime.Singleton).AsSelf().As<ILateTickable, IDisposable>();
            builder.Register<SoldiersView>(Lifetime.Singleton).As<ILateTickable>();
            builder.Register<ResourcesView>(Lifetime.Singleton).As<ILateTickable>();
            builder.Register<SitesView>(Lifetime.Singleton).As<ILateTickable>();
            builder.Register<CameraRig>(Lifetime.Singleton).As<ILateTickable>();
            builder.Register<BattleSounds>(Lifetime.Singleton).As<ILateTickable, IDisposable>();
        }
    }
}
