namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// Numbers of one battle. The owner tunes them in Meta/BattleSystem/Configuration/BattleConfig; this assembly sees no Unity.
    /// </summary>
    public readonly struct BattleSettings
    {
        public readonly int TicksPerSecond;
        public readonly int ZoneCount;
        public readonly int FactionCount;
        public readonly int StartTroops;
        public readonly int ProductionPeriodTicks;
        public readonly int WaveTravelTicks;
        public readonly int WaveTravelJitterTicks;
        public readonly uint Seed;

        public BattleSettings(int ticksPerSecond, int zoneCount, int factionCount, int startTroops, int productionPeriodTicks,
            int waveTravelTicks, int waveTravelJitterTicks, uint seed)
        {
            TicksPerSecond = ticksPerSecond;
            ZoneCount = zoneCount;
            FactionCount = factionCount;
            StartTroops = startTroops;
            ProductionPeriodTicks = productionPeriodTicks;
            WaveTravelTicks = waveTravelTicks;
            WaveTravelJitterTicks = waveTravelJitterTicks;
            Seed = seed;
        }
    }
}
