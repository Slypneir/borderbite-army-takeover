namespace Core.BattleSystem.Structs
{
    /// <summary>A faction sends a wave from one zone to another at the given tick: the only way anything enters the simulation.</summary>
    public readonly struct BattleCommand
    {
        public readonly int Tick;
        public readonly int Faction;
        public readonly int FromZone;
        public readonly int ToZone;

        public BattleCommand(int tick, int faction, int fromZone, int toZone)
        {
            Tick = tick;
            Faction = faction;
            FromZone = fromZone;
            ToZone = toZone;
        }
    }
}
