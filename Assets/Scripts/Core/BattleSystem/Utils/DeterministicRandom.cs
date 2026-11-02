namespace Core.BattleSystem.Utils
{
    /// <summary>xorshift32: the same numbers from the same seed on every browser and device.</summary>
    public struct DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : seed;
        }

        public uint State => _state;

        public uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        /// <summary>A number in [0, <paramref name="exclusiveMax"/>); 0 when the range is empty.</summary>
        public int Next(int exclusiveMax) => exclusiveMax <= 0 ? 0 : (int)(NextUInt() % (uint)exclusiveMax);
    }
}
