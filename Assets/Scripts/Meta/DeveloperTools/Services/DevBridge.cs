#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using Core.PlatformSystem.Enums;
using Core.PlatformSystem.Interfaces;
using Core.ScreenSystem.Interfaces;
using Cysharp.Threading.Tasks;
using Meta.BattleSystem.Interfaces;
using Meta.LocalizationSystem.Services;
using UnityEngine;
#if UNITY_EDITOR
using Core.PlatformSystem.Services;
#endif

namespace Meta.DeveloperTools.Services
{
    /// <summary>
    /// Static commands of the developer for the editor bridge and for tests in the browser. Not in a build for players.
    /// </summary>
    public static class DevBridge
    {
        private const int CheckTick = 300;

        private static IPlatform _platform;
        private static IPlaybackGate _gate;
        private static IScreenLayout _layout;
        private static IBattleDriver _battle;
        private static IAdService _ads;
        private static readonly Dictionary<int, ulong> Hashes = new Dictionary<int, ulong>();

        public static void Bind(IPlatform platform, IPlaybackGate gate, IScreenLayout layout, IBattleDriver battle, IAdService ads)
        {
            _platform = platform;
            _gate = gate;
            _layout = layout;
            _battle = battle;
            _ads = ads;
        }

        public static string State() =>
            $"language {Loc.Language}, shape {_layout?.Shape}, frozen {_gate?.Frozen}, muted {_gate?.Muted}, timeScale {Time.timeScale}, " +
            $"audioPaused {AudioListener.pause}, tick {_battle?.Current?.Simulation.Tick}, frameRate {Application.targetFrameRate}, screen {Screen.width}x{Screen.height}";

        public static string SetLanguage(string language)
        {
            Loc.Set(language);
            return Loc.Language;
        }

        /// <summary>Starts a battle with fixed commands at a frame rate; <see cref="BattleCheckResult"/> gives the hash at tick 300.</summary>
        public static string BattleCheckStart(int frameRate)
        {
            Application.targetFrameRate = frameRate;
            QualitySettings.vSyncCount = 0;
            Hashes.Clear();
            var runner = _battle.StartBattle(12345);
            for (var tick = 10; tick < CheckTick; tick += 10)
                runner.Simulation.Enqueue(new BattleCommand(tick, tick % 3, tick / 10 % 12, (tick / 10 + 5) % 12));
            runner.OnStep += () => Remember(runner);
            return "started at " + frameRate;
        }

        public static string BattleCheckResult()
        {
            if (!Hashes.TryGetValue(CheckTick, out var hash))
                return "tick " + _battle.Current?.Simulation.Tick + " of " + CheckTick;

            _battle.StopBattle();
            return $"{CheckTick}:{hash:X16} real fps {1f / Time.unscaledDeltaTime:0}";
        }

        public static string Interstitial(string placement)
        {
            var kind = (AdPlacement)System.Enum.Parse(typeof(AdPlacement), placement);
            _ads.TryShowInterstitialAsync(kind).ContinueWith(shown => Debug.Log($"[DevBridge] interstitial {placement}: {shown}")).Forget();
            return "asked";
        }

        private static void Remember(BattleRunner runner)
        {
            if (runner.Simulation.Tick == CheckTick)
                Hashes[CheckTick] = runner.Simulation.ComputeHash();
        }

#if UNITY_EDITOR
        public static string SimulatePause(bool paused)
        {
            ((EditorPlatform)_platform).SimulatePause(paused);
            return State();
        }

        public static string SimulateAudio(bool enabled)
        {
            ((EditorPlatform)_platform).SimulateAudio(enabled);
            return State();
        }

        public static string SimulateFocus(bool focused)
        {
            ((EditorPlatform)_platform).SimulateFocus(focused);
            return State();
        }
#endif
    }
}
#endif
