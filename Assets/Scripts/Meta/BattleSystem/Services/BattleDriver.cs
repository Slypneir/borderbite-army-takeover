using Core.BattleSystem.Services;
using Core.PlatformSystem.Interfaces;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using UnityEngine;
using VContainer.Unity;

namespace Meta.BattleSystem.Services
{
    /// <summary>Feeds the real time of a frame to the battle; the frame rate reaches nothing beyond the clock of the runner.</summary>
    public sealed class BattleDriver : IBattleDriver, ITickable
    {
        private const double MicrosecondsPerSecond = 1000000d;

        private readonly BattleConfig _config;
        private readonly IPlaybackGate _gate;
        private readonly IPlatform _platform;

        public BattleDriver(BattleConfig config, IPlaybackGate gate, IPlatform platform)
        {
            _config = config;
            _gate = gate;
            _platform = platform;
        }

        public BattleRunner Current { get; private set; }

        public BattleRunner StartBattle(uint seed)
        {
            Current = new BattleRunner(_config.ToSettings(seed), _config.MaxStepsPerFrame);
            _platform.SignalGameplay(true);
            Debug.Log($"[BattleDriver] battle started, seed {seed}");
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
            if (Current != null && !_gate.Frozen)
                Current.Advance((long)(Time.unscaledDeltaTime * MicrosecondsPerSecond));
        }
    }
}
