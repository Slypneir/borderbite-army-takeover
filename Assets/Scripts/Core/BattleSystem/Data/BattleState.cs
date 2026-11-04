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
