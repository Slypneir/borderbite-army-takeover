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
            Grid = new CellGrid(settings.MapCells);
            Random = new DeterministicRandom(settings.Seed);
            Sites = new SiteState[settings.SiteCells.Length / 2];
            for (var i = 0; i < Sites.Length; i++)
                Sites[i] = new SiteState { CenterX = settings.SiteCells[i * 2], CenterY = settings.SiteCells[i * 2 + 1] };
        }

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

        public static long Distance2(int x0, int y0, int x1, int y1)
        {
            long dx = x1 - x0;
            long dy = y1 - y0;
            return dx * dx + dy * dy;
        }
    }
}
