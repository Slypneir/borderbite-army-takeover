namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// Numbers of one battle in the units of the simulation: a cell is <see cref="UnitsPerCell"/> units, time is ticks. The owner
    /// tunes them in meters and seconds in Meta/BattleSystem/Configuration/BattleConfig; this assembly sees no Unity.
    /// </summary>
    public readonly struct BattleSettings
    {
        public const int UnitsPerCell = 1000;

        public readonly int TicksPerSecond;
        public readonly int MapCells;
        public readonly int StartRadiusCells;
        public readonly int StartOffsetCells;
        public readonly int HeroStepUnits;
        public readonly int HeroRadiusUnits;
        public readonly int HeroBrushUnits;
        public readonly int EnclosureEveryTicks;
        public readonly int MaxEnclosurePermille;
        public readonly int VictoryPermille;
        public readonly uint Seed;

        public BattleSettings(int ticksPerSecond, int mapCells, int startRadiusCells, int startOffsetCells, int heroStepUnits,
            int heroRadiusUnits, int heroBrushUnits, int enclosureEveryTicks, int maxEnclosurePermille, int victoryPermille, uint seed)
        {
            TicksPerSecond = ticksPerSecond;
            MapCells = mapCells;
            StartRadiusCells = startRadiusCells;
            StartOffsetCells = startOffsetCells;
            HeroStepUnits = heroStepUnits;
            HeroRadiusUnits = heroRadiusUnits;
            HeroBrushUnits = heroBrushUnits;
            EnclosureEveryTicks = enclosureEveryTicks;
            MaxEnclosurePermille = maxEnclosurePermille;
            VictoryPermille = victoryPermille;
            Seed = seed;
        }

        public int MapUnits => MapCells * UnitsPerCell;
    }
}
