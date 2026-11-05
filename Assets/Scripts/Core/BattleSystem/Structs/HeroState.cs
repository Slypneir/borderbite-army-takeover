using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// A hero: position in units from the lower left corner of the map, the direction it walks and the one it faces, health in
    /// points. A dead hero waits for <see cref="RespawnTick"/> and comes back at <see cref="HomeX"/>, <see cref="HomeY"/>.
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
        public int Cooldown;
        public int RespawnTick;
        public int InvulnerableUntil;
        /// <summary>The tick it last was hurt or struck; it heals only some time after it (T16).</summary>
        public int FightTick;
        /// <summary>The blows at it on its own land, for the shield of the player's side (task 4.32).</summary>
        public int ShieldHits;
        public int HomeX;
        public int HomeY;

        public bool Alive => RespawnTick == 0;

        public bool Moving => DirectionX != 0 || DirectionY != 0;
    }
}
