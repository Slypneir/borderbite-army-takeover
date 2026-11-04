using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Core.BattleSystem.Utils;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// How soldiers walk (stage 1 spec 3.4, owner's decision of 4 November): they do not follow the hero; once hired a soldier
    /// goes to attack on its own: the nearest enemy hero or soldier within sight (8 m), else the nearest enemy barracks, else
    /// the enemy base. It keeps its target until the target falls or leaves its sight, or an enemy comes within sight (T15); the
    /// enemy's soldiers do the same. Soldiers part from each other and give way to their hero; the death of the hero changes
    /// nothing. Integers only, in a fixed order: the same army on every device.
    /// </summary>
    public sealed class ArmyRules
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;

        public ArmyRules(BattleState state)
        {
            _state = state;
            _settings = state.Settings;
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
                X = IntMath.Clamp(x, 0, settings.MapUnits),
                Y = IntMath.Clamp(y, 0, settings.MapUnits),
                Health = settings.SoldierHealthOf(side),
                FightTick = state.Tick,
                FacingY = side == Side.Player ? 1 : -1,
            });
            return id;
        }

        public void Step()
        {
            for (var i = 0; i < _state.Soldiers.Count; i++)
            {
                var soldier = _state.Soldiers[i];
                Choose(ref soldier);
                if (Locate(soldier, out var x, out var y, out var radius))
                {
                    // to the target, stopping well within the reach of a blow from the edge of its body
                    var stop = _settings.SoldierBodyUnits + radius + _settings.SoldierReachUnits * 3 / 4;
                    if (BattleState.Distance2(soldier.X, soldier.Y, x, y) > (long)stop * stop)
                        Walk(ref soldier, x, y, stop);
                    else
                        Face(ref soldier, x, y);
                }

                _state.Soldiers[i] = soldier;
            }

            Part();
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
                a.X = IntMath.Clamp(a.X, _settings.HeroRadiusUnits, _settings.MapUnits - _settings.HeroRadiusUnits);
                a.Y = IntMath.Clamp(a.Y, _settings.HeroRadiusUnits, _settings.MapUnits - _settings.HeroRadiusUnits);
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
