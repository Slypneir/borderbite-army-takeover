using System;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using Core.InputManagement.Interfaces;
using Core.PlatformSystem.Interfaces;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using UnityEngine;
using VContainer.Unity;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// Feeds the real time of a frame to the battle; the frame rate reaches nothing beyond the clock of the runner. A change of the
    /// direction of the player becomes a command for the next tick: up on the screen is +y of the map.
    /// </summary>
    public sealed class BattleDriver : IBattleDriver, ITickable
    {
        private const double MicrosecondsPerSecond = 1000000d;

        private readonly BattleConfig _config;
        private readonly IPlaybackGate _gate;
        private readonly IPlatform _platform;
        private readonly IMoveInput _input;
        private Vector2Int _sent;
        private bool _finished;

        public BattleDriver(BattleConfig config, IPlaybackGate gate, IPlatform platform, IMoveInput input)
        {
            _config = config;
            _gate = gate;
            _platform = platform;
            _input = input;
        }

        public BattleRunner Current { get; private set; }

        public event Action<BattleRunner> OnStarted;

        public event Action<BattleResult> OnFinished;

        public BattleRunner StartBattle(uint seed, Action<BattleSettings> prepare = null)
        {
            var settings = _config.ToSettings(seed);
            prepare?.Invoke(settings);
            Current = new BattleRunner(settings, _config.MaxStepsPerFrame);
            _sent = Vector2Int.zero;
            _finished = false;
            _platform.SignalGameplay(true);
            Debug.Log($"[BattleDriver] battle started, seed {seed}");
            OnStarted?.Invoke(Current);
            return Current;
        }

        public void StopBattle()
        {
            if (Current == null)
                return;

            Current = null;
            _platform.SignalGameplay(false);
        }

        public void Tick()
        {
            if (Current == null || _gate.Frozen)
                return;

            SendDirection();
            Current.Advance((long)(Time.unscaledDeltaTime * MicrosecondsPerSecond));

            var result = Current.Simulation.Result;
            if (result == BattleResult.None || _finished)
                return;

            _finished = true;
            _platform.SignalGameplay(false);
            Debug.Log($"[BattleDriver] {result} at tick {Current.Simulation.Tick}");
            OnFinished?.Invoke(result);
        }

        public void Order(ArmyOrder order)
        {
            var simulation = Current?.Simulation;
            simulation?.Enqueue(new BattleCommand(simulation.Tick + 1, Side.Player, order));
        }

        private void SendDirection()
        {
            var direction = _input.Direction;
            var quantized = new Vector2Int(
                Mathf.RoundToInt(direction.x * BattleCommand.MaxAxis),
                Mathf.RoundToInt(direction.y * BattleCommand.MaxAxis));
            if (quantized == _sent)
                return;

            _sent = quantized;
            var simulation = Current.Simulation;
            simulation.Enqueue(new BattleCommand(simulation.Tick + 1, Side.Player, quantized.x, quantized.y));
        }
    }
}
