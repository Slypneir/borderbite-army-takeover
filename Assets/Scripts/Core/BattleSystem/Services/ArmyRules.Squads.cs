using System.Collections.Generic;
using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// The army of the enemy in squads and waves (owner, 5 November; stage 1 spec 4.2, «Армия врага»; task 4.27): the player must
    /// have the right to a mistake, the blows and the health stay.
    /// - Squads of 3–5: a new soldier joins the squad still forming, else the smallest one at home, else starts a new one. The
    ///   first squads become the guard, which keeps by the base; the others stay home between the waves — on their own land,
    ///   painting its edge and the neutral land by it, never stepping on the player's land.
    /// - At home a unit of the player on the enemy's land within 4 m is struck; with the alarm (task 4.28) every squad at home,
    ///   the guard too, goes for any unit of the player on its land, however far, and drives it to the edge of that land.
    /// - At the 20th second, then every 30 s, a wave of up to two squads from home (the guard stays): chosen 5 s before it leaves — the sign shows where
    ///   they gather — it goes to the player's barracks nearest to it, else the base, striking units of the player within 4 m on
    ///   the way. With no target left, or fewer than 2 soldiers in a squad, that squad goes home.
    /// - The fall of the player's hero sends every wave home; meanwhile no wave leaves.
    /// Integers only, in a fixed order: the same army on every device.
    /// </summary>
    public sealed partial class ArmyRules
    {
        // how far from the nearest building of the enemy its soldiers at home paint, and how far they look for a cell
        private const int HomeSearchCells = 16;

        private readonly List<int> _members = new List<int>();

        /// <summary>A soldier of the enemy that keeps off the player's land now: any during the truce, else all but the waves.</summary>
        private bool StaysHome(SoldierState soldier)
        {
            if (_state.Truce)
                return true;

            var role = RoleOf(soldier.Squad);
            return role != SquadRole.Wave && role != SquadRole.Return;
        }

        private SquadRole RoleOf(int squad)
        {
            var index = SquadIndex(squad);
            return index < 0 ? SquadRole.Forming : _state.Squads[index].Role;
        }

        private int SquadIndex(int squad)
        {
            for (var i = 0; i < _state.Squads.Count; i++)
            {
                if (_state.Squads[i].Id == squad)
                    return i;
            }

            return -1;
        }

        /// <summary>The squads of this tick: the dead ones go, the new soldiers join, the roles and the waves move on.</summary>
        private void OrganizeSquads()
        {
            var squads = _state.Squads;
            Count();
            for (var i = squads.Count - 1; i >= 0; i--)
            {
                if (_members[i] > 0)
                    continue;

                squads.RemoveAt(i);
                _members.RemoveAt(i);
            }

            for (var s = 0; s < _state.Soldiers.Count; s++)
            {
                var soldier = _state.Soldiers[s];
                if (soldier.Side != Side.Enemy || soldier.Squad != 0)
                    continue;

                var pick = Join();
                soldier.Squad = squads[pick].Id;
                _members[pick]++;
                _state.Soldiers[s] = soldier;
            }

            var guards = 0;
            foreach (var squad in squads)
            {
                if (squad.Role == SquadRole.Guard)
                    guards++;
            }

            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                var promote = guards < _settings.GuardSquads &&
                              ((squad.Role == SquadRole.Forming && _members[i] >= _settings.SquadMin) || squad.Role == SquadRole.Home);
                if (promote)
                {
                    squad.Role = SquadRole.Guard;
                    guards++;
                }
                else if (squad.Role == SquadRole.Forming && _members[i] >= _settings.SquadMin)
                    squad.Role = SquadRole.Home;

                squads[i] = squad;
            }

            Waves();
        }

        /// <summary>The squad a new soldier joins: the one forming, else the smallest at home or on guard, else a new one.</summary>
        private int Join()
        {
            var squads = _state.Squads;
            for (var i = 0; i < squads.Count; i++)
            {
                if (squads[i].Role == SquadRole.Forming && _members[i] < _settings.SquadMax)
                    return i;
            }

            var pick = -1;
            for (var i = 0; i < squads.Count; i++)
            {
                var role = squads[i].Role;
                if ((role == SquadRole.Guard || role == SquadRole.Home) && _members[i] < _settings.SquadMax && (pick < 0 || _members[i] < _members[pick]))
                    pick = i;
            }

            if (pick >= 0)
                return pick;

            squads.Add(new SquadState { Id = ++_state.LastSquadId, Role = SquadRole.Forming, Target = -1 });
            _members.Add(0);
            return squads.Count - 1;
        }

        private void Count()
        {
            _members.Clear();
            foreach (var squad in _state.Squads)
            {
                var count = 0;
                foreach (var soldier in _state.Soldiers)
                {
                    if (soldier.Side == Side.Enemy && soldier.Squad == squad.Id)
                        count++;
                }

                _members.Add(count);
            }
        }

        private void Waves()
        {
            var squads = _state.Squads;
            var heroIndex = _state.HeroOf(Side.Player);
            var heroAlive = heroIndex < 0 || _state.Heroes[heroIndex].Alive;
            var heroFell = _state.PlayerHeroWasAlive && !heroAlive;
            _state.PlayerHeroWasAlive = heroAlive;

            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                switch (squad.Role)
                {
                    case SquadRole.Wave:
                        // the hero of the player fell: everyone home (owner: «убил героя — отходит»)
                        if (heroFell || _members[i] < _settings.WaveLeaveBelow)
                            squad.Role = SquadRole.Return;
                        else if (!Stands(squad.Target))
                        {
                            Middle(squad.Id, out var x, out var y);
                            if (TargetNear(x, y, out var target))
                                squad.Target = target;
                            else
                                squad.Role = SquadRole.Return;
                        }

                        break;
                    case SquadRole.Return:
                        if (AllHome(squad.Id))
                            squad.Role = SquadRole.Home;
                        break;
                }

                squads[i] = squad;
            }

            var tick = _state.Tick;
            if (!_state.WaveAnnounced && tick >= _state.NextWaveTick - _settings.WaveWarnTicks)
            {
                long sumX = 0;
                long sumY = 0;
                var chosen = 0;
                for (var i = 0; i < squads.Count && chosen < _settings.WaveSquads; i++)
                {
                    var squad = squads[i];
                    if (squad.Role != SquadRole.Home || _members[i] < _settings.SquadMin)
                        continue;

                    squad.Role = SquadRole.Gathering;
                    squads[i] = squad;
                    Middle(squad.Id, out var x, out var y);
                    sumX += x;
                    sumY += y;
                    chosen++;
                }

                if (chosen == 0)
                    _state.NextWaveTick += _settings.WaveEveryTicks;
                else
                {
                    _state.WaveAnnounced = true;
                    _state.WaveFromX = (int)(sumX / chosen);
                    _state.WaveFromY = (int)(sumY / chosen);
                }
            }

            // the wave leaves when its time comes and the player's hero stands (it does not go while that hero is down)
            if (!_state.WaveAnnounced || tick < _state.NextWaveTick || _state.PlayerHeroDown)
                return;

            var found = TargetNear(_state.WaveFromX, _state.WaveFromY, out var goal);
            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad.Role != SquadRole.Gathering)
                    continue;

                squad.Role = found ? SquadRole.Wave : SquadRole.Home;
                squad.Target = goal;
                squads[i] = squad;
            }

            _state.WaveAnnounced = false;
            _state.NextWaveTick = tick + _settings.WaveEveryTicks;
        }

        /// <summary>The target of a wave still stands: a barracks of the player on its place, or the player's base (-1).</summary>
        private bool Stands(int target) =>
            target < 0 ? _state.Bases[(int)Side.Player].Standing : target < _state.Sites.Length && _state.Sites[target].Barracks == Side.Player;

        /// <summary>The player's barracks nearest to a point, else its base (-1); false when neither stands.</summary>
        private bool TargetNear(int x, int y, out int target)
        {
            target = -1;
            var best = long.MaxValue;
            for (var i = 0; i < _state.Sites.Length; i++)
            {
                var site = _state.Sites[i];
                if (site.Barracks != Side.Player)
                    continue;

                var distance = BattleState.Distance2(x, y, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY));
                if (distance >= best)
                    continue;

                best = distance;
                target = i;
            }

            return target >= 0 || _state.Bases[(int)Side.Player].Standing;
        }

        private void Middle(int squad, out int x, out int y)
        {
            long sumX = 0;
            long sumY = 0;
            var count = 0;
            foreach (var soldier in _state.Soldiers)
            {
                if (soldier.Side != Side.Enemy || soldier.Squad != squad)
                    continue;

                sumX += soldier.X;
                sumY += soldier.Y;
                count++;
            }

            var gate = _state.Bases[(int)Side.Enemy];
            x = count > 0 ? (int)(sumX / count) : gate.GateX;
            y = count > 0 ? (int)(sumY / count) : gate.GateY;
        }

        private bool AllHome(int squad)
        {
            foreach (var soldier in _state.Soldiers)
            {
                if (soldier.Side == Side.Enemy && soldier.Squad == squad && _state.ColourAt(soldier.X, soldier.Y) != Side.Enemy)
                    return false;
            }

            return true;
        }

        /// <summary>One soldier of a squad of the enemy walks by the role of its squad.</summary>
        private void SquadWalk(ref SoldierState soldier)
        {
            var index = SquadIndex(soldier.Squad);
            var squad = index >= 0 ? _state.Squads[index] : new SquadState { Role = SquadRole.Forming, Target = -1 };
            var gate = _state.Bases[(int)Side.Enemy];
            switch (squad.Role)
            {
                case SquadRole.Wave:
                    if (Chase(ref soldier, false, _settings.DefendUnits))
                        return;

                    if (squad.Target >= 0)
                    {
                        var site = _state.Sites[squad.Target];
                        soldier.Goal = SoldierGoal.Barracks;
                        soldier.GoalId = squad.Target;
                        Approach(ref soldier, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY), _settings.BarracksHalfUnits);
                    }
                    else
                    {
                        var place = _state.Bases[(int)Side.Player];
                        soldier.Goal = SoldierGoal.Base;
                        Approach(ref soldier, place.X, place.Y, _settings.BaseHalfUnits);
                    }

                    return;
                case SquadRole.Return:
                    soldier.Goal = SoldierGoal.None;
                    Walk(ref soldier, gate.GateX + Jitter(soldier.Id, 0), gate.GateY + Jitter(soldier.Id, 16), 0);
                    return;
                case SquadRole.Guard:
                    if (Chase(ref soldier, true, HomeReach()))
                        return;

                    soldier.Goal = SoldierGoal.None;
                    var (dx, dy) = _slots[soldier.Id % _slots.Count];
                    Walk(ref soldier, gate.GateX + dx + Jitter(soldier.Id, 0), gate.GateY + dy + Jitter(soldier.Id, 16), 0);
                    return;
                default:
                    // at home, forming or gathering for a wave: defend the own land, else paint its edge
                    if (Chase(ref soldier, true, HomeReach()))
                        return;

                    if (AimAtEdge(ref soldier))
                    {
                        var size = _state.Grid.Width;
                        Walk(ref soldier, BattleState.Middle(soldier.GoalId % size), BattleState.Middle(soldier.GoalId / size), 0);
                        return;
                    }

                    var (sx, sy) = _slots[soldier.Id % _slots.Count];
                    Walk(ref soldier, gate.GateX + sx * 2, gate.GateY + sy * 2, 0);
                    return;
            }
        }

        /// <summary>
        /// The nearest unit of the player within 4 m — at home only one on the enemy's land — is chased and struck; false when
        /// none.
        /// </summary>
        private bool Chase(ref SoldierState soldier, bool ownLandOnly, long reach)
        {
            var body = _settings.SoldierBodyUnits;
            var best = reach;
            var found = false;
            int tx = 0, ty = 0, radius = 0;
            foreach (var hero in _state.Heroes)
            {
                if (hero.Side != Side.Player || !hero.Alive || hero.InvulnerableUntil > _state.Tick || (ownLandOnly && !OnEnemyLand(hero.X, hero.Y)))
                    continue;

                var gap = Gap(soldier.X, soldier.Y, hero.X, hero.Y, body + _settings.HeroRadiusUnits);
                if (gap > best)
                    continue;

                best = gap;
                found = true;
                tx = hero.X;
                ty = hero.Y;
                radius = _settings.HeroRadiusUnits;
            }

            foreach (var other in _state.Soldiers)
            {
                if (other.Side != Side.Player || (ownLandOnly && !OnEnemyLand(other.X, other.Y)))
                    continue;

                var gap = Gap(soldier.X, soldier.Y, other.X, other.Y, body + body);
                if (gap >= best && found)
                    continue;
                if (gap > best)
                    continue;

                best = gap;
                found = true;
                tx = other.X;
                ty = other.Y;
                radius = body;
            }

            if (!found)
                return false;

            soldier.Goal = SoldierGoal.Soldier;
            Approach(ref soldier, tx, ty, radius);
            return true;
        }

        /// <summary>
        /// A unit of the player is on the enemy's land: the cell under it is the enemy's (task 4.31: the brush follows the trail, so
        /// the cell under a unit on the move keeps its colour; a unit standing still makes it its own in 0.4 s).
        /// </summary>
        private bool OnEnemyLand(int x, int y) => _state.ColourAt(x, y) == Side.Enemy;

        /// <summary>How far a squad at home goes for a unit of the player on its land: 4 m, or anywhere on it with the alarm.</summary>
        private long HomeReach() => _settings.EnemyAlarm ? long.MaxValue / 4 : _settings.DefendUnits;

        /// <summary>To a target of a radius, stopping within the reach of a blow from the edge of its body.</summary>
        private void Approach(ref SoldierState soldier, int x, int y, int radius)
        {
            var stop = _settings.SoldierBodyUnits + radius + _settings.SoldierReachUnits * 3 / 4;
            if (BattleState.Distance2(soldier.X, soldier.Y, x, y) > (long)stop * stop)
                Walk(ref soldier, x, y, stop);
            else
                Face(ref soldier, x, y);
        }

        private static long Gap(int x, int y, int toX, int toY, int radii) =>
            Utils.IntMath.Sqrt(BattleState.Distance2(x, y, toX, toY)) - radii;

        /// <summary>
        /// A soldier at home keeps its cell while it is still neutral, else takes the nearest neutral cell by the enemy's land, near
        /// its buildings and not taken by another soldier; false when there is none close.
        /// </summary>
        private bool AimAtEdge(ref SoldierState soldier)
        {
            var grid = _state.Grid;
            var size = grid.Width;
            var height = grid.Height;
            if (soldier.Goal == SoldierGoal.Cell && grid[soldier.GoalId] == Side.Neutral && !grid.IsProtected(soldier.GoalId))
                return true;

            soldier.Goal = SoldierGoal.None;
            var cx = Utils.IntMath.Clamp(soldier.X / BattleSettings.UnitsPerCell, 0, size - 1);
            var cy = Utils.IntMath.Clamp(soldier.Y / BattleSettings.UnitsPerCell, 0, height - 1);
            for (var ring = 0; ring <= HomeSearchCells; ring++)
            {
                var found = -1;
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
                        if (grid[index] != Side.Neutral || grid.IsProtected(index) || !ByEnemyLand(x, y) || !NearHome(x, y) ||
                            Claimed(Side.Enemy, soldier.Id, x, y))
                            continue;

                        var distance = (long)(x - cx) * (x - cx) + (long)(y - cy) * (y - cy);
                        if (distance >= best)
                            continue;

                        best = distance;
                        found = index;
                    }
                }

                if (found < 0)
                    continue;

                soldier.Goal = SoldierGoal.Cell;
                soldier.GoalId = found;
                return true;
            }

            return false;
        }

        private bool ByEnemyLand(int x, int y)
        {
            var grid = _state.Grid;
            return (x > 0 && grid.At(x - 1, y) == Side.Enemy) || (x < grid.Width - 1 && grid.At(x + 1, y) == Side.Enemy) ||
                   (y > 0 && grid.At(x, y - 1) == Side.Enemy) || (y < grid.Height - 1 && grid.At(x, y + 1) == Side.Enemy);
        }

        /// <summary>A cell within the reach of home of the base or a barracks of the enemy: the land «рядом», not the whole map.</summary>
        private bool NearHome(int x, int y)
        {
            var reach = (long)_settings.HomeReachUnits;
            var px = BattleState.Middle(x);
            var py = BattleState.Middle(y);
            var place = _state.Bases[(int)Side.Enemy];
            if (place.Standing && BattleState.Distance2(px, py, place.X, place.Y) <= reach * reach)
                return true;

            foreach (var site in _state.Sites)
            {
                if (site.Barracks == Side.Enemy &&
                    BattleState.Distance2(px, py, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY)) <= reach * reach)
                    return true;
            }

            return false;
        }
    }
}
