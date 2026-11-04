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
    /// A tick: commands → the commanders choose where their heroes walk → dead heroes come back, blows and shots → heroes walk →
    /// soldiers walk → pickups, new piles, building, hiring, healing → brushes paint (the enemy first, so a cell under both
    /// brushes is the player's) → enclosure for the player → for the enemy → a fallen base or the shares decide the result.
    /// </summary>
    public sealed class BattleSimulation
    {
        private const ulong Prime = 1099511628211UL;

        private readonly BattleState _state;
        private readonly List<BattleCommand> _commands = new List<BattleCommand>();
        private readonly List<int[]> _taken = new List<int[]>();
        private readonly List<int> _lost = new List<int>();
        private readonly Commander _enemyCommander;
        private readonly Commander _playerCommander;
        private readonly EnclosureFinder _enclosure;
        private readonly EconomyRules _economy;
        private readonly ArmyRules _army;
        private readonly CombatRules _combat;

        public BattleSimulation(BattleSettings settings)
        {
            _state = new BattleState(settings);
            _enclosure = new EnclosureFinder(Grid.Total);
            _economy = new EconomyRules(_state);
            _army = new ArmyRules(_state);
            _combat = new CombatRules(_state, _economy);

            var near = settings.StartOffsetCells;
            var far = settings.MapCells - settings.StartOffsetCells;
            PaintStart(near, near, Side.Player);
            PaintStart(far, far, Side.Enemy);
            // the bases face the middle of the map across it: the gate of the player's looks east, the enemy's west, so a hero at
            // its gate is never behind the walls for the camera from the south
            PlaceBase(Side.Player, near * BattleSettings.UnitsPerCell, settings.BaseGateUnits);
            PlaceBase(Side.Enemy, far * BattleSettings.UnitsPerCell, -settings.BaseGateUnits);
            AddHero(Side.Player);
            if (settings.EnemyHero)
            {
                AddHero(Side.Enemy);
                _enemyCommander = new Commander(_state, Side.Enemy);
            }

            _playerCommander = new Commander(_state, Side.Player);
            _state.Sides[(int)Side.Player].Gold = settings.PlayerStartGold;
            _economy.LayStartPiles();
        }

        /// <summary>The number of ticks already run.</summary>
        public int Tick => _state.Tick;

        public BattleSettings Settings => _state.Settings;

        public CellGrid Grid => _state.Grid;

        public IReadOnlyList<HeroState> Heroes => _state.Heroes;

        public IReadOnlyList<SoldierState> Soldiers => _state.Soldiers;

        public IReadOnlyList<ResourceState> Resources => _state.Resources;

        public IReadOnlyList<SiteState> Sites => _state.Sites;

        /// <summary>The bases by side: [Side.Player] and [Side.Enemy].</summary>
        public IReadOnlyList<BaseState> Bases => _state.Bases;

        /// <summary>The priority the enemy commander follows now (GDD 5.8), 0 when none; for the developer.</summary>
        public int EnemyPriority => _enemyCommander?.Priority ?? 0;
        /// <summary>Every area taken by enclosure since the start, in order.</summary>
        public IReadOnlyList<Capture> Captures => _state.Captures;

        /// <summary>Every pickup, blow, death and building since the start, in order.</summary>
        public IReadOnlyList<BattleEvent> Events => _state.Events;

        public BattleResult Result => _state.Result;

        public SideState SideOf(Side side) => _state.Sides[(int)side];

        public int ArmyOf(Side side) => _state.ArmyOf(side);

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

            _state.Tick++;
            ApplyCommands();
            _enemyCommander?.Step(_state.Settings.EnemyReactionTicks);
            if (_state.Settings.PlayerAutopilot)
                _playerCommander.Step(_state.Settings.EnemyReactionTicks);
            _combat.Step();
            MoveHeroes();
            _army.Step();
            _economy.Step();
            PaintBrushes(Side.Enemy);
            PaintBrushes(Side.Player);
            if (Tick % _state.Settings.EnclosureEveryTicks == 0)
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
        /// The developer gives a side cells until it has at least <paramref name="permille"/> of the map: rows from its own edge (the
        /// bottom for the player, the top for the enemy), neutral cells first, then the other side.
        /// </summary>
        public void FillShare(Side side, int permille)
        {
            var target = (int)(((long)Grid.Total * permille + 999) / 1000);
            Fill(side, target, Side.Neutral);
            Fill(side, target, side == Side.Player ? Side.Enemy : Side.Player);
        }

        /// <summary>The developer ends the battle at once.</summary>
        public void ForceResult(BattleResult result) => _state.Result = result;

        /// <summary>The developer adds gold and wood to a side.</summary>
        public void Give(Side side, int gold, int wood)
        {
            _state.Sides[(int)side].Gold += gold;
            _state.Sides[(int)side].Wood += wood;
        }

        /// <summary>The developer puts soldiers of a side around a point in units; without a hero they keep to it.</summary>
        public void AddSoldiers(Side side, int count, int x, int y)
        {
            for (var i = 0; i < count; i++)
                ArmyRules.Spawn(_state, side, x + i % 5 * 1000, y + i / 5 * 1000);
        }

        /// <summary>
        /// The strength of the enemy of a level (GDD 5.9), at once and in the middle of a battle: the speed of its hero, its
        /// reaction, the health and damage of its new soldiers, from which tick it may build. The developer's switch (spec 2.7).
        /// </summary>
        public void SetEnemyStrength(int speedPermille, int reactionTicks, int soldierPermille, int buildFromTicks)
        {
            _state.Settings.EnemySpeedPermille = speedPermille;
            _state.Settings.EnemyReactionTicks = reactionTicks;
            _state.Settings.EnemySoldierPermille = soldierPermille;
            _state.Settings.EnemyBuildFromTicks = buildFromTicks;
        }

        /// <summary>The developer sets the health of a base, tenths; 0 brings it down, and the battle ends on the next tick.</summary>
        public void SetBaseHealth(Side side, int health)
        {
            _combat.HitBase(side, _state.Bases[(int)side].Health - health);
        }

        /// <summary>The developer puts the hero of a side at a point in units, standing.</summary>
        public void TeleportHero(Side side, int x, int y)
        {
            var index = _state.HeroOf(side);
            if (index < 0)
                return;

            var hero = _state.Heroes[index];
            hero.X = x;
            hero.Y = y;
            hero.DirectionX = 0;
            hero.DirectionY = 0;
            _state.Heroes[index] = hero;
        }

        /// <summary>The developer builds a barracks of a side on a place at once, free.</summary>
        public void BuildBarracks(int site, Side side)
        {
            ref var place = ref _state.Sites[site];
            Grid.Set(place.CenterY * Grid.Size + place.CenterX, side);
            _economy.BuildBarracks(site, side);
        }

        /// <summary>FNV-1a of the whole state: two runs are the same exactly when their hashes match at every tick.</summary>
        public ulong ComputeHash()
        {
            var hash = 14695981039346656037UL;
            hash = Mix(hash, Tick);
            hash = Mix(hash, (int)_state.Random.State);
            hash = Mix(hash, (int)Result);
            foreach (var hero in _state.Heroes)
            {
                hash = Mix(hash, hero.X);
                hash = Mix(hash, hero.Y);
                hash = Mix(hash, hero.DirectionX);
                hash = Mix(hash, hero.DirectionY);
                hash = Mix(hash, hero.Health);
                hash = Mix(hash, hero.RespawnTick);
            }

            foreach (var soldier in _state.Soldiers)
            {
                hash = Mix(hash, soldier.Id);
                hash = Mix(hash, soldier.X);
                hash = Mix(hash, soldier.Y);
                hash = Mix(hash, soldier.Health);
            }

            foreach (var resource in _state.Resources)
                hash = Mix(hash, resource.Cell * 2 + (int)resource.Kind);

            foreach (var place in _state.Bases)
                hash = Mix(hash, place.Health);

            foreach (var site in _state.Sites)
            {
                hash = Mix(hash, (int)site.Barracks);
                hash = Mix(hash, site.Health);
                hash = Mix(hash, site.BuildProgress);
            }

            foreach (var side in _state.Sides)
            {
                hash = Mix(hash, side.Gold);
                hash = Mix(hash, side.Wood);
            }

            for (var i = 0; i < Grid.Total; i++)
                hash = (hash ^ (byte)Grid[i]) * Prime;

            return hash;
        }

        private void PlaceBase(Side side, int middle, int gate)
        {
            var settings = _state.Settings;
            _state.Bases[(int)side] = new BaseState
            {
                X = middle,
                Y = middle,
                Health = settings.BaseHealth,
                GateX = middle + gate,
                GateY = middle,
            };
            _economy.ProtectBase(side);
        }

        private void AddHero(Side side)
        {
            var place = _state.Bases[(int)side];
            _state.Heroes.Add(new HeroState
            {
                Side = side,
                X = place.GateX,
                Y = place.GateY,
                HomeX = place.GateX,
                HomeY = place.GateY,
                FacingX = side == Side.Player ? 1 : -1,
                Health = _state.Settings.HeroHealthOf(side),
            });
        }

        private void PaintStart(int centerX, int centerY, Side side)
        {
            // a cell is in the circle when its middle is: (x + 0.5 - c)^2 + (y + 0.5 - c)^2 <= r^2, doubled to stay in integers
            var radius = _state.Settings.StartRadiusCells;
            for (var y = 0; y < Grid.Size; y++)
            for (var x = 0; x < Grid.Size; x++)
            {
                var dx = 2 * x + 1 - 2 * centerX;
                var dy = 2 * y + 1 - 2 * centerY;
                if (dx * dx + dy * dy <= 4 * radius * radius)
                    Grid.Set(y * Grid.Size + x, side);
            }
        }

        private void ApplyCommands()
        {
            while (_commands.Count > 0 && _commands[0].Tick == Tick)
            {
                var command = _commands[0];
                _commands.RemoveAt(0);
                for (var i = 0; i < _state.Heroes.Count; i++)
                {
                    if (_state.Heroes[i].Side != command.Side || (command.Side == Side.Player && _state.Settings.PlayerAutopilot))
                        continue;

                    var hero = _state.Heroes[i];
                    hero.DirectionX = IntMath.Clamp(command.DirectionX, -BattleCommand.MaxAxis, BattleCommand.MaxAxis);
                    hero.DirectionY = IntMath.Clamp(command.DirectionY, -BattleCommand.MaxAxis, BattleCommand.MaxAxis);
                    if (hero.Moving)
                    {
                        hero.FacingX = hero.DirectionX;
                        hero.FacingY = hero.DirectionY;
                    }

                    _state.Heroes[i] = hero;
                }
            }
        }

        private void MoveHeroes()
        {
            var settings = _state.Settings;
            var min = settings.HeroRadiusUnits;
            var max = settings.MapUnits - settings.HeroRadiusUnits;
            for (var i = 0; i < _state.Heroes.Count; i++)
            {
                var hero = _state.Heroes[i];
                if (!hero.Alive || !hero.Moving)
                    continue;

                // the same step in every direction: the direction is scaled to the step, its own length does not count; the enemy's
                // hero walks at the speed of the level
                var step = hero.Side == Side.Enemy ? (int)((long)settings.HeroStepUnits * settings.EnemySpeedPermille / 1000) : settings.HeroStepUnits;
                var length = IntMath.Sqrt((long)hero.DirectionX * hero.DirectionX + (long)hero.DirectionY * hero.DirectionY);
                hero.X = IntMath.Clamp(hero.X + (int)(hero.DirectionX * (long)step / length), min, max);
                hero.Y = IntMath.Clamp(hero.Y + (int)(hero.DirectionY * (long)step / length), min, max);
                _state.Heroes[i] = hero;
            }
        }

        private void PaintBrushes(Side side)
        {
            foreach (var hero in _state.Heroes)
            {
                if (hero.Side == side && hero.Alive)
                    PaintCircle(hero.X, hero.Y, _state.Settings.HeroBrushUnits + (side == Side.Player ? _state.Settings.PlayerBrushBonusUnits : 0), side);
            }

            _army.Paint(side, PaintCircle);
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
            _lost.Clear();
            _enclosure.Enclose(Grid, side, _state.Settings.MaxEnclosureCells, _taken, _lost);
            for (var i = 0; i < _taken.Count; i++)
                _state.Captures.Add(CaptureOf(side, _taken[i], _lost[i]));
        }

        private Capture CaptureOf(Side side, int[] cells, int fromEnemy)
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
            foreach (var hero in _state.Heroes)
            {
                if (hero.Side != side || !hero.Alive)
                    continue;

                originX = hero.X;
                originY = hero.Y;
            }

            return new Capture(Tick, side, cells, originX, originY, centerX, centerY, Grid.Total, fromEnemy);
        }

        private void Decide()
        {
            if (_state.Result != BattleResult.None)
                return;

            var victory = _state.Settings.VictoryPermille;
            if (!_state.Bases[(int)Side.Enemy].Standing)
                _state.Result = BattleResult.Victory;
            else if (!_state.Bases[(int)Side.Player].Standing)
                _state.Result = BattleResult.Defeat;
            else if (Grid.Permille(Side.Player) >= victory)
                _state.Result = BattleResult.Victory;
            else if (Grid.Permille(Side.Enemy) >= victory)
                _state.Result = BattleResult.Defeat;
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
