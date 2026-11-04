namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// Numbers of one battle in the units of the simulation: a cell is <see cref="UnitsPerCell"/> units (0.5 m, so a meter is
    /// 2000 units), time is ticks, health is tenths of a point. The defaults are the starting values of the stage 1 spec at 30
    /// ticks a second; the owner tunes them in meters and seconds in Meta/BattleSystem/Configuration/BattleConfig. This
    /// assembly sees no Unity.
    /// </summary>
    public sealed class BattleSettings
    {
        public const int UnitsPerCell = 1000;

        // layer 1: map, hero, painting, enclosure, victory
        public int TicksPerSecond = 30;
        public int MapCells = 64;
        public int StartRadiusCells = 6;
        public int StartOffsetCells = 8;
        public int HeroStepUnits = 333;
        public int HeroRadiusUnits = 600;
        public int HeroBrushUnits = 2000;
        public int EnclosureEveryTicks = 1;
        public int MaxEnclosureCells = 250;
        public int VictoryPermille = 700;
        public uint Seed = 1;

        // layer 2: resources
        public int GoldPortion = 5;
        public int GoldEveryTicks = 60;
        public int GoldMax = 12;
        public int WoodPortion = 2;
        public int WoodEveryTicks = 90;
        public int WoodMax = 8;
        public int ResourceAwayUnits = 4000;
        public int PickupUnits = 2000;
        public int NeutralLowPermille = 100;

        /// <summary>Start piles of the player, cell x and y pairs; the enemy gets them mirrored through the centre.</summary>
        public int[] StartGoldCells = { 15, 9, 13, 13, 9, 15 };
        public int[] StartWoodCells = { 15, 12, 12, 15 };

        // layer 2: places to build and barracks
        /// <summary>Centres of the 3 × 3 places, cell x and y pairs, mirrored through the centre of the map.</summary>
        public int[] SiteCells = { 15, 15, 48, 48, 12, 36, 51, 27, 36, 12, 27, 51 };
        public int BuildTicks = 30;
        public int BarracksWood = 10;
        public int BarracksHealth = 3000;
        public int AuraCells = 3;
        public int HireUnits = 4000;
        public int HireEveryTicks = 15;
        public int SoldierGold = 5;
        public int ArmyMax = 30;

        // layer 2: soldiers
        public int SoldierStepUnits = 367;
        public int SoldierBrushUnits = 1000;
        public int SoldierHealth = 300;
        public int SoldierDamage = 50;
        public int SoldierCooldownTicks = 24;
        public int SoldierReachUnits = 2000;
        public int SoldierSightUnits = 8000;
        public int SoldierLeashUnits = 16000;
        public int SoldierSpacingUnits = 1000;
        public int SlotSpacingUnits = 2000;
        public int PlayerSoldierPermille = 1100;
        public int EnemySoldierPermille = 1000;

        // layer 2: the hero fights
        public int HeroHealth = 1000;
        public int HeroRegenPerSecond = 50;
        public int MeleeReachUnits = 2400;
        public int MeleeDamage = 100;
        public int MeleeCooldownTicks = 15;
        public int GunReachUnits = 12000;
        public int GunDamage = 80;
        public int GunCooldownTicks = 45;
        public int RespawnTicks = 90;
        public int InvulnerableTicks = 30;

        public int MapUnits => MapCells * UnitsPerCell;
    }
}
