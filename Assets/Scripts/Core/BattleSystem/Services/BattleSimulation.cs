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
    /// soldiers walk → standing buildings push them out → pickups, new piles, building, hiring, healing → brushes paint (the enemy first, so a cell under both
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
        private readonly OrphanFinder _orphans;
        private readonly List<int[]> _dropped = new List<int[]>();
        private readonly EconomyRules _economy;
        private readonly ArmyRules _army;
        private readonly CombatRules _combat;
        private readonly BuildingColliders _colliders;
        private readonly BrushTrails _trails;

        public BattleSimulation(BattleSettings settings)
        {
            _state = new BattleState(settings);
            _enclosure = new EnclosureFinder(Grid.Total);
            _orphans = new OrphanFinder(Grid.Total);
            _economy = new EconomyRules(_state);
            _army = new ArmyRules(_state);
            _combat = new CombatRules(_state, _economy);
            _colliders = new BuildingColliders(_state);
            _trails = new BrushTrails(settings.BrushDelayTicks);

            var map = _state.Map;
            foreach (var side in new[] { Side.Player, Side.Enemy })
            {
                var place = map.Bases[(int)side];
                PaintStart(place.X, place.Y, place.StartRadius, side);
            }

            foreach (var side in new[] { Side.Player, Side.Enemy })
            {
                var place = map.Bases[(int)side];
                PlaceBase(side, place.X * BattleSettings.UnitsPerCell, place.Y * BattleSettings.UnitsPerCell, settings.BaseGateUnits);
            }

            AddHero(Side.Player);
            if (settings.EnemyHero)
            {
                AddHero(Side.Enemy);
                _enemyCommander = new Commander(_state, Side.Enemy);
            }

            _playerCommander = new Commander(_state, Side.Player);
            _state.Sides[(int)Side.Player].Gold = settings.PlayerStartGold;
            _state.NextWaveTick = settings.FirstWaveTicks;
            // a special level may start with barracks standing (layer 5 spec 2.3)
            for (var i = 0; i < map.Sites.Count; i++)
            {
                if (map.Sites[i].Barracks != Side.Neutral)
                    BuildBarracks(i, map.Sites[i].Barracks);
            }

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

        /// <summary>The squads of the enemy (task 4.27).</summary>
        public IReadOnlyList<SquadState> Squads => _state.Squads;

        /// <summary>
        /// The next wave of the enemy is chosen and leaves soon (task 4.27): where it gathers, in units — for the sign at the edge
        /// of the screen.
        /// </summary>
        /// <summary>The tick the next wave of the enemy leaves (task 4.27): the count of its sign.</summary>
        public int NextWaveTick => _state.NextWaveTick;

        public bool WaveComing(out int x, out int y)
        {
            x = _state.WaveFromX;
            y = _state.WaveFromY;
            return _state.WaveAnnounced;
        }

        /// <summary>The army of the enemy of a level (task 4.27): truce, squads, waves, the cap; at once, also in a running battle.</summary>
        public void SetEnemyArmy(int truceTicks, int squadMin, int squadMax, int firstWaveTicks, int waveEveryTicks, int waveSquads,
            int waveWarnTicks, int armySlack, int armyFloor, int defendUnits, int homeReachUnits, bool alarm)
        {
            var settings = _state.Settings;
            // a wave not yet announced keeps to the new first second while the battle has not passed it
            if (!_state.WaveAnnounced && _state.NextWaveTick == settings.FirstWaveTicks && Tick < firstWaveTicks)
                _state.NextWaveTick = firstWaveTicks;
            settings.FirstWaveTicks = firstWaveTicks;
            settings.EnemyAlarm = alarm;
            settings.TruceTicks = truceTicks;
            settings.SquadMin = System.Math.Max(1, squadMin);
            settings.SquadMax = System.Math.Max(settings.SquadMin, squadMax);
            settings.WaveEveryTicks = System.Math.Max(1, waveEveryTicks);
            settings.WaveSquads = System.Math.Max(1, waveSquads);
            settings.WaveWarnTicks = System.Math.Max(0, waveWarnTicks);
            settings.EnemyArmySlack = armySlack;
            settings.EnemyArmyFloor = armyFloor;
            settings.DefendUnits = defendUnits;
            settings.HomeReachUnits = homeReachUnits;
        }

        /// <summary>The priority the enemy commander follows now (GDD 5.8), 0 when none; for the developer.</summary>
        public int EnemyPriority => _enemyCommander?.Priority ?? 0;
        /// <summary>Every area taken by enclosure since the start, in order.</summary>
        public IReadOnlyList<Capture> Captures => _state.Captures;

        /// <summary>Every pickup, blow, death and building since the start, in order.</summary>
        public IReadOnlyList<BattleEvent> Events => _state.Events;

        public BattleResult Result => _state.Result;

        /// <summary>The side holding the second chance (task 4.36), Neutral while neither has reached the middle of the map.</summary>
        public Side SecondChance => _state.ChanceHolder;

        public SideState SideOf(Side side) => _state.Sides[(int)side];

        public int ArmyOf(Side side) => _state.ArmyOf(side);

        /// <summary>The order the army of a side follows now (spec 3.8).</summary>
        public ArmyOrder OrderOf(Side side) => _state.Sides[(int)side].Order;

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
            {
                _playerCommander.Step(_state.Settings.EnemyReactionTicks);
                // the autopilot gathers its army before it lets it attack on its own (a tool of the runs)
                var gather = _state.ArmyOf(Side.Player) < _state.Settings.AutopilotRallyBelow;
                _army.SetOrder(Side.Player, gather ? ArmyOrder.Rally : ArmyOrder.Free, false);
            }
            _combat.Step();
            MoveHeroes();
            _army.Step();
            _colliders.Step();
            _economy.Step();
            RecordTrails();
            PaintBrushes(Side.Enemy);
            PaintBrushes(Side.Player);
            if (Tick % _state.Settings.EnclosureEveryTicks == 0)
            {
                Enclose(Side.Player);
                Enclose(Side.Enemy);
                DropCutOff(Side.Enemy);
                DropCutOff(Side.Player);
            }

            Decide();
        }

        /// <summary>
        /// The developer announces a wave gathering at a cell, leaving in the warning time (its sign and count show); no squad
        /// gathers, so none leaves.
        /// </summary>
        public void AnnounceWave(int cellX, int cellY)
        {
            _state.WaveAnnounced = true;
            _state.WaveFromX = cellX * BattleSettings.UnitsPerCell + BattleSettings.UnitsPerCell / 2;
            _state.WaveFromY = cellY * BattleSettings.UnitsPerCell + BattleSettings.UnitsPerCell / 2;
            _state.NextWaveTick = _state.Tick + _state.Settings.WaveWarnTicks;
        }

        /// <summary>The developer paints a rectangle of cells, both corners included; no enclosure until the next tick.</summary>
        public void PaintRect(Side side, int x0, int y0, int x1, int y1)
        {
            for (var y = IntMath.Clamp(System.Math.Min(y0, y1), 0, Grid.Height - 1); y <= IntMath.Clamp(System.Math.Max(y0, y1), 0, Grid.Height - 1); y++)
            for (var x = IntMath.Clamp(System.Math.Min(x0, x1), 0, Grid.Width - 1); x <= IntMath.Clamp(System.Math.Max(x0, x1), 0, Grid.Width - 1); x++)
                Grid.Paint(y * Grid.Width + x, side);
        }

        /// <summary>
        /// The developer gives a side cells until it has at least <paramref name="permille"/> of the map: rows from its own edge (the
        /// bottom for the player, the top for the enemy), neutral cells first, then the other side.
        /// </summary>
        public void FillShare(Side side, int permille)
        {
            var target = (int)(((long)Grid.LandTotal * permille + 999) / 1000);
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

        /// <summary>The developer puts soldiers of a side around a point in units; they go to attack at once.</summary>
        public void AddSoldiers(Side side, int count, int x, int y)
        {
            for (var i = 0; i < count; i++)
                ArmyRules.Spawn(_state, side, x + i % 5 * 1000, y + i / 5 * 1000);
        }

        /// <summary>
        /// The strength of the enemy of a level (GDD 5.9), at once and in the middle of a battle: the speed of its hero, its
        /// reaction, from which tick it may build. The developer's switch (spec 2.7). The factor of its soldiers is off while
        /// health is whole points (owner, 4 November).
        /// </summary>
        public void SetEnemyStrength(int speedPermille, int reactionTicks, int buildFromTicks)
        {
            _state.Settings.EnemySpeedPermille = speedPermille;
            _state.Settings.EnemyReactionTicks = reactionTicks;
            _state.Settings.EnemyBuildFromTicks = buildFromTicks;
        }

        /// <summary>The pace of the enemy of a level (task 4.23): a blow of mining and a hire every so many ticks.</summary>
        public void SetEnemyPace(int mineCooldownTicks, int hireEveryTicks)
        {
            _state.Settings.EnemyMineCooldownTicks = System.Math.Max(1, mineCooldownTicks);
            _state.Settings.EnemyHireEveryTicks = System.Math.Max(1, hireEveryTicks);
        }

        /// <summary>The developer sets the health of a base, points; 0 brings it down, and the battle ends on the next tick.</summary>
        public void SetBaseHealth(Side side, int health)
        {
            _combat.HitBase(side, _state.Bases[(int)side].Health - health);
        }

        /// <summary>The developer hurts the hero of a side by points, as blows would; enough of them and it falls.</summary>
        public void HurtHero(Side side, int damage)
        {
            var index = _state.HeroOf(side);
            if (index >= 0 && _state.Heroes[index].Alive)
                _combat.HurtHero(index, damage);
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
            Grid.Set(place.CenterY * Grid.Width + place.CenterX, side);
            _economy.BuildBarracks(site, side);
        }

        /// <summary>The developer brings down a barracks at once, as the last blow would.</summary>
        public void DestroyBarracks(int site)
        {
            if (_state.Sites[site].Built)
                _economy.Destroy(site);
        }

        /// <summary>FNV-1a of the whole state: two runs are the same exactly when their hashes match at every tick.</summary>
        public ulong ComputeHash()
        {
            var hash = 14695981039346656037UL;
            hash = Mix(hash, Tick);
            hash = Mix(hash, (int)_state.Random.State);
            hash = Mix(hash, (int)Result);
            hash = Mix(hash, (int)_state.ChanceHolder);
            foreach (var hero in _state.Heroes)
            {
                hash = Mix(hash, hero.X);
                hash = Mix(hash, hero.Y);
                hash = Mix(hash, hero.DirectionX);
                hash = Mix(hash, hero.DirectionY);
                hash = Mix(hash, hero.Health);
                hash = Mix(hash, hero.RespawnTick);
                hash = Mix(hash, hero.ShieldHits);
            }

            foreach (var squad in _state.Squads)
                hash = Mix(hash, squad.Id * 64 + (int)squad.Role * 8 + squad.Target);
            hash = Mix(hash, _state.NextWaveTick);
            foreach (var soldier in _state.Soldiers)
            {
                hash = Mix(hash, soldier.Id);
                hash = Mix(hash, soldier.Squad);
                hash = Mix(hash, soldier.ShieldHits);
                hash = Mix(hash, soldier.X);
                hash = Mix(hash, soldier.Y);
                hash = Mix(hash, soldier.Health);
                hash = Mix(hash, (int)soldier.Goal * 1000 + soldier.GoalId);
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
                hash = Mix(hash, (int)site.Builder);
            }

            foreach (var side in _state.Sides)
            {
                hash = Mix(hash, side.Gold);
                hash = Mix(hash, (int)side.Order);
                hash = Mix(hash, side.Wood);
            }

            for (var i = 0; i < Grid.Total; i++)
                hash = (hash ^ (byte)Grid[i]) * Prime;

            return hash;
        }

        private void PlaceBase(Side side, int x, int y, int gate)
        {
            var settings = _state.Settings;
            _state.Bases[(int)side] = new BaseState
            {
                X = x,
                Y = y,
                Health = settings.BaseHealth,
                // the gate to the camera (south) for both sides (task 4.24): the hero comes back in sight, by the door
                GateX = x,
                GateY = y - System.Math.Abs(gate),
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

        private void PaintStart(int centerX, int centerY, int radius, Side side)
        {
            // a cell is in the circle when its middle is: (x + 0.5 - c)^2 + (y + 0.5 - c)^2 <= r^2, doubled to stay in integers
            for (var y = 0; y < Grid.Height; y++)
            for (var x = 0; x < Grid.Width; x++)
            {
                var dx = 2 * x + 1 - 2 * centerX;
                var dy = 2 * y + 1 - 2 * centerY;
                if (dx * dx + dy * dy <= 4 * radius * radius)
                    Grid.Set(y * Grid.Width + x, side);
            }
        }

        private void ApplyCommands()
        {
            while (_commands.Count > 0 && _commands[0].Tick == Tick)
            {
                var command = _commands[0];
                _commands.RemoveAt(0);
                if (command.IsOrder)
                {
                    // the enemy gets no orders (spec 3.8); the same order again is the free attack (T17)
                    if (command.Side != Side.Enemy)
                    {
                        var order = _state.Sides[(int)command.Side].Order == command.Order ? ArmyOrder.Free : command.Order;
                        _army.SetOrder(command.Side, order, false);
                    }

                    continue;
                }

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
            var maxX = settings.MapWidthUnits - settings.HeroRadiusUnits;
            var maxY = settings.MapHeightUnits - settings.HeroRadiusUnits;
            for (var i = 0; i < _state.Heroes.Count; i++)
            {
                var hero = _state.Heroes[i];
                if (!hero.Alive || !hero.Moving)
                    continue;

                // the same step in every direction: the direction is scaled to the step, its own length does not count; the enemy's
                // hero walks at the speed of the level
                var step = hero.Side == Side.Enemy ? (int)((long)settings.HeroStepUnits * settings.EnemySpeedPermille / 1000) : settings.HeroStepUnits;
                // own land helps, the other side's slows (task 4.32), over the speed of the level
                step = _state.LandStep(hero.Side, hero.X, hero.Y, step);
                var length = IntMath.Sqrt((long)hero.DirectionX * hero.DirectionX + (long)hero.DirectionY * hero.DirectionY);
                var fromX = hero.X;
                var fromY = hero.Y;
                hero.X = IntMath.Clamp(hero.X + (int)(hero.DirectionX * (long)step / length), min, maxX);
                hero.Y = IntMath.Clamp(hero.Y + (int)(hero.DirectionY * (long)step / length), min, maxY);
                // nobody walks into water (layer 5 spec 2.2): along the shore instead
                _state.Settle(ref hero.X, ref hero.Y, fromX, fromY, settings.HeroRadiusUnits);
                // the truce: the enemy's hero stays off the player's land (task 4.27)
                if (hero.Side == Side.Enemy && _state.Truce)
                    _state.KeepOff(ref hero.X, ref hero.Y, fromX, fromY, Side.Player);
                _state.Heroes[i] = hero;
            }
        }

        /// <summary>Where every living hero and soldier stands after this tick's walk: the trails of the brushes (task 4.31).</summary>
        private void RecordTrails()
        {
            for (var i = 0; i < _state.Heroes.Count; i++)
            {
                var hero = _state.Heroes[i];
                if (hero.Alive)
                    _trails.Record(-1 - i, hero.X, hero.Y, Tick);
            }

            foreach (var soldier in _state.Soldiers)
                _trails.Record(soldier.Id, soldier.X, soldier.Y, Tick);
            _trails.Forget(Tick);
        }

        /// <summary>
        /// The brushes of a side: round the point of the trail of each unit (task 4.31). The cell under a unit on the move is left
        /// as it is on this tick — the trail paints it once it comes; a unit standing still has its trail on it.
        /// </summary>
        private void PaintBrushes(Side side)
        {
            for (var i = 0; i < _state.Heroes.Count; i++)
            {
                var hero = _state.Heroes[i];
                if (hero.Side != side || !hero.Alive)
                    continue;

                _trails.Point(-1 - i, out var x, out var y);
                var radius = _state.Settings.HeroBrushUnits + (side == Side.Player ? _state.Settings.PlayerBrushBonusUnits : 0);
                PaintCircle(x, y, radius, side, Under(x, y, hero.X, hero.Y));
            }

            foreach (var soldier in _state.Soldiers)
            {
                if (soldier.Side != side)
                    continue;

                _trails.Point(soldier.Id, out var x, out var y);
                PaintCircle(x, y, _state.Settings.SoldierBrushUnits, side, Under(x, y, soldier.X, soldier.Y));
            }
        }

        /// <summary>The cell under a unit when its trail is behind it, else none (-1): it stands, the trail is on it.</summary>
        private int Under(int trailX, int trailY, int x, int y)
        {
            if (trailX == x && trailY == y)
                return -1;

            var cx = x / BattleSettings.UnitsPerCell;
            var cy = y / BattleSettings.UnitsPerCell;
            return Grid.Inside(cx, cy) ? cy * Grid.Width + cx : -1;
        }

        /// <summary>Every cell whose middle is within <paramref name="radius"/> units of the point becomes the side's, but one to skip.</summary>
        private void PaintCircle(int x, int y, int radius, Side side, int skip = -1)
        {
            const int unit = BattleSettings.UnitsPerCell;
            var radius2 = (long)radius * radius;
            var x0 = IntMath.Clamp((x - radius) / unit, 0, Grid.Width - 1);
            var x1 = IntMath.Clamp((x + radius) / unit, 0, Grid.Width - 1);
            var y0 = IntMath.Clamp((y - radius) / unit, 0, Grid.Height - 1);
            var y1 = IntMath.Clamp((y + radius) / unit, 0, Grid.Height - 1);
            for (var cy = y0; cy <= y1; cy++)
            for (var cx = x0; cx <= x1; cx++)
            {
                long dx = cx * unit + unit / 2 - x;
                long dy = cy * unit + unit / 2 - y;
                var index = cy * Grid.Width + cx;
                if (dx * dx + dy * dy <= radius2 && index != skip)
                    Grid.Paint(index, side);
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

        /// <summary>
        /// Land of a side cut off from its base and barracks goes back to grass (owner, 4 November), within the limit of the
        /// enclosures; told as a capture of the neutral side, for the wave of the drawing.
        /// </summary>
        private void DropCutOff(Side side)
        {
            if (!_state.Settings.DropCutOffLand)
                return;

            _dropped.Clear();
            _orphans.Drop(Grid, side, _state.Settings.MaxEnclosureCells, _dropped);
            foreach (var cells in _dropped)
                _state.Captures.Add(CaptureOf(Side.Neutral, cells, 0));
        }

        private Capture CaptureOf(Side side, int[] cells, int fromEnemy)
        {
            long sumX = 0;
            long sumY = 0;
            foreach (var index in cells)
            {
                sumX += index % Grid.Width;
                sumY += index / Grid.Width;
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

            PassChance();
            var victory = _state.Settings.VictoryPermille;
            if (Lost(Side.Enemy))
                _state.Result = BattleResult.Victory;
            else if (Lost(Side.Player))
                _state.Result = BattleResult.Defeat;
            else if (Grid.Permille(Side.Player) >= victory)
                _state.Result = BattleResult.Victory;
            else if (Grid.Permille(Side.Enemy) >= victory)
                _state.Result = BattleResult.Defeat;
        }

        /// <summary>
        /// The second chance (task 4.36, spec 2.6a): the first side at 50% of the map gets it, the other takes it at 51%; one
        /// event each time it moves.
        /// </summary>
        private void PassChance()
        {
            var take = _state.Settings.SecondChancePermille;
            if (take <= 0)
                return;

            var holder = _state.ChanceHolder;
            var player = Grid.Permille(Side.Player);
            var enemy = Grid.Permille(Side.Enemy);
            var next = holder;
            if (holder == Side.Neutral)
                next = player >= take && player >= enemy ? Side.Player : enemy >= take ? Side.Enemy : Side.Neutral;
            else if ((holder == Side.Player ? enemy : player) >= _state.Settings.SecondChanceStealPermille)
                next = holder == Side.Player ? Side.Enemy : Side.Player;

            if (next == holder)
                return;

            _state.ChanceHolder = next;
            _state.Emit(EventKind.SecondChance, next, 0, (int)holder, 0, 0);
        }

        /// <summary>
        /// A side has lost for good: its base fell without the second chance, or it fell with it and then the chance was taken
        /// away, or nothing of the side is left: no hero (it does not come back without a base) and no soldier (spec 2.6a).
        /// </summary>
        private bool Lost(Side side)
        {
            if (_state.Bases[(int)side].Standing)
                return false;

            if (_state.ChanceHolder != side)
                return true;

            foreach (var hero in _state.Heroes)
            {
                if (hero.Side == side && hero.Alive)
                    return false;
            }

            return _state.ArmyOf(side) == 0;
        }

        private void Fill(Side side, int target, Side from)
        {
            var size = Grid.Width;
            for (var row = 0; row < Grid.Height && Grid.Count(side) < target; row++)
            {
                var y = side == Side.Player ? row : Grid.Height - 1 - row;
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
