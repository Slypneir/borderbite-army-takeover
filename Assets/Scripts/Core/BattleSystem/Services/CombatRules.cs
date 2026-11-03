using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// Blows and shots without buttons (stage 1 spec 3.4, 3.5): the nearest enemy hero or soldier first, a building only when no
    /// such enemy is in reach. A hero strikes with the sword close by, else shoots the gun; a soldier strikes only close by.
    /// Deaths, the fall of a barracks and the return of a dead hero after a while, invulnerable for a moment.
    /// </summary>
    public sealed class CombatRules
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;
        private readonly EconomyRules _economy;

        public CombatRules(BattleState state, EconomyRules economy)
        {
            _state = state;
            _settings = state.Settings;
            _economy = economy;
        }

        public enum TargetKind
        {
            Hero,
            Soldier,
            Barracks,
        }

        public struct Target
        {
            public TargetKind Kind;
            public int Index;
            public int X;
            public int Y;
        }

        /// <summary>The nearest enemy of <paramref name="side"/> within reach: a hero or soldier if any, else a barracks.</summary>
        public static bool NearestTarget(BattleState state, Side side, int x, int y, int reach, out Target target)
        {
            return NearestUnit(state, side, x, y, reach, out target) || NearestBarracks(state, side, x, y, reach, out target);
        }

        public void Step()
        {
            Respawn();
            Cool();
            HeroesFight();
            SoldiersFight();
        }

        private void Respawn()
        {
            for (var i = 0; i < _state.Heroes.Count; i++)
            {
                var hero = _state.Heroes[i];
                if (hero.Alive || hero.RespawnTick > _state.Tick)
                    continue;

                hero.RespawnTick = 0;
                hero.X = hero.HomeX;
                hero.Y = hero.HomeY;
                hero.Health = _settings.HeroHealth;
                hero.Cooldown = 0;
                hero.InvulnerableUntil = _state.Tick + _settings.InvulnerableTicks;
                _state.Heroes[i] = hero;
                _state.Emit(EventKind.HeroRespawned, hero.Side, i, 0, hero.X, hero.Y);
            }
        }

        private void Cool()
        {
            for (var i = 0; i < _state.Heroes.Count; i++)
            {
                var hero = _state.Heroes[i];
                if (hero.Cooldown > 0)
                {
                    hero.Cooldown--;
                    _state.Heroes[i] = hero;
                }
            }

            for (var i = 0; i < _state.Soldiers.Count; i++)
            {
                var soldier = _state.Soldiers[i];
                if (soldier.Cooldown > 0)
                {
                    soldier.Cooldown--;
                    _state.Soldiers[i] = soldier;
                }
            }
        }

        private void HeroesFight()
        {
            for (var i = 0; i < _state.Heroes.Count; i++)
            {
                var hero = _state.Heroes[i];
                if (!hero.Alive || hero.Cooldown > 0)
                    continue;

                // units first: the sword close by, else the gun; a building only when no unit is in the reach of the gun
                Target target;
                bool melee;
                if (NearestUnit(_state, hero.Side, hero.X, hero.Y, _settings.MeleeReachUnits, out target))
                    melee = true;
                else if (NearestUnit(_state, hero.Side, hero.X, hero.Y, _settings.GunReachUnits, out target))
                    melee = false;
                else if (NearestBarracks(_state, hero.Side, hero.X, hero.Y, _settings.MeleeReachUnits, out target))
                    melee = true;
                else if (NearestBarracks(_state, hero.Side, hero.X, hero.Y, _settings.GunReachUnits, out target))
                    melee = false;
                else
                    continue;

                hero.Cooldown = melee ? _settings.MeleeCooldownTicks : _settings.GunCooldownTicks;
                hero.FacingX = target.X - hero.X;
                hero.FacingY = target.Y - hero.Y;
                _state.Heroes[i] = hero;
                _state.Emit(melee ? EventKind.Strike : EventKind.Shot, hero.Side, -1, 0, hero.X, hero.Y, target.X, target.Y);
                Hurt(target, melee ? _settings.MeleeDamage : _settings.GunDamage);
            }
        }

        private void SoldiersFight()
        {
            var reach = _settings.SoldierReachUnits;
            for (var i = 0; i < _state.Soldiers.Count; i++)
            {
                var soldier = _state.Soldiers[i];
                if (soldier.Cooldown > 0 || !NearestTarget(_state, soldier.Side, soldier.X, soldier.Y, reach, out var target))
                    continue;

                soldier.Cooldown = _settings.SoldierCooldownTicks;
                _state.Soldiers[i] = soldier;
                _state.Emit(EventKind.Strike, soldier.Side, soldier.Id, 0, soldier.X, soldier.Y, target.X, target.Y);
                var removedBefore = Hurt(target, soldier.Damage) && target.Kind == TargetKind.Soldier && target.Index < i;
                if (removedBefore)
                    i--;
            }
        }

        /// <summary>Deals damage; true when a soldier died and left the list.</summary>
        private bool Hurt(Target target, int damage)
        {
            switch (target.Kind)
            {
                case TargetKind.Hero:
                {
                    var hero = _state.Heroes[target.Index];
                    if (hero.InvulnerableUntil > _state.Tick)
                        return false;

                    hero.Health -= damage;
                    if (hero.Health <= 0)
                    {
                        hero.Health = 0;
                        hero.RespawnTick = _state.Tick + _settings.RespawnTicks;
                        hero.DirectionX = 0;
                        hero.DirectionY = 0;
                        _state.Emit(EventKind.HeroDied, hero.Side, target.Index, 0, hero.X, hero.Y);
                    }

                    _state.Heroes[target.Index] = hero;
                    return false;
                }
                case TargetKind.Soldier:
                {
                    var soldier = _state.Soldiers[target.Index];
                    soldier.Health -= damage;
                    if (soldier.Health > 0)
                    {
                        _state.Soldiers[target.Index] = soldier;
                        return false;
                    }

                    _state.Soldiers.RemoveAt(target.Index);
                    _state.Emit(EventKind.SoldierDied, soldier.Side, soldier.Id, 0, soldier.X, soldier.Y);
                    return true;
                }
                default:
                {
                    ref var site = ref _state.Sites[target.Index];
                    site.Health -= damage;
                    if (site.Health <= 0)
                        _economy.Destroy(target.Index);
                    return false;
                }
            }
        }

        private static bool NearestUnit(BattleState state, Side side, int x, int y, int reach, out Target target)
        {
            target = default;
            var best = (long)reach * reach;
            var found = false;
            var enemy = BattleState.Enemy(side);
            for (var i = 0; i < state.Heroes.Count; i++)
            {
                var hero = state.Heroes[i];
                var distance = BattleState.Distance2(x, y, hero.X, hero.Y);
                if (hero.Side != enemy || !hero.Alive || distance > best)
                    continue;

                best = distance;
                found = true;
                target = new Target { Kind = TargetKind.Hero, Index = i, X = hero.X, Y = hero.Y };
            }

            for (var i = 0; i < state.Soldiers.Count; i++)
            {
                var soldier = state.Soldiers[i];
                var distance = BattleState.Distance2(x, y, soldier.X, soldier.Y);
                if (soldier.Side != enemy || distance > best || (found && distance == best))
                    continue;

                best = distance;
                found = true;
                target = new Target { Kind = TargetKind.Soldier, Index = i, X = soldier.X, Y = soldier.Y };
            }

            return found;
        }

        private static bool NearestBarracks(BattleState state, Side side, int x, int y, int reach, out Target target)
        {
            target = default;
            var best = (long)reach * reach;
            var found = false;
            var enemy = BattleState.Enemy(side);
            for (var i = 0; i < state.Sites.Length; i++)
            {
                var site = state.Sites[i];
                var sx = BattleState.Middle(site.CenterX);
                var sy = BattleState.Middle(site.CenterY);
                var distance = BattleState.Distance2(x, y, sx, sy);
                if (site.Barracks != enemy || distance > best || (found && distance == best))
                    continue;

                best = distance;
                found = true;
                target = new Target { Kind = TargetKind.Barracks, Index = i, X = sx, Y = sy };
            }

            return found;
        }
    }
}
