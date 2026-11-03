using System.Collections.Generic;
using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Core.BattleSystem.Utils;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// The battle: integers only, one call of <see cref="Step"/> is one tick, commands wait for their tick. The same settings
    /// and commands give the same <see cref="ComputeHash"/> at every tick on any device and frame rate.
    /// A tick: commands → heroes walk → brushes paint (the enemy first, so a cell under both brushes is the player's) →
    /// enclosure for the player → for the enemy → the shares decide the result.
    /// </summary>
    public sealed class BattleSimulation
    {
        private const ulong Prime = 1099511628211UL;

        private readonly BattleSettings _settings;
        private readonly List<BattleCommand> _commands = new List<BattleCommand>();
        private readonly List<HeroState> _heroes = new List<HeroState>();
        private readonly List<Capture> _captures = new List<Capture>();
        private readonly List<int[]> _taken = new List<int[]>();
        private readonly EnclosureFinder _enclosure;
        private DeterministicRandom _random;

        public BattleSimulation(BattleSettings settings)
        {
            _settings = settings;
            _random = new DeterministicRandom(settings.Seed);
            Grid = new CellGrid(settings.MapCells);
            _enclosure = new EnclosureFinder(Grid.Total);

            var near = settings.StartOffsetCells;
            var far = settings.MapCells - settings.StartOffsetCells;
            PaintStart(near, near, Side.Player);
            PaintStart(far, far, Side.Enemy);
            _heroes.Add(new HeroState
            {
                Side = Side.Player,
                X = near * BattleSettings.UnitsPerCell,
                Y = near * BattleSettings.UnitsPerCell,
                FacingY = 1,
            });
        }

        /// <summary>The number of ticks already run.</summary>
        public int Tick { get; private set; }

        public BattleSettings Settings => _settings;

        public CellGrid Grid { get; }

        public IReadOnlyList<HeroState> Heroes => _heroes;

        /// <summary>Every area taken by enclosure since the start, in order.</summary>
        public IReadOnlyList<Capture> Captures => _captures;

        public BattleResult Result { get; private set; }

        /// <summary>Queues a command for a future tick; a command for a past tick is refused, or two runs could differ.</summary>
        public bool Enqueue(BattleCommand command)
        {
            if (command.Tick <= Tick)
                return false;

            var index = _commands.Count;
            while (index > 0 && _commands[index - 1].Tick > command.Tick)
                index--;

            _commands.Insert(index, command);
            return true;
        }

        public void Step()
        {
            if (Result != BattleResult.None)
                return;

            Tick++;
            ApplyCommands();
            MoveHeroes();
            PaintBrushes(Side.Enemy);
            PaintBrushes(Side.Player);
            if (Tick % _settings.EnclosureEveryTicks == 0)
            {
                Enclose(Side.Player);
                Enclose(Side.Enemy);
            }

            Decide();
        }

        /// <summary>The developer paints a rectangle of cells, both corners included; no enclosure until the next tick.</summary>
        public void PaintRect(Side side, int x0, int y0, int x1, int y1)
        {
            for (var y = IntMath.Clamp(System.Math.Min(y0, y1), 0, Grid.Size - 1); y <= IntMath.Clamp(System.Math.Max(y0, y1), 0, Grid.Size - 1); y++)
            for (var x = IntMath.Clamp(System.Math.Min(x0, x1), 0, Grid.Size - 1); x <= IntMath.Clamp(System.Math.Max(x0, x1), 0, Grid.Size - 1); x++)
                Grid.Paint(y * Grid.Size + x, side);
        }

        /// <summary>
        /// The developer gives a side cells until it has at least <paramref name="permille"/> of the map: rows from its own edge (the bottom
        /// for the player, the top for the enemy), neutral cells first, then the other side.
        /// </summary>
        public void FillShare(Side side, int permille)
        {
            var target = (int)(((long)Grid.Total * permille + 999) / 1000);
            Fill(side, target, Side.Neutral);
            Fill(side, target, side == Side.Player ? Side.Enemy : Side.Player);
        }

        /// <summary>The developer ends the battle at once.</summary>
        public void ForceResult(BattleResult result) => Result = result;

        /// <summary>FNV-1a of the whole state: two runs are the same exactly when their hashes match at every tick.</summary>
        public ulong ComputeHash()
        {
            var hash = 14695981039346656037UL;
            hash = Mix(hash, Tick);
            hash = Mix(hash, (int)_random.State);
            hash = Mix(hash, (int)Result);
            for (var i = 0; i < _heroes.Count; i++)
            {
                hash = Mix(hash, _heroes[i].X);
                hash = Mix(hash, _heroes[i].Y);
                hash = Mix(hash, _heroes[i].DirectionX);
                hash = Mix(hash, _heroes[i].DirectionY);
            }

            for (var i = 0; i < Grid.Total; i++)
                hash = (hash ^ (byte)Grid[i]) * Prime;

            return hash;
        }

        private void PaintStart(int centerX, int centerY, Side side)
        {
            // a cell is in the circle when its middle is: (x + 0.5 - c)^2 + (y + 0.5 - c)^2 <= r^2, doubled to stay in integers
            var radius2 = 4 * _settings.StartRadiusCells * _settings.StartRadiusCells;
            for (var y = 0; y < Grid.Size; y++)
            for (var x = 0; x < Grid.Size; x++)
            {
                var dx = 2 * x + 1 - 2 * centerX;
                var dy = 2 * y + 1 - 2 * centerY;
                if (dx * dx + dy * dy <= radius2)
                    Grid.Set(y * Grid.Size + x, side);
            }
        }

        private void ApplyCommands()
        {
            while (_commands.Count > 0 && _commands[0].Tick == Tick)
            {
                var command = _commands[0];
                _commands.RemoveAt(0);
                for (var i = 0; i < _heroes.Count; i++)
                {
                    if (_heroes[i].Side != command.Side)
                        continue;

                    var hero = _heroes[i];
                    hero.DirectionX = IntMath.Clamp(command.DirectionX, -BattleCommand.MaxAxis, BattleCommand.MaxAxis);
                    hero.DirectionY = IntMath.Clamp(command.DirectionY, -BattleCommand.MaxAxis, BattleCommand.MaxAxis);
                    if (hero.Moving)
                    {
                        hero.FacingX = hero.DirectionX;
                        hero.FacingY = hero.DirectionY;
                    }

                    _heroes[i] = hero;
                }
            }
        }

        private void MoveHeroes()
        {
            var min = _settings.HeroRadiusUnits;
            var max = _settings.MapUnits - _settings.HeroRadiusUnits;
            for (var i = 0; i < _heroes.Count; i++)
            {
                var hero = _heroes[i];
                if (!hero.Moving)
                    continue;

                // the same step in every direction: the direction is scaled to the step, its own length does not count
                var length = IntMath.Sqrt((long)hero.DirectionX * hero.DirectionX + (long)hero.DirectionY * hero.DirectionY);
                hero.X = IntMath.Clamp(hero.X + (int)(hero.DirectionX * (long)_settings.HeroStepUnits / length), min, max);
                hero.Y = IntMath.Clamp(hero.Y + (int)(hero.DirectionY * (long)_settings.HeroStepUnits / length), min, max);
                _heroes[i] = hero;
            }
        }

        private void PaintBrushes(Side side)
        {
            for (var i = 0; i < _heroes.Count; i++)
            {
                if (_heroes[i].Side == side)
                    PaintCircle(_heroes[i].X, _heroes[i].Y, _settings.HeroBrushUnits, side);
            }
        }

        /// <summary>Every cell whose middle is within <paramref name="radius"/> units of the point becomes the side's.</summary>
        private void PaintCircle(int x, int y, int radius, Side side)
        {
            const int unit = BattleSettings.UnitsPerCell;
            var radius2 = (long)radius * radius;
            var x0 = IntMath.Clamp((x - radius) / unit, 0, Grid.Size - 1);
            var x1 = IntMath.Clamp((x + radius) / unit, 0, Grid.Size - 1);
            var y0 = IntMath.Clamp((y - radius) / unit, 0, Grid.Size - 1);
            var y1 = IntMath.Clamp((y + radius) / unit, 0, Grid.Size - 1);
            for (var cy = y0; cy <= y1; cy++)
            for (var cx = x0; cx <= x1; cx++)
            {
                long dx = cx * unit + unit / 2 - x;
                long dy = cy * unit + unit / 2 - y;
                if (dx * dx + dy * dy <= radius2)
                    Grid.Paint(cy * Grid.Size + cx, side);
            }
        }

        private void Enclose(Side side)
        {
            _taken.Clear();
            var maxCells = (int)((long)Grid.Total * _settings.MaxEnclosurePermille / 1000);
            _enclosure.Enclose(Grid, side, maxCells, _taken);
            foreach (var cells in _taken)
                _captures.Add(CaptureOf(side, cells));
        }

        private Capture CaptureOf(Side side, int[] cells)
        {
            long sumX = 0;
            long sumY = 0;
            foreach (var index in cells)
            {
                sumX += index % Grid.Size;
                sumY += index / Grid.Size;
            }

            const int unit = BattleSettings.UnitsPerCell;
            var centerX = (int)(sumX * unit / cells.Length) + unit / 2;
            var centerY = (int)(sumY * unit / cells.Length) + unit / 2;
            var originX = centerX;
            var originY = centerY;
            foreach (var hero in _heroes)
            {
                if (hero.Side != side)
                    continue;

                originX = hero.X;
                originY = hero.Y;
            }

            return new Capture(Tick, side, cells, originX, originY, centerX, centerY, Grid.Total);
        }

        private void Decide()
        {
            if (Grid.Permille(Side.Player) >= _settings.VictoryPermille)
                Result = BattleResult.Victory;
            else if (Grid.Permille(Side.Enemy) >= _settings.VictoryPermille)
                Result = BattleResult.Defeat;
        }

        private void Fill(Side side, int target, Side from)
        {
            var size = Grid.Size;
            for (var row = 0; row < size && Grid.Count(side) < target; row++)
            {
                var y = side == Side.Player ? row : size - 1 - row;
                for (var x = 0; x < size && Grid.Count(side) < target; x++)
                {
                    var index = y * size + x;
                    if (Grid[index] == from)
                        Grid.Paint(index, side);
                }
            }
        }

        private static ulong Mix(ulong hash, int value)
        {
            var bits = (uint)value;
            for (var i = 0; i < 4; i++)
            {
                hash ^= (bits >> (i * 8)) & 0xFF;
                hash *= Prime;
            }

            return hash;
        }
    }
}
