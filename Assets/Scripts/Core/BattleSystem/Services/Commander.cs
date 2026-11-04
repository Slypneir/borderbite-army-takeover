using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Core.BattleSystem.Utils;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// The commander of a side (stage 1 spec 4.2, GDD 5.8): walks its hero by the same rules as the player's, choosing once in a
    /// reaction time, by priority:
    /// 1. hero health under 30% — back to its own land;
    /// 2. an empty place of its own colour (or one to paint) and wood for a barracks — go and build;
    /// 3. a barracks, gold for a soldier and room in the army — go and hire;
    /// 4. an army of 8 or more and at least 80% of the other's — to the nearest building of the other side, else its base
    ///    (the enemy's soldiers follow its hero there, task 4.15);
    /// 5. else to the nearest pile on neutral land in sight, else to the nearest neutral cell at its own border.
    /// It sees piles only as far as a player sees around the hero, gets nothing beyond the rules, and when its goal comes no
    /// closer for a while it leaves that priority for the next one. Integers only: the same battle on every device.
    /// </summary>
    public sealed class Commander
    {
        private const int Priorities = 5;
        private const int Arrived = 500;

        private readonly BattleState _state;
        private readonly BattleSettings _settings;
        private readonly Side _side;
        private readonly int[] _blockedUntil = new int[Priorities + 1];

        private int _nextDecision;
        private int _priority;
        private int _targetX;
        private int _targetY;
        private int _stopWithin;
        private long _bestDistance;
        private int _bestTick;

        public Commander(BattleState state, Side side)
        {
            _state = state;
            _settings = state.Settings;
            _side = side;
        }

        /// <summary>The priority it follows now, 1..5, or 0 while its hero is dead; for the developer.</summary>
        public int Priority => _priority;

        public void Step(int reactionTicks)
        {
            var index = _state.HeroOf(_side);
            if (index < 0)
                return;

            var hero = _state.Heroes[index];
            if (!hero.Alive)
            {
                _priority = 0;
                _nextDecision = _state.Tick;
                return;
            }

            if (_state.Tick >= _nextDecision)
            {
                _nextDecision = _state.Tick + System.Math.Max(1, reactionTicks);
                Decide(hero);
            }

            Walk(index, hero);
        }

        private void Decide(HeroState hero)
        {
            var previous = _priority;
            for (var priority = 1; priority <= Priorities; priority++)
            {
                if (_blockedUntil[priority] > _state.Tick || !Goal(priority, hero, out var x, out var y, out var stop))
                    continue;

                if (priority != previous || x != _targetX || y != _targetY)
                {
                    _bestDistance = long.MaxValue;
                    _bestTick = _state.Tick;
                }

                _priority = priority;
                _targetX = x;
                _targetY = y;
                _stopWithin = stop;
                return;
            }

            _priority = 0;
        }

        private void Walk(int index, HeroState hero)
        {
            var directionX = 0;
            var directionY = 0;
            if (_priority > 0)
            {
                var distance = BattleState.Distance2(hero.X, hero.Y, _targetX, _targetY);
                if (distance > (long)_stopWithin * _stopWithin)
                {
                    // a goal that comes no closer by a cell in a while is left for the next priority
                    if (distance + 2L * BattleSettings.UnitsPerCell * IntMath.Sqrt(distance) < _bestDistance)
                    {
                        _bestDistance = distance;
                        _bestTick = _state.Tick;
                    }
                    else if (_state.Tick - _bestTick > _settings.CommanderStuckTicks)
                    {
                        _blockedUntil[_priority] = _state.Tick + _settings.CommanderStuckTicks;
                        _nextDecision = _state.Tick;
                    }

                    var dx = _targetX - hero.X;
                    var dy = _targetY - hero.Y;
                    var longest = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));
                    directionX = (int)((long)dx * BattleCommand.MaxAxis / longest);
                    directionY = (int)((long)dy * BattleCommand.MaxAxis / longest);
                }
                else
                {
                    _bestTick = _state.Tick;
                }
            }

            hero.DirectionX = directionX;
            hero.DirectionY = directionY;
            if (hero.Moving)
            {
                hero.FacingX = directionX;
                hero.FacingY = directionY;
            }

            _state.Heroes[index] = hero;
        }

        private bool Goal(int priority, HeroState hero, out int x, out int y, out int stop)
        {
            x = 0;
            y = 0;
            stop = Arrived;
            var purse = _state.Sides[(int)_side];
            switch (priority)
            {
                case 1:
                    return hero.Health * 1000 < _settings.HeroHealthOf(_side) * _settings.CommanderRetreatPermille && NearestCell(hero, _side, false, out x, out y);
                case 2:
                    return purse.Wood >= _settings.BarracksWood && _state.Tick >= BuildFrom() && NearestPlace(hero, out x, out y);
                case 3:
                    // by the walls, within the reach of hiring, not inside the barracks
                    stop = _settings.HireUnits / 2 + _settings.HeroRadiusUnits + _settings.BarracksHalfUnits;
                    return purse.Gold >= _settings.SoldierGold && _state.ArmyOf(_side) < _settings.ArmyMax && NearestBarracks(hero, _side, out x, out y);
                case 4:
                {
                    var army = _state.ArmyOf(_side);
                    var theirs = _state.ArmyOf(BattleState.Enemy(_side));
                    if (army < _settings.CommanderAttackArmy || army * 1000L < theirs * (long)_settings.CommanderAttackPermille)
                        return false;

                    // at the walls, within the reach of the sword
                    stop = _settings.MeleeReachUnits / 2 + _settings.HeroRadiusUnits + _settings.BarracksHalfUnits;
                    return NearestBarracks(hero, BattleState.Enemy(_side), out x, out y) || EnemyBase(out x, out y);
                }
                default:
                    stop = _settings.PickupUnits / 2;
                    return NearestPile(hero, out x, out y) || NearestCell(hero, Side.Neutral, true, out x, out y);
            }
        }

        private int BuildFrom() => _side == Side.Enemy ? _settings.EnemyBuildFromTicks : 0;

        /// <summary>The nearest empty place of its colour, or a neutral one to paint on the way: the brush makes it its own.</summary>
        private bool NearestPlace(HeroState hero, out int x, out int y)
        {
            x = 0;
            y = 0;
            var best = long.MaxValue;
            var other = BattleState.Enemy(_side);
            foreach (var site in _state.Sites)
            {
                var owner = _state.Grid.At(site.CenterX, site.CenterY);
                if (site.Built || owner == other)
                    continue;

                var sx = BattleState.Middle(site.CenterX);
                var sy = BattleState.Middle(site.CenterY);
                var distance = BattleState.Distance2(hero.X, hero.Y, sx, sy);
                if (distance >= best)
                    continue;

                best = distance;
                x = sx;
                y = sy;
            }

            return best != long.MaxValue;
        }

        private bool NearestBarracks(HeroState hero, Side owner, out int x, out int y)
        {
            x = 0;
            y = 0;
            var best = long.MaxValue;
            foreach (var site in _state.Sites)
            {
                if (site.Barracks != owner)
                    continue;

                var sx = BattleState.Middle(site.CenterX);
                var sy = BattleState.Middle(site.CenterY);
                var distance = BattleState.Distance2(hero.X, hero.Y, sx, sy);
                if (distance >= best)
                    continue;

                best = distance;
                x = sx;
                y = sy;
            }

            return best != long.MaxValue;
        }

        private bool EnemyBase(out int x, out int y)
        {
            var target = _state.Bases[(int)BattleState.Enemy(_side)];
            x = target.X;
            y = target.Y;
            return target.Standing;
        }

        /// <summary>The nearest pile on neutral land within the sight of a player around the hero.</summary>
        private bool NearestPile(HeroState hero, out int x, out int y)
        {
            x = 0;
            y = 0;
            var best = (long)_settings.CommanderSightUnits * _settings.CommanderSightUnits;
            var found = false;
            var size = _state.Grid.Size;
            foreach (var pile in _state.Resources)
            {
                if (_state.Grid[pile.Cell] != Side.Neutral)
                    continue;

                var px = BattleState.Middle(pile.Cell % size);
                var py = BattleState.Middle(pile.Cell / size);
                var distance = BattleState.Distance2(hero.X, hero.Y, px, py);
                if (distance >= best)
                    continue;

                best = distance;
                found = true;
                x = px;
                y = py;
            }

            return found;
        }

        /// <summary>
        /// The nearest cell of <paramref name="colour"/>; with <paramref name="atBorder"/> only one next to a cell of its own side.
        /// </summary>
        private bool NearestCell(HeroState hero, Side colour, bool atBorder, out int x, out int y)
        {
            x = 0;
            y = 0;
            var grid = _state.Grid;
            var size = grid.Size;
            var best = long.MaxValue;
            for (var cy = 0; cy < size; cy++)
            for (var cx = 0; cx < size; cx++)
            {
                if (grid.At(cx, cy) != colour || grid.IsWaterAt(cx, cy) || (atBorder && !NextToOwn(cx, cy)))
                    continue;

                var mx = BattleState.Middle(cx);
                var my = BattleState.Middle(cy);
                var distance = BattleState.Distance2(hero.X, hero.Y, mx, my);
                if (distance >= best)
                    continue;

                best = distance;
                x = mx;
                y = my;
            }

            return best != long.MaxValue;
        }

        private bool NextToOwn(int x, int y)
        {
            var grid = _state.Grid;
            var last = grid.Size - 1;
            return (x > 0 && grid.At(x - 1, y) == _side) || (x < last && grid.At(x + 1, y) == _side) ||
                   (y > 0 && grid.At(x, y - 1) == _side) || (y < last && grid.At(x, y + 1) == _side);
        }
    }
}
