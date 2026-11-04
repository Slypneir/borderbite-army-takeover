using System;
using Core.BattleSystem.Structs;

namespace Core.BattleSystem.Services
{
    /// <summary>A battle and its clock: the only way frame time reaches the simulation.</summary>
    public sealed class BattleRunner
    {
        private readonly FixedStepClock _clock;

        public BattleRunner(BattleSettings settings, int maxStepsPerAdvance)
        {
            Simulation = new BattleSimulation(settings);
            _clock = new FixedStepClock(settings.TicksPerSecond, maxStepsPerAdvance);
        }

        public BattleSimulation Simulation { get; }

        public double Alpha => _clock.Alpha;

        /// <summary>A tick was run.</summary>
        public event Action OnStep;

        public void Advance(long elapsedMicroseconds)
        {
            var steps = _clock.Advance(elapsedMicroseconds);
            // a finished battle takes no more ticks, even inside the frame it ended in
            for (var i = 0; i < steps && Simulation.Result == Enums.BattleResult.None; i++)
            {
                Simulation.Step();
                OnStep?.Invoke();
            }
        }
    }
}
