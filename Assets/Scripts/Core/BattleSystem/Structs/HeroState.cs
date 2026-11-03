using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>A hero: position in units from the lower left corner of the map, the direction it walks and the one it faces.</summary>
    public struct HeroState
    {
        public Side Side;
        public int X;
        public int Y;
        public int DirectionX;
        public int DirectionY;
        public int FacingX;
        public int FacingY;

        public bool Moving => DirectionX != 0 || DirectionY != 0;
    }
}
