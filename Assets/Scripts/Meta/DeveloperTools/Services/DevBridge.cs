#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
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
        private static MenuFlow _menu;
        private static Meta.ProgressSystem.Services.ProgressService _progress;
        private static readonly Dictionary<int, ulong> Hashes = new Dictionary<int, ulong>();

        public static void Bind(IPlatform platform, IPlaybackGate gate, IScreenLayout layout, IBattleDriver battle, IAdService ads,
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
                   $"health {hero.Health}{(hero.Alive ? "" : " dead")}, captures {simulation.Captures.Count}, gold {purse.Gold}, wood {purse.Wood}, " +
                   $"army {simulation.ArmyOf(Side.Player)} vs {simulation.ArmyOf(Side.Enemy)}, barracks {barracks}, " +
                   $"piles {simulation.Resources.Count}, bases {simulation.Bases[(int)Side.Player].Health} vs {simulation.Bases[(int)Side.Enemy].Health}" +
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
            var early30 = 0;
            for (var seed = 1; seed <= count; seed++)
            {
                var settings = template.Clone();
                settings.Seed = (uint)seed;
                settings.PlayerAutopilot = true;
                var simulation = new BattleSimulation(settings);
                var limit = maxSeconds * settings.TicksPerSecond;
                // the first second each army reached its limit (task 4.19: the economy is richer)
                var full = new[] { -1f, -1f, -1f };
                // the waves that left and the second of the first (task 4.35)
                var left = 0;
                var firstWave = -1f;
                while (simulation.Result == BattleResult.None && simulation.Tick < limit)
                {
                    var announced = simulation.WaveComing(out _, out _);
                    simulation.Step();
                    if (announced && !simulation.WaveComing(out _, out _) && simulation.Squads.Any(q => q.Role == SquadRole.Wave))
                    {
                        left++;
                        if (firstWave < 0f)
                            firstWave = simulation.Tick / (float)settings.TicksPerSecond;
                    }

                    foreach (var side in new[] { Side.Player, Side.Enemy })
                    {
                        if (full[(int)side] < 0f && simulation.ArmyOf(side) >= settings.ArmyMax)
                            full[(int)side] = simulation.Tick / (float)settings.TicksPerSecond;
                    }
                }

                var built = new int[3];
                var hired = new int[3];
                var died = new int[3];
                var early = new int[3];
                var back = new[] { -1, -1, -1 };
                foreach (var e in simulation.Events)
                {
                    if (e.Kind == EventKind.Built) built[(int)e.Side]++;
                    if (e.Kind == EventKind.Hired) hired[(int)e.Side]++;
                    if (e.Kind == EventKind.HeroDied) died[(int)e.Side]++;
                    if (e.Kind == EventKind.HeroRespawned) back[(int)e.Side] = e.Tick;
                    // a death within 5 s of coming back (task 4.23: the respawn must be safe)
                    if (e.Kind == EventKind.HeroDied && back[(int)e.Side] >= 0 && e.Tick - back[(int)e.Side] <= 5 * settings.TicksPerSecond) early[(int)e.Side]++;
                }

                // how soon the player's base fell after the last death of its hero (task 4.27: a mistake must not be the end)
                var lastDeath = -1;
                var waves = 0;
                foreach (var e in simulation.Events)
                {
                    if (e.Kind == EventKind.HeroDied && e.Side == Side.Player)
                        lastDeath = e.Tick;
                }

                var fell = !simulation.Bases[(int)Side.Player].Standing && lastDeath >= 0
                    ? $"{(simulation.Tick - lastDeath) / (float)settings.TicksPerSecond:0}s after the last death"
                    : "-";
                foreach (var squad in simulation.Squads)
                {
                    if (squad.Role == SquadRole.Wave)
                        waves++;
                }

                // the enemy's base broken in the first 30 s (task 4.28)
                if (!simulation.Bases[(int)Side.Enemy].Standing && simulation.Tick <= 30 * settings.TicksPerSecond)
                    early30++;

                lines.Add($"seed {seed}: {simulation.Tick / (float)settings.TicksPerSecond:0}s {simulation.Result}, base fell {fell}, squads {simulation.Squads.Count} ({waves} in a wave), waves {left} (first at {Seconds(firstWave)}), blue {simulation.Grid.Permille(Side.Player) / 10f:0}% red {simulation.Grid.Permille(Side.Enemy) / 10f:0}%, " +
                          $"bases {simulation.Bases[(int)Side.Player].Health}/{simulation.Bases[(int)Side.Enemy].Health}, built {built[1]}/{built[2]}, hired {hired[1]}/{hired[2]}, hero deaths {died[1]}/{died[2]} (within 5 s of coming back {early[1]}/{early[2]}), army {settings.ArmyMax} at {Seconds(full[1])}/{Seconds(full[2])}");
            }

            lines.Add($"the enemy's base broken in the first 30 s: {early30}/{count}");
            return string.Join(" | ", lines);
        }

        private static string Seconds(float seconds) => seconds < 0f ? "never" : seconds.ToString("0") + "s";

        /// <summary>The progress: level, crowns, steps of the upgrades, the step of the hint.</summary>
        public static string Progress()
        {
            var data = _progress.Data;
            return $"level {data.Level}, crowns {data.Crowns}, upgrades {string.Join(",", data.Upgrades)}, hint {data.HintStep}";
        }

        /// <summary>Sets the crowns, for the screens of the upgrades.</summary>
        public static string Crowns(int crowns)
        {
            _progress.Data.Crowns = crowns;
            _progress.Save();
            return Progress();
        }

        /// <summary>Starts anew: no crowns, no upgrades, the hint from its first step; saved.</summary>
        public static string ResetProgress()
        {
            _progress.Load(null);
            _progress.Save();
            return Progress();
        }

        /// <summary>Buys a step of an upgrade 0–3 (HeroPower, HeroBrush, ArmyPower, StartGold) if the crowns allow.</summary>
        public static string Buy(int upgrade)
        {
            _progress.Buy((Meta.ProgressSystem.Enums.Upgrade)upgrade);
            return Progress();
        }

        /// <summary>Sets the step of the hint of level 1, 0–4 (4: done), for its screenshots.</summary>
        public static string Hint(int step)
        {
            _progress.Data.HintStep = step;
            _progress.Save();
            return Progress();
        }

        /// <summary>Puts the hero of a side at a point in cells (fractions allowed), standing.</summary>
        public static string Teleport(string side, float x, float y)
        {
            _battle.Current.Simulation.TeleportHero(SideOf(side), Mathf.RoundToInt(x * BattleSettings.UnitsPerCell), Mathf.RoundToInt(y * BattleSettings.UnitsPerCell));
            return Battle();
        }

        /// <summary>Opens the pause as the HUD button would.</summary>
        public static string Pause()
        {
            _flow.OpenPause();
            return State();
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
            _battle.Current.Simulation.SetBaseHealth(SideOf(side), health);
            return Battle();
        }

        /// <summary>The same commander as the enemy's walks the player's hero; the keys and the stick are ignored meanwhile.</summary>
        public static string Autopilot(bool on)
        {
            _battle.Current.Simulation.Settings.PlayerAutopilot = on;
            return Battle();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Plays a map of Assets/ScriptableObjects/Maps by its name from a new battle at once (layer 5 spec 2.6); an empty name — the
        /// map of the test level again.
        /// </summary>
        public static string Map(string name)
        {
            var map = string.IsNullOrEmpty(name)
                ? null
                : UnityEditor.AssetDatabase.LoadAssetAtPath<Meta.BattleSystem.Configuration.BattleMapAsset>($"Assets/ScriptableObjects/Maps/{name}.asset");
            if (!string.IsNullOrEmpty(name) && map == null)
                return $"no map {name}";

            _debug.PlayMap(map);
            _flow.Begin();
            return Battle();
        }
#endif

        /// <summary>How many events of a kind the battle has had, by side: «Shielded» → «player N, enemy M».</summary>
        public static string Events(string kind)
        {
            var wanted = (EventKind)System.Enum.Parse(typeof(EventKind), kind);
            var player = 0;
            var enemy = 0;
            foreach (var e in _battle.Current.Simulation.Events)
            {
                if (e.Kind != wanted)
                    continue;
                if (e.Side == Side.Player)
                    player++;
                else if (e.Side == Side.Enemy)
                    enemy++;
            }

            return $"{kind}: player {player}, enemy {enemy}";
        }

        /// <summary>«Play» of the main menu: the test level on its own map.</summary>
        public static string Play()
        {
            _menu.Play();
            return Battle();
        }

        /// <summary>The main menu, as «Menu» of the pause does.</summary>
        public static string Menu()
        {
            _menu.ShowMenu();
            return State();
        }

        /// <summary>«Editor» of the main menu.</summary>
        public static string Editor()
        {
            _menu.OpenEditor();
            return State();
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

        /// <summary>
        /// Gives the player's army an order from the next tick (spec 3.8): Rally, Capture, Units, Barracks, Base; the same order
        /// again — the free attack. Free — straight back to the free attack.
        /// </summary>
        public static string Order(string order)
        {
            var simulation = _battle.Current.Simulation;
            var wanted = (ArmyOrder)System.Enum.Parse(typeof(ArmyOrder), order);
            if (wanted == ArmyOrder.Free)
                wanted = simulation.OrderOf(Side.Player);
            simulation.Enqueue(new BattleCommand(simulation.Tick + 1, Side.Player, wanted));
            return "order " + order + " from tick " + (simulation.Tick + 1);
        }

        /// <summary>Adds gold and wood to a side.</summary>
        public static string Give(string side, int gold, int wood)
        {
            _battle.Current.Simulation.Give(SideOf(side), gold, wood);
            return Battle();
        }

        /// <summary>Puts soldiers of a side around a cell; they go to attack at once.</summary>
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
                             $"barracks {site.Barracks} {site.Health}, progress {site.BuildProgress}; ");
            }

            return lines.ToString();
        }

        /// <summary>Paints a rectangle of cells, corners included, for a side: Player, Enemy or Neutral.</summary>
        public static string PaintRect(string side, int x0, int y0, int x1, int y1)
        {
            _battle.Current.Simulation.PaintRect(SideOf(side), x0, y0, x1, y1);
            return Battle();
        }

        /// <summary>Announces a wave of the enemy gathering at a cell (for a look at its sign); no squad leaves.</summary>
        public static string AnnounceWave(int x, int y)
        {
            _battle.Current.Simulation.AnnounceWave(x, y);
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
        /// <summary>The pause of the platform, as the SDK would send it: it opens the pause screen (unless a screenshot froze it).</summary>
        public static string SimulatePause(bool paused)
        {
            if (!paused)
                _debug.QuietFreeze = false;
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
            _debug.QuietFreeze = true;
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
            _debug.QuietFreeze = true;
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

        /// <summary>
        /// The next death of a unit (HeroDied, or SoldierDied with <paramref name="soldiers"/>) pauses the game this many seconds
        /// after it: the pieces and the dust (task 4.13) stay on the screen for a screenshot despite the slow bridge.
        /// </summary>
        public static string FreezeAfterDeath(float seconds, bool soldiers)
        {
            _debug.QuietFreeze = true;
            var runner = _battle.Current;
            var seen = runner.Simulation.Events.Count;
            void Check()
            {
                var events = runner.Simulation.Events;
                var died = false;
                for (var i = System.Math.Min(seen, events.Count); i < events.Count && !died; i++)
                    died = events[i].Kind == EventKind.HeroDied || (soldiers && events[i].Kind == EventKind.SoldierDied);
                seen = events.Count;
                if (!died)
                    return;

                runner.OnStep -= Check;
                UniTask.Delay(System.TimeSpan.FromSeconds(seconds), DelayType.Realtime)
                    .ContinueWith(() => ((EditorPlatform)_platform).SimulatePause(true)).Forget();
            }

            runner.OnStep += Check;
            return "waiting for a death";
        }

        /// <summary>The next blow of a side pauses the game this many seconds after it: the red flash of a hit (task 4.20) stays on the screen.</summary>
        public static string FreezeAfterStrike(string side, float seconds) => FreezeAfter(EventKind.Strike, side, seconds);

        /// <summary>
        /// The look of a block (task 4.33) at the player's hero and its first soldier at once, then the game pauses this many
        /// seconds later: a screenshot of the drawing, the rules untouched.
        /// </summary>
        public static string ShowBlock(float seconds)
        {
            var view = Object.FindFirstObjectByType<Meta.BattleSystem.Views.OwnLandView>();
            view.ShowBlock(-1);
            foreach (var soldier in _battle.Current.Simulation.Soldiers)
            {
                if (soldier.Side != Side.Player)
                    continue;

                view.ShowBlock(soldier.Id);
                break;
            }

            return FreezeIn(seconds);
        }

        /// <summary>The next blow a shield of own land takes (task 4.33): the game pauses this many seconds after it.</summary>
        public static string FreezeAfterBlock(float seconds) => FreezeAfter(EventKind.Shielded, "Player", seconds);

        private static string FreezeAfter(EventKind kind, string side, float seconds)
        {
            _debug.QuietFreeze = true;
            var runner = _battle.Current;
            var striker = SideOf(side);
            var seen = runner.Simulation.Events.Count;
            void Check()
            {
                var events = runner.Simulation.Events;
                var struck = false;
                for (var i = System.Math.Min(seen, events.Count); i < events.Count && !struck; i++)
                    struck = events[i].Kind == kind && events[i].Side == striker;
                seen = events.Count;
                if (!struck)
                    return;

                runner.OnStep -= Check;
                UniTask.Delay(System.TimeSpan.FromSeconds(seconds), DelayType.Realtime)
                    .ContinueWith(() => ((EditorPlatform)_platform).SimulatePause(true)).Forget();
            }

            runner.OnStep += Check;
            return "waiting for a blow of " + side;
        }
#endif
    }
}
#endif
