using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Core.BattleSystem.Utils;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// How soldiers walk (stage 1 spec 3.4). The player's do not follow the hero (owner's decision of 4 November): once hired a
    /// soldier goes to attack on its own — the nearest enemy hero or soldier within sight (8 m), else the nearest enemy barracks,
    /// else the enemy base; it keeps its target until the target falls or leaves its sight, or an enemy comes within sight (T15);
    /// the death of the hero changes nothing. The enemy's follow their hero as a crowd 1–4 m around it (task 4.15): an enemy
    /// unit within 4 m, else a building of the player within 4 m, is struck while the soldier is no further than 8 m from its
    /// hero; one further drops it and catches up. While their hero is dead they go to their base. All part from each other and
    /// give way to their hero. The player's army may follow an order (spec 3.8): «Ко мне» — the crowd of the enemy's; «Казармы»,
    /// «Штаб» — straight to the nearest enemy barracks, the enemy base; «Бойцы» — the nearest enemy soldier anywhere; «Захват»
    /// — each to the nearest cell not of its side, neutral first, never two to the same spot. With no target left the order
    /// turns to «Ко мне» by itself (T18). Integers only, in a fixed order: the same army on every device.
    /// </summary>
    public sealed partial class ArmyRules
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;
        private readonly System.Collections.Generic.List<(int X, int Y)> _slots = new System.Collections.Generic.List<(int X, int Y)>();
        private readonly System.Collections.Generic.List<(int X, int Y)> _from = new System.Collections.Generic.List<(int X, int Y)>();

        public ArmyRules(BattleState state)
        {
            _state = state;
            _settings = state.Settings;
            BuildSlots();
        }

        /// <summary>A new soldier of a side with the full health of its side; returns its id.</summary>
        public static int Spawn(BattleState state, Side side, int x, int y)
        {
            var settings = state.Settings;
            var id = state.NextId();
            state.Soldiers.Add(new SoldierState
            {
                Id = id,
                Side = side,
                X = IntMath.Clamp(x, 0, settings.MapWidthUnits),
                Y = IntMath.Clamp(y, 0, settings.MapHeightUnits),
                Health = settings.SoldierHealthOf(side),
                FightTick = state.Tick,
                AnchorX = x,
                AnchorY = y,
                FacingY = side == Side.Player ? 1 : -1,
            });
            return id;
        }

        public void Step()
        {
            // where everyone stood: a soldier walked or pushed into water goes back along the shore (layer 5 spec 2.2), one of the
            // enemy at home back off the player's land (task 4.27)
            var squads = _settings.EnemySquads;
            if (squads)
                OrganizeSquads();
            var wet = _state.Grid.HasWater || squads;
            if (wet)
            {
                _from.Clear();
                foreach (var soldier in _state.Soldiers)
                    _from.Add((soldier.X, soldier.Y));
            }

            KeepOrder(Side.Player);
            var slot = new int[3];
            var noCell = false;
            for (var i = 0; i < _state.Soldiers.Count; i++)
            {
                var soldier = _state.Soldiers[i];
                if (squads && soldier.Side == Side.Enemy)
                {
                    SquadWalk(ref soldier);
                    _state.Soldiers[i] = soldier;
                    continue;
                }

                var order = _state.Sides[(int)soldier.Side].Order;
                if (Follows(soldier.Side) || order == ArmyOrder.Rally)
                {
                    Follow(ref soldier, slot[(int)soldier.Side]++);
                    _state.Soldiers[i] = soldier;
                    continue;
                }

                switch (order)
                {
                    case ArmyOrder.Barracks:
                        if (soldier.Goal != SoldierGoal.Barracks || !Locate(soldier, out _, out _, out _))
                            AimAtBarracks(ref soldier);
                        break;
                    case ArmyOrder.Base:
                        soldier.Goal = SoldierGoal.Base;
                        break;
                    case ArmyOrder.Units:
                        AimAtSoldier(ref soldier);
                        break;
                    case ArmyOrder.Capture:
                        if (!AimAtCell(ref soldier))
                            noCell = true;
                        break;
                    default:
                        if (soldier.Goal == SoldierGoal.Cell)
                            soldier.Goal = SoldierGoal.None;
                        Choose(ref soldier);
                        break;
                }

                if (Locate(soldier, out var x, out var y, out var radius))
                {
                    // to the target, stopping well within the reach of a blow from the edge of its body; on a cell — onto it
                    var stop = soldier.Goal == SoldierGoal.Cell ? 0 : _settings.SoldierBodyUnits + radius + _settings.SoldierReachUnits * 3 / 4;
                    if (BattleState.Distance2(soldier.X, soldier.Y, x, y) > (long)stop * stop)
                        Walk(ref soldier, x, y, stop);
                    else if (stop > 0)
                        Face(ref soldier, x, y);
                }

                _state.Soldiers[i] = soldier;
            }

            // «Захват» with no cell left for anyone: «Ко мне» from the next tick
            if (noCell && _state.Sides[(int)Side.Player].Order == ArmyOrder.Capture && !AnyCell(Side.Player))
                SetOrder(Side.Player, ArmyOrder.Rally, true);

            Part();
            if (!wet)
                return;

            for (var i = 0; i < _state.Soldiers.Count; i++)
            {
                var soldier = _state.Soldiers[i];
                _state.Settle(ref soldier.X, ref soldier.Y, _from[i].X, _from[i].Y, _settings.SoldierBodyUnits);
                if (squads && soldier.Side == Side.Enemy && StaysHome(soldier))
                    _state.KeepOff(ref soldier.X, ref soldier.Y, _from[i].X, _from[i].Y, Side.Player);
                _state.Soldiers[i] = soldier;
            }
        }

        /// <summary>Sets the order of the army of a side and tells the drawing; <paramref name="byItself"/> — no targets were left (T18).</summary>
        public void SetOrder(Side side, ArmyOrder order, bool byItself)
        {
            ref var purse = ref _state.Sides[(int)side];
            if (purse.Order == order)
                return;

            purse.Order = order;
            for (var i = 0; i < _state.Soldiers.Count; i++)
            {
                var soldier = _state.Soldiers[i];
                if (soldier.Side != side)
                    continue;

                soldier.Goal = SoldierGoal.None;
                _state.Soldiers[i] = soldier;
            }

            _state.Emit(EventKind.OrderChanged, side, byItself ? 1 : 0, (int)order, 0, 0);
        }

        /// <summary>An order whose targets are all gone turns to «Ко мне» (T18): no enemy barracks, no enemy soldiers, a fallen base.</summary>
        private void KeepOrder(Side side)
        {
            var enemy = BattleState.Enemy(side);
            var left = _state.Sides[(int)side].Order switch
            {
                ArmyOrder.Barracks => System.Array.Exists(_state.Sites, site => site.Barracks == enemy),
                ArmyOrder.Units => _state.ArmyOf(enemy) > 0,
                ArmyOrder.Base => _state.Bases[(int)enemy].Standing,
                _ => true,
            };
            if (!left)
                SetOrder(side, ArmyOrder.Rally, true);
        }

        /// <summary>Keeps a standing enemy barracks as its goal, else takes the nearest.</summary>
        private void AimAtBarracks(ref SoldierState soldier)
        {
            var enemy = BattleState.Enemy(soldier.Side);
            var best = long.MaxValue;
            soldier.Goal = SoldierGoal.None;
            for (var i = 0; i < _state.Sites.Length; i++)
            {
                var site = _state.Sites[i];
                if (site.Barracks != enemy)
                    continue;

                var distance = BattleState.Distance2(soldier.X, soldier.Y, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY));
                if (distance >= best)
                    continue;

                best = distance;
                soldier.Goal = SoldierGoal.Barracks;
                soldier.GoalId = i;
            }
        }

        /// <summary>The nearest enemy soldier anywhere on the map («Бойцы»); the hero is struck on the way, not hunted.</summary>
        private void AimAtSoldier(ref SoldierState soldier)
        {
            var enemy = BattleState.Enemy(soldier.Side);
            var best = long.MaxValue;
            soldier.Goal = SoldierGoal.None;
            foreach (var other in _state.Soldiers)
            {
                if (other.Side != enemy)
                    continue;

                var distance = BattleState.Distance2(soldier.X, soldier.Y, other.X, other.Y);
                if (distance >= best)
                    continue;

                best = distance;
                soldier.Goal = SoldierGoal.Soldier;
                soldier.GoalId = other.Id;
            }
        }

        /// <summary>
        /// «Захват» (T20): keeps its cell while the cell is not yet of its side, else takes the nearest unprotected neutral cell, else
        /// the nearest enemy one, no closer than <see cref="BattleSettings.CaptureSpacingCells"/> to the cell of another soldier.
        /// False when no cell is left for it.
        /// </summary>
        private bool AimAtCell(ref SoldierState soldier)
        {
            var grid = _state.Grid;
            if (soldier.Goal == SoldierGoal.Cell && grid[soldier.GoalId] != soldier.Side && !grid.IsProtected(soldier.GoalId))
                return true;

            var cx = IntMath.Clamp(soldier.X / BattleSettings.UnitsPerCell, 0, grid.Width - 1);
            var cy = IntMath.Clamp(soldier.Y / BattleSettings.UnitsPerCell, 0, grid.Height - 1);
            soldier.Goal = SoldierGoal.None;
            foreach (var colour in new[] { Side.Neutral, BattleState.Enemy(soldier.Side) })
            {
                if (!NearestCell(soldier.Side, soldier.Id, cx, cy, colour, out var cell))
                    continue;

                soldier.Goal = SoldierGoal.Cell;
                soldier.GoalId = cell;
                return true;
            }

            return false;
        }

        /// <summary>The nearest free cell of a colour by square rings around (cx, cy), the nearest by distance within the first ring with one.</summary>
        private bool NearestCell(Side side, int id, int cx, int cy, Side colour, out int found)
        {
            var grid = _state.Grid;
            var size = grid.Width;
            var height = grid.Height;
            found = -1;
            for (var ring = 0; ring < System.Math.Max(size, height); ring++)
            {
                var best = long.MaxValue;
                for (var y = cy - ring; y <= cy + ring; y++)
                {
                    if (y < 0 || y >= height)
                        continue;

                    var edge = y == cy - ring || y == cy + ring;
                    var step = edge || ring == 0 ? 1 : 2 * ring;
                    for (var x = cx - ring; x <= cx + ring; x += step)
                    {
                        if (x < 0 || x >= size)
                            continue;

                        var index = y * size + x;
                        if (grid[index] != colour || grid.IsProtected(index) || Claimed(side, id, x, y))
                            continue;

                        var distance = (long)(x - cx) * (x - cx) + (long)(y - cy) * (y - cy);
                        if (distance >= best)
                            continue;

                        best = distance;
                        found = index;
                    }
                }

                if (found >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>Another soldier of the side aims at a cell this close (they do not follow each other).</summary>
        private bool Claimed(Side side, int id, int x, int y)
        {
            var size = _state.Grid.Width;
            var spacing = _settings.CaptureSpacingCells;
            foreach (var other in _state.Soldiers)
            {
                if (other.Side != side || other.Id == id || other.Goal != SoldierGoal.Cell)
                    continue;

                if (System.Math.Abs(other.GoalId % size - x) < spacing && System.Math.Abs(other.GoalId / size - y) < spacing)
                    return true;
            }

            return false;
        }

        /// <summary>Is any unprotected cell not of the side left on the map.</summary>
        private bool AnyCell(Side side)
        {
            var grid = _state.Grid;
            for (var i = 0; i < grid.Total; i++)
            {
                if (grid[i] != side && !grid.IsProtected(i))
                    return true;
            }

            return false;
        }

        /// <summary>Every soldier paints its cells; called by side, so the brushes of the player go last.</summary>
        public void Paint(Side side, System.Action<int, int, int, Side> paintCircle)
        {
            foreach (var soldier in _state.Soldiers)
            {
                if (soldier.Side == side)
                    paintCircle(soldier.X, soldier.Y, _settings.SoldierBrushUnits, side);
            }
        }

        private bool Follows(Side side) => side == Side.Enemy && _settings.EnemyArmyFollows;

        /// <summary>
        /// A follower: to an enemy (or a building) in sight while not too far from its hero, else to its place in the crowd. Its
        /// anchor is the hero; while the hero is dead, the gate of its base; a side without a hero keeps to where it was put.
        /// </summary>
        private void Follow(ref SoldierState soldier, int place)
        {
            int anchorX;
            int anchorY;
            var heroIndex = _state.HeroOf(soldier.Side);
            if (heroIndex < 0)
            {
                anchorX = soldier.AnchorX;
                anchorY = soldier.AnchorY;
            }
            else if (_state.Heroes[heroIndex].Alive)
            {
                anchorX = _state.Heroes[heroIndex].X;
                anchorY = _state.Heroes[heroIndex].Y;
            }
            else
            {
                var home = _state.Bases[(int)soldier.Side];
                anchorX = home.GateX;
                anchorY = home.GateY;
            }

            var leash2 = (long)_settings.SoldierLeashUnits * _settings.SoldierLeashUnits;
            var body = _settings.SoldierBodyUnits;
            if (BattleState.Distance2(soldier.X, soldier.Y, anchorX, anchorY) <= leash2 &&
                CombatRules.NearestTarget(_state, soldier.Side, soldier.X, soldier.Y, _settings.FollowerSightUnits, body, out var target))
            {
                var radius = target.Kind switch
                {
                    CombatRules.TargetKind.Hero => _settings.HeroRadiusUnits,
                    CombatRules.TargetKind.Soldier => _settings.SoldierBodyUnits,
                    CombatRules.TargetKind.Barracks => _settings.BarracksHalfUnits,
                    _ => _settings.BaseHalfUnits,
                };
                var stop = body + radius + _settings.SoldierReachUnits * 3 / 4;
                if (BattleState.Distance2(soldier.X, soldier.Y, target.X, target.Y) > (long)stop * stop)
                    Walk(ref soldier, target.X, target.Y, stop);
                else
                    Face(ref soldier, target.X, target.Y);
                return;
            }

            var (dx, dy) = _slots[place % _slots.Count];
            Walk(ref soldier, anchorX + dx + Jitter(soldier.Id, 0), anchorY + dy + Jitter(soldier.Id, 16), 0);
        }

        /// <summary>A lasting offset of a soldier from its place, up to 30 % of the spacing: a crowd, not a parade.</summary>
        private int Jitter(int id, int shift)
        {
            var hash = (uint)id * 2654435761u;
            var range = _settings.SlotSpacingUnits * 3 / 10;
            return (int)((hash >> shift) % (uint)(range * 2 + 1)) - range;
        }

        /// <summary>The places of the crowd: a square spiral, nearest first, behind the hero before in front, left before right.</summary>
        private void BuildSlots()
        {
            var spacing = _settings.SlotSpacingUnits;
            const int rings = 4;
            for (var y = -rings; y <= rings; y++)
            for (var x = -rings; x <= rings; x++)
            {
                if (x != 0 || y != 0)
                    _slots.Add((x * spacing, y * spacing));
            }

            _slots.Sort((a, b) =>
            {
                var da = (long)a.X * a.X + (long)a.Y * a.Y;
                var db = (long)b.X * b.X + (long)b.Y * b.Y;
                if (da != db)
                    return da.CompareTo(db);
                return a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
            });
        }

        /// <summary>Keeps the target while it stands and no enemy comes close, else takes the next one by the order of T15.</summary>
        private void Choose(ref SoldierState soldier)
        {
            var sight = _settings.SoldierSightUnits;
            var body = _settings.SoldierBodyUnits;
            var enemy = BattleState.Enemy(soldier.Side);
            if (soldier.Goal == SoldierGoal.Hero || soldier.Goal == SoldierGoal.Soldier)
            {
                if (Locate(soldier, out var ux, out var uy, out var radius) &&
                    IntMath.Sqrt(BattleState.Distance2(soldier.X, soldier.Y, ux, uy)) - radius - body <= sight)
                    return;
            }
            else if (!CombatRules.NearestUnit(_state, soldier.Side, soldier.X, soldier.Y, sight, body, out _) &&
                     ((soldier.Goal == SoldierGoal.Barracks && _state.Sites[soldier.GoalId].Barracks == enemy) ||
                      (soldier.Goal == SoldierGoal.Base && _state.Bases[(int)enemy].Standing)))
            {
                return;
            }

            if (CombatRules.NearestUnit(_state, soldier.Side, soldier.X, soldier.Y, sight, body, out var target))
            {
                var isHero = target.Kind == CombatRules.TargetKind.Hero;
                soldier.Goal = isHero ? SoldierGoal.Hero : SoldierGoal.Soldier;
                soldier.GoalId = isHero ? target.Index : _state.Soldiers[target.Index].Id;
                return;
            }

            var best = long.MaxValue;
            soldier.Goal = SoldierGoal.None;
            for (var i = 0; i < _state.Sites.Length; i++)
            {
                var site = _state.Sites[i];
                if (site.Barracks != enemy)
                    continue;

                var distance = BattleState.Distance2(soldier.X, soldier.Y, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY));
                if (distance >= best)
                    continue;

                best = distance;
                soldier.Goal = SoldierGoal.Barracks;
                soldier.GoalId = i;
            }

            if (soldier.Goal == SoldierGoal.None && _state.Bases[(int)enemy].Standing)
                soldier.Goal = SoldierGoal.Base;
        }

        /// <summary>Where the target of a soldier is and the radius of its body, or false when it is gone.</summary>
        private bool Locate(SoldierState soldier, out int x, out int y, out int radius)
        {
            x = 0;
            y = 0;
            radius = 0;
            var enemy = BattleState.Enemy(soldier.Side);
            switch (soldier.Goal)
            {
                case SoldierGoal.Hero:
                {
                    var hero = _state.Heroes[soldier.GoalId];
                    x = hero.X;
                    y = hero.Y;
                    radius = _settings.HeroRadiusUnits;
                    return hero.Alive;
                }
                case SoldierGoal.Soldier:
                    foreach (var other in _state.Soldiers)
                    {
                        if (other.Id != soldier.GoalId)
                            continue;

                        x = other.X;
                        y = other.Y;
                        radius = _settings.SoldierBodyUnits;
                        return true;
                    }

                    return false;
                case SoldierGoal.Barracks:
                {
                    var site = _state.Sites[soldier.GoalId];
                    x = BattleState.Middle(site.CenterX);
                    y = BattleState.Middle(site.CenterY);
                    radius = _settings.BarracksHalfUnits;
                    return site.Barracks == enemy;
                }
                case SoldierGoal.Base:
                {
                    var place = _state.Bases[(int)enemy];
                    x = place.X;
                    y = place.Y;
                    radius = _settings.BaseHalfUnits;
                    return place.Standing;
                }
                case SoldierGoal.Cell:
                {
                    var size = _state.Grid.Width;
                    x = BattleState.Middle(soldier.GoalId % size);
                    y = BattleState.Middle(soldier.GoalId / size);
                    return true;
                }
                default:
                    return false;
            }
        }

        private void Walk(ref SoldierState soldier, int x, int y, int stopAt)
        {
            long dx = x - soldier.X;
            long dy = y - soldier.Y;
            var length = IntMath.Sqrt(dx * dx + dy * dy);
            if (length <= stopAt || length == 0)
                return;

            var step = System.Math.Min(_settings.SoldierStepUnits, length - stopAt);
            soldier.X += (int)(dx * step / length);
            soldier.Y += (int)(dy * step / length);
            soldier.FacingX = (int)(dx * 1000 / length);
            soldier.FacingY = (int)(dy * 1000 / length);
        }

        private static void Face(ref SoldierState soldier, int x, int y)
        {
            if (x == soldier.X && y == soldier.Y)
                return;

            soldier.FacingX = x - soldier.X;
            soldier.FacingY = y - soldier.Y;
        }

        /// <summary>Soldiers too close push each other apart by half the overlap each and give way to their hero; all stay on the map.</summary>
        private void Part()
        {
            var spacing = _settings.SoldierSpacingUnits;
            var soldiers = _state.Soldiers;
            for (var i = 0; i < soldiers.Count; i++)
            {
                var a = soldiers[i];
                for (var j = i + 1; j < soldiers.Count; j++)
                {
                    var b = soldiers[j];
                    long dx = b.X - a.X;
                    long dy = b.Y - a.Y;
                    var distance = IntMath.Sqrt(dx * dx + dy * dy);
                    if (distance >= spacing)
                        continue;

                    if (distance == 0)
                    {
                        // the same point: part along x, the earlier one to the left
                        dx = 1;
                        distance = 1;
                    }

                    var push = (spacing - distance) / 2 + 1;
                    a.X -= (int)(dx * push / distance);
                    a.Y -= (int)(dy * push / distance);
                    b.X += (int)(dx * push / distance);
                    b.Y += (int)(dy * push / distance);
                    soldiers[j] = b;
                }

                GiveWay(ref a);
                a.X = IntMath.Clamp(a.X, _settings.HeroRadiusUnits, _settings.MapWidthUnits - _settings.HeroRadiusUnits);
                a.Y = IntMath.Clamp(a.Y, _settings.HeroRadiusUnits, _settings.MapHeightUnits - _settings.HeroRadiusUnits);
                soldiers[i] = a;
            }
        }

        private void GiveWay(ref SoldierState soldier)
        {
            var heroIndex = _state.HeroOf(soldier.Side);
            if (heroIndex < 0 || !_state.Heroes[heroIndex].Alive)
                return;

            var hero = _state.Heroes[heroIndex];
            var room = _settings.SoldierSpacingUnits;
            long dx = soldier.X - hero.X;
            long dy = soldier.Y - hero.Y;
            var distance = IntMath.Sqrt(dx * dx + dy * dy);
            if (distance >= room)
                return;

            if (distance == 0)
            {
                dy = -1;
                distance = 1;
            }

            soldier.X = hero.X + (int)(dx * room / distance);
            soldier.Y = hero.Y + (int)(dy * room / distance);
        }
    }
}
