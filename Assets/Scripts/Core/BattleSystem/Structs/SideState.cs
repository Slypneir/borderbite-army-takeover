namespace Core.BattleSystem.Structs
{
    /// <summary>The purse of a side for the level, the timer of its hiring and the order its army follows (spec 3.8).</summary>
    public struct SideState
    {
        public int Gold;
        public int Wood;
        public int HireTimer;
        public Enums.ArmyOrder Order;
    }
}
