using Core.BattleSystem.Services;
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
        [Tooltip("The map of the test level (layer 5 spec 2.6): its land and water, bases, places, piles and rocks. Empty — the " +
                 "square island of the numbers below and of the places to build.")]
        [SerializeField] private BattleMapAsset map;

        [System.NonSerialized] private BattleMapAsset _playing;

        [Tooltip("Cells along a side of the square map with no map file. Starting value: 64.")]
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

        [Tooltip("The brush of a hero or a soldier paints round where it was this long ago, seconds (task 4.31: the paint follows " +
                 "the trail). Starting value: 0.4.")]
        [Min(0f)] [SerializeField] private float brushDelay = 0.4f;

        [Tooltip("Radius of the brush of the hero: cells whose middle is this close become its colour, cells. Starting value: 2.")]
        [Min(0.5f)] [SerializeField] private float heroBrush = 2f;

        [Tooltip("Health of the player's hero, points: a blow takes one (owner, 4 November). Starting value: 10.")]
        [Min(1)] [SerializeField] private int heroHealth = 10;

        [Tooltip("Health of the enemy hero, points. Starting value: 7.")]
        [Min(1)] [SerializeField] private int enemyHeroHealth = 7;

        [Tooltip("The player's hero heals only out of a fight, on any land: not hurt and not striking this long, seconds (T16, task 4.14). Starting value: 1.")]
        [Min(0f)] [SerializeField] private float heroHealAfter = 1f;

        [Tooltip("The enemy hero and the soldiers of both sides heal only after this long out of a fight, seconds (task 4.14). Starting value: 2.")]
        [Min(0f)] [SerializeField] private float healAfter = 2f;

        [Tooltip("Out of a fight the player's hero heals a point every this many seconds. Starting value: 0.5.")]
        [Min(0.05f)] [SerializeField] private float heroHealEvery = 0.5f;

        [Tooltip("Out of a fight the enemy hero and the soldiers of both sides heal a point every this many seconds. Starting value: 1.")]
        [Min(0.05f)] [SerializeField] private float healEvery = 1f;

        [Tooltip("Sword: reach in meters, from the edge of the hero to the edge of the target (a body, the walls of a building). Starting value: 1.2.")]
        [Min(0.1f)] [SerializeField] private float meleeReach = 1.2f;

        [Tooltip("Sword: points a blow takes. Starting value: 1.")]
        [Min(0)] [SerializeField] private int meleeDamage = 1;

        [Tooltip("Sword: seconds between blows. Starting value: 0.5.")]
        [Min(0.05f)] [SerializeField] private float meleeEvery = 0.5f;

        [Tooltip("Heroes shoot the gun (task 4.8: off for now — both heroes fight only with the sword; the numbers below stay for " +
                 "when it is on again). Starting value: off.")]
        [SerializeField] private bool heroGun;

        [Tooltip("Gun: reach in meters. Starting value: 6.")]
        [Min(0.1f)] [SerializeField] private float gunReach = 6f;

        [Tooltip("Gun: points a shot takes. Starting value: 1.")]
        [Min(0)] [SerializeField] private int gunDamage = 1;

        [Tooltip("Gun: seconds between shots. Starting value: 1.5.")]
        [Min(0.05f)] [SerializeField] private float gunEvery = 1.5f;

        [Tooltip("A dead hero comes back at home after this many seconds. Starting value: 3.")]
        [Min(0f)] [SerializeField] private float respawnSeconds = 3f;

        [Tooltip("A hero that came back takes no damage for this many seconds. Meanwhile no enemy picks it as a target (task 4.23). Starting value: 3.")]
        [Min(0f)] [SerializeField] private float invulnerableSeconds = 3f;

        [Header("Land")]
        [Tooltip("The enclosure is looked for every this many ticks; 3 is the fallback for a slow device. Starting value: 1.")]
        [Min(1)] [SerializeField] private int enclosureEveryTicks = 1;

        [Tooltip("The largest area one enclosure takes, cells, protected ones not counted; a larger one stays as it is, whole. The same " +
                 "for both sides. Starting value: 250 (about 6% of the map, owner's decision of 4 November, stage 1 spec 2.5).")]
        [Min(1)] [SerializeField] private int maxEnclosureCells = 250;

        [Tooltip("A side with this share of the map wins, percent. Starting value: 70.")]
        [Range(1f, 100f)] [SerializeField] private float victoryShare = 70f;

        [Header("Resources")]
        [Tooltip("Gold in a pile. Starting value: 4.")]
        [Min(1)] [SerializeField] private int goldPortion = 4;

        [Tooltip("A new pile of gold every this many seconds. Starting value: 2.")]
        [Min(0.1f)] [SerializeField] private float goldEvery = 2f;

        [Tooltip("The most piles of gold on the map. Starting value: 12.")]
        [Min(0)] [SerializeField] private int goldMax = 12;

        [Tooltip("Wood in a bundle. Starting value: 4.")]
        [Min(1)] [SerializeField] private int woodPortion = 4;

        [Tooltip("A new bundle of wood every this many seconds. Starting value: 3.")]
        [Min(0.1f)] [SerializeField] private float woodEvery = 3f;

        [Tooltip("The most bundles of wood on the map. Starting value: 8.")]
        [Min(0)] [SerializeField] private int woodMax = 8;

        [Tooltip("A new resource never appears closer than this to a hero or a soldier, meters. Starting value: 2.")]
        [Min(0f)] [SerializeField] private float resourceAway = 2f;

        [Tooltip("A new resource never appears this close to the edge of a place to build, empty or not, or to the walls of a base, " +
                 "meters (task 4.29). Starting value: 2.")]
        [Min(0f)] [SerializeField] private float resourceKeepOff = 2f;

        [Tooltip("The hero picks a resource up within this distance, meters. Starting value: 1.")]
        [Min(0.1f)] [SerializeField] private float pickupReach = 1f;

        [Tooltip("A hero mines a pile whose middle is this far from the edge of its body, meters (task 4.19). Starting value: 1.2.")]
        [Min(0.1f)] [SerializeField] private float mineReach = 1.2f;

        [Tooltip("A blow of mining every this many seconds, each takes one unit (T21). Starting value: 0.25.")]
        [Min(0.05f)] [SerializeField] private float mineEvery = 0.25f;

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

        [Tooltip("Wood a barracks costs. Starting value: 5.")]
        [Min(0)] [SerializeField] private int barracksWood = 5;

        [Tooltip("Health of a barracks, points (T13). Starting value: 50.")]
        [Min(1)] [SerializeField] private int barracksHealth = 50;

        [Tooltip("Half the side of a barracks, meters: blows and hiring reach its walls. Starting value: 1.125 (the model is 2.25 m).")]
        [Min(0f)] [SerializeField] private float barracksHalfSize = 1.125f;

        [Tooltip("A hero builds when the edge of its body is this close to the walls of the barracks to be, or on the place, meters. " +
                 "Starting value: 1 (owner, 5 November: near the place, as near as hiring; was 0.3 m from the 1.5 m place).")]
        [Min(0f)] [SerializeField] private float buildReach = 1f;

        [Tooltip("Radius of the aura of a barracks: cells always of its colour, cells. Starting value: 3.")]
        [Min(1)] [SerializeField] private int auraCells = 3;

        [Tooltip("The hero hires within this distance from the edge of its body to the walls of its barracks, meters. Starting value: 1 " +
                 "(was 2 m to the middle; the same place by the door).")]
        [Min(0.1f)] [SerializeField] private float hireReach = 1f;

        [Tooltip("A soldier every this many seconds while hiring. Starting value: 0.5.")]
        [Min(0.05f)] [SerializeField] private float hireEvery = 0.5f;

        [Tooltip("Gold a soldier costs. Starting value: 1.")]
        [Min(0)] [SerializeField] private int soldierGold = 1;

        [Tooltip("The most soldiers of a side. Starting value: 30.")]
        [Min(0)] [SerializeField] private int armyMax = 30;

        [Header("Soldiers")]
        [Tooltip("Speed of a soldier, meters per second. Starting value: 5.5.")]
        [Min(0.1f)] [SerializeField] private float soldierSpeed = 5.5f;

        [Tooltip("Radius of the brush of a soldier, cells. Starting value: 1.")]
        [Min(0.5f)] [SerializeField] private float soldierBrush = 1f;

        [Tooltip("Health of a soldier of either side, points. Starting value: 5.")]
        [Min(1)] [SerializeField] private int soldierHealth = 5;

        [Tooltip("Points a blow of a soldier takes. Starting value: 1.")]
        [Min(0)] [SerializeField] private int soldierDamage = 1;

        [Tooltip("Seconds between the blows of a soldier. Starting value: 0.8.")]
        [Min(0.05f)] [SerializeField] private float soldierEvery = 0.8f;

        [Tooltip("Reach of a blow of a soldier, meters, from the edge of its body to the edge of the target. Starting value: 1.")]
        [Min(0.1f)] [SerializeField] private float soldierReach = 1f;

        [Tooltip("A soldier turns on an enemy hero or soldier this close, meters; else it walks to the nearest enemy barracks, else " +
                 "the enemy base (T15). Starting value: 8.")]
        [Min(0.1f)] [SerializeField] private float soldierSight = 8f;

        [Tooltip("The enemy's soldiers follow their hero (task 4.15); off — they attack on their own like the player's. Starting value: on.")]
        [SerializeField] private bool enemyArmyFollows = true;

        [Tooltip("The enemy's army in squads and waves (owner, 5 November; spec 4.2, «Армия врага», task 4.27); off — the crowd " +
                 "round its hero of task 4.15. Its numbers are in the table of the enemy levels.")]
        [SerializeField] private bool enemySquads = true;

        [Tooltip("A soldier that follows its hero sees enemy units, and buildings to strike, this far, meters. Starting value: 4.")]
        [Min(0.1f)] [SerializeField] private float followerSight = 4f;

        [Tooltip("Further than this from its hero a following soldier drops the enemy and catches up, meters. Starting value: 8.")]
        [Min(0.1f)] [SerializeField] private float soldierLeash = 8f;

        [Tooltip("Places in the crowd around the hero are this far apart, meters; the crowd keeps within 1–4 m. Starting value: 1.")]
        [Min(0.1f)] [SerializeField] private float slotSpacing = 1f;

        [Tooltip("Under the order «Захват» two soldiers never aim at cells closer than this, cells: they do not follow each other (spec 3.8). Starting value: 3.")]
        [Min(1)] [SerializeField] private int captureSpacing = 3;

        [Tooltip("Soldiers keep this far from each other and from their hero, meters. Starting value: 0.5.")]
        [Min(0.05f)] [SerializeField] private float soldierSpacing = 0.5f;

        [Tooltip("Radius of the body of a soldier, meters: every reach goes from the edge of a body to the edge of another. Starting value: 0.25.")]
        [Min(0f)] [SerializeField] private float soldierBody = 0.25f;


        public int MaxStepsPerFrame => maxStepsPerFrame;
        public int TicksPerSecond => ticksPerSecond;
        public int MapCells => Map != null ? Mathf.Max(Map.Width, Map.Height) : mapCells;
        public float CellSize => cellSize;
        public float MapSize => MapCells * cellSize;

        /// <summary>The map of the next battle: one the developer chose to play, else that of the test level; null — the square island.</summary>
        public BattleMapAsset Map => _playing != null ? _playing : map;

        /// <summary>The map of the test level, whatever the developer plays now.</summary>
        public BattleMapAsset TestMap => map;

        /// <summary>The developer plays another map from the next battle (layer 5 spec 2.6); null — back to the map of the test level.</summary>
        public void Play(BattleMapAsset other) => _playing = other;

        /// <summary>The square island of the numbers of this config, whatever map is set: the test map is made from it (task 5.5).</summary>
        public Core.BattleSystem.Data.BattleMap SquareMap()
        {
            var settings = ToSettings(0);
            settings.Map = null;
            return Core.BattleSystem.Data.BattleMap.Square(settings);
        }

#if UNITY_EDITOR
        public void SetMapForEditor(BattleMapAsset value) => map = value;
#endif
        public float VictoryShare => victoryShare;
        public int BarracksWood => barracksWood;
        public int SoldierGold => soldierGold;
        public int ArmyMax => armyMax;
        public int HeroHealth => heroHealth;
        public int BarracksHealth => barracksHealth;
        public int BaseHealth => baseHealth;

        /// <summary>Meters of the world for units of the simulation.</summary>
        public float MetersPerUnit => cellSize / BattleSettings.UnitsPerCell;

        /// <summary>No resource closer than this to a place to build or the walls of a base, meters (task 4.29).</summary>
        public float ResourceKeepOff => resourceKeepOff;

        [Header("Bases")]
        [Tooltip("Health of a base, points (spec 4.1, T13, task 4.16). Starting value: 200.")]
        [Min(1)] [SerializeField] private int baseHealth = 200;

        [Tooltip("The aura of a base, cells: its colour around it, protected while it stands. Starting value: 4.")]
        [Min(0)] [SerializeField] private int baseAura = 4;

        [Tooltip("Half the side of a base, meters: blows and shots reach its walls. Starting value: 1.5 (the model is 3 m).")]
        [Min(0f)] [SerializeField] private float baseHalfSize = 1.5f;

        [Tooltip("The hero comes back this far from the middle of its base, at the gate, meters: clear of the walls (1.5 m). Starting value: 2.5.")]
        [Min(0f)] [SerializeField] private float baseGate = 2.5f;

        [Header("Enemy")]
        [Tooltip("The level whose enemy the test level takes (owner, 4 November). Starting value: 3.")]
        [Min(1)] [SerializeField] private int testLevel = 3;

        [Tooltip("The enemy of levels 1, 2, 3, … (GDD 5.9); past the last row the steps below go on.")]
        [SerializeField] private EnemyLevel[] enemyLevels =
        {
            new EnemyLevel { speed = 0.7f, reaction = 1.5f, soldiers = 0.8f, buildFrom = 60f, mineEvery = 0.4f, hireEvery = 0.8f,
                truce = 5f, squadMin = 3, squadMax = 5, firstWave = 20f, waveEvery = 30f, alarm = true, waveSquads = 2, waveWarn = 5f, armySlack = 3, armyFloor = 5, defend = 4f, homeReach = 10f },
            new EnemyLevel { speed = 0.75f, reaction = 1.3f, soldiers = 0.85f, buildFrom = 40f, mineEvery = 0.4f, hireEvery = 0.8f,
                truce = 5f, squadMin = 3, squadMax = 5, firstWave = 20f, waveEvery = 30f, alarm = true, waveSquads = 2, waveWarn = 5f, armySlack = 3, armyFloor = 5, defend = 4f, homeReach = 10f },
            new EnemyLevel { speed = 0.8f, reaction = 1.1f, soldiers = 0.9f, buildFrom = 0f, mineEvery = 0.4f, hireEvery = 0.8f,
                truce = 5f, squadMin = 3, squadMax = 5, firstWave = 20f, waveEvery = 30f, alarm = true, waveSquads = 2, waveWarn = 5f, armySlack = 3, armyFloor = 5, defend = 4f, homeReach = 10f },
            new EnemyLevel { speed = 0.85f, reaction = 1f, soldiers = 0.95f, buildFrom = 0f, mineEvery = 0.4f, hireEvery = 0.8f,
                truce = 5f, squadMin = 3, squadMax = 5, firstWave = 20f, waveEvery = 30f, alarm = true, waveSquads = 2, waveWarn = 5f, armySlack = 3, armyFloor = 5, defend = 4f, homeReach = 10f },
        };

        [Tooltip("Past the table, each level: speed +, up to the most. Starting values: +0.03, up to ×1.0.")]
        [SerializeField] private Vector2 speedStep = new Vector2(0.03f, 1f);

        [Tooltip("Past the table, each level: reaction −, down to the least, seconds. Starting values: −0.05, down to 0.5.")]
        [SerializeField] private Vector2 reactionStep = new Vector2(0.05f, 0.5f);

        [Tooltip("Past the table, each level: soldiers +, up to the most. Starting values: +0.03, up to ×1.1.")]
        [SerializeField] private Vector2 soldierStep = new Vector2(0.03f, 1.1f);

        [Header("Commander")]
        [Tooltip("The commander sees piles this far around its hero, meters: as far as a player sees. Starting value: 12.")]
        [Min(1f)] [SerializeField] private float commanderSight = 12f;

        [Tooltip("A goal that comes no closer this long is left for the next priority, seconds (spec 4.2). Starting value: 3.")]
        [Min(0.5f)] [SerializeField] private float commanderStuck = 3f;

        [Tooltip("Under this share of health the hero goes back to its land, percent. Starting value: 30.")]
        [Range(1f, 100f)] [SerializeField] private float commanderRetreat = 30f;

        [Tooltip("The commander attacks with at least this many soldiers. Starting value: 8.")]
        [Min(1)] [SerializeField] private int commanderAttackArmy = 8;

        [Tooltip("…and at least this share of the other army, percent. Starting value: 80.")]
        [Range(0f, 200f)] [SerializeField] private float commanderAttackShare = 80f;

        public int TestLevel => testLevel;

        /// <summary>The enemy of a level (GDD 5.9): the table, then the steps, each up to its bound.</summary>
        public EnemyLevel EnemyOf(int level)
        {
            level = Mathf.Max(1, level);
            if (enemyLevels.Length == 0)
                return new EnemyLevel { speed = 1f, reaction = 1f, soldiers = 1f, mineEvery = 0.25f, hireEvery = 0.5f, truce = 5f, squadMin = 3, squadMax = 5, firstWave = 20f, waveEvery = 30f, alarm = true, waveSquads = 2, waveWarn = 5f, armySlack = 3, armyFloor = 5, defend = 4f, homeReach = 10f };
            if (level <= enemyLevels.Length)
                return enemyLevels[level - 1];

            var last = enemyLevels[enemyLevels.Length - 1];
            var past = level - enemyLevels.Length;
            return new EnemyLevel
            {
                speed = Mathf.Min(speedStep.y, last.speed + speedStep.x * past),
                reaction = Mathf.Max(reactionStep.y, last.reaction - reactionStep.x * past),
                soldiers = Mathf.Min(soldierStep.y, last.soldiers + soldierStep.x * past),
                buildFrom = 0f,
                mineEvery = last.mineEvery,
                hireEvery = last.hireEvery,
                truce = last.truce,
                squadMin = last.squadMin,
                squadMax = last.squadMax,
                firstWave = last.firstWave,
                waveEvery = last.waveEvery,
                alarm = last.alarm,
                waveSquads = last.waveSquads,
                waveWarn = last.waveWarn,
                armySlack = last.armySlack,
                armyFloor = last.armyFloor,
                defend = last.defend,
                homeReach = last.homeReach,
            };
        }

        /// <summary>Gives a running battle the enemy of a level at once (the developer's switch, spec 2.7).</summary>
        public void ApplyEnemy(BattleSimulation simulation, int level)
        {
            var enemy = EnemyOf(level);
            simulation.SetEnemyStrength(Mathf.RoundToInt(enemy.speed * 1000f), Ticks(enemy.reaction), Mathf.RoundToInt(enemy.buildFrom * ticksPerSecond));
            simulation.SetEnemyPace(PaceTicks(enemy.mineEvery, mineEvery), PaceTicks(enemy.hireEvery, hireEvery));
            simulation.SetEnemyArmy(Ticks(enemy.truce), enemy.squadMin, enemy.squadMax, Ticks(enemy.firstWave), Ticks(enemy.waveEvery),
                enemy.waveSquads, Ticks(enemy.waveWarn), enemy.armySlack, enemy.armyFloor, Units(enemy.defend), Units(enemy.homeReach), enemy.alarm);
        }

        [System.Serializable]
        public struct EnemyLevel
        {
            [Tooltip("Speed of the enemy hero, a factor of the player's.")]
            public float speed;
            [Tooltip("The commander chooses once in this many seconds.")]
            public float reaction;
            [Tooltip("Health and damage of enemy soldiers, a factor; not used while health is whole points (owner, 4 November).")]
            public float soldiers;
            [Tooltip("The enemy may build from this second.")]
            public float buildFrom;
            [Tooltip("The enemy hero mines a blow every this many seconds (task 4.23; the player's is mineEvery). 0 — as the player.")]
            public float mineEvery;
            [Tooltip("The enemy barracks hire a soldier every this many seconds (task 4.23; the player's is hireEvery). 0 — as the player.")]
            public float hireEvery;

            [Header("Army (task 4.27)")]
            [Tooltip("The first this many seconds no unit of the enemy steps on the player's land. Starting value: 5.")]
            public float truce;
            [Tooltip("The least soldiers of a squad. Starting value: 3.")]
            public int squadMin;
            [Tooltip("The most soldiers of a squad. Starting value: 5.")]
            public int squadMax;
            [Tooltip("The first wave leaves at this second (task 4.28). Starting value: 20.")]
            public float firstWave;
            [Tooltip("Then a wave every this many seconds. Starting value: 30.")]
            public float waveEvery;
            [Tooltip("The alarm (task 4.28): units of the player on the enemy's land — every squad at home, the guard too, drives them " +
                     "to the edge of that land. Starting value: on.")]
            public bool alarm;
            [Tooltip("Squads in a wave; one squad stays on guard. Starting value: 2.")]
            public int waveSquads;
            [Tooltip("The sign of a wave shows this many seconds before it leaves. Starting value: 5.")]
            public float waveWarn;
            [Tooltip("The enemy's army is at most the player's and this many more. Starting value: 3.")]
            public int armySlack;
            [Tooltip("…but never held under this many. Starting value: 5.")]
            public int armyFloor;
            [Tooltip("The enemy's soldiers strike a unit of the player this close, meters (at home only on their land). Starting value: 4.")]
            public float defend;
            [Tooltip("At home they paint the neutral land by theirs only this far from the base or a barracks of the enemy, meters. Starting value: 10.")]
            public float homeReach;
        }

        public BattleSettings ToSettings(uint seed)
        {
            var enemy = EnemyOf(testLevel);
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
                Map = Map != null ? Map.ToMap() : null,
                StartRadiusCells = startRadius,
                StartOffsetCells = startOffset,
                HeroStepUnits = Units(heroSpeed / ticksPerSecond),
                HeroRadiusUnits = Units(heroRadius),
                HeroBrushUnits = Mathf.RoundToInt(heroBrush * BattleSettings.UnitsPerCell),
                BrushDelayTicks = Mathf.RoundToInt(brushDelay * ticksPerSecond),
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
                ResourceKeepOffUnits = Units(resourceKeepOff),
                PickupUnits = Units(pickupReach),
                MineReachUnits = Units(mineReach),
                MineCooldownTicks = Ticks(mineEvery),
                NeutralLowPermille = Mathf.RoundToInt(neutralLowShare * 10f),

                SiteCells = siteCells,
                BuildTicks = Ticks(buildSeconds),
                BarracksWood = barracksWood,
                BarracksHealth = barracksHealth,
                AuraCells = auraCells,
                HireUnits = Units(hireReach),
                HireEveryTicks = Ticks(hireEvery),
                SoldierGold = soldierGold,
                ArmyMax = armyMax,

                SoldierStepUnits = Units(soldierSpeed / ticksPerSecond),
                SoldierBrushUnits = Mathf.RoundToInt(soldierBrush * BattleSettings.UnitsPerCell),
                SoldierHealth = soldierHealth,
                SoldierDamage = soldierDamage,
                SoldierCooldownTicks = Ticks(soldierEvery),
                SoldierReachUnits = Units(soldierReach),
                SoldierSightUnits = Units(soldierSight),
                EnemyArmyFollows = enemyArmyFollows,
                EnemySquads = enemySquads,
                TruceTicks = Ticks(enemy.truce),
                SquadMin = Mathf.Max(1, enemy.squadMin),
                SquadMax = Mathf.Max(1, enemy.squadMax),
                FirstWaveTicks = Ticks(enemy.firstWave),
                WaveEveryTicks = Ticks(enemy.waveEvery),
                EnemyAlarm = enemy.alarm,
                WaveSquads = Mathf.Max(1, enemy.waveSquads),
                WaveWarnTicks = Ticks(enemy.waveWarn),
                EnemyArmySlack = enemy.armySlack,
                EnemyArmyFloor = enemy.armyFloor,
                DefendUnits = Units(enemy.defend),
                HomeReachUnits = Units(enemy.homeReach),
                FollowerSightUnits = Units(followerSight),
                SoldierLeashUnits = Units(soldierLeash),
                SlotSpacingUnits = Units(slotSpacing),
                CaptureSpacingCells = captureSpacing,
                SoldierSpacingUnits = Units(soldierSpacing),
                SoldierBodyUnits = Units(soldierBody),
                BarracksHalfUnits = Units(barracksHalfSize),
                BuildReachUnits = Units(buildReach),

                HeroHealth = heroHealth,
                EnemyHeroHealth = enemyHeroHealth,
                PlayerHeroHealAfterTicks = Mathf.RoundToInt(heroHealAfter * ticksPerSecond),
                HealAfterTicks = Mathf.RoundToInt(healAfter * ticksPerSecond),
                PlayerHeroHealEveryTicks = Ticks(heroHealEvery),
                HealEveryTicks = Ticks(healEvery),
                MeleeReachUnits = Units(meleeReach),
                MeleeDamage = meleeDamage,
                MeleeCooldownTicks = Ticks(meleeEvery),
                HeroGun = heroGun,
                GunReachUnits = Units(gunReach),
                GunDamage = gunDamage,
                GunCooldownTicks = Ticks(gunEvery),
                RespawnTicks = Ticks(respawnSeconds),
                InvulnerableTicks = Ticks(invulnerableSeconds),

                BaseHealth = baseHealth,
                BaseAuraCells = baseAura,
                BaseHalfUnits = Units(baseHalfSize),
                BaseGateUnits = Units(baseGate),

                EnemyHero = true,
                EnemySpeedPermille = Mathf.RoundToInt(enemy.speed * 1000f),
                EnemyReactionTicks = Ticks(enemy.reaction),
                EnemyBuildFromTicks = Mathf.RoundToInt(enemy.buildFrom * ticksPerSecond),
                EnemyMineCooldownTicks = PaceTicks(enemy.mineEvery, mineEvery),
                EnemyHireEveryTicks = PaceTicks(enemy.hireEvery, hireEvery),
                CommanderSightUnits = Units(commanderSight),
                CommanderStuckTicks = Ticks(commanderStuck),
                CommanderRetreatPermille = Mathf.RoundToInt(commanderRetreat * 10f),
                CommanderAttackArmy = commanderAttackArmy,
                CommanderAttackPermille = Mathf.RoundToInt(commanderAttackShare * 10f),
            };
        }

        private int Units(float meters) => Mathf.RoundToInt(meters * BattleSettings.UnitsPerCell / cellSize);

        private int Ticks(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * ticksPerSecond));

        /// <summary>The pace of the enemy in ticks; a level without its own (0) takes the player's.</summary>
        private int PaceTicks(float enemySeconds, float playerSeconds) => Ticks(enemySeconds > 0f ? enemySeconds : playerSeconds);
    }
}
