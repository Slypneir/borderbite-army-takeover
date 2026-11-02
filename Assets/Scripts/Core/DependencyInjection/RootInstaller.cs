using Core.PlatformSystem.Configuration;
using Core.PlatformSystem.Installers;
using Core.ScreenSystem.Configuration;
using Core.ScreenSystem.Installers;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Installers;
using Meta.DeveloperTools.Installers;
using Meta.FlowSystem.Installers;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.DependencyInjection
{
    /// <summary>
    /// The single scene-level <see cref="LifetimeScope"/>. It owns the scene and asset references and composes the application
    /// container from per-system <see cref="IInstaller"/>s.
    /// To add a system: create an <c>IInstaller</c> next to it and add one line below.
    /// A build puts the ad policy of its platform into <see cref="adPolicyConfig"/> (Editor/AdPolicyInjector).
    /// </summary>
    public sealed class RootInstaller : LifetimeScope
    {
        [SerializeField] private AdPolicyConfig adPolicyConfig;
        [SerializeField] private BattleConfig battleConfig;
        [SerializeField] private ScreenLayoutConfig screenLayoutConfig;
        [SerializeField] private Camera mainCamera;

        protected override void Configure(IContainerBuilder builder)
        {
            new PlatformSystemInstaller(adPolicyConfig).Install(builder);
            new ScreenSystemInstaller(screenLayoutConfig, mainCamera).Install(builder);
            new BattleSystemInstaller(battleConfig).Install(builder);
            new FlowSystemInstaller().Install(builder);
            new DeveloperToolsInstaller().Install(builder);
        }
    }
}
