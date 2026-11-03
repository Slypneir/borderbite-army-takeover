using System;
using Core.AudioManagement.Enums;
using Core.AudioManagement.Interfaces;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Utils;
using UnityEngine;
using VContainer.Unity;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// The sounds of the battle (stage 1 spec 6), all through the table of volumes: a capture, a pickup, a building, a hire, a
    /// blow, a shot, the end. The same sound plays at most once in <see cref="MinGapSeconds"/>: thirty soldiers striking at once
    /// are one blow, not a roar.
    /// </summary>
    public sealed class BattleSounds : ILateTickable, IDisposable
    {
        private const float MinGapSeconds = 0.08f;

        private readonly IBattleDriver _battle;
        private readonly ISfxPlayer _sfx;
        private readonly float[] _last = new float[Enum.GetValues(typeof(Sfx)).Length];
        private BattleSimulation _simulation;
        private EventCursor _events;
        private int _captures;

        public BattleSounds(IBattleDriver battle, ISfxPlayer sfx)
        {
            _battle = battle;
            _sfx = sfx;
            _battle.OnFinished += Finish;
        }

        public void LateTick()
        {
            var simulation = _battle.Current?.Simulation;
            if (simulation != _simulation)
            {
                _simulation = simulation;
                _captures = simulation?.Captures.Count ?? 0;
            }

            if (simulation == null)
                return;

            if (_captures != simulation.Captures.Count)
            {
                _captures = simulation.Captures.Count;
                Play(Sfx.Capture);
            }

            _events.Read(simulation, Handle);
        }

        public void Dispose() => _battle.OnFinished -= Finish;

        private void Handle(BattleEvent battleEvent)
        {
            switch (battleEvent.Kind)
            {
                case EventKind.Pickup when battleEvent.Side == Side.Player:
                    Play(Sfx.Pickup);
                    break;
                case EventKind.Built:
                    Play(Sfx.Build);
                    break;
                case EventKind.Hired when battleEvent.Side == Side.Player:
                    Play(Sfx.Hire);
                    break;
                case EventKind.Strike:
                    Play(Sfx.Strike);
                    break;
                case EventKind.Shot:
                    Play(Sfx.Shot);
                    break;
            }
        }

        private void Finish(BattleResult result) => Play(result == BattleResult.Victory ? Sfx.Victory : Sfx.Defeat);

        private void Play(Sfx sound)
        {
            var now = Time.unscaledTime;
            if (now - _last[(int)sound] < MinGapSeconds)
                return;

            _last[(int)sound] = now;
            _sfx.Play(sound);
        }
    }
}
