namespace Core.BattleSystem.Services
{
    /// <summary>
    /// Turns the time of a frame into whole simulation steps. Integer arithmetic: no drift and no rounding that depends on
    /// the frame rate. A long hitch (a hidden tab, a slow phone) runs at most <c>maxStepsPerAdvance</c> steps and drops the rest.
    /// </summary>
    public sealed class FixedStepClock
    {
        private const long MicrosecondsPerSecond = 1000000;

        private readonly long _ticksPerSecond;
        private readonly int _maxStepsPerAdvance;
        private long _accumulated;

        public FixedStepClock(int ticksPerSecond, int maxStepsPerAdvance)
        {
            _ticksPerSecond = ticksPerSecond;
            _maxStepsPerAdvance = maxStepsPerAdvance;
        }

        /// <summary>Progress between the last step and the next one, 0..1: for drawing only, never for the simulation.</summary>
        public double Alpha => (double)_accumulated / MicrosecondsPerSecond;

        /// <summary>Adds the time of a frame; returns how many steps to run now.</summary>
        public int Advance(long elapsedMicroseconds)
        {
            _accumulated += elapsedMicroseconds * _ticksPerSecond;
            var steps = _accumulated / MicrosecondsPerSecond;
            _accumulated %= MicrosecondsPerSecond;
            return steps > _maxStepsPerAdvance ? _maxStepsPerAdvance : (int)steps;
        }
    }
}
