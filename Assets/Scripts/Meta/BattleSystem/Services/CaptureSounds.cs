using Core.AudioManagement.Enums;
using Core.AudioManagement.Interfaces;
using Core.BattleSystem.Services;
using Meta.BattleSystem.Interfaces;
using VContainer.Unity;

namespace Meta.BattleSystem.Services
{
    /// <summary>The short sound of a capture by enclosure, once per frame however many areas closed in it.</summary>
    public sealed class CaptureSounds : ILateTickable
    {
        private readonly IBattleDriver _battle;
        private readonly ISfxPlayer _sfx;
        private BattleSimulation _simulation;
        private int _seen;

        public CaptureSounds(IBattleDriver battle, ISfxPlayer sfx)
        {
            _battle = battle;
            _sfx = sfx;
        }

        public void LateTick()
        {
            var simulation = _battle.Current?.Simulation;
            if (simulation != _simulation)
            {
                _simulation = simulation;
                _seen = simulation?.Captures.Count ?? 0;
            }

            if (simulation == null || _seen == simulation.Captures.Count)
                return;

            _seen = simulation.Captures.Count;
            _sfx.Play(Sfx.Capture);
        }
    }
}
