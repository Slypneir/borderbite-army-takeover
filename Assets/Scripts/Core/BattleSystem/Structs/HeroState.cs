using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// A hero: position in units from the lower left corner of the map, the direction it walks and the one it faces, health in
    /// tenths. A dead hero waits for <see cref="RespawnTick"/> and comes back at <see cref="HomeX"/>, <see cref="HomeY"/>.
    /// </summary>
    public struct HeroState
    {
        public Side Side;
        public int X;
        public int Y;
        public int DirectionX;
        public int DirectionY;
        public int FacingX;
        public int FacingY;
        public int Health;
        public int RegenAccumulator;
        public int Cooldown;
        public int RespawnTick;
        public int InvulnerableUntil;
        /// <summary>The tick it was last hurt; it heals only some time after it.</summary>
        public int HurtTick;
        public int HomeX;
        public int HomeY;

        public bool Alive => RespawnTick == 0;

        public bool Moving => DirectionX != 0 || DirectionY != 0;
    }
}
