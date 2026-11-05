using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Core.BattleSystem.Utils;

namespace Core.BattleSystem.Data
{
    /// <summary>
    /// Everything a battle is at a tick; the rules (Services) change it, the simulation orders them. Lists keep the order things
    /// appeared in: every loop over them is the same on every run.
    /// </summary>
    public sealed class BattleState
    {
        public readonly BattleSettings Settings;
        public readonly CellGrid Grid;
        public readonly List<HeroState> Heroes = new List<HeroState>();
        public readonly List<SoldierState> Soldiers = new List<SoldierState>();
        public readonly List<ResourceState> Resources = new List<ResourceState>();
        public readonly SiteState[] Sites;
        public readonly SideState[] Sides = new SideState[3];
        /// <summary>The bases by side: [Player] and [Enemy]; [Neutral] stays empty.</summary>
        public readonly BaseState[] Bases = new BaseState[3];
        public readonly List<BattleEvent> Events = new List<BattleEvent>();
        public readonly List<Capture> Captures = new List<Capture>();
        /// <summary>The squads of the enemy (task 4.27), in the order they were made.</summary>
        public readonly List<SquadState> Squads = new List<SquadState>();
        public int LastSquadId;
        /// <summary>The tick the next wave leaves; its sign shows <see cref="BattleSettings.WaveWarnTicks"/> before.</summary>
        public int NextWaveTick;
        /// <summary>The squads of the next wave are chosen and gather; the sign points to them.</summary>
        public bool WaveAnnounced;
        public int WaveFromX;
        public int WaveFromY;
        /// <summary>The player's hero was alive on the last tick: its fall sends the enemy home.</summary>
        public bool PlayerHeroWasAlive = true;
        public DeterministicRandom Random;
        public int Tick;
        public BattleResult Result;
        private int _nextId;

        public BattleState(BattleSettings settings)
        {
            Settings = settings;
            // a battle with no map of its own is fought on the square island of the settings
            settings.Map ??= BattleMap.Square(settings);
            settings.MapCells = settings.Map.Size;
            Map = settings.Map;
            Grid = new CellGrid(Map.Size, Map.Water);
            Random = new DeterministicRandom(settings.Seed);
            Sites = new SiteState[Map.Sites.Count];
            for (var i = 0; i < Sites.Length; i++)
                Sites[i] = new SiteState { CenterX = Map.Sites[i].X, CenterY = Map.Sites[i].Y };
        }

        public BattleMap Map { get; }

        public int NextId() => ++_nextId;

        public void Emit(EventKind kind, Side side, int id, int value, int x, int y, int toX = 0, int toY = 0, int detail = 0) =>
            Events.Add(new BattleEvent(Tick, kind, side, id, value, x, y, toX, toY, detail));

        /// <summary>The hero of a side, or -1 when the side has none.</summary>
        public int HeroOf(Side side)
        {
            for (var i = 0; i < Heroes.Count; i++)
            {
                if (Heroes[i].Side == side)
                    return i;
            }

            return -1;
        }

        public int ArmyOf(Side side)
        {
            var count = 0;
            foreach (var soldier in Soldiers)
            {
                if (soldier.Side == side)
                    count++;
            }

            return count;
        }

        public static Side Enemy(Side side) => side == Side.Player ? Side.Enemy : Side.Player;

        /// <summary>The first seconds of a level: no unit of the enemy steps on the player's land (task 4.27).</summary>
        public bool Truce => Settings.EnemySquads && Tick <= Settings.TruceTicks;

        /// <summary>
        /// The player's hero is dead or still unhurtable after coming back: the enemy spares the player's base and barracks
        /// meanwhile (task 4.27).
        /// </summary>
        public bool PlayerHeroDown
        {
            get
            {
                var index = HeroOf(Side.Player);
                return index >= 0 && (!Heroes[index].Alive || Heroes[index].InvulnerableUntil > Tick);
            }
        }

        /// <summary>
        /// The most soldiers a side may have: 30, and the enemy's at most the player's army and the slack, never under the floor
        /// (task 4.27: fall behind — it waits, catch up — it grows).
        /// </summary>
        public int ArmyCapOf(Side side)
        {
            if (side != Side.Enemy || !Settings.EnemySquads)
                return Settings.ArmyMax;

            var cap = System.Math.Max(Settings.EnemyArmyFloor, ArmyOf(Side.Player) + Settings.EnemyArmySlack);
            return System.Math.Min(Settings.ArmyMax, cap);
        }

        /// <summary>
        /// A body that stepped from a cell not of <paramref name="colour"/> onto one of it goes back along the axis that did it,
        /// so it slides along the border; one already standing on that colour walks freely (it is on its way out).
        /// </summary>
        public void KeepOff(ref int x, ref int y, int fromX, int fromY, Side colour)
        {
            if (ColourAt(x, y) != colour || ColourAt(fromX, fromY) == colour)
                return;

            if (ColourAt(x, fromY) != colour)
                y = fromY;
            else if (ColourAt(fromX, y) != colour)
                x = fromX;
            else
            {
                x = fromX;
                y = fromY;
            }
        }

        /// <summary>The colour of the cell under a point in units; off the map — neutral.</summary>
        public Side ColourAt(int x, int y)
        {
            var cx = x / BattleSettings.UnitsPerCell;
            var cy = y / BattleSettings.UnitsPerCell;
            return cx < 0 || cy < 0 || cx >= Grid.Size || cy >= Grid.Size ? Side.Neutral : Grid.At(cx, cy);
        }

        /// <summary>The middle of a cell in units.</summary>
        public static int Middle(int cell) => cell * BattleSettings.UnitsPerCell + BattleSettings.UnitsPerCell / 2;

        /// <summary>A body of <paramref name="radius"/> at a point in units stands on land: no cell under the corners of its square or its middle is water.</summary>
        public bool Dry(int x, int y, int radius)
        {
            if (!Grid.HasWater)
                return true;

            const int unit = BattleSettings.UnitsPerCell;
            var x0 = (x - radius) / unit;
            var x1 = (x + radius) / unit;
            var y0 = (y - radius) / unit;
            var y1 = (y + radius) / unit;
            return !Grid.IsWaterAt(x0, y0) && !Grid.IsWaterAt(x1, y0) && !Grid.IsWaterAt(x0, y1) && !Grid.IsWaterAt(x1, y1) &&
                   !Grid.IsWaterAt(x / unit, y / unit);
        }

        /// <summary>
        /// A body that moved from a dry point into water goes back along the axis that wet it, so it slides along the shore; back
        /// to where it was when both do. Nothing changes on a map without water.
        /// </summary>
        public void Settle(ref int x, ref int y, int fromX, int fromY, int radius)
        {
            if (Dry(x, y, radius))
                return;

            if (Dry(x, fromY, radius))
                y = fromY;
            else if (Dry(fromX, y, radius))
                x = fromX;
            else
            {
                x = fromX;
                y = fromY;
            }
        }

        public static long Distance2(int x0, int y0, int x1, int y1)
        {
            long dx = x1 - x0;
            long dy = y1 - y0;
            return dx * dx + dy * dy;
        }
    }
}
