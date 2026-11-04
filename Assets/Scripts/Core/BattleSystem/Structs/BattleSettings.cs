namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// Numbers of one battle in the units of the simulation: a cell is <see cref="UnitsPerCell"/> units (0.5 m, so a meter is
    /// 2000 units), time is ticks, health is whole points: a blow takes one (owner, 4 November). The defaults are the starting values of the stage 1 spec at 30
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
        /// <summary>Land of a side cut off from its base and barracks goes back to grass, a piece up to the enclosure limit (owner, 4 November).</summary>
        public bool DropCutOffLand = true;
        public int VictoryPermille = 700;
        public uint Seed = 1;

        // layer 2: resources
        public int GoldPortion = 4;
        public int GoldEveryTicks = 60;
        public int GoldMax = 12;
        public int WoodPortion = 4;
        public int WoodEveryTicks = 90;
        public int WoodMax = 8;
        public int ResourceAwayUnits = 4000;
        /// <summary>The commander stops this close to a pile it walks to (half of it): then its hero mines it.</summary>
        public int PickupUnits = 2000;
        /// <summary>A hero mines a pile whose middle is this far from the edge of its body (task 4.19: 1.2 m).</summary>
        public int MineReachUnits = 2400;
        /// <summary>A blow of mining every this many ticks; each takes one unit (0.25 s, T21).</summary>
        public int MineCooldownTicks = 8;
        public int NeutralLowPermille = 100;

        /// <summary>Start piles of the player, cell x and y pairs; the enemy gets them mirrored through the centre.</summary>
        public int[] StartGoldCells = { 15, 9, 13, 13, 9, 15 };
        public int[] StartWoodCells = { 15, 12, 12, 15 };

        // layer 2: places to build and barracks
        /// <summary>Centres of the 3 × 3 places, cell x and y pairs, mirrored through the centre of the map.</summary>
        public int[] SiteCells = { 15, 15, 48, 48, 12, 36, 51, 27, 36, 12, 27, 51 };
        public int BuildTicks = 30;
        public int BarracksWood = 5;
        public int BarracksHealth = 50;
        /// <summary>Half the side of a barracks (2.25 m): blows, hiring and the reach of everything go to its walls.</summary>
        public int BarracksHalfUnits = 2250;
        /// <summary>A hero builds when the edge of its body is this close to the edge of the place (or on it).</summary>
        public int BuildReachUnits = 600;
        public int AuraCells = 3;
        /// <summary>Hiring reaches from the edge of the hero to the walls of the barracks.</summary>
        public int HireUnits = 2000;
        public int HireEveryTicks = 15;
        public int SoldierGold = 1;
        public int ArmyMax = 30;

        // layer 2: soldiers
        public int SoldierStepUnits = 367;
        public int SoldierBrushUnits = 1000;
        /// <summary>Health of a soldier of either side, points (owner, 4 November: no factors of the sides).</summary>
        public int SoldierHealth = 5;
        public int SoldierDamage = 1;
        public int SoldierCooldownTicks = 24;
        public int SoldierReachUnits = 2000;
        /// <summary>A soldier turns on an enemy hero or soldier this close, else walks to the nearest enemy barracks, else the base (T15).</summary>
        public int SoldierSightUnits = 16000;
        /// <summary>The enemy's soldiers follow their hero (task 4.15: the attack on their own is only for the player's).</summary>
        public bool EnemyArmyFollows = true;
        /// <summary>A soldier that follows its hero sees enemies, and buildings to strike, this far.</summary>
        public int FollowerSightUnits = 8000;
        /// <summary>Further than this from its hero a following soldier drops the enemy and catches up.</summary>
        public int SoldierLeashUnits = 16000;
        /// <summary>Places in the crowd around the hero are this far apart: the crowd keeps within 1–4 m.</summary>
        public int SlotSpacingUnits = 2000;
        /// <summary>Under the order «Захват» two soldiers never aim at cells closer than this, cells: they do not follow each other.</summary>
        public int CaptureSpacingCells = 3;
        public int SoldierSpacingUnits = 1000;
        /// <summary>Radius of the body of a soldier: reaches are measured from the edge of a body to the edge of another.</summary>
        public int SoldierBodyUnits = 500;

        // layer 2: the hero fights
        /// <summary>Health of the player's hero, points.</summary>
        public int HeroHealth = 10;
        /// <summary>Health of the enemy hero, points.</summary>
        public int EnemyHeroHealth = 7;
        /// <summary>The player's hero heals only out of a fight: it was not hurt and did not strike this long (T16), on any land.</summary>
        public int PlayerHeroHealAfterTicks = 30;
        /// <summary>The enemy hero and the soldiers of both sides heal only after this long out of a fight (task 4.14).</summary>
        public int HealAfterTicks = 60;
        /// <summary>Out of a fight the player's hero heals a point every this many ticks.</summary>
        public int PlayerHeroHealEveryTicks = 15;
        /// <summary>Out of a fight the enemy hero and the soldiers of both sides heal a point every this many ticks.</summary>
        public int HealEveryTicks = 30;
        public int MeleeReachUnits = 2400;
        public int MeleeDamage = 1;
        public int MeleeCooldownTicks = 15;
        /// <summary>Heroes shoot the gun (task 4.8: off for now, both heroes fight only with the sword).</summary>
        public bool HeroGun;
        public int GunReachUnits = 12000;
        public int GunDamage = 1;
        public int GunCooldownTicks = 45;
        public int RespawnTicks = 90;
        public int InvulnerableTicks = 30;

        // layer 3: bases
        public int BaseHealth = 200;
        public int BaseAuraCells = 4;
        /// <summary>Half the side of a base (3 m): blows and shots reach its walls, not only its middle.</summary>
        public int BaseHalfUnits = 3000;
        /// <summary>The hero comes back this far before the middle of its base, at the gate (marker `spawn` of the model).</summary>
        public int BaseGateUnits = 5000;
        /// <summary>Standing bases and barracks push units out (owner, 4 November).</summary>
        public bool SolidBuildings = true;

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
        /// <summary>Points of health added to the player's hero (T14: a blow stays 1).</summary>
        public int PlayerHeroBonusHealth;
        /// <summary>Points of health added to each soldier of the player.</summary>
        public int PlayerSoldierBonusHealth;
        /// <summary>Added to the brush of the player's hero, units.</summary>
        public int PlayerBrushBonusUnits;
        /// <summary>Gold the player starts a level with.</summary>
        public int PlayerStartGold;

        /// <summary>The developer's autopilot: the same commander walks the hero of the player; its commands are ignored.</summary>
        public bool PlayerAutopilot;
        /// <summary>The autopilot orders «Ко мне» while the army is smaller than this, then frees it: a tool of the runs, not a rule.</summary>
        public int AutopilotRallyBelow = 8;

        public int MapUnits => MapCells * UnitsPerCell;

        /// <summary>The full health of the hero of a side: the player's grows with its upgrade.</summary>
        public int HeroHealthOf(Enums.Side side) => side == Enums.Side.Player ? HeroHealth + PlayerHeroBonusHealth : EnemyHeroHealth;

        /// <summary>The full health of a soldier of a side: the player's grows with its upgrade.</summary>
        public int SoldierHealthOf(Enums.Side side) => side == Enums.Side.Player ? SoldierHealth + PlayerSoldierBonusHealth : SoldierHealth;

        /// <summary>A hero of a side heals only after this long out of a fight.</summary>
        public int HeroHealAfterOf(Enums.Side side) => side == Enums.Side.Player ? PlayerHeroHealAfterTicks : HealAfterTicks;

        /// <summary>Out of a fight a hero of a side heals a point every this many ticks.</summary>
        public int HeroHealEveryOf(Enums.Side side) => side == Enums.Side.Player ? PlayerHeroHealEveryTicks : HealEveryTicks;

        /// <summary>A copy to change for another battle; the arrays of places and piles are shared, nobody writes them.</summary>
        public BattleSettings Clone() => (BattleSettings)MemberwiseClone();
    }
}
