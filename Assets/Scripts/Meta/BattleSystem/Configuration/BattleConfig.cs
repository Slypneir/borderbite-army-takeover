using Core.BattleSystem.Structs;
using UnityEngine;

namespace Meta.BattleSystem.Configuration
{
    /// <summary>
    /// The rules of a battle in meters and seconds (stage 1 spec, layers 1 and 2). All are starting values; the simulation gets
    /// them in its own units through <see cref="ToSettings"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleConfig", menuName = "Borderbite/Battle/Battle Config")]
    public sealed class BattleConfig : ScriptableObject
    {
        [Header("Clock")]
        [Tooltip("Simulation steps per second. Starting value: 30.")]
        [Min(1)] [SerializeField] private int ticksPerSecond = 30;

        [Tooltip("The most steps one frame may run after a hitch; the rest of the time is dropped. Starting value: 8.")]
        [Min(1)] [SerializeField] private int maxStepsPerFrame = 8;

        [Header("Map")]
        [Tooltip("Cells along a side of the square map. Starting value: 64.")]
        [Min(8)] [SerializeField] private int mapCells = 64;

        [Tooltip("Side of a cell, meters. Starting value: 0.5.")]
        [Min(0.1f)] [SerializeField] private float cellSize = 0.5f;

        [Tooltip("Radius of a start area, cells. Starting value: 6.")]
        [Min(1)] [SerializeField] private int startRadius = 6;

        [Tooltip("Distance from the centre of a start area to the two nearest edges, cells. Starting value: 8.")]
        [Min(1)] [SerializeField] private int startOffset = 8;

        [Header("Hero")]
        [Tooltip("Speed of the hero in any direction, meters per second. Starting value: 5.")]
        [Min(0.1f)] [SerializeField] private float heroSpeed = 5f;

        [Tooltip("Radius of the body of the hero: how close it comes to the edge of the map, meters. Starting value: 0.3.")]
        [Min(0f)] [SerializeField] private float heroRadius = 0.3f;

        [Tooltip("Radius of the brush of the hero: cells whose middle is this close become its colour, cells. Starting value: 2.")]
        [Min(0.5f)] [SerializeField] private float heroBrush = 2f;

        [Tooltip("Health of the hero. Starting value: 100.")]
        [Min(1f)] [SerializeField] private float heroHealth = 100f;

        [Tooltip("Healing of the hero on the land of its side, health per second. Starting value: 5.")]
        [Min(0f)] [SerializeField] private float heroRegen = 5f;

        [Tooltip("Sword: reach in meters. Starting value: 1.2.")]
        [Min(0.1f)] [SerializeField] private float meleeReach = 1.2f;

        [Tooltip("Sword: damage of a blow. Starting value: 10.")]
        [Min(0f)] [SerializeField] private float meleeDamage = 10f;

        [Tooltip("Sword: seconds between blows. Starting value: 0.5.")]
        [Min(0.05f)] [SerializeField] private float meleeEvery = 0.5f;

        [Tooltip("Gun: reach in meters. Starting value: 6.")]
        [Min(0.1f)] [SerializeField] private float gunReach = 6f;

        [Tooltip("Gun: damage of a shot. Starting value: 8.")]
        [Min(0f)] [SerializeField] private float gunDamage = 8f;

        [Tooltip("Gun: seconds between shots. Starting value: 1.5.")]
        [Min(0.05f)] [SerializeField] private float gunEvery = 1.5f;

        [Tooltip("A dead hero comes back at home after this many seconds. Starting value: 3.")]
        [Min(0f)] [SerializeField] private float respawnSeconds = 3f;

        [Tooltip("A hero that came back takes no damage for this many seconds. Starting value: 1.")]
        [Min(0f)] [SerializeField] private float invulnerableSeconds = 1f;

        [Header("Land")]
        [Tooltip("The enclosure is looked for every this many ticks; 3 is the fallback for a slow device. Starting value: 1.")]
        [Min(1)] [SerializeField] private int enclosureEveryTicks = 1;

        [Tooltip("The largest area one enclosure takes, cells, protected ones not counted; a larger one stays as it is, whole. The same " +
                 "for both sides. Starting value: 250 (about 6% of the map, owner's decision of 4 November, stage 1 spec 2.5).")]
        [Min(1)] [SerializeField] private int maxEnclosureCells = 250;

        [Tooltip("A side with this share of the map wins, percent. Starting value: 70.")]
        [Range(1f, 100f)] [SerializeField] private float victoryShare = 70f;

        [Header("Resources")]
        [Tooltip("Gold in a pile. Starting value: 5.")]
        [Min(1)] [SerializeField] private int goldPortion = 5;

        [Tooltip("A new pile of gold every this many seconds. Starting value: 2.")]
        [Min(0.1f)] [SerializeField] private float goldEvery = 2f;

        [Tooltip("The most piles of gold on the map. Starting value: 12.")]
        [Min(0)] [SerializeField] private int goldMax = 12;

        [Tooltip("Wood in a bundle. Starting value: 2.")]
        [Min(1)] [SerializeField] private int woodPortion = 2;

        [Tooltip("A new bundle of wood every this many seconds. Starting value: 3.")]
        [Min(0.1f)] [SerializeField] private float woodEvery = 3f;

        [Tooltip("The most bundles of wood on the map. Starting value: 8.")]
        [Min(0)] [SerializeField] private int woodMax = 8;

        [Tooltip("A new resource never appears closer than this to a hero or a soldier, meters. Starting value: 2.")]
        [Min(0f)] [SerializeField] private float resourceAway = 2f;

        [Tooltip("The hero picks a resource up within this distance, meters. Starting value: 1.")]
        [Min(0.1f)] [SerializeField] private float pickupReach = 1f;

        [Tooltip("Under this share of neutral land resources appear on any unprotected cell, percent. Starting value: 10.")]
        [Range(0f, 100f)] [SerializeField] private float neutralLowShare = 10f;

        [Header("Barracks")]
        [Tooltip("Centres of the 3 × 3 places to build, cells; mirrored through the centre of the map. Starting value: 6 places.")]
        [SerializeField] private Vector2Int[] sites =
        {
            new Vector2Int(15, 15), new Vector2Int(48, 48), new Vector2Int(12, 36),
            new Vector2Int(51, 27), new Vector2Int(36, 12), new Vector2Int(27, 51),
        };

        [Tooltip("The hero stands on its empty place this long to build, seconds. Starting value: 1.")]
        [Min(0.05f)] [SerializeField] private float buildSeconds = 1f;

        [Tooltip("Wood a barracks costs. Starting value: 10.")]
        [Min(0)] [SerializeField] private int barracksWood = 10;

        [Tooltip("Health of a barracks. Starting value: 300.")]
        [Min(1f)] [SerializeField] private float barracksHealth = 300f;

        [Tooltip("Radius of the aura of a barracks: cells always of its colour, cells. Starting value: 3.")]
        [Min(1)] [SerializeField] private int auraCells = 3;

        [Tooltip("The hero hires within this distance of its barracks, meters. Starting value: 2.")]
        [Min(0.1f)] [SerializeField] private float hireReach = 2f;

        [Tooltip("A soldier every this many seconds while hiring. Starting value: 0.5.")]
        [Min(0.05f)] [SerializeField] private float hireEvery = 0.5f;

        [Tooltip("Gold a soldier costs. Starting value: 5.")]
        [Min(0)] [SerializeField] private int soldierGold = 5;

        [Tooltip("The most soldiers of a side. Starting value: 30.")]
        [Min(0)] [SerializeField] private int armyMax = 30;

        [Header("Soldiers")]
        [Tooltip("Speed of a soldier, meters per second. Starting value: 5.5.")]
        [Min(0.1f)] [SerializeField] private float soldierSpeed = 5.5f;

        [Tooltip("Radius of the brush of a soldier, cells. Starting value: 1.")]
        [Min(0.5f)] [SerializeField] private float soldierBrush = 1f;

        [Tooltip("Health of a soldier before the factor of its side. Starting value: 30.")]
        [Min(1f)] [SerializeField] private float soldierHealth = 30f;

        [Tooltip("Damage of a blow before the factor of its side. Starting value: 5.")]
        [Min(0f)] [SerializeField] private float soldierDamage = 5f;

        [Tooltip("Seconds between the blows of a soldier. Starting value: 0.8.")]
        [Min(0.05f)] [SerializeField] private float soldierEvery = 0.8f;

        [Tooltip("Reach of a blow, meters. Starting value: 1.")]
        [Min(0.1f)] [SerializeField] private float soldierReach = 1f;

        [Tooltip("A soldier sees enemies this far, meters. Starting value: 4.")]
        [Min(0.1f)] [SerializeField] private float soldierSight = 4f;

        [Tooltip("Further than this from its hero a soldier drops the enemy and comes back, meters. Starting value: 8.")]
        [Min(0.1f)] [SerializeField] private float soldierLeash = 8f;

        [Tooltip("Soldiers keep this far from each other and from their hero, meters. Starting value: 0.5.")]
        [Min(0.05f)] [SerializeField] private float soldierSpacing = 0.5f;

        [Tooltip("Places in the crowd around the hero are this far apart, meters; the crowd keeps within 1–4 m. Starting value: 1.")]
        [Min(0.1f)] [SerializeField] private float slotSpacing = 1f;

        [Tooltip("Factor of health and damage of the soldiers of the player. Starting value: 1.1.")]
        [Min(0.1f)] [SerializeField] private float playerSoldierFactor = 1.1f;

        [Tooltip("Factor of health and damage of the soldiers of the enemy; by level later (GDD 5.9). Starting value: 1.")]
        [Min(0.1f)] [SerializeField] private float enemySoldierFactor = 1f;

        public int MaxStepsPerFrame => maxStepsPerFrame;
        public int TicksPerSecond => ticksPerSecond;
        public int MapCells => mapCells;
        public float CellSize => cellSize;
        public float MapSize => mapCells * cellSize;
        public float VictoryShare => victoryShare;
        public int BarracksWood => barracksWood;
        public int SoldierGold => soldierGold;
        public int ArmyMax => armyMax;
        public float HeroHealth => heroHealth;

        /// <summary>Meters of the world for units of the simulation.</summary>
        public float MetersPerUnit => cellSize / BattleSettings.UnitsPerCell;

        public BattleSettings ToSettings(uint seed)
        {
            var siteCells = new int[sites.Length * 2];
            for (var i = 0; i < sites.Length; i++)
            {
                siteCells[i * 2] = sites[i].x;
                siteCells[i * 2 + 1] = sites[i].y;
            }

            return new BattleSettings
            {
                TicksPerSecond = ticksPerSecond,
                MapCells = mapCells,
                StartRadiusCells = startRadius,
                StartOffsetCells = startOffset,
                HeroStepUnits = Units(heroSpeed / ticksPerSecond),
                HeroRadiusUnits = Units(heroRadius),
                HeroBrushUnits = Mathf.RoundToInt(heroBrush * BattleSettings.UnitsPerCell),
                EnclosureEveryTicks = enclosureEveryTicks,
                MaxEnclosureCells = maxEnclosureCells,
                VictoryPermille = Mathf.RoundToInt(victoryShare * 10f),
                Seed = seed,

                GoldPortion = goldPortion,
                GoldEveryTicks = Ticks(goldEvery),
                GoldMax = goldMax,
                WoodPortion = woodPortion,
                WoodEveryTicks = Ticks(woodEvery),
                WoodMax = woodMax,
                ResourceAwayUnits = Units(resourceAway),
                PickupUnits = Units(pickupReach),
                NeutralLowPermille = Mathf.RoundToInt(neutralLowShare * 10f),

                SiteCells = siteCells,
                BuildTicks = Ticks(buildSeconds),
                BarracksWood = barracksWood,
                BarracksHealth = Tenths(barracksHealth),
                AuraCells = auraCells,
                HireUnits = Units(hireReach),
                HireEveryTicks = Ticks(hireEvery),
                SoldierGold = soldierGold,
                ArmyMax = armyMax,

                SoldierStepUnits = Units(soldierSpeed / ticksPerSecond),
                SoldierBrushUnits = Mathf.RoundToInt(soldierBrush * BattleSettings.UnitsPerCell),
                SoldierHealth = Tenths(soldierHealth),
                SoldierDamage = Tenths(soldierDamage),
                SoldierCooldownTicks = Ticks(soldierEvery),
                SoldierReachUnits = Units(soldierReach),
                SoldierSightUnits = Units(soldierSight),
                SoldierLeashUnits = Units(soldierLeash),
                SoldierSpacingUnits = Units(soldierSpacing),
                SlotSpacingUnits = Units(slotSpacing),
                PlayerSoldierPermille = Mathf.RoundToInt(playerSoldierFactor * 1000f),
                EnemySoldierPermille = Mathf.RoundToInt(enemySoldierFactor * 1000f),

                HeroHealth = Tenths(heroHealth),
                HeroRegenPerSecond = Tenths(heroRegen),
                MeleeReachUnits = Units(meleeReach),
                MeleeDamage = Tenths(meleeDamage),
                MeleeCooldownTicks = Ticks(meleeEvery),
                GunReachUnits = Units(gunReach),
                GunDamage = Tenths(gunDamage),
                GunCooldownTicks = Ticks(gunEvery),
                RespawnTicks = Ticks(respawnSeconds),
                InvulnerableTicks = Ticks(invulnerableSeconds),
            };
        }

        private int Units(float meters) => Mathf.RoundToInt(meters * BattleSettings.UnitsPerCell / cellSize);

        private int Ticks(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * ticksPerSecond));

        private static int Tenths(float points) => Mathf.RoundToInt(points * 10f);
    }
}
