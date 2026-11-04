#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Core.PlatformSystem.Interfaces;
using Core.ScreenSystem.Interfaces;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using Meta.FlowSystem.Services;
using VContainer.Unity;

namespace Meta.DeveloperTools.Services
{
    /// <summary>Hands the services of the container to the static <see cref="DevBridge"/>.</summary>
    public sealed class DevTools : IStartable
    {
        private readonly IPlatform _platform;
        private readonly IPlaybackGate _gate;
        private readonly IScreenLayout _layout;
        private readonly IBattleDriver _battle;
        private readonly IAdService _ads;
        private readonly BattleDebug _debug;
        private readonly BattleFlow _flow;
        private readonly MenuFlow _menu;
        private readonly Meta.ProgressSystem.Services.ProgressService _progress;

        public DevTools(IPlatform platform, IPlaybackGate gate, IScreenLayout layout, IBattleDriver battle, IAdService ads,
            BattleDebug debug, BattleFlow flow, MenuFlow menu, Meta.ProgressSystem.Services.ProgressService progress)
        {
            _menu = menu;
            _progress = progress;
            _platform = platform;
            _gate = gate;
            _layout = layout;
            _battle = battle;
            _ads = ads;
            _debug = debug;
            _flow = flow;
        }

        public void Start() => DevBridge.Bind(_platform, _gate, _layout, _battle, _ads, _debug, _flow, _menu, _progress);
    }
}
#endif
