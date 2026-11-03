using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// A soldier: follows the hero of its side to a slot around it; without a hero it keeps to its anchor. Health in tenths;
    /// <see cref="Id"/> stays the same for its whole life, for the drawing.
    /// </summary>
    public struct SoldierState
    {
        public int Id;
        public Side Side;
        public int X;
        public int Y;
        public int Health;
        public int Damage;
        public int Cooldown;
        public int AnchorX;
        public int AnchorY;
        public int FacingX;
        public int FacingY;
    }
}
