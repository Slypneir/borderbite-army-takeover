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
        /// <summary>Half the side of a barracks (2.25 m): blows, hiring and the reach of everything go to its walls.</summary>
        public int BarracksHalfUnits = 2250;
        /// <summary>A hero builds when the edge of its body is this close to the edge of the place (or on it).</summary>
        public int BuildReachUnits = 600;
        public int AuraCells = 3;
        /// <summary>Hiring reaches from the edge of the hero to the walls of the barracks.</summary>
        public int HireUnits = 2000;
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
        /// <summary>Radius of the body of a soldier: reaches are measured from the edge of a body to the edge of another.</summary>
        public int SoldierBodyUnits = 500;
        public int SlotSpacingUnits = 2000;
        public int PlayerSoldierPermille = 1100;
        public int EnemySoldierPermille = 900;

        // layer 2: the hero fights
        public int HeroHealth = 1000;
        public int HeroRegenPerSecond = 50;
        /// <summary>A hero heals only when nothing hurt it this long (task 4.7: out of a fight, or it cannot be killed at home).</summary>
        public int HealAfterTicks = 90;
        public int MeleeReachUnits = 2400;
        public int MeleeDamage = 100;
        public int MeleeCooldownTicks = 15;
        /// <summary>Heroes shoot the gun (task 4.8: off for now, both heroes fight only with the sword).</summary>
        public bool HeroGun;
        public int GunReachUnits = 12000;
        public int GunDamage = 80;
        public int GunCooldownTicks = 45;
        public int RespawnTicks = 90;
        public int InvulnerableTicks = 30;

        // layer 3: bases
        public int BaseHealth = 6000;
        public int BaseAuraCells = 4;
        /// <summary>Half the side of a base (3 m): blows and shots reach its walls, not only its middle.</summary>
        public int BaseHalfUnits = 3000;
        /// <summary>The hero comes back this far before the middle of its base, at the gate (marker `spawn` of the model).</summary>
        public int BaseGateUnits = 5000;

        // layer 3: the enemy commander, the strength of the level (GDD 5.9; level 3 for the test level)
        public bool EnemyHero = true;
        public int EnemySpeedPermille = 800;
        public int EnemyReactionTicks = 33;
        public int EnemyBuildFromTicks = 0;
        public int CommanderSightUnits = 24000;
        public int CommanderStuckTicks = 90;
        public int CommanderRetreatPermille = 300;
        public int CommanderAttackArmy = 8;
        public int CommanderAttackPermille = 800;
        // layer 4: the lasting upgrades of the player (GDD 6), given by the meta at the start of a level
        /// <summary>Health and damage of the player's hero, permille.</summary>
        public int PlayerHeroPermille = 1000;
        /// <summary>Added to the brush of the player's hero, units.</summary>
        public int PlayerBrushBonusUnits;
        /// <summary>Gold the player starts a level with.</summary>
        public int PlayerStartGold;

        /// <summary>The developer's autopilot: the same commander walks the hero of the player; its commands are ignored.</summary>
        public bool PlayerAutopilot;

        public int MapUnits => MapCells * UnitsPerCell;

        /// <summary>The full health of the hero of a side: the player's grows with its upgrade.</summary>
        public int HeroHealthOf(Enums.Side side) => side == Enums.Side.Player ? HeroHealth * PlayerHeroPermille / 1000 : HeroHealth;

        /// <summary>A blow or shot of the hero of a side: the player's grows with its upgrade.</summary>
        public int HeroDamageOf(Enums.Side side, int damage) => side == Enums.Side.Player ? damage * PlayerHeroPermille / 1000 : damage;

        /// <summary>A copy to change for another battle; the arrays of places and piles are shared, nobody writes them.</summary>
        public BattleSettings Clone() => (BattleSettings)MemberwiseClone();
    }
}
