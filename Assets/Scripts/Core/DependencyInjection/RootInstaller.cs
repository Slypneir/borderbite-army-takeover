using Core.AudioManagement.Configuration;
using Core.AudioManagement.Installers;
using Core.InputManagement.Configuration;
using Core.InputManagement.Installers;
using Core.PlatformSystem.Configuration;
using Core.PlatformSystem.Installers;
using Core.ScreenSystem.Configuration;
using Core.ScreenSystem.Installers;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Installers;
using Meta.BattleSystem.Views;
using Meta.DeveloperTools.Installers;
using Meta.FlowSystem.Installers;
using Meta.FlowSystem.Views;
using Meta.MapEditorSystem.Installers;
using Meta.MapEditorSystem.Views;
using Meta.ProgressSystem.Configuration;
using Meta.ProgressSystem.Installers;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.DependencyInjection
{
    /// <summary>
    /// The single scene-level <see cref="LifetimeScope"/>. It owns the scene and asset references and composes the application
    /// container from per-system <see cref="IInstaller"/>s. Views under the canvas are injected through
    /// <c>autoInjectGameObjects</c>.
    /// To add a system: create an <c>IInstaller</c> next to it and add one line below.
    /// A build puts the ad policy of its platform into <see cref="adPolicyConfig"/> (Editor/AdPolicyInjector).
    /// </summary>
    public sealed class RootInstaller : LifetimeScope
    {
        [SerializeField] private AdPolicyConfig adPolicyConfig;
        [SerializeField] private BattleConfig battleConfig;
        [SerializeField] private BattleViewConfig battleViewConfig;
        [SerializeField] private ScreenLayoutConfig screenLayoutConfig;
        [SerializeField] private MoveStickConfig moveStickConfig;
        [SerializeField] private SoundVolumes soundVolumes;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private BattleWorld battleWorld;
        [SerializeField] private ResultsView resultsView;
        [SerializeField] private UpgradesView upgradesView;
        [SerializeField] private PauseView pauseView;
        [SerializeField] private MainMenuView mainMenuView;
        [SerializeField] private MapEditorView mapEditorView;
        [SerializeField] private ProgressConfig progressConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            new PlatformSystemInstaller(adPolicyConfig).Install(builder);
            new ScreenSystemInstaller(screenLayoutConfig, mainCamera).Install(builder);
            new InputManagementInstaller(moveStickConfig).Install(builder);
            new AudioManagementInstaller(soundVolumes).Install(builder);
            new BattleSystemInstaller(battleConfig, battleViewConfig, battleWorld).Install(builder);
            new ProgressSystemInstaller(progressConfig).Install(builder);
            new MapEditorSystemInstaller(mapEditorView).Install(builder);
            new FlowSystemInstaller(resultsView, upgradesView, pauseView, mainMenuView).Install(builder);
            new DeveloperToolsInstaller().Install(builder);
        }
    }
}
