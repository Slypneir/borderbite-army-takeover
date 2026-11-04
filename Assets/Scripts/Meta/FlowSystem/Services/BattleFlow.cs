using System;
using System.Threading;
using Core.BattleSystem.Enums;
using Core.InputManagement.Interfaces;
using Core.PlatformSystem.Interfaces;
using Cysharp.Threading.Tasks;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using Meta.FlowSystem.Views;
using Meta.ProgressSystem.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Meta.FlowSystem.Services
{
    /// <summary>
    /// The loop of a level (stage 1 spec 5.2, 5.4): the main menu or the map editor starts it (<see cref="MenuFlow"/>); the end stops the
    /// battle, gives the crowns and saves, and 1 s later shows the results; their button leads to the upgrades, whose button
    /// starts the level again (one level for now, task 4.1 waits). The upgrades act from that start. A lost focus or a pause of
    /// the platform, Esc or the pause button of the HUD open the pause, which holds the game until «Go on»; «Again» there starts
    /// the level anew without crowns, or giving up would pay, and «Menu» leaves the battle (<see cref="Left"/>). The input is off
    /// while a screen is shown.
    /// </summary>
    public sealed class BattleFlow : ITickable, IDisposable
    {
        private const float ResultsDelaySeconds = 1f;
        private const uint Seed = 1;

        private readonly IBattleDriver _battle;
        private readonly IMoveInput _input;
        private readonly IPlaybackGate _gate;
        private readonly ResultsView _results;
        private readonly UpgradesView _upgrades;
        private readonly PauseView _pause;
        private readonly ProgressService _progress;
        private readonly BattleDebug _debug;
        private readonly BattleScreen _screen;
        private readonly CancellationTokenSource _life = new CancellationTokenSource();
        private bool _finished;

        /// <summary>A battle started: the menu and the editor step aside.</summary>
        public event Action Began;

        /// <summary>The pause was left by «Menu»: the battle is to stop and the menu or the editor to show.</summary>
        public event Action Left;

        /// <summary>The words of the button that leaves a battle: «Menu» or «Editor».</summary>
        public string LeaveKey { get; set; } = "pause.menu";

        public BattleFlow(IBattleDriver battle, IMoveInput input, IPlaybackGate gate, ResultsView results, UpgradesView upgrades,
            PauseView pause, ProgressService progress, BattleDebug debug, BattleScreen screen)
        {
            _screen = screen;
            _battle = battle;
            _input = input;
            _gate = gate;
            _results = results;
            _upgrades = upgrades;
            _pause = pause;
            _progress = progress;
            _debug = debug;
            _battle.OnFinished += Finish;
            _gate.OnChanged += HandleGate;
        }

        /// <summary>A battle runs and no screen is over it: the pause may open.</summary>
        public bool Playing => _battle.Current != null && !_finished && !_results.Shown && !_upgrades.Shown;

        public void Begin()
        {
            HideScreens();
            Began?.Invoke();
            _screen.Covered = false;
            _screen.Between = false;
            _finished = false;
            _input.Enabled = true;
            _battle.StartBattle(Seed, _progress.Apply);
        }

        /// <summary>The results, the upgrades and the pause hide, and the game is not held by the player.</summary>
        public void HideScreens()
        {
            _results.Hide();
            _upgrades.Hide();
            _pause.Hide();
            _gate.SetPlayerPaused(false);
            _finished = false;
        }

        public void OpenPause()
        {
            if (!Playing || _pause.Shown)
                return;

            _gate.SetPlayerPaused(true);
            _screen.Covered = true;
            _pause.Show(Resume, Begin, () => Left?.Invoke(), LeaveKey);
        }

        public void Tick()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (_pause.Shown)
                Resume();
            else
                OpenPause();
        }

        public void Dispose()
        {
            _battle.OnFinished -= Finish;
            _gate.OnChanged -= HandleGate;
            _life.Cancel();
            _life.Dispose();
        }

        private void Resume()
        {
            _pause.Hide();
            _gate.SetPlayerPaused(false);
            _screen.Covered = false;
        }

        /// <summary>The platform or a lost focus froze the game: the pause opens and stays when they let it go.</summary>
        private void HandleGate()
        {
            if (_gate.Frozen && !_gate.PlayerPaused && !_debug.QuietFreeze)
                OpenPause();
        }

        private void Finish(BattleResult result)
        {
            _finished = true;
            _input.Enabled = false;
            _pause.Hide();
            _gate.SetPlayerPaused(false);
            var simulation = _battle.Current.Simulation;
            var baseFell = !simulation.Bases[(int)Side.Player].Standing || !simulation.Bases[(int)Side.Enemy].Standing;
            var share = Mathf.RoundToInt(simulation.Grid.Permille(Side.Player) / 10f);
            var winner = Mathf.RoundToInt(simulation.Grid.Permille(result == BattleResult.Victory ? Side.Player : Side.Enemy) / 10f);
            var crowns = _progress.Award(result);
            ShowResultsAsync(result, baseFell, share, winner, crowns, _life.Token).Forget();
        }

        private async UniTaskVoid ShowResultsAsync(BattleResult result, bool baseFell, int share, int winner, int crowns, CancellationToken ct)
        {
            // game time: a pause of the platform holds the results back too
            await UniTask.Delay(TimeSpan.FromSeconds(ResultsDelaySeconds), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
            Debug.Log($"[BattleFlow] results: {result}, +{crowns} crowns, {_progress.Data.Crowns} in all");
            var victory = result == BattleResult.Victory;
            _screen.Covered = true;
            _screen.Between = true;
            _results.Show(result, baseFell, share, winner, crowns, () =>
            {
                _results.Hide();
                _upgrades.Show(victory, Begin);
            });
        }
    }
}
