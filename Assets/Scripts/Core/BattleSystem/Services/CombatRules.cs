using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Core.BattleSystem.Utils;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// Blows and shots without buttons (stage 1 spec 3.4, 3.5, 4.1): the nearest enemy hero or soldier first, a building (a
    /// barracks or the base) only when no such enemy is in reach. Every reach is the gap between the edges of two bodies — a
    /// hero, a soldier, the walls of a building — not between their middles: a hero right by an enemy always strikes, whatever
    /// way either faces (owner, 4 November). A hero strikes with the sword close by, else shoots the gun; a soldier strikes only close by.
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
            Base,
        }

        public struct Target
        {
            public TargetKind Kind;
            public int Index;
            public int X;
            public int Y;
        }

        /// <summary>The nearest enemy of <paramref name="side"/> within reach: a hero or soldier if any, else a barracks.</summary>
        public static bool NearestTarget(BattleState state, Side side, int x, int y, int reach, int body, out Target target)
        {
            return NearestUnit(state, side, x, y, reach, body, out target) || NearestBuilding(state, side, x, y, reach, body, out target);
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
                hero.Health = _settings.HeroHealthOf(hero.Side);
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
                var body = _settings.HeroRadiusUnits;
                if (NearestUnit(_state, hero.Side, hero.X, hero.Y, _settings.MeleeReachUnits, body, out target))
                    melee = true;
                else if (_settings.HeroGun && NearestUnit(_state, hero.Side, hero.X, hero.Y, _settings.GunReachUnits, body, out target))
                    melee = false;
                else if (NearestBuilding(_state, hero.Side, hero.X, hero.Y, _settings.MeleeReachUnits, body, out target))
                    melee = true;
                else if (_settings.HeroGun && NearestBuilding(_state, hero.Side, hero.X, hero.Y, _settings.GunReachUnits, body, out target))
                    melee = false;
                else
                {
                    // nothing to fight: a blow at a pile in reach mines one unit (task 4.19); it is no fight, healing goes on
                    if (_economy.Mine(hero, out var pileX, out var pileY))
                    {
                        hero.Cooldown = _settings.MineCooldownOf(hero.Side);
                        hero.FacingX = pileX - hero.X;
                        hero.FacingY = pileY - hero.Y;
                        _state.Heroes[i] = hero;
                        _state.Emit(EventKind.Strike, hero.Side, -1, 0, hero.X, hero.Y, pileX, pileY);
                    }

                    continue;
                }

                hero.Cooldown = melee ? _settings.MeleeCooldownTicks : _settings.GunCooldownTicks;
                hero.FightTick = _state.Tick;
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
                if (soldier.Cooldown > 0 || !SoldierTarget(_state, soldier, reach, out var target))
                    continue;

                soldier.Cooldown = _settings.SoldierCooldownTicks;
                soldier.FightTick = _state.Tick;
                _state.Soldiers[i] = soldier;
                _state.Emit(EventKind.Strike, soldier.Side, soldier.Id, 0, soldier.X, soldier.Y, target.X, target.Y);
                var removedBefore = Hurt(target, _settings.SoldierDamage) && target.Kind == TargetKind.Soldier && target.Index < i;
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
                    hero.FightTick = _state.Tick;
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
                    soldier.FightTick = _state.Tick;
                    if (soldier.Health > 0)
                    {
                        _state.Soldiers[target.Index] = soldier;
                        return false;
                    }

                    _state.Soldiers.RemoveAt(target.Index);
                    _state.Emit(EventKind.SoldierDied, soldier.Side, soldier.Id, 0, soldier.X, soldier.Y);
                    return true;
                }
                case TargetKind.Base:
                    HitBase((Side)target.Index, damage);
                    return false;
                default:
                {
                    ref var site = ref _state.Sites[target.Index];
                    site.Health -= damage;
                    var owner = site.Barracks;
                    _state.Emit(EventKind.BuildingHit, owner, target.Index, System.Math.Max(0, site.Health), target.X, target.Y);
                    if (site.Health <= 0)
                        _economy.Destroy(target.Index);
                    return false;
                }
            }
        }

        /// <summary>A base of a side takes damage; at zero it falls and the battle ends on this tick's decision.</summary>
        public void HitBase(Side side, int damage)
        {
            ref var place = ref _state.Bases[(int)side];
            if (!place.Standing || damage <= 0)
                return;

            place.Health = System.Math.Max(0, place.Health - damage);
            _state.Emit(EventKind.BuildingHit, side, -1, place.Health, place.X, place.Y);
            if (!place.Standing)
            {
                _state.Emit(EventKind.Destroyed, side, -1, 0, place.X, place.Y);
                _economy.ProtectBase(side);
            }
        }

        /// <summary>
        /// What a soldier strikes within reach under the order of its side (spec 3.8): «Казармы» only a barracks, «Штаб» only the
        /// base, «Бойцы» and «Захват» only units; no order or «Ко мне» — a unit first, else a building.
        /// </summary>
        private static bool SoldierTarget(BattleState state, SoldierState soldier, int reach, out Target target)
        {
            var body = state.Settings.SoldierBodyUnits;
            var x = soldier.X;
            var y = soldier.Y;
            switch (state.Sides[(int)soldier.Side].Order)
            {
                case ArmyOrder.Barracks:
                    return NearestBarracks(state, soldier.Side, x, y, reach, body, out target);
                case ArmyOrder.Base:
                {
                    target = default;
                    var enemy = BattleState.Enemy(soldier.Side);
                    var place = state.Bases[(int)enemy];
                    if (!place.Standing || Gap(x, y, place.X, place.Y, body + state.Settings.BaseHalfUnits) > reach)
                        return false;

                    target = new Target { Kind = TargetKind.Base, Index = (int)enemy, X = place.X, Y = place.Y };
                    return true;
                }
                case ArmyOrder.Units:
                case ArmyOrder.Capture:
                    return NearestUnit(state, soldier.Side, x, y, reach, body, out target);
                default:
                    return NearestTarget(state, soldier.Side, x, y, reach, body, out target);
            }
        }

        /// <summary>The gap between the edges of two bodies, units: the distance of the middles less both radii.</summary>
        private static long Gap(int x, int y, int toX, int toY, int radii) =>
            IntMath.Sqrt(BattleState.Distance2(x, y, toX, toY)) - radii;

        /// <summary>The nearest living enemy hero or soldier within reach of the edge of a body.</summary>
        public static bool NearestUnit(BattleState state, Side side, int x, int y, int reach, int body, out Target target)
        {
            target = default;
            var best = (long)reach;
            var found = false;
            var enemy = BattleState.Enemy(side);
            var settings = state.Settings;
            for (var i = 0; i < state.Heroes.Count; i++)
            {
                var hero = state.Heroes[i];
                // a hero back at home and still unhurtable is no target: the enemy strikes others (task 4.23)
                if (hero.Side != enemy || !hero.Alive || hero.InvulnerableUntil > state.Tick)
                    continue;

                var gap = Gap(x, y, hero.X, hero.Y, body + settings.HeroRadiusUnits);
                if (gap > best)
                    continue;

                best = gap;
                found = true;
                target = new Target { Kind = TargetKind.Hero, Index = i, X = hero.X, Y = hero.Y };
            }

            for (var i = 0; i < state.Soldiers.Count; i++)
            {
                var soldier = state.Soldiers[i];
                if (soldier.Side != enemy)
                    continue;

                var gap = Gap(x, y, soldier.X, soldier.Y, body + settings.SoldierBodyUnits);
                if (gap > best || (found && gap == best))
                    continue;

                best = gap;
                found = true;
                target = new Target { Kind = TargetKind.Soldier, Index = i, X = soldier.X, Y = soldier.Y };
            }

            return found;
        }

        /// <summary>The nearest enemy barracks within reach of its walls, else the enemy base within reach of its walls.</summary>
        private static bool NearestBuilding(BattleState state, Side side, int x, int y, int reach, int body, out Target target)
        {
            if (NearestBarracks(state, side, x, y, reach, body, out target))
                return true;

            var enemy = BattleState.Enemy(side);
            var place = state.Bases[(int)enemy];
            if (!place.Standing || Gap(x, y, place.X, place.Y, body + state.Settings.BaseHalfUnits) > reach)
                return false;

            target = new Target { Kind = TargetKind.Base, Index = (int)enemy, X = place.X, Y = place.Y };
            return true;
        }

        private static bool NearestBarracks(BattleState state, Side side, int x, int y, int reach, int body, out Target target)
        {
            target = default;
            var best = (long)reach;
            var found = false;
            var enemy = BattleState.Enemy(side);
            for (var i = 0; i < state.Sites.Length; i++)
            {
                var site = state.Sites[i];
                if (site.Barracks != enemy)
                    continue;

                var sx = BattleState.Middle(site.CenterX);
                var sy = BattleState.Middle(site.CenterY);
                var gap = Gap(x, y, sx, sy, body + state.Settings.BarracksHalfUnits);
                if (gap > best || (found && gap == best))
                    continue;

                best = gap;
                found = true;
                target = new Target { Kind = TargetKind.Barracks, Index = i, X = sx, Y = sy };
            }

            return found;
        }
    }
}
