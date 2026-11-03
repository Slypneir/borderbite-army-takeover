using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>A pile of gold or a bundle of wood lying in the middle of a cell, until a hero picks it up.</summary>
    public struct ResourceState
    {
        public int Id;
        public ResourceKind Kind;
        public int Cell;
        public int X;
        public int Y;
        public int Amount;
    }
}
