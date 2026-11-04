namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// The base (headquarters) of a side (stage 1 spec 4.1): its centre in units, its health in tenths, and where its hero comes
    /// back, before the gate. Its fall ends the battle.
    /// </summary>
    public struct BaseState
    {
        public int X;
        public int Y;
        public int Health;
        public int GateX;
        public int GateY;

        public bool Standing => Health > 0;
    }
}
