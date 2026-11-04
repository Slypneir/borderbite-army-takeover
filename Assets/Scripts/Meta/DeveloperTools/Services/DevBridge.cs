#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using Core.PlatformSystem.Enums;
using Core.PlatformSystem.Interfaces;
using Core.ScreenSystem.Interfaces;
using Cysharp.Threading.Tasks;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using Meta.FlowSystem.Services;
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
        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        private static IPlatform _platform;
        private static IPlaybackGate _gate;
        private static IScreenLayout _layout;
        private static IBattleDriver _battle;
        private static IAdService _ads;
        private static BattleDebug _debug;
        private static BattleFlow _flow;
        private static readonly Dictionary<int, ulong> Hashes = new Dictionary<int, ulong>();

        public static void Bind(IPlatform platform, IPlaybackGate gate, IScreenLayout layout, IBattleDriver battle, IAdService ads,
            BattleDebug debug, BattleFlow flow)
        {
            _platform = platform;
            _gate = gate;
            _layout = layout;
            _battle = battle;
            _ads = ads;
            _debug = debug;
            _flow = flow;
        }

        public static string State() =>
            $"language {Loc.Language}, shape {_layout?.Shape}, frozen {_gate?.Frozen}, muted {_gate?.Muted}, timeScale {Time.timeScale}, " +
            $"audioPaused {AudioListener.pause}, tick {_battle?.Current?.Simulation.Tick}, frameRate {Application.targetFrameRate}, screen {Screen.width}x{Screen.height}";

        public static string SetLanguage(string language)
        {
            Loc.Set(language);
            return Loc.Language;
        }

        /// <summary>The battle now: tick, result, shares in permille, the hero in cells, the count of enclosures.</summary>
        public static string Battle()
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation == null)
                return "no battle";

            var hero = simulation.Heroes[0];
            var purse = simulation.SideOf(Side.Player);
            const float unit = BattleSettings.UnitsPerCell;
            var barracks = 0;
            foreach (var site in simulation.Sites)
                barracks += site.Built ? 1 : 0;

            return $"tick {simulation.Tick}, result {simulation.Result}, player {simulation.Grid.Permille(Side.Player)} permille, " +
                   $"enemy {simulation.Grid.Permille(Side.Enemy)} permille, hero {(hero.X / unit).ToString("0.00", Inv)};{(hero.Y / unit).ToString("0.00", Inv)} cells, " +
                   $"health {hero.Health / 10}{(hero.Alive ? "" : " dead")}, captures {simulation.Captures.Count}, gold {purse.Gold}, wood {purse.Wood}, " +
                   $"army {simulation.ArmyOf(Side.Player)} vs {simulation.ArmyOf(Side.Enemy)}, barracks {barracks}, " +
                   $"piles {simulation.Resources.Count}, bases {simulation.Bases[(int)Side.Player].Health / 10} vs {simulation.Bases[(int)Side.Enemy].Health / 10}" +
                   Enemy(simulation);
        }

        private static string Enemy(BattleSimulation simulation)
        {
            foreach (var hero in simulation.Heroes)
            {
                if (hero.Side != Side.Enemy)
                    continue;

                const float unit = BattleSettings.UnitsPerCell;
                var purse = simulation.SideOf(Side.Enemy);
                return $", enemy L{_debug?.EnemyLevel} p{simulation.EnemyPriority} at {(hero.X / unit).ToString("0.0", Inv)};{(hero.Y / unit).ToString("0.0", Inv)}" +
                       $"{(hero.Alive ? "" : " dead")}, gold {purse.Gold}, wood {purse.Wood}";
            }

            return ", no enemy hero";
        }

        /// <summary>
        /// Whole battles of the test level at once, off screen (spec 3.4 self-check): the enemy commander against the same
        /// commander on the player's side, seeds 1.., each up to <paramref name="maxSeconds"/>. Gives the time, the result and the
        /// shares of each, and what was built and hired.
        /// </summary>
        public static string SimulateLevels(int count, int maxSeconds)
        {
            var template = _battle.Current.Simulation.Settings;
            var lines = new List<string>();
            for (var seed = 1; seed <= count; seed++)
            {
                var settings = template.Clone();
                settings.Seed = (uint)seed;
                settings.PlayerAutopilot = true;
                var simulation = new BattleSimulation(settings);
                var limit = maxSeconds * settings.TicksPerSecond;
                while (simulation.Result == BattleResult.None && simulation.Tick < limit)
                    simulation.Step();

                var built = new int[3];
                var hired = new int[3];
                var died = new int[3];
                foreach (var e in simulation.Events)
                {
                    if (e.Kind == EventKind.Built) built[(int)e.Side]++;
                    if (e.Kind == EventKind.Hired) hired[(int)e.Side]++;
                    if (e.Kind == EventKind.HeroDied) died[(int)e.Side]++;
                }

                lines.Add($"seed {seed}: {simulation.Tick / (float)settings.TicksPerSecond:0}s {simulation.Result}, blue {simulation.Grid.Permille(Side.Player) / 10f:0}% red {simulation.Grid.Permille(Side.Enemy) / 10f:0}%, " +
                          $"bases {simulation.Bases[(int)Side.Player].Health / 10}/{simulation.Bases[(int)Side.Enemy].Health / 10}, built {built[1]}/{built[2]}, hired {hired[1]}/{hired[2]}, hero deaths {died[1]}/{died[2]}");
            }

            return string.Join(" | ", lines);
        }

        /// <summary>The enemy of a level of GDD 5.9 at once, without starting anew (spec 2.7); a new battle keeps it.</summary>
        public static string EnemyLevel(int level)
        {
            _debug.SetEnemyLevel(level);
            return Battle();
        }

        /// <summary>The health of a base in points; 0 brings it down (spec 3.1 self-check).</summary>
        public static string BaseHealth(string side, int health)
        {
            _battle.Current.Simulation.SetBaseHealth(SideOf(side), health * 10);
            return Battle();
        }

        /// <summary>The same commander as the enemy's walks the player's hero; the keys and the stick are ignored meanwhile.</summary>
        public static string Autopilot(bool on)
        {
            _battle.Current.Simulation.Settings.PlayerAutopilot = on;
            return Battle();
        }

        /// <summary>Starts the level anew, as «Again» does.</summary>
        public static string Restart()
        {
            _flow.Begin();
            return Battle();
        }

        /// <summary>The hero of the player walks this way from the next tick, (0, 0) stops it; the keys or the stick override it.</summary>
        public static string Walk(int x, int y)
        {
            var simulation = _battle.Current.Simulation;
            simulation.Enqueue(new BattleCommand(simulation.Tick + 1, Side.Player, x, y));
            return Battle();
        }

        /// <summary>The hero of the player walks this way for exactly this many ticks, then stands: exact paths despite the bridge.</summary>
        public static string WalkTicks(int x, int y, int ticks)
        {
            var simulation = _battle.Current.Simulation;
            simulation.Enqueue(new BattleCommand(simulation.Tick + 1, Side.Player, x, y));
            simulation.Enqueue(new BattleCommand(simulation.Tick + 1 + ticks, Side.Player, 0, 0));
            return Battle();
        }

        /// <summary>The hero walks legs one after another, "x,y,ticks;x,y,ticks;…", then stands.</summary>
        public static string WalkPath(string legs)
        {
            var simulation = _battle.Current.Simulation;
            var tick = simulation.Tick + 1;
            foreach (var leg in legs.Split(';'))
            {
                var parts = leg.Split(',');
                simulation.Enqueue(new BattleCommand(tick, Side.Player, int.Parse(parts[0]), int.Parse(parts[1])));
                tick += int.Parse(parts[2]);
            }

            simulation.Enqueue(new BattleCommand(tick, Side.Player, 0, 0));
            return Battle() + ", path ends at tick " + tick;
        }

        /// <summary>Adds gold and wood to a side.</summary>
        public static string Give(string side, int gold, int wood)
        {
            _battle.Current.Simulation.Give(SideOf(side), gold, wood);
            return Battle();
        }

        /// <summary>Puts soldiers of a side around a cell; without a hero they keep to it.</summary>
        public static string Soldiers(string side, int count, int x, int y)
        {
            const int unit = BattleSettings.UnitsPerCell;
            _battle.Current.Simulation.AddSoldiers(SideOf(side), count, x * unit + unit / 2, y * unit + unit / 2);
            return Battle();
        }

        /// <summary>Builds a barracks of a side on a place (0–5) at once, free.</summary>
        public static string Build(string side, int site)
        {
            _battle.Current.Simulation.BuildBarracks(site, SideOf(side));
            return Battle();
        }

        /// <summary>The places to build: centre cell, owner by colour, barracks and its health, progress of building.</summary>
        public static string Sites()
        {
            var simulation = _battle.Current.Simulation;
            var lines = new System.Text.StringBuilder();
            for (var i = 0; i < simulation.Sites.Count; i++)
            {
                var site = simulation.Sites[i];
                lines.Append($"{i}: {site.CenterX};{site.CenterY} owner {simulation.Grid.At(site.CenterX, site.CenterY)}, " +
                             $"barracks {site.Barracks} {site.Health / 10}, progress {site.BuildProgress}; ");
            }

            return lines.ToString();
        }

        /// <summary>Paints a rectangle of cells, corners included, for a side: Player, Enemy or Neutral.</summary>
        public static string PaintRect(string side, int x0, int y0, int x1, int y1)
        {
            _battle.Current.Simulation.PaintRect(SideOf(side), x0, y0, x1, y1);
            return Battle();
        }

        /// <summary>Gives a side cells until it has this percent of the map.</summary>
        public static string FillShare(string side, int percent)
        {
            _battle.Current.Simulation.FillShare(SideOf(side), percent * 10);
            return Battle();
        }

        /// <summary>Ends the battle at once: Victory or Defeat.</summary>
        public static string ForceResult(string result)
        {
            _battle.Current.Simulation.ForceResult((BattleResult)System.Enum.Parse(typeof(BattleResult), result));
            return Battle();
        }

        /// <summary>The grid of cells and the line of ticks and frames, as F1.</summary>
        public static string ShowDebug(bool shown)
        {
            _debug.ShowDebug = shown;
            return "debug " + shown;
        }

        /// <summary>The camera shows the whole map from straight above, or follows the hero again.</summary>
        public static string Overview(bool shown)
        {
            _debug.Overview = shown;
            return "overview " + shown;
        }

        /// <summary>
        /// Starts a battle where the hero walks a fixed square spiral at a frame rate; <see cref="BattleCheckResult"/> gives the
        /// hash at tick 300. The stick and the keys must stay untouched meanwhile.
        /// </summary>
        public static string BattleCheckStart(int frameRate)
        {
            Application.targetFrameRate = frameRate;
            QualitySettings.vSyncCount = 0;
            Hashes.Clear();
            var runner = _battle.StartBattle(12345);
            int[] xs = { 1000, 0, -1000, 0 };
            int[] ys = { 0, 1000, 0, -1000 };
            for (var leg = 0; leg * 15 + 1 < CheckTick; leg++)
                runner.Simulation.Enqueue(new BattleCommand(leg * 15 + 1, Side.Player, xs[leg % 4] + leg * 20, ys[leg % 4] + 300));
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

        private static Side SideOf(string side) => (Side)System.Enum.Parse(typeof(Side), side);

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

        /// <summary>Pauses the game as the platform would, this many real seconds from now: a moment kept for a screenshot.</summary>
        public static string FreezeIn(float seconds)
        {
            UniTask.Delay(System.TimeSpan.FromSeconds(seconds), DelayType.Realtime)
                .ContinueWith(() => ((EditorPlatform)_platform).SimulatePause(true)).Forget();
            return "pause in " + seconds;
        }

        /// <summary>
        /// The next enclosure pauses the game this many seconds after it, as the platform would: the wave and «+N%» stay on the
        /// screen for a screenshot despite the slow bridge. SimulatePause(false) goes on.
        /// </summary>
        public static string FreezeAfterCapture(float seconds)
        {
            var runner = _battle.Current;
            var seen = runner.Simulation.Captures.Count;
            void Check()
            {
                if (runner.Simulation.Captures.Count == seen)
                    return;

                runner.OnStep -= Check;
                UniTask.Delay(System.TimeSpan.FromSeconds(seconds), DelayType.Realtime)
                    .ContinueWith(() => ((EditorPlatform)_platform).SimulatePause(true)).Forget();
            }

            runner.OnStep += Check;
            return "waiting for a capture";
        }
#endif
    }
}
#endif
